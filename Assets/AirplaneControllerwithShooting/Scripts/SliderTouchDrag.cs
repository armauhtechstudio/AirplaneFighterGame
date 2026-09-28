using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AirplaneControllerwithShooting
{
    // Makes a Slider follow the finger that pressed it, every frame until that finger lifts.
    // It relies only on the press (which always reaches the slider) and then reads that exact
    // touch directly, so it works even when the EventSystem's own drag never starts, and it
    // keeps working while other fingers are on the joystick / buttons.
    [RequireComponent(typeof(Slider))]
    public class SliderTouchDrag : MonoBehaviour, IPointerDownHandler
    {
        Slider slider;
        bool dragging;
        int pointerId;
        Camera eventCamera;

        void Awake()
        {
            slider = GetComponent<Slider>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!slider.IsActive() || !slider.IsInteractable()) return;
            dragging = true;
            pointerId = eventData.pointerId;
            eventCamera = eventData.pressEventCamera;
            SetFromScreen(eventData.position);
        }

        void OnDisable()
        {
            dragging = false;
        }

        void Update()
        {
            if (!dragging) return;

            if (TryGetPointer(out Vector2 position))
                SetFromScreen(position);
            else
                dragging = false; // finger lifted / cancelled
        }

        // Touch pointers use the finger id; mouse pointers (editor) are negative ids
        bool TryGetPointer(out Vector2 position)
        {
            if (pointerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.fingerId != pointerId) continue;
                    position = t.position;
                    return t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                }
                position = default;
                return false;
            }

            position = Input.mousePosition;
            return Input.GetMouseButton(0);
        }

        // Finger position along the handle's travel area -> slider value
        void SetFromScreen(Vector2 screen)
        {
            RectTransform area = slider.handleRect != null && slider.handleRect.parent != null
                ? (RectTransform)slider.handleRect.parent
                : (RectTransform)slider.transform;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, eventCamera, out Vector2 local))
                return;

            Rect r = area.rect;
            float t;
            switch (slider.direction)
            {
                case Slider.Direction.LeftToRight: t = Mathf.InverseLerp(r.xMin, r.xMax, local.x); break;
                case Slider.Direction.RightToLeft: t = Mathf.InverseLerp(r.xMax, r.xMin, local.x); break;
                case Slider.Direction.TopToBottom: t = Mathf.InverseLerp(r.yMax, r.yMin, local.y); break;
                default: t = Mathf.InverseLerp(r.yMin, r.yMax, local.y); break; // BottomToTop
            }
            slider.normalizedValue = t; // fires onValueChanged -> engine power
        }
    }
}
