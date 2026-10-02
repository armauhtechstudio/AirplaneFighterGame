using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Drop this on any UI panel to give it a punchy pop-in every time it's shown
/// (SetActive(true) / OnEnable), instead of just snapping into view. Optionally a short
/// delay before it starts, and its direct children (buttons/text) can cascade in one after
/// another instead of appearing all at once.
/// Uses unscaled time so it still animates while Time.timeScale == 0 (pause/win/fail panels).
/// </summary>
public class PunchyPanel : MonoBehaviour
{
    [Header("Panel Pop-in")]
    [Tooltip("Wait this long (unscaled seconds) after the panel is shown before popping in.")]
    public float startDelay = 0.12f;
    public float introDuration = 0.28f;
    public float introOvershoot = 1.12f;

    [Header("Staggered Children")]
    [Tooltip("Direct children (buttons, text, etc.) pop in one after another instead of all at once.")]
    public bool staggerChildren = true;
    public float childStagger = 0.07f;
    public float childIntroDuration = 0.22f;
    public float childIntroOvershoot = 1.2f;

    RectTransform rect;
    Vector3 baseScale = Vector3.one;
    readonly List<RectTransform> children = new List<RectTransform>();
    readonly List<Vector3> childBaseScales = new List<Vector3>();
    Coroutine playing;

    void Awake()
    {
        rect = transform as RectTransform;
        if (rect == null) return;

        baseScale = rect.localScale;

        for (int i = 0; i < rect.childCount; i++)
        {
            RectTransform child = rect.GetChild(i) as RectTransform;
            if (child == null) continue;
            children.Add(child);
            childBaseScales.Add(child.localScale);
        }
    }

    void OnEnable()
    {
        if (rect == null) return;
        if (playing != null) StopCoroutine(playing);
        if (Application.isPlaying) GameSfx.Popup(); // soft whoosh as the popup opens

        // Hide the panel right away: showing it at full size during startDelay and then snapping to
        // zero made it flash in, vanish and pop in again. Only the panel itself — children are zeroed
        // later in PlayIntro, because their own Awake (e.g. PunchyButton caching its normal scale)
        // runs after this OnEnable and would otherwise remember a scale of 0.
        rect.localScale = Vector3.zero;
        playing = StartCoroutine(PlayIntro());
    }

    // Elastic-feeling overshoot: shoots past the target then settles back, like recoil.
    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = (overshoot - 1f) * 2f + 1f;
        float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    IEnumerator PlayIntro()
    {
        if (startDelay > 0f)
            yield return new WaitForSecondsRealtime(startDelay);
        else
            yield return null; // let the children's Awake run first

        if (staggerChildren)
        {
            for (int i = 0; i < children.Count; i++)
            {
                children[i].localScale = Vector3.zero;
            }
        }

        StartCoroutine(ScaleTo(rect, baseScale, introDuration, introOvershoot, 0f));

        if (staggerChildren)
        {
            for (int i = 0; i < children.Count; i++)
            {
                StartCoroutine(ScaleTo(children[i], childBaseScales[i], childIntroDuration, childIntroOvershoot, i * childStagger));
            }
        }
        else
        {
            for (int i = 0; i < children.Count; i++)
            {
                children[i].localScale = childBaseScales[i];
            }
        }
    }

    static IEnumerator ScaleTo(RectTransform target, Vector3 targetScale, float duration, float overshoot, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            target.localScale = targetScale * EaseOutBack(p, overshoot);
            yield return null;
        }
        target.localScale = targetScale;
    }
}
