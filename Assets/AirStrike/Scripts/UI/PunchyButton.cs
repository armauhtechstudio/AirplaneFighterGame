using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Tactile press feedback for buttons: quick squash on press-down, springy overshoot release
/// on press-up — like a trigger click, instead of a flat instant color-swap.
/// Uses unscaled time so it still works on buttons inside paused (Time.timeScale == 0) panels.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PunchyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public float pressScale = 0.9f;
    public float pressDuration = 0.08f;
    public float releaseOvershoot = 1.08f;
    public float releaseDuration = 0.18f;

    RectTransform rect;
    Vector3 baseScale = Vector3.one;
    Coroutine playing;

    void Awake()
    {
        rect = transform as RectTransform;
        baseScale = rect.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Awake can run while a parent's intro animation has us at scale 0 — never spring back to 0
        if (baseScale.sqrMagnitude < 0.0001f)
            baseScale = rect.localScale.sqrMagnitude > 0.0001f ? rect.localScale : Vector3.one;

        Play(PressDown());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Play(ReleaseUp());
    }

    void Play(IEnumerator co)
    {
        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(co);
    }

    IEnumerator PressDown()
    {
        Vector3 start = rect.localScale;
        Vector3 target = baseScale * pressScale;
        float t = 0f;
        while (t < pressDuration)
        {
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(start, target, t / pressDuration);
            yield return null;
        }
        rect.localScale = target;
    }

    // Elastic-feeling overshoot: shoots past the target then settles back, like recoil.
    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = (overshoot - 1f) * 2f + 1f;
        float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    IEnumerator ReleaseUp()
    {
        Vector3 start = rect.localScale;
        float t = 0f;
        while (t < releaseDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / releaseDuration);
            rect.localScale = Vector3.LerpUnclamped(start, baseScale, EaseOutBack(p, releaseOvershoot));
            yield return null;
        }
        rect.localScale = baseScale;
    }
}
