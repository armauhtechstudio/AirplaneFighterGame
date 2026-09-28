using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System;
using System.Collections;
using UnityEngine;

public class InterstitialAdManager : MonoBehaviour
{
  public static InterstitialAdManager Instance;

    private InterstitialAd _interstitialAd;
    private bool _isShowingAd;

    public string _adUnitId;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
         //   DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
       // LoadAd();
    }
    public void LoadAd()
    {
        Debug.Log("Requesting Interstitial Ad...");

        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        var adRequest = new AdRequest();

        InterstitialAd.Load(_adUnitId, adRequest, (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogError("Interstitial failed to load: " + error);
                return;
            }

            _interstitialAd = ad;
            RegisterEventHandlers(ad);
            Debug.Log("Interstitial loaded successfully.");
        });
    }

    public bool IsAdAvailable()
    {
        return _interstitialAd != null && !_isShowingAd;
    }

    public void ShowAd()
    {
        if (PlayerPrefs.GetInt("RemoveAds", 0) == 1)
        {
            return;
        }
        if (!IsAdAvailable())
        {
            Debug.Log("Interstitial not ready.");
            LoadAd();
            return;
        }

        _isShowingAd = true;
        AppOpenAdManager.Instance.PauseAppOpenAds();
        _interstitialAd.Show();
    }

    private void RegisterEventHandlers(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Interstitial closed.");
            _isShowingAd = false;
            AppOpenAdManager.NotifyFullScreenAdClosed();
            LoadAd();
            if (AdsManager.Instance != null) AdsManager.Instance.OnInterstitialClosed(); // optional app open
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogError("Interstitial failed to show: " + error);
            _isShowingAd = false;
            AppOpenAdManager.NotifyFullScreenAdClosed();
            LoadAd();
        };
    }
}
