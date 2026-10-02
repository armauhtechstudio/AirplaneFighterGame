using UnityEngine;

/// <summary>
/// Animates the Remove Ads button so it stands out from the other menu buttons:
///  - intro: pops in with an overshoot shortly after the menu shows
///  - breathing pulse
///  - a gold shine sweeping across the plate every few seconds
///  - an attention wiggle (quick damped wobble) every few seconds
/// Unscaled time, so it also runs while the game is paused. The press "punch" stays on PunchyButton
/// (on the parent), so they don't fight over the scale.
/// </summary>
public class RemoveAdsButtonFx : MonoBehaviour
{
    [Tooltip("Animated part (a child of the button, not the button itself).")]
    public RectTransform pulseTarget;
    [Tooltip("Shine band, a child of the plate (the plate's Mask clips it to the plate).")]
    public RectTransform shine;

    [Header("Intro")]
    public float introDelay = 0.35f;
    public float introDuration = 0.45f;
    public float introOvershoot = 1.18f;

    [Header("Pulse")]
    public float pulseAmount = 0.035f;
    public float pulseSpeed = 2.4f;

    [Header("Shine")]
    public float shineInterval = 3.2f;
    public float shineDuration = 0.75f;

    [Header("Wiggle")]
    public float wiggleInterval = 5f;
    public float wiggleDuration = 0.55f;
    public float wiggleAngle = 7f;

    Vector3 baseScale = Vector3.one;
    float time;

    void OnEnable()
    {
        if (pulseTarget != null)
        {
            if (pulseTarget.localScale.sqrMagnitude > 0.0001f) baseScale = pulseTarget.localScale;
            pulseTarget.localScale = Vector3.zero; // hidden until the intro pops it in
            pulseTarget.localRotation = Quaternion.identity;
        }
        time = 0f;
    }

    void OnDisable()
    {
        if (pulseTarget != null)
        {
            pulseTarget.localScale = baseScale;
            pulseTarget.localRotation = Quaternion.identity;
        }
        if (shine != null) shine.gameObject.SetActive(false);
    }

    void Update()
    {
        time += Time.unscaledDeltaTime;
        float t = time - introDelay - introDuration; // time since the intro finished

        if (pulseTarget != null)
        {
            float scale;
            if (time < introDelay) scale = 0f;
            else if (t < 0f) scale = EaseOutBack((time - introDelay) / introDuration, introOvershoot);
            else scale = 1f + pulseAmount * Mathf.Sin(t * pulseSpeed);
            pulseTarget.localScale = baseScale * scale;

            // Wiggle: damped wobble at the start of each interval (first one 1.5s after the intro)
            float angle = 0f;
            if (t > 0f)
            {
                float w = (t + wiggleInterval - 1.5f) % wiggleInterval;
                if (w < wiggleDuration)
                {
                    float p = w / wiggleDuration;
                    angle = wiggleAngle * Mathf.Sin(p * Mathf.PI * 5f) * (1f - p);
                }
            }
            pulseTarget.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Shine sweep (first one 0.6s after the intro)
        if (shine == null) return;
        var plate = shine.parent as RectTransform;
        float phase = t > 0f ? (t + shineInterval - 0.6f) % shineInterval : -1f;
        bool sweeping = phase >= 0f && phase < shineDuration && plate != null;
        if (shine.gameObject.activeSelf != sweeping) shine.gameObject.SetActive(sweeping);
        if (!sweeping) return;

        float halfTravel = plate.rect.width * 0.5f + shine.rect.width;
        float s = phase / shineDuration;
        s = s * s * (3f - 2f * s); // ease in-out
        shine.anchoredPosition = new Vector2(Mathf.Lerp(-halfTravel, halfTravel, s), 0f);
    }

    // Shoots past the target then settles back
    static float EaseOutBack(float x, float overshoot)
    {
        x = Mathf.Clamp01(x);
        float c1 = (overshoot - 1f) * 2f + 1f, c3 = c1 + 1f;
        x -= 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
