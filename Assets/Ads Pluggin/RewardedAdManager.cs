using System;
using UnityEngine;
using GoogleMobileAds.Api;

public class RewardedAdManager : MonoBehaviour
{
    public static RewardedAdManager Instance;

    private RewardedAd _rewardedAd;
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
        Debug.Log("Requesting Rewarded Ad...");

        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        var adRequest = new AdRequest();

        RewardedAd.Load(_adUnitId, adRequest, (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogError("Rewarded Ad failed to load: " + error);
                return;
            }

            _rewardedAd = ad;
            RegisterEventHandlers(ad);
            Debug.Log("Rewarded Ad loaded successfully.");
        });
    }

    public bool IsAdAvailable()
    {
        return _rewardedAd != null && !_isShowingAd;
    }

    // Callbacks for the ad currently showing: closed (reward earned or not) / failed to show
    private Action pendingClosed, pendingFailed;

    public void ShowAd(Action onRewardedCallback, Action onFailedCallback = null, Action onClosedCallback = null)
    {
        if (!IsAdAvailable())
        {
            Debug.LogWarning("Rewarded Ad not ready.");
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

        _rewardedAd.Show((Reward reward) =>
        {
            Debug.Log($"Rewarded Ad earned reward: {reward.Amount} {reward.Type}");
            onRewardedCallback?.Invoke();
        });
    }

    private void RegisterEventHandlers(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded Ad closed.");
            _isShowingAd = false;
            AppOpenAdManager.NotifyFullScreenAdClosed();
            LoadAd();
            Action closed = pendingClosed;
            pendingClosed = pendingFailed = null;
            closed?.Invoke();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogError("Rewarded Ad failed to show: " + error);
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
                AppOpenAdManager.Instance.LogAdmobRevenue(
                    "Admob",
                    ad.GetAdUnitID(),
                    AdFormat.REWARDED.ToString(),
                    adValue.Value
                );
            }
        };
    }
}
