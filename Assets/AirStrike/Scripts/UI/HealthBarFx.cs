using UnityEngine;
using UnityEngine.UI;

// Lively horizontal health bar:
//  - fill colour follows health: green -> gold -> red
//  - on damage the fill flashes white, the bar shakes, and a pale "damage trail" shows what was lost
//    before draining away after a short delay; healing snaps the trail up
//  - a light sheen sweeps across the fill now and then
//  - below the low threshold the heart icon beats and the fill pulses
// Uses unscaled time (keeps animating on pause panels).
[RequireComponent(typeof(Slider))]
public class HealthBarFx : MonoBehaviour
{
    public Image fill;
    public RectTransform trail;          // sibling of the fill inside the Fill Area
    public RectTransform shine;          // child of the fill (clipped by a RectMask2D on the fill)
    public RectTransform heartIcon;      // optional

    public Color full = new Color(0.30f, 0.90f, 0.35f);
    public Color half = new Color(1.00f, 0.80f, 0.20f);
    public Color empty = new Color(1.00f, 0.22f, 0.15f);
    [Range(0f, 1f)] public float lowThreshold = 0.3f;
    public float trailDelay = 0.45f;
    public float trailSpeed = 0.7f;      // fraction of the bar per second
    public float shakePixels = 6f;

    Slider slider;
    RectTransform rt;
    Vector2 restPos;
    float lastValue = -1f, trailValue, flash, shake, delay, sheenT;

    void Awake()
    {
        slider = GetComponent<Slider>();
        rt = (RectTransform)transform;
        restPos = rt.anchoredPosition;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime, time = Time.unscaledTime;
        float v = slider.normalizedValue;
        if (lastValue < 0f) { lastValue = trailValue = v; }

        if (v < lastValue - 0.0001f)        // took damage
        {
            flash = 1f;
            shake = 0.3f;
            delay = trailDelay;
        }
        else if (v > lastValue)              // healed / refilled
        {
            trailValue = Mathf.Max(trailValue, v);
        }
        lastValue = v;

        // Damage trail drains after a short delay
        delay -= dt;
        if (delay <= 0f) trailValue = Mathf.MoveTowards(trailValue, v, trailSpeed * dt);
        trailValue = Mathf.Max(trailValue, v);
        if (trail != null) trail.anchorMax = new Vector2(trailValue, trail.anchorMax.y);

        // Colour by health, white flash on hit, pulse when low
        Color c = v > 0.5f ? Color.Lerp(half, full, (v - 0.5f) * 2f) : Color.Lerp(empty, half, v * 2f);
        bool low = v < lowThreshold && v > 0f;
        float beat = low ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 7f)), 6f) : 0f; // heartbeat-like spikes
        if (low) c = Color.Lerp(c, Color.white, 0.35f * beat);
        flash = Mathf.Max(0f, flash - dt * 4f);
        if (fill != null) fill.color = Color.Lerp(c, Color.white, flash * 0.8f);

        // Shake on hit
        shake = Mathf.Max(0f, shake - dt);
        Vector2 offset = shake > 0f
            ? new Vector2((Mathf.PerlinNoise(time * 50f, 0f) - 0.5f) * 2f, (Mathf.PerlinNoise(0f, time * 50f) - 0.5f) * 2f) * shakePixels * (shake / 0.3f)
            : Vector2.zero;
        rt.anchoredPosition = restPos + offset;

        // Heart icon beats when low, pops on hit
        if (heartIcon != null)
            heartIcon.localScale = Vector3.one * (1f + 0.25f * beat + 0.3f * flash);

        // Sheen sweeping across the fill every few seconds
        if (shine != null && fill != null)
        {
            sheenT += dt;
            const float sweep = 0.9f, cycle = 3f;
            float p = Mathf.Repeat(sheenT, cycle) / sweep;
            var fillRT = (RectTransform)fill.transform;
            float w = fillRT.rect.width, bandW = shine.rect.width;
            shine.gameObject.SetActive(p <= 1f && w > 40f);
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-bandW, w + bandW, Mathf.Clamp01(p)) - w * 0.5f, 0f);
        }
    }
}
