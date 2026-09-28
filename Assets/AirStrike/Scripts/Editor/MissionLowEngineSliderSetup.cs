using System.Collections.Generic;
using System.Text;
using AirplaneControllerwithShooting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Re-skins Mission Low's two vertical gauges in the military-metal style, with motion (GaugeFx):
//  - ENGINE (throttle) slider: steel slot, glowing power fill (green -> gold -> red) with a light band
//    flowing up it, ticks that light up to the level, gold-bevelled steel knob with a pulsing glow
//    and a slight rumble at full throttle
//  - FUEL gauge: same slot / fill / ticks (red when low -> amber when full), flashes red below 20%
// Art: Assets/SS/Gameplay/Engine (Tools/AirStrike/Generate Engine Slider Art).
// Only visuals change: slider values, touch/drag handling and hints keep working. Safe to re-run.
public static class MissionLowEngineSliderSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    const string Art = EngineSliderArtGenerator.Folder;
    static readonly Vector2 KnobSize = new Vector2(120f, 94f);
    static readonly Color TickGold = new Color(1f, 0.82f, 0.3f, 1f);

    [MenuItem("Tools/AirStrike/Engine Slider Visuals (Mission Low)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MissionLowEngineSliderSetup]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        GameCanvas gc = null;
        GameManagerMode2 gm = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (gc == null) gc = root.GetComponentInChildren<GameCanvas>(true);
            if (gm == null) gm = root.GetComponentInChildren<GameManagerMode2>(true);
        }
        if (gc == null || gc.Slider_EnginePower == null) return "ERROR: GameCanvas / engine slider not found.";

        var art = new Dictionary<string, Sprite>();
        foreach (string name in new[] { "engine_track", "engine_fill", "engine_ticks", "engine_knob", "engine_shine", "engine_glow" })
        {
            art[name] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/{name}.png");
            if (art[name] == null) return $"ERROR: {name}.png missing — run Tools/AirStrike/Generate Engine Slider Art first.";
        }
        Font font = gm != null && gm.housesText != null ? gm.housesText.font : null; // Bulletproof
        var log = new StringBuilder();

        // ---- ENGINE ----
        Slider engine = gc.Slider_EnginePower;
        GaugeFx engineFx = SkinGauge(engine, art, log, true);
        SkinKnob(engine, art, font, log);
        engineFx.knobGlow = MakeGlow(engine, art["engine_glow"]);
        engineFx.low = new Color(0.30f, 0.90f, 0.35f);
        engineFx.mid = new Color(1.00f, 0.80f, 0.20f);
        engineFx.high = new Color(1.00f, 0.30f, 0.18f);
        engineFx.rumbleAbove = 0.85f;
        engineFx.lowWarning = 0f;
        PrefabOverrides.Record(engineFx);

        // Mobile grows the knob to at least this size at runtime: keep the new knob's proportions
        var gcSO = new SerializedObject(gc);
        var handleSize = gcSO.FindProperty("engineHandleSize");
        if (handleSize != null) { handleSize.vector2Value = KnobSize; gcSO.ApplyModifiedProperties(); }

        // ---- FUEL ----
        Slider fuel = gc.Slider_CurrentFuel;
        if (fuel != null)
        {
            log.AppendLine("Fuel gauge before:");
            foreach (Transform t in fuel.GetComponentsInChildren<Transform>(true))
                log.AppendLine($"  {t.name} {(t.GetComponent<Graphic>() != null ? t.GetComponent<Graphic>().GetType().Name : "")} size={((RectTransform)t).rect.size}");

            // Keep the whole steel frame on screen (the fuel bar sat right against the left edge)
            var fuelRT = (RectTransform)fuel.transform;
            var fuelParent = (RectTransform)fuelRT.parent;
            var corners = new Vector3[4];
            fuelRT.GetWorldCorners(corners);
            float left = fuelParent.InverseTransformPoint(corners[0]).x - fuelParent.rect.xMin;
            if (left < 22f) { fuelRT.anchoredPosition += new Vector2(22f - left, 0f); PrefabOverrides.Record(fuelRT); }

            GaugeFx fuelFx = SkinGauge(fuel, art, log, false, true);
            fuelFx.low = new Color(1.00f, 0.25f, 0.15f);
            fuelFx.mid = new Color(1.00f, 0.62f, 0.12f);
            fuelFx.high = new Color(1.00f, 0.84f, 0.20f);
            fuelFx.rumbleAbove = 2f;     // no knob shake
            fuelFx.lowWarning = 0.2f;    // flash red below 20%
            fuelFx.shineSecondsSlow = 3f;
            fuelFx.shineSecondsFast = 2f;

            // Fuel value text + pump icon flash with the warning; text in Bulletproof like the rest
            var flash = new List<Graphic>();
            if (gc.Text_CurrentFuel != null)
            {
                Text value = gc.Text_CurrentFuel;
                if (font != null) value.font = font;
                value.color = Color.white;
                var outline = value.GetComponent<Outline>();
                if (outline == null) outline = value.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);
                PrefabOverrides.Record(value, outline);
                flash.Add(value);
            }
            foreach (Image img in fuel.GetComponentsInChildren<Image>(true))
            {
                if (img == fuelFx.fill || img.transform.IsChildOf(fuel.fillRect) || img.name == "Background" ||
                    img.name == "Ticks" || img.name == "TicksLit" || img.transform.parent.name == "TicksLit") continue;
                if (img.sprite != null && img.sprite.name.StartsWith("engine_")) continue;
                if (img.rectTransform == fuel.handleRect) continue; // zero-height, unused handle
                flash.Add(img); // e.g. the pump icon
                img.transform.SetAsLastSibling(); // keep it on top of the new track / fill
            }
            fuelFx.flashOnLow = flash.ToArray();
            PrefabOverrides.Record(fuelFx);
            log.AppendLine($"Fuel gauge re-skinned; flashing when low: {string.Join(", ", flash.ConvertAll(g => g.name))}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        log.Insert(0, "Engine slider re-skinned (track, fill, shine, ticks, knob, glow).\n");
        return log.ToString();
    }

    // Track + fill (+ shine) + ticks (+ lit ticks) + GaugeFx on a vertical slider
    static GaugeFx SkinGauge(Slider slider, Dictionary<string, Sprite> art, StringBuilder log, bool touchable, bool ticksOnRight = false)
    {
        var sliderRT = (RectTransform)slider.transform;

        // Track: steel slot, a little wider than the slider and past both ends so the fill sits inside it
        Transform bgT = sliderRT.Find("Background");
        RectTransform bg = bgT != null ? (RectTransform)bgT : NewUI("Background", sliderRT);
        bg.SetSiblingIndex(0);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.offsetMin = new Vector2(-8f, -22f);
        bg.offsetMax = new Vector2(8f, 22f);
        SetImage(bg, art["engine_track"], Image.Type.Sliced, Color.white, touchable); // only the engine is draggable
        PrefabOverrides.Record(bg, bg.gameObject);

        Image fillImg = null;
        if (slider.fillRect != null)
        {
            var fillArea = (RectTransform)slider.fillRect.parent;
            if (fillArea != sliderRT)
            {
                fillArea.anchorMin = new Vector2(0.3f, 0f);
                fillArea.anchorMax = new Vector2(0.7f, 1f);
                fillArea.offsetMin = new Vector2(0f, 2f);
                fillArea.offsetMax = new Vector2(0f, -2f);
                PrefabOverrides.Record(fillArea);
            }
            var fillRT = slider.fillRect;
            fillRT.offsetMin = new Vector2(0f, fillRT.offsetMin.y);
            fillRT.offsetMax = new Vector2(0f, fillRT.offsetMax.y);
            fillImg = SetImage(fillRT, art["engine_fill"], Image.Type.Sliced, Color.white, false);
            fillImg.enabled = true;                               // was switched off in the prefab
            fillRT.gameObject.SetActive(true);
            if (fillRT.GetComponent<RectMask2D>() == null) fillRT.gameObject.AddComponent<RectMask2D>(); // clips the shine
            PrefabOverrides.Record(fillRT, fillImg, fillRT.gameObject);
        }

        // Ticks (dim) and lit ticks (gold, grow with the level)
        RectTransform ticks = Child(sliderRT, "Ticks");
        ticks.SetSiblingIndex(1);
        PlaceTicks(ticks, ticksOnRight);
        SetImage(ticks, art["engine_ticks"], Image.Type.Tiled, new Color(1f, 1f, 1f, 0.45f), false);

        RectTransform lit = Child(sliderRT, "TicksLit");
        lit.SetSiblingIndex(2);
        PlaceTicks(lit, ticksOnRight);
        lit.pivot = new Vector2(0.5f, 0f); // grows up from the bottom
        if (lit.GetComponent<RectMask2D>() == null) lit.gameObject.AddComponent<RectMask2D>();
        RectTransform litTicks = Child(lit, "Ticks");
        litTicks.anchorMin = new Vector2(0f, 0f);
        litTicks.anchorMax = new Vector2(1f, 0f);
        litTicks.pivot = new Vector2(0.5f, 0f);
        litTicks.anchoredPosition = Vector2.zero;
        litTicks.sizeDelta = new Vector2(0f, sliderRT.rect.height - 8f);
        SetImage(litTicks, art["engine_ticks"], Image.Type.Tiled, TickGold, false);

        // Flowing light band inside the fill
        RectTransform shine = null;
        if (fillImg != null)
        {
            shine = Child(fillImg.rectTransform, "Shine");
            shine.anchorMin = new Vector2(0f, 0.5f);
            shine.anchorMax = new Vector2(1f, 0.5f);
            shine.pivot = new Vector2(0.5f, 0.5f);
            shine.sizeDelta = new Vector2(0f, 70f);
            SetImage(shine, art["engine_shine"], Image.Type.Simple, new Color(1f, 1f, 1f, 0.55f), false);
        }

        var fx = slider.GetComponent<GaugeFx>();
        if (fx == null) fx = slider.gameObject.AddComponent<GaugeFx>();
        fx.fill = fillImg;
        fx.shine = shine;
        fx.ticksLit = lit;
        log.AppendLine($"  {slider.name}: track/fill/ticks/shine done");
        return fx;
    }

    // Scale ticks beside the track: left of it (engine) or right of it (fuel, which sits at the screen edge)
    static void PlaceTicks(RectTransform rt, bool right)
    {
        float side = right ? 1f : 0f;
        rt.anchorMin = new Vector2(side, 0f);
        rt.anchorMax = new Vector2(side, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f); // flip (right side) around the centre, not onto the track
        rt.offsetMin = new Vector2(right ? 10f : -40f, 4f);
        rt.offsetMax = new Vector2(right ? 40f : -10f, -4f);
        rt.localScale = new Vector3(right ? -1f : 1f, 1f, 1f); // ticks point towards the track
    }

    static void SkinKnob(Slider slider, Dictionary<string, Sprite> art, Font font, StringBuilder log)
    {
        RectTransform handle = slider.handleRect;
        if (handle == null) return;
        var sliderRT = (RectTransform)slider.transform;
        handle.sizeDelta = new Vector2(KnobSize.x - sliderRT.rect.width, KnobSize.y); // width stretches with the slider
        Image knob = SetImage(handle, art["engine_knob"], Image.Type.Simple, Color.white, true);
        knob.preserveAspect = false;
        slider.targetGraphic = knob;
        PrefabOverrides.Record(handle, knob, slider);

        foreach (Text t in handle.GetComponentsInChildren<Text>(true))
        {
            // Small Bulletproof text turns to mush with a heavy outline: gold text + soft shadow instead
            if (font != null) t.font = font;
            t.color = new Color(1f, 0.84f, 0.32f);
            t.alignment = TextAnchor.MiddleCenter;
            t.resizeTextForBestFit = false;
            if (t.text.ToUpperInvariant().Contains("ENGINE")) t.fontSize = 20;
            var outline = t.GetComponent<Outline>();
            if (outline != null) { outline.enabled = false; PrefabOverrides.Record(outline); }
            Shadow soft = null;
            foreach (Shadow s in t.GetComponents<Shadow>()) if (!(s is Outline)) soft = s;
            if (soft == null) soft = t.gameObject.AddComponent<Shadow>();
            soft.enabled = true;
            soft.effectColor = new Color(0f, 0f, 0f, 0.85f);
            soft.effectDistance = new Vector2(1f, -2f);
            PrefabOverrides.Record(t, soft, t.transform);
            log.AppendLine($"  knob text \"{t.text.Replace("\n", "|")}\" restyled");
        }
    }

    // Soft glow behind the knob (a sibling drawn before the knob; GaugeFx keeps it on the knob)
    static Image MakeGlow(Slider slider, Sprite glow)
    {
        RectTransform handle = slider.handleRect;
        if (handle == null) return null;
        Transform area = handle.parent;
        RectTransform rt = Child((RectTransform)area, "KnobGlow");
        rt.SetSiblingIndex(0);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(190f, 190f);
        return SetImage(rt, glow, Image.Type.Simple, new Color(0.3f, 0.9f, 0.35f, 0.4f), false);
    }

    static RectTransform Child(RectTransform parent, string name)
    {
        Transform t = parent.Find(name);
        return t != null ? (RectTransform)t : NewUI(name, parent);
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image SetImage(RectTransform rt, Sprite sprite, Image.Type type, Color color, bool raycast)
    {
        var image = rt.GetComponent<Image>();
        if (image == null) image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.enabled = true;
        image.pixelsPerUnitMultiplier = 1f; // the old fuel bar used a big multiplier, which hid the 9-slice frame
        image.raycastTarget = raycast;
        PrefabOverrides.Record(image, rt);
        return image;
    }
}
