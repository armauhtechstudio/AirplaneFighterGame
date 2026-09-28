using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AirplaneControllerwithShooting
{
    // Level-start hint for mobile: a pulsing "Tap or drag ENGINE to control speed" label with an
    // arrow pointing at the engine slider. Waits until gameplay runs (objective popup closed),
    // hides while the game is paused, and removes itself the first time the slider is touched.
    [RequireComponent(typeof(Slider))]
    public class EngineSliderHint : MonoBehaviour, IPointerDownHandler
    {
        public string message = "Tap or drag ENGINE to start\n& control your speed";

        RectTransform hint;
        Text label;
        Vector3 baseScale = Vector3.one;

        void Start()
        {
            CreateHint();
            hint.gameObject.SetActive(false);
        }

        void Update()
        {
            if (hint == null) return;

            // Only while playing: not over the objective / pause / win / fail popups (they freeze time)
            bool show = Time.timeScale > 0f;
            if (hint.gameObject.activeSelf != show) hint.gameObject.SetActive(show);
            if (!show) return;

            float s = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f);
            hint.localScale = baseScale * s;
            label.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Destroy(this);
        }

        void OnDestroy()
        {
            if (hint != null) Destroy(hint.gameObject);
        }

        // Label to the left of the slider, vertically centred on it, drawn on the same canvas
        void CreateHint()
        {
            var sliderRT = (RectTransform)transform;
            var parent = (RectTransform)sliderRT.parent;

            var go = new GameObject("EngineSliderHint", typeof(RectTransform), typeof(Text), typeof(Outline));
            hint = (RectTransform)go.transform;
            hint.SetParent(parent, false);
            hint.SetAsLastSibling();
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0.5f);
            hint.pivot = new Vector2(1f, 0.5f);
            hint.sizeDelta = new Vector2(520f, 140f);

            Vector3[] corners = new Vector3[4];
            sliderRT.GetWorldCorners(corners);
            Vector3 leftMiddle = (corners[0] + corners[1]) * 0.5f;
            Vector2 local = parent.InverseTransformPoint(leftMiddle);
            hint.anchoredPosition = local - parent.rect.center + new Vector2(-70f, 0f); // clear of the touch area

            label = go.GetComponent<Text>();
            Text existing = GetComponentInChildren<Text>(true);
            label.font = existing != null ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 40;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleRight;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = message + "  →";
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            go.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            baseScale = hint.localScale;
        }
    }
}
