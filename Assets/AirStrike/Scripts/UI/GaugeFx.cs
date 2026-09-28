using UnityEngine;
using UnityEngine.UI;

// "Alive" gauge for a vertical Slider (engine throttle, fuel): fill colour follows the level, a light
// band flows up through the fill, the scale ticks light up to the current level, and optionally the
// knob glows / rumbles with power and the gauge flashes when low. Uses unscaled time.
[RequireComponent(typeof(Slider))]
public class GaugeFx : MonoBehaviour
{
    [Header("Parts (set up by the Mission Low setup tools)")]
    public Image fill;
    public RectTransform shine;          // child of the fill (clipped by a RectMask2D on the fill)
    public RectTransform ticksLit;       // RectMask2D container; its height follows the level
    public Image knobGlow;               // optional soft glow behind the knob
    public Graphic[] flashOnLow;         // optional (e.g. the fuel icon / value text)

    [Header("Colour by level (0 -> 1)")]
    public Color low = new Color(0.30f, 0.90f, 0.35f);
    public Color mid = new Color(1.00f, 0.80f, 0.20f);
    public Color high = new Color(1.00f, 0.30f, 0.18f);

    [Header("Motion")]
    [Tooltip("Seconds for the shine to travel the fill at the lowest / highest level.")]
    public float shineSecondsSlow = 2.2f, shineSecondsFast = 0.6f;
    public bool glowPulse = true;
    [Tooltip("Knob shake above this level (0-1), like an engine at full throttle. >1 = never.")]
    public float rumbleAbove = 0.85f;

    [Header("Low warning")]
    [Tooltip("Flash red below this level (0-1). 0 = never.")]
    public float lowWarning = 0f;

    Slider slider;
    RectTransform handle;
    Vector2 handleRest;
    float shinePhase;
    Color[] flashBase;

    void Awake()
    {
        slider = GetComponent<Slider>();
        handle = slider.handleRect;
        if (flashOnLow != null)
        {
            flashBase = new Color[flashOnLow.Length];
            for (int i = 0; i < flashOnLow.Length; i++) if (flashOnLow[i] != null) flashBase[i] = flashOnLow[i].color;
        }
    }

    void LateUpdate()
    {
        float v = slider.normalizedValue;
        float time = Time.unscaledTime;
        Color c = v < 0.5f ? Color.Lerp(low, mid, v * 2f) : Color.Lerp(mid, high, (v - 0.5f) * 2f);

        // Low warning: flash between the level colour and bright red
        bool warn = lowWarning > 0f && v < lowWarning;
        float blink = warn ? 0.5f + 0.5f * Mathf.Sin(time * 10f) : 0f;
        if (warn) c = Color.Lerp(c, new Color(1f, 0.12f, 0.08f), blink);
        if (fill != null) fill.color = c;

        // Light band flowing up through the fill, faster at higher levels
        if (shine != null && fill != null)
        {
            float seconds = Mathf.Lerp(shineSecondsSlow, shineSecondsFast, v);
            shinePhase = Mathf.Repeat(shinePhase + Time.unscaledDeltaTime / seconds, 1f);
            var fillRT = (RectTransform)fill.transform;
            float h = fillRT.rect.height, bandH = shine.rect.height;
            shine.anchoredPosition = new Vector2(0f, Mathf.Lerp(-bandH, h + bandH, shinePhase) - h * 0.5f);

            // A short fill has pointed ends; the rectangular band would poke out, so fade it out there
            var shineImg = shine.GetComponent<Graphic>();
            if (shineImg != null)
            {
                Color s = shineImg.color;
                s.a = 0.55f * Mathf.Clamp01((h - 50f) / 60f);
                shineImg.color = s;
            }
        }

        // Ticks light up to the current level
        if (ticksLit != null)
        {
            var parent = (RectTransform)ticksLit.parent;
            ticksLit.sizeDelta = new Vector2(ticksLit.sizeDelta.x, parent.rect.height * v - parent.rect.height);
        }

        // Knob: glow pulses (quicker with power); rumble near full power
        if (knobGlow != null)
        {
            if (handle != null) knobGlow.rectTransform.position = handle.position; // sits behind the knob
            float speed = Mathf.Lerp(2f, 9f, v);
            float pulse = glowPulse ? 0.5f + 0.5f * Mathf.Sin(time * speed) : 1f;
            Color g = c;
            g.a = Mathf.Lerp(0.25f, 0.75f, v) * Mathf.Lerp(0.6f, 1f, pulse);
            knobGlow.color = g;
            knobGlow.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse * v);
        }
        if (handle != null)
        {
            if (handleRest == Vector2.zero) handleRest = new Vector2(handle.anchoredPosition.x, 0f);
            float shake = v > rumbleAbove ? (v - rumbleAbove) / Mathf.Max(0.01f, 1f - rumbleAbove) : 0f;
            float jx = shake > 0f ? (Mathf.PerlinNoise(time * 40f, 0.3f) - 0.5f) * 3f * shake : 0f;
            handle.anchoredPosition = new Vector2(handleRest.x + jx, handle.anchoredPosition.y);
        }

        // Low warning also flashes the linked icons / texts
        if (flashOnLow != null && flashBase != null)
            for (int i = 0; i < flashOnLow.Length; i++)
                if (flashOnLow[i] != null)
                    flashOnLow[i].color = warn ? Color.Lerp(flashBase[i], new Color(1f, 0.2f, 0.15f), blink) : flashBase[i];
    }
}
