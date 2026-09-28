using System;
using UnityEngine;
using GoogleMobileAds.Api;

public class RewardedInterstitialAdManager : MonoBehaviour
{
    public static RewardedInterstitialAdManager Instance;

    private RewardedInterstitialAd _rewardedInterstitialAd;
    private bool _isShowingAd;

    public string _adUnitId;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadAd()
    {
        Debug.Log("Requesting Rewarded Interstitial Ad...");

        if (_rewardedInterstitialAd != null)
        {
            _rewardedInterstitialAd.Destroy();
            _rewardedInterstitialAd = null;
        }

        var adRequest = new AdRequest();

        RewardedInterstitialAd.Load(_adUnitId, adRequest, (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogError("Rewarded Interstitial Ad failed to load: " + error);
                return;
            }

            _rewardedInterstitialAd = ad;
            RegisterEventHandlers(ad);
            Debug.Log("Rewarded Interstitial Ad loaded successfully.");
        });
    }

    public bool IsAdAvailable()
    {
        return _rewardedInterstitialAd != null && !_isShowingAd;
    }

    // Callbacks for the ad currently showing: closed (reward earned or not) / failed to show
    private Action pendingClosed, pendingFailed;

    public void ShowAd(Action onRewardedCallback, Action onFailedCallback = null, Action onClosedCallback = null)
    {
        if (!IsAdAvailable())
        {
            Debug.LogWarning("Rewarded Interstitial Ad not ready.");
            onFailedCallback?.Invoke();
            LoadAd();
            return;
        }

        _isShowingAd = true;
        pendingClosed = onClosedCallback;
        pendingFailed = onFailedCallback;

        if (AppOpenAdManager.Instance != null)
        {
            AppOpenAdManager.Instance.PauseAppOpenAds();
        }

        _rewardedInterstitialAd.Show((Reward reward) =>
        {
            Debug.Log($"Rewarded Interstitial Ad earned reward: {reward.Amount} {reward.Type}");
            onRewardedCallback?.Invoke();
        });
    }

    private void RegisterEventHandlers(RewardedInterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded Interstitial Ad closed.");
            _isShowingAd = false;
            AppOpenAdManager.NotifyFullScreenAdClosed();
            LoadAd();
            Action closed = pendingClosed;
            pendingClosed = pendingFailed = null;
            closed?.Invoke();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogError("Rewarded Interstitial Ad failed to show: " + error);
            _isShowingAd = false;
            AppOpenAdManager.NotifyFullScreenAdClosed();
            LoadAd();
            Action failed = pendingFailed;
            pendingClosed = pendingFailed = null;
            failed?.Invoke();
        };

        ad.OnAdPaid += (AdValue adValue) =>
        {
            if (AppOpenAdManager.Instance != null)
            {
                // Fallback to "REWARDED" if "REWARDED_INTERSTITIAL" is not in AdFormat enum or just pass string.
                AppOpenAdManager.Instance.LogAdmobRevenue(
                    "Admob",
                    ad.GetAdUnitID(),
                    "REWARDED_INTERSTITIAL",
                    adValue.Value
                );
            }
        };
    }
}
