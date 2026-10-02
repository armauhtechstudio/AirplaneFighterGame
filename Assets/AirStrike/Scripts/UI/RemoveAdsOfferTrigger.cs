using System.Collections;
using UnityEngine;

/// <summary>
/// Sits on the mode selection panel: every time that panel shows, opens the Remove Ads offer after a
/// short delay (not once ads are removed).
/// </summary>
public class RemoveAdsOfferTrigger : MonoBehaviour
{
    public RemoveAdsOfferPanel offer;
    [Tooltip("Real seconds after the mode selection shows before the offer pops up.")]
    public float delay = 0.4f;

    void OnEnable()
    {
        if (offer != null && !AdsManager.AdsRemoved) StartCoroutine(ShowSoon());
    }

    IEnumerator ShowSoon()
    {
        yield return new WaitForSecondsRealtime(delay);
        if (isActiveAndEnabled) offer.Open(); // only if the mode selection is still open
    }
}
