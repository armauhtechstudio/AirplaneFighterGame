using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tutorial spotlight for the Shoot button: dims the whole screen to 50% black, keeps the Shoot
// button bright (drawn above the dim) and pulsing, and shows a "press SHOOT" hint next to it.
// Added to the Shoot button by TutorialManager; removes itself on the first press.
public class TutorialShootFocus : MonoBehaviour, IPointerDownHandler
{
    const int OverlayOrder = 500;
    const string Message = "Press the SHOOT button on the right side to fire!";

    GameObject overlay;
    Canvas buttonCanvas;
    bool addedButtonCanvas;
    Vector3 baseScale;
    Coroutine pulse;

    public static TutorialShootFocus Show(Button shootButton)
    {
        if (shootButton == null) return null;
        var focus = shootButton.GetComponent<TutorialShootFocus>();
        if (focus == null) focus = shootButton.gameObject.AddComponent<TutorialShootFocus>();
        return focus;
    }

    void Start()
    {
        baseScale = transform.localScale;
        CreateOverlay();
        RaiseButton(true);
        pulse = StartCoroutine(Pulse());
    }

    void Update()
    {
        // Pause / win / fail panels freeze time: don't cover them with the dim
        bool show = Time.timeScale > 0f;
        if (overlay != null && overlay.activeSelf != show)
        {
            overlay.SetActive(show);
            RaiseButton(show);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Hide();
    }

    public void Hide()
    {
        Destroy(this);
    }

    void OnDestroy()
    {
        if (pulse != null) StopCoroutine(pulse);
        transform.localScale = baseScale;
        RaiseButton(false);
        if (overlay != null) Destroy(overlay);
    }

    // A nested canvas with override sorting draws the button above the dim overlay and keeps it clickable
    void RaiseButton(bool raise)
    {
        if (buttonCanvas == null)
        {
            if (!raise) return;
            buttonCanvas = GetComponent<Canvas>();
            if (buttonCanvas == null)
            {
                buttonCanvas = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<GraphicRaycaster>();
                addedButtonCanvas = true;
            }
        }
        if (!addedButtonCanvas) return; // never touch a canvas the scene already had

        buttonCanvas.overrideSorting = raise;
        buttonCanvas.sortingOrder = raise ? OverlayOrder + 1 : 0;
    }

    void CreateOverlay()
    {
        Canvas sourceCanvas = GetComponentInParent<Canvas>().rootCanvas;

        overlay = new GameObject("TutorialShootFocusOverlay", typeof(Canvas), typeof(CanvasScaler));
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlayOrder;

        // Same scaling as the controls' canvas so the hint lines up with the button on every screen
        var scaler = overlay.GetComponent<CanvasScaler>();
        var sourceScaler = sourceCanvas.GetComponent<CanvasScaler>();
        if (sourceScaler != null)
        {
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            scaler.scaleFactor = sourceScaler.scaleFactor;
        }

        // 50% black over everything; doesn't block touches so the player can still steer
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        var dimRT = (RectTransform)dim.transform;
        dimRT.SetParent(overlay.transform, false);
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = dimRT.offsetMax = Vector2.zero;
        var dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.5f);
        dimImage.raycastTarget = false;

        CreateHint(overlay.transform as RectTransform, canvas);
    }

    // Hint text right-aligned just left of the button, with an arrow pointing at it
    void CreateHint(RectTransform overlayRT, Canvas overlayCanvas)
    {
        Canvas.ForceUpdateCanvases();

        var hint = new GameObject("Hint", typeof(RectTransform), typeof(Text), typeof(Outline));
        var rt = (RectTransform)hint.transform;
        rt.SetParent(overlayRT, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 160f);

        // Button's left-middle edge in overlay space
        var buttonRT = (RectTransform)transform;
        Vector3[] corners = new Vector3[4];
        buttonRT.GetWorldCorners(corners);
        Vector2 leftMiddle = (corners[0] + corners[1]) * 0.5f;
        Camera cam = GetComponentInParent<Canvas>().rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : GetComponentInParent<Canvas>().rootCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, leftMiddle);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRT, screen, null, out Vector2 local);
        rt.anchoredPosition = local + new Vector2(-30f, 0f);

        var text = hint.GetComponent<Text>();
        Text existing = GetComponentInChildren<Text>(true);
        text.font = existing != null ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 46;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleRight;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = Message + "  →";
        hint.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
    }

    IEnumerator Pulse()
    {
        while (true)
        {
            float s = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            transform.localScale = baseScale * s;
            yield return null;
        }
    }
}
