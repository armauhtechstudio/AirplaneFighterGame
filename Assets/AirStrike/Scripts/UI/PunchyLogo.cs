using UnityEngine;
using System.Collections;

/// <summary>
/// Drop this on (or point it at) a logo RectTransform for a punchy pop-in intro
/// followed by a subtle rhythmic recoil-style idle kick, instead of a static logo.
/// </summary>
public class PunchyLogo : MonoBehaviour
{
    [Tooltip("Leave empty to use this GameObject's own RectTransform.")]
    public RectTransform logo;

    [Header("Intro (punchy pop-in)")]
    public float introDuration = 0.45f;
    public float introOvershoot = 1.18f;

    [Header("Idle (rhythmic recoil kick)")]
    public float idleKickInterval = 1.4f;
    public float idleKickStrength = 0.09f;
    public float idleKickDuration = 0.18f;

    Vector3 baseScale = Vector3.one;

    void Start()
    {
        if (logo == null) logo = transform as RectTransform;
        if (logo == null) return;

        baseScale = logo.localScale;
        logo.localScale = Vector3.zero;
        StartCoroutine(IntroThenIdle());
    }

    // Elastic-feeling overshoot: shoots past the target then settles back, like recoil.
    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = (overshoot - 1f) * 2f + 1f;
        float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    IEnumerator IntroThenIdle()
    {
        float t = 0f;
        while (t < introDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / introDuration);
            logo.localScale = baseScale * EaseOutBack(p, introOvershoot);
            yield return null;
        }
        logo.localScale = baseScale;

        while (true)
        {
            yield return new WaitForSeconds(idleKickInterval);
            yield return StartCoroutine(KickPulse());
        }
    }

    IEnumerator KickPulse()
    {
        float t = 0f;
        while (t < idleKickDuration)
        {
            t += Time.deltaTime;
            float p = t / idleKickDuration;
            // Quick punch out, softer settle back — asymmetric like an impulse, not a smooth sine.
            float kick = Mathf.Sin(p * Mathf.PI) * idleKickStrength * (1f - p * 0.3f);
            logo.localScale = baseScale * (1f + kick);
            yield return null;
        }
        logo.localScale = baseScale;
    }
}
