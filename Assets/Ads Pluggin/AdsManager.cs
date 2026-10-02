using System;
using UnityEngine;
using GoogleMobileAds.Api;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance;

    [Header("Ad Enable Toggles")]
    public bool enableAppOpen = true;
    public bool enableInterstitial = true;
    public bool enableRewarded = true;
    public bool enableRewardedInterstitial = true;
    public bool enableBanner = true;
    public bool enableBanner2 = true;
    public bool enableMRec = true;

    // ------------------------------------------------------------------ Remote config gates
    // FirebaseRemoteConfigManager (GameConfigData from "AdsDataMulti") switches ad types on/off
    // remotely, on top of the toggles above. Until the fetch arrives its defaults apply.

    static GameConfigData RC => FirebaseRemoteConfigManager.Config;

    // ------------------------------------------------------------------ Remove Ads
    // PlayerPrefs "RemoveAds" == 1: the SDK still initializes but only the rewarded ads (the player's
    // choice, e.g. the revive) are loaded and shown. App open, banners, MREC and interstitials are never
    // loaded or shown, even one that is already loaded (e.g. bought during this session).

    public const string RemoveAdsKey = "RemoveAds";
    public static bool AdsRemoved => PlayerPrefs.GetInt(RemoveAdsKey, 0) == 1;

    /// <summary>Call after the Remove Ads purchase: saves it and takes down what is on screen now.</summary>
    public void RemoveAdsNow()
    {
        PlayerPrefs.SetInt(RemoveAdsKey, 1);
        PlayerPrefs.Save();
        if (BannerAdManager.Instance != null) BannerAdManager.Instance.DestroyBanner();
        if (BannerAdManager2.Instance != null) BannerAdManager2.Instance.DestroyBanner();
        HideMRecView();
        Debug.Log("[AdsManager] Ads removed.");
    }

    /// <summary>App open ad when the game returns from the background (not the launch app open).</summary>
    public static bool CanShowAppOpenFromBackground =>
        !AdsRemoved && (Instance == null || Instance.enableAppOpen) && (RC == null || RC.AppOpenFromBackground);

    public bool TopBannerAllowed => enableBanner && (RC == null || RC.TopBanner);
    public bool MRecAllowed => enableMRec && (RC == null || RC.IsMedRect);
    public bool InterstitialAllowed => enableInterstitial && (RC == null || RC.IsInterstialAd);
    public bool AppOpenAfterInterstitialAllowed => enableAppOpen && RC != null && RC.AppOpenAfterInterstitial;

    [Tooltip("Seconds between the interstitial closing and the app open ad (when AppOpenAfterInterstitial is on).")]
    public float appOpenAfterInterstitialDelay = 0.5f;

    /// <summary>Called by InterstitialAdManager when an interstitial has been closed.</summary>
    public void OnInterstitialClosed()
    {
        if (AdsRemoved || !AppOpenAfterInterstitialAllowed || AppOpenAdManager.Instance == null) return;
        StartCoroutine(ShowAppOpenAfterInterstitial());
    }

    System.Collections.IEnumerator ShowAppOpenAfterInterstitial()
    {
        yield return new WaitForSecondsRealtime(appOpenAfterInterstitialDelay);
        if (AdsRemoved) yield break;
        Debug.Log("[AdsManager] Interstitial closed -> app open (AppOpenAfterInterstitial).");
        AppOpenAdManager.Instance.ShowAdIfAvailable();
    }

    // What the game has asked for, so a later config change can show / hide accordingly
    bool wantTopBanner = true; // LoadAllAds puts the top banner up at start
    bool wantMRec;
    bool mrecShowing;

    void OnEnable()
    {
        FirebaseRemoteConfigManager.OnConfigApplied += ApplyRemoteConfig;
    }

    void OnDisable()
    {
        FirebaseRemoteConfigManager.OnConfigApplied -= ApplyRemoteConfig;
    }

    // The config usually arrives after the ads were loaded: bring what's on screen in line with it
    void ApplyRemoteConfig(GameConfigData config)
    {
        if (AdsRemoved) return;
        Debug.Log($"[AdsManager] Remote config: AppOpenFromBackground={config.AppOpenFromBackground} TopBanner={config.TopBanner} IsMedRect={config.IsMedRect} IsInterstialAd={config.IsInterstialAd}");

        if (BannerAdManager.Instance != null)
        {
            if (!TopBannerAllowed) BannerAdManager.Instance.DestroyBanner();
            else if (wantTopBanner) BannerAdManager.Instance.ShowBanner(); // loads it if it was never loaded
        }

        if (!MRecAllowed) HideMRecView();
        else if (wantMRec && !mrecShowing) ShowMRecView();

        if (InterstitialAllowed && InterstitialAdManager.Instance != null && !InterstitialAdManager.Instance.IsAdAvailable())
            InterstitialAdManager.Instance.LoadAd();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeAds();
    }

    private void InitializeAds()
    {
        Debug.Log("Initializing Google Mobile Ads SDK..." + (AdsRemoved ? " (ads removed: rewarded ads only)" : ""));
        // Ad callbacks (closed, failed, loaded...) arrive on Unity's main thread, so they can safely
        // show the next ad / touch Unity objects
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("Google Mobile Ads SDK Initialized.");
            
            // Load ads on all managers once initialized
            LoadAllAds();
        });
    }

    public void LoadAllAds()
    {
        // Rewarded ads always (also with Remove Ads)
        if (enableRewarded && RewardedAdManager.Instance != null)
            RewardedAdManager.Instance.LoadAd();

        if (enableRewardedInterstitial && RewardedInterstitialAdManager.Instance != null)
            RewardedInterstitialAdManager.Instance.LoadAd();

        if (AdsRemoved) return; // everything below is removed by Remove Ads

        if (enableAppOpen && AppOpenAdManager.Instance != null)
            AppOpenAdManager.Instance.LoadAd();

        if (InterstitialAllowed && InterstitialAdManager.Instance != null)
            InterstitialAdManager.Instance.LoadAd();

        if (TopBannerAllowed && wantTopBanner && BannerAdManager.Instance != null)
            BannerAdManager.Instance.LoadBanner();

        if (enableBanner2 && BannerAdManager2.Instance != null)
            BannerAdManager2.Instance.LoadBanner();
    }

    // Convenience API to call ads from gameplay code:

    /// <summary>
    /// Interstitial when the given remote config flag is on (it counts as on until the config arrives),
    /// e.g. AdsManager.ShowInterstitialIf(c => c.isInterRestart). ShowInterstitial still checks
    /// IsInterstialAd / enableInterstitial / Remove Ads.
    /// </summary>
    public static void ShowInterstitialIf(Func<GameConfigData, bool> flag)
    {
        GameConfigData config = FirebaseRemoteConfigManager.Config;
        if (Instance != null && (config == null || flag(config)))
            Instance.ShowInterstitial();
    }

    public void ShowInterstitial()
    {
        if (AdsRemoved || !InterstitialAllowed) return;

        if (InterstitialAdManager.Instance != null)
        {
            InterstitialAdManager.Instance.ShowAd();
        }
        else
        {
            Debug.LogWarning("InterstitialAdManager Instance is null.");
        }
    }

    /// <summary>onClosed runs when the ad closes (reward earned or not); onFailed when none could be shown.</summary>
    public void ShowRewardedVideo(Action onReward, Action onFailed = null, Action onClosed = null)
    {
        if (!enableRewarded && !enableRewardedInterstitial)
        {
            onFailed?.Invoke();
            return;
        }

        if (enableRewarded && RewardedAdManager.Instance != null && RewardedAdManager.Instance.IsAdAvailable())
        {
            RewardedAdManager.Instance.ShowAd(onReward, onFailed, onClosed);
        }
        else if (enableRewardedInterstitial && RewardedInterstitialAdManager.Instance != null && RewardedInterstitialAdManager.Instance.IsAdAvailable())
        {
            Debug.Log("Rewarded Video not available. Showing Rewarded Interstitial instead.");
            RewardedInterstitialAdManager.Instance.ShowAd(onReward, onFailed, onClosed);
        }
        else
        {
            Debug.LogWarning("Neither Rewarded Video nor Rewarded Interstitial is available.");
            if (enableRewarded && RewardedAdManager.Instance != null)
            {
                RewardedAdManager.Instance.LoadAd();
            }
            if (enableRewardedInterstitial && RewardedInterstitialAdManager.Instance != null)
            {
                RewardedInterstitialAdManager.Instance.LoadAd();
            }
            onFailed?.Invoke();
        }
    }

    public void ShowRewardedInterstitial(Action onReward, Action onFailed = null)
    {
        if (!enableRewardedInterstitial)
        {
            onFailed?.Invoke();
            return;
        }

        if (RewardedInterstitialAdManager.Instance != null)
        {
            RewardedInterstitialAdManager.Instance.ShowAd(onReward, onFailed);
        }
        else
        {
            Debug.LogWarning("RewardedInterstitialAdManager Instance is null.");
            onFailed?.Invoke();
        }
    }

    /// <summary>Top banner (BannerAdManager). Shown only while remote config TopBanner allows it.</summary>
    public void ShowBanner()
    {
        wantTopBanner = true;
        if (AdsRemoved || !TopBannerAllowed) return;

        if (BannerAdManager.Instance != null)
        {
            BannerAdManager.Instance.ShowBanner();
        }
        else
        {
            Debug.LogWarning("BannerAdManager Instance is null.");
        }
    }

    public void HideBanner()
    {
        wantTopBanner = false;
        if (BannerAdManager.Instance != null)
        {
            BannerAdManager.Instance.HideBanner();
        }
    }

    public void ShowBanner2()
    {
        if (AdsRemoved || !enableBanner2) return;

        if (BannerAdManager2.Instance != null)
        {
            BannerAdManager2.Instance.ShowBanner();
        }
        else
        {
            Debug.LogWarning("BannerAdManager2 Instance is null.");
        }
    }

    public void HideBanner2()
    {
        if (BannerAdManager2.Instance != null)
        {
            BannerAdManager2.Instance.HideBanner();
        }
    }

    /// <summary>MREC. Shown only while remote config IsMedRect allows it.</summary>
    public void ShowMRec()
    {
        wantMRec = true;
        if (AdsRemoved || !MRecAllowed) return;
        ShowMRecView();
    }

    public void HideMRec()
    {
        wantMRec = false;
        HideMRecView();
    }

    void ShowMRecView()
    {
        if (AdsRemoved) return;
        if (MRecAdManager.Instance == null)
        {
            Debug.LogWarning("MRecAdManager Instance is null.");
            return;
        }
        if (mrecShowing) MRecAdManager.Instance.HideMrec(); // ShowMrec creates a new view each call
        MRecAdManager.Instance.ShowMrec();
        mrecShowing = true;
    }

    void HideMRecView()
    {
        if (MRecAdManager.Instance != null) MRecAdManager.Instance.HideMrec();
        mrecShowing = false;
    }

    public void ShowAppOpen()
    {
        if (AdsRemoved || !enableAppOpen) return;

        if (AppOpenAdManager.Instance != null)
        {
            AppOpenAdManager.Instance.ShowAdIfAvailable();
        }
    }
}
