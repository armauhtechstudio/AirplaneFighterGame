using UnityEngine;
using GoogleMobileAds.Api;

public class BannerAdManager2 : MonoBehaviour
{
    public static BannerAdManager2 Instance;

    private BannerView _bannerView;

    public string _adUnitId;
    public AdPosition _adPosition = AdPosition.Bottom;

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

    public void LoadBanner()
    {
        Debug.Log("Requesting Banner Ad 2...");

        if (PlayerPrefs.GetInt("RemoveAds", 0) == 1)
        {
            return;
        }

        // Clean up previous banner before loading new one
        DestroyBanner();

        // Create a banner view at the configured position
        _bannerView = new BannerView(_adUnitId, AdSize.Banner, _adPosition);

        RegisterEventHandlers(_bannerView);

        var adRequest = new AdRequest();
        _bannerView.LoadAd(adRequest);
    }

    public void ShowBanner()
    {
        if (PlayerPrefs.GetInt("RemoveAds", 0) == 1)
        {
            return;
        }

        if (_bannerView == null)
        {
            LoadBanner();
        }
        else
        {
            _bannerView.Show();
        }
    }

    public void HideBanner()
    {
        _bannerView?.Hide();
    }

    public void DestroyBanner()
    {
        if (_bannerView != null)
        {
            _bannerView.Destroy();
            _bannerView = null;
        }
    }

    private void RegisterEventHandlers(BannerView banner)
    {
        banner.OnAdPaid += (AdValue adValue) =>
        {
            if (AppOpenAdManager.Instance != null)
            {
                AppOpenAdManager.Instance.LogAdmobRevenue(
                    "Admob",
                    banner.GetAdUnitID(),
                    AdFormat.BANNER.ToString(),
                    adValue.Value
                );
            }
        };
    }

    void OnDestroy()
    {
        DestroyBanner();
    }
}
