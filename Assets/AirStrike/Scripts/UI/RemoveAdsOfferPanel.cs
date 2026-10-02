using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "REMOVE ADS" offer popup (Tools/AirStrike/Create Remove Ads Offer Panel (Mainmenu)).
/// Opened by RemoveAdsOfferTrigger whenever the mode selection shows; never once ads are removed.
/// Close (X) is wired to Close(). CLAIM NOW is not wired: hook it to your purchase and, on success,
/// MainMenuController.RemoveAdsFromGame() (which also hides this panel).
/// Animations use unscaled time: the chevrons glow one after the other toward the emblem, the emblem
/// bobs and the CLAIM NOW button breathes.
/// </summary>
public class RemoveAdsOfferPanel : MonoBehaviour
{
    public RectTransform claimButton;
    public RectTransform emblem;
    [Tooltip("Close (X) button: hidden when the panel opens, pops in after closeDelay. Found as bg/CloseButton if empty.")]
    public RectTransform closeButton;
    [Tooltip("Real seconds before the close button appears.")]
    public float closeDelay = 2f;
    [Tooltip("Ordered from the one nearest the emblem outward.")]
    public Image[] chevrons;

    Vector3 claimBaseScale = Vector3.one;
    Vector3 closeBaseScale = Vector3.one;
    Vector2 emblemBasePos;
    float time;

    void Awake()
    {
        if (closeButton == null) closeButton = transform.Find("bg/CloseButton") as RectTransform;
        if (claimButton != null) claimBaseScale = claimButton.localScale;
        if (closeButton != null) closeBaseScale = closeButton.localScale;
        if (emblem != null) emblemBasePos = emblem.anchoredPosition;
    }

    public void Open()
    {
        if (AdsManager.AdsRemoved) return;
        transform.SetAsLastSibling(); // above the menu panels
        gameObject.SetActive(true);
    }

    /// <summary>Close (X) button.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        time = 0f;
        if (closeButton != null) closeButton.gameObject.SetActive(false); // appears after closeDelay
    }

    void Update()
    {
        // Already purchased: never show, however the panel was switched on (Open, a button's SetActive...).
        // Done here, not in OnEnable (Unity doesn't allow deactivating while activating); Update runs
        // before the frame is drawn.
        if (AdsManager.AdsRemoved)
        {
            gameObject.SetActive(false);
            return;
        }

        time += Time.unscaledDeltaTime;

        // Close button: pops in (with a small overshoot) once closeDelay has passed
        if (closeButton != null && time >= closeDelay)
        {
            if (!closeButton.gameObject.activeSelf) closeButton.gameObject.SetActive(true);
            float p = Mathf.Clamp01((time - closeDelay) / 0.25f);
            float pop = p < 1f ? Mathf.Sin(p * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(p * Mathf.PI)) : 1f;
            closeButton.localScale = closeBaseScale * pop;
        }

        if (claimButton != null)
            claimButton.localScale = claimBaseScale * (1f + 0.045f * Mathf.Sin(time * 3.2f));

        if (emblem != null)
            emblem.anchoredPosition = emblemBasePos + new Vector2(0f, 6f * Mathf.Sin(time * 2f));

        // A glow travels from the outer chevron to the one nearest the emblem ("<<<" pointing at it)
        if (chevrons != null)
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null) continue;
                int fromOuter = chevrons.Length - 1 - i;
                float wave = Mathf.Repeat(time * 1.6f - fromOuter * 0.22f, 1f);
                float glow = Mathf.Clamp01(1f - Mathf.Abs(wave - 0.5f) * 3f);
                Color c = chevrons[i].color;
                c.a = 0.35f + 0.65f * glow;
                chevrons[i].color = c;
                chevrons[i].rectTransform.localScale = Vector3.one * (1f + 0.12f * glow);
            }
    }
}
