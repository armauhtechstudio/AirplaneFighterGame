using System;
using UnityEngine;
using GoogleMobileAds.Api;
using System.Collections;

public class AppOpenAdManager : MonoBehaviour
{
    public static AppOpenAdManager Instance;

    private AppOpenAd _appOpenAd = null;
    private DateTime _loadTime;
    private bool _isShowingAd = false;

    public string _adUnitId;

    private bool CanShowAdCheck = true;

    // Closing any full-screen ad (interstitial, rewarded, app open) makes the app "resume". Those
    // resumes must not trigger another app open, so remember when the last one closed.
    static float lastFullScreenAdClosed = -100f;
    const float IgnoreResumeAfterAdSeconds = 3f;

    public static void NotifyFullScreenAdClosed()
    {
        lastFullScreenAdClosed = Time.realtimeSinceStartup;
    }

    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // If there's no central AdsManager in the scene, handle initialization here.
            // Otherwise, AdsManager will initialize the SDK and trigger LoadAd().
            if (FindObjectOfType<AdsManager>() == null)
            {
                MobileAds.Initialize(initStatus =>
                {
                    Debug.Log("GoogleMobileAds initialized (fallback).");

                    LoadAd();

                    if (InterstitialAdManager.Instance != null)
                        InterstitialAdManager.Instance.LoadAd();
                });
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PauseAppOpenAds()
    {
        Debug.Log("Hamza App Paused");

        CanShowAdCheck = false;
        Invoke(nameof(ResumeAppOpenAds), 5f);
    }

    private void ResumeAppOpenAds()
    {
        Debug.Log("Hamza App Resumed");
        CanShowAdCheck = true;
    }

    public IEnumerator OnApplicationPause(bool pause)
    {
        if (pause)
            yield break;

        yield return new WaitForSeconds(0.5f);

        if (!CanShowAdCheck)
            yield break;

        // Resumed because one of our own full-screen ads just closed, not from the background
        if (_isShowingAd || Time.realtimeSinceStartup - lastFullScreenAdClosed < IgnoreResumeAfterAdSeconds)
            yield break;

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 0)
            yield break;

        // Remote config AppOpenFromBackground (+ AdsManager's enableAppOpen)
        if (!AdsManager.CanShowAppOpenFromBackground)
        {
            Debug.Log("App resumed from background: app open disabled by remote config.");
            yield break;
        }

        Debug.Log("App resumed from background");

        ShowAdIfAvailable();
    }

    public void LoadAd()
    {
        Debug.Log("Requesting App Open Ad...");

        var adRequest = new AdRequest();

        AppOpenAd.Load(_adUnitId, adRequest, (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogError("App Open Ad failed to load: " + error);
                return;
            }

            Debug.Log("App Open Ad loaded successfully.");

            _appOpenAd = ad;
            _loadTime = DateTime.Now;

            _appOpenAd.OnAdFullScreenContentClosed += OnAdClosed;
            _appOpenAd.OnAdFullScreenContentFailed += OnAdFailedToShow;

            RegisterEventHandlers(_appOpenAd);
        });
    }

    private void RegisterEventHandlers(AppOpenAd ad)
    {
        if (ad == null)
            return;

        ad.OnAdPaid += (AdValue adValue) =>
        {
            LogAdmobRevenue(
                "Admob",
                ad.GetAdUnitID(),
                AdFormat.APP_OPEN_AD.ToString(),
                adValue.Value
            );
        };
    }

    private void OnAdClosed()
    {
        Debug.Log("App Open Ad closed.");

        _isShowingAd = false;
        NotifyFullScreenAdClosed();
        _appOpenAd = null;

        LoadAd();
    }

    private void OnAdFailedToShow(AdError adError)
    {
        Debug.LogError("App Open Ad failed to show: " + adError);

        _isShowingAd = false;
        NotifyFullScreenAdClosed();
        _appOpenAd = null;

        LoadAd();
    }

    public bool IsAdAvailable()
    {
        if (_appOpenAd == null)
            return false;

        return true;
    }

    public void ShowAdIfAvailable()
    {
        if (!IsAdAvailable() || _isShowingAd)
        {
            Debug.Log("No App Open Ad available to show at this moment.");
            return;
        }

        Debug.Log("Showing App Open Ad...");

        _isShowingAd = true;
        _appOpenAd.Show();
    }

    public void LogAdmobRevenue(string adSourceName, string AdUnitId, string adFormat, double revenue)
    {
        Debug.Log($"SEHandler: Logging Admob Ad Revenue for {AdUnitId} of type {adFormat} with revenue {revenue} USD");
    }

    private int GetAdTypeFromFormat(string adFormat)
    {
        if (string.IsNullOrEmpty(adFormat))
            return 0;

        Debug.Log($"SEHandler: Converting ad format '{adFormat}' to ad type integer.");

        switch (adFormat.ToUpperInvariant())
        {
            case "LEADER":
            case "BANNER":
                return 5;

            case "INTER":
            case "INTERSTITIAL":
                return 3;

            case "REWARDED":
                return 1;

            case "MREC":
                return 10;

            case "APP OPEN":
            case "APPOPEN":
                return 2;

            default:
                return 0;
        }
    }
}
