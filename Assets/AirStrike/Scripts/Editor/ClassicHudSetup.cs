using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Classic scene HUD:
//  - Kills / Score / Time texts on bars in the style of the objective bars (Assets/SS/obj.png + red
//    bracket frame), white Bulletproof text; the leftover static "Time:" label is hidden
//  - Health bar re-skinned (steel slot, glossy fill, heart icon) with HealthBarFx motion
// Safe to re-run.
public static class ClassicHudSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Classic.unity";
    static readonly Vector2 BarSize = new Vector2(300f, 64f);

    [MenuItem("Tools/AirStrike/Classic HUD (bars + health bar)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[ClassicHudSetup]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        GameUI ui = null;
        GameManager gm = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (ui == null) ui = root.GetComponentInChildren<GameUI>(true);
            if (gm == null) gm = root.GetComponentInChildren<GameManager>(true);
        }
        if (ui == null) return "ERROR: GameUI not found in Classic.";

        var log = new StringBuilder();
        Sprite bar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SS/obj.png");
        Sprite brackets = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SS/obj panel bar.png");
        if (bar == null || brackets == null) return "ERROR: Assets/SS/obj.png or 'obj panel bar.png' not found.";

        // ---- Kills / Score / Time bars, stacked top-left ----
        Text[] texts = { ui.killsText, ui.scoreText, gm != null ? gm.timerText : null };
        string[] samples = { "Kills: 0", "Score: 0", "Time: 0" };
        float y = -136f;
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            WrapInBar(texts[i], bar, brackets, new Vector2(24f, y), samples[i]);
            log.AppendLine($"{texts[i].name}: bar at y={y}");
            y -= 74f;
        }

        // Leftover label under the timer that always says "Time:" (not linked to the tutorial)
        Transform panel = ui.killsText != null ? ui.killsText.transform.parent : null;
        while (panel != null && panel.name.EndsWith("Bar")) panel = panel.parent;
        Transform stray = panel != null ? panel.Find("tutorialText") : null;
        TutorialManager tm = Object.FindObjectOfType<TutorialManager>(true);
        if (stray != null && (tm == null || tm.tutorialText == null || tm.tutorialText.transform != stray))
        {
            stray.gameObject.SetActive(false);
            PrefabOverrides.Record(stray.gameObject);
            log.AppendLine("Hid the static \"Time:\" label (tutorialText, not linked to the tutorial).");
        }

        // ---- Health bar ----
        if (ui.slider != null) log.Append(SkinHealth(ui.slider, ui.healthText));

        EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    static void WrapInBar(Text text, Sprite bar, Sprite brackets, Vector2 topLeft, string sample)
    {
        string barName = text.name + "Bar";
        RectTransform barRT;
        if (text.transform.parent != null && text.transform.parent.name == barName)
        {
            barRT = (RectTransform)text.transform.parent;
        }
        else
        {
            var go = new GameObject(barName, typeof(RectTransform), typeof(Image));
            barRT = (RectTransform)go.transform;
            barRT.SetParent(text.transform.parent, false);
            barRT.SetSiblingIndex(text.transform.GetSiblingIndex());

            var sidebar = new GameObject("Sidebar", typeof(RectTransform), typeof(Image));
            var sRT = (RectTransform)sidebar.transform;
            sRT.SetParent(barRT, false);
            sRT.anchorMin = Vector2.zero;
            sRT.anchorMax = Vector2.one;
            sRT.offsetMin = new Vector2(-4f, 0f);
            sRT.offsetMax = new Vector2(8f, 0f);
            var sImg = sidebar.GetComponent<Image>();
            sImg.sprite = brackets;
            sImg.color = new Color(1f, 0.016f, 0f);
            sImg.raycastTarget = false;

            text.transform.SetParent(barRT, false);
        }

        barRT.anchorMin = barRT.anchorMax = new Vector2(0f, 1f);
        barRT.pivot = new Vector2(0f, 1f);
        barRT.sizeDelta = BarSize;
        barRT.anchoredPosition = topLeft;
        var barImg = barRT.GetComponent<Image>();
        barImg.sprite = bar;
        barImg.raycastTarget = false;

        var tRT = (RectTransform)text.transform;
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.pivot = new Vector2(0.5f, 0.5f);
        tRT.offsetMin = new Vector2(26f, 8f);
        tRT.offsetMax = new Vector2(-26f, -8f);
        tRT.localScale = Vector3.one;
        text.color = Color.white;                 // was black, for the old bar-less look
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = 34;
        text.raycastTarget = false;
        if (string.IsNullOrEmpty(text.text) || text.text == "adsd") text.text = sample;
        var outline = text.GetComponent<Outline>();
        if (outline != null) { outline.effectColor = new Color(0f, 0f, 0f, 0.8f); outline.effectDistance = new Vector2(2f, -2f); }
        PrefabOverrides.Record(text, tRT, barRT, barImg, outline);
    }

    static string SkinHealth(Slider slider, Text value)
    {
        const string art = EngineSliderArtGenerator.Folder;
        Sprite track = AssetDatabase.LoadAssetAtPath<Sprite>($"{art}/health_track.png");
        Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{art}/health_fill.png");
        Sprite shineSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{art}/health_shine.png");
        Sprite heart = AssetDatabase.LoadAssetAtPath<Sprite>($"{ReviveArtGenerator.Folder}/revive_heart.png");
        if (track == null || fillSprite == null || shineSprite == null)
            return "ERROR: health art missing — run Tools/AirStrike/Generate Engine Slider Art first.\n";

        var sliderRT = (RectTransform)slider.transform;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        // Steel slot (left end leaves room for the heart icon)
        RectTransform bg = (RectTransform)sliderRT.Find("Background");
        bg.anchorMin = new Vector2(0f, 0f);
        bg.anchorMax = new Vector2(1f, 1f);
        bg.offsetMin = new Vector2(-6f, -4f);
        bg.offsetMax = new Vector2(6f, 4f);
        Image bgImg = Img(bg, track, Image.Type.Sliced, Color.white);

        // Fill area inside the slot's frame, after the heart
        var fillArea = (RectTransform)slider.fillRect.parent;
        fillArea.anchorMin = new Vector2(0f, 0.22f);
        fillArea.anchorMax = new Vector2(1f, 0.78f);
        fillArea.offsetMin = new Vector2(58f, 0f);
        fillArea.offsetMax = new Vector2(-14f, 0f);
        fillArea.sizeDelta = new Vector2(fillArea.sizeDelta.x, 0f);

        // Damage trail behind the fill
        RectTransform trail = Child(fillArea, "Trail");
        trail.SetSiblingIndex(0);
        trail.anchorMin = new Vector2(0f, 0f);
        trail.anchorMax = new Vector2(1f, 1f);
        trail.offsetMin = trail.offsetMax = Vector2.zero;
        Img(trail, fillSprite, Image.Type.Sliced, new Color(1f, 0.93f, 0.75f, 0.85f));

        RectTransform fillRT = slider.fillRect;
        fillRT.offsetMin = new Vector2(fillRT.offsetMin.x, 0f);
        fillRT.offsetMax = new Vector2(fillRT.offsetMax.x, 0f);
        Image fillImg = Img(fillRT, fillSprite, Image.Type.Sliced, new Color(0.3f, 0.9f, 0.35f));
        if (fillRT.GetComponent<RectMask2D>() == null) fillRT.gameObject.AddComponent<RectMask2D>();

        RectTransform shine = Child(fillRT, "Shine");
        shine.anchorMin = new Vector2(0.5f, 0f);
        shine.anchorMax = new Vector2(0.5f, 1f);
        shine.pivot = new Vector2(0.5f, 0.5f);
        shine.sizeDelta = new Vector2(70f, 0f);
        Img(shine, shineSprite, Image.Type.Simple, new Color(1f, 1f, 1f, 0.6f));

        // Heart icon at the left end
        RectTransform heartRT = null;
        if (heart != null)
        {
            heartRT = Child(sliderRT, "Heart");
            heartRT.anchorMin = heartRT.anchorMax = new Vector2(0f, 0.5f);
            heartRT.pivot = new Vector2(0.5f, 0.5f);
            heartRT.anchoredPosition = new Vector2(28f, 0f);
            heartRT.sizeDelta = new Vector2(56f, 56f);
            Img(heartRT, heart, Image.Type.Simple, Color.white).preserveAspect = true;
        }

        // Health number: white, centred on the bar, drawn on top
        if (value != null)
        {
            var vRT = (RectTransform)value.transform;
            vRT.anchorMin = Vector2.zero;
            vRT.anchorMax = Vector2.one;
            vRT.pivot = new Vector2(0.5f, 0.5f);
            vRT.offsetMin = new Vector2(58f, 0f);
            vRT.offsetMax = new Vector2(-14f, 0f);
            value.alignment = TextAnchor.MiddleCenter;
            value.color = Color.white;
            value.fontSize = 34;
            value.resizeTextForBestFit = false;
            value.transform.SetAsLastSibling();
            var outline = value.GetComponent<Outline>();
            if (outline != null) { outline.effectColor = new Color(0f, 0f, 0f, 0.85f); outline.effectDistance = new Vector2(2f, -2f); }
            PrefabOverrides.Record(value, vRT, outline);
        }

        var fx = slider.GetComponent<HealthBarFx>();
        if (fx == null) fx = slider.gameObject.AddComponent<HealthBarFx>();
        fx.fill = fillImg;
        fx.trail = trail;
        fx.shine = shine;
        fx.heartIcon = heartRT;
        PrefabOverrides.Record(slider, sliderRT, bg, bgImg, fillArea, fillRT, fillImg, fx);
        return "Health bar re-skinned (steel slot, glossy fill, damage trail, sheen, heart).\n";
    }

    static RectTransform Child(RectTransform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return (RectTransform)t;
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image Img(RectTransform rt, Sprite sprite, Image.Type type, Color color)
    {
        var image = rt.GetComponent<Image>();
        if (image == null) image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.enabled = true;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = false;
        PrefabOverrides.Record(image, rt);
        return image;
    }
}
