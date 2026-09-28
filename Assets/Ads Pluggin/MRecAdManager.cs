using GoogleMobileAds.Api;
using UnityEngine;


public class MRecAdManager : MonoBehaviour
{
    public static MRecAdManager Instance;

    private BannerView _bannerView;

    public string _adUnitId;
    public AdPosition _adPosition = AdPosition.BottomLeft;
    private void Awake()

    {
        if (Instance == null)
        {
            Instance = this;
          //  DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowMrec()
    {
        if (PlayerPrefs.GetInt("RemoveAds", 0) == 1)
        {
            return;
        }

        // if (_bannerView != null)
        //     return;

        _bannerView = new BannerView(_adUnitId, AdSize.MediumRectangle, _adPosition);

        var adRequest = new AdRequest();
        _bannerView.LoadAd(adRequest);
    }

    public void HideMrec()
    {
        _bannerView?.Destroy();
    }

}