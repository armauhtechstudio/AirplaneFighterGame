using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Restyles Classic's objective panel (Canvas/ObjPanel) in the military-metal popup style, in place:
// diamond-plate body, steel plaque "MISSION / OBJECTIVE", dark inset behind the objective text, green
// OKAY button. Keeps the existing objects and links (GameUI.objectivePanel / objectiveText, the OKAY
// button's OnObjectiveOkayPressed). Safe to re-run.
public static class ClassicObjectivePanelRestyle
{
    const string ScenePath = "Assets/AirStrike/Demo/Classic.unity";
    static readonly Color PlaqueText = new Color(0.86f, 0.88f, 0.91f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.20f);

    [MenuItem("Tools/AirStrike/Restyle Objective Panel (Classic)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[ClassicObjectivePanelRestyle] " + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        Sprite body = Load("Assets/SS/Popup/back.png"), header = Load("Assets/SS/Popup/Revive/revive_header.png"),
               green = Load("Assets/SS/Popup/Revive/revive_button.png");
        if (body == null || header == null || green == null) return "ERROR: popup art missing (back.png / Revive art).";

        GameUI ui = null;
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (GameUI g in r.GetComponentsInChildren<GameUI>(true))
                if (ui == null && g.objectivePanel != null) ui = g;
        if (ui == null) return "ERROR: GameUI with an objective panel not found.";

        Transform panel = ui.objectivePanel.transform;
        var bg = panel.Find("bg") as RectTransform;
        if (bg == null) return "ERROR: ObjPanel/bg not found.";
        Text bodyText = ui.objectiveText;
        Font font = bodyText != null ? bodyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Dim behind the popup (the panel root covers the screen if it's stretched)
        var panelRT = (RectTransform)panel;
        if (panelRT.anchorMin == Vector2.zero && panelRT.anchorMax == Vector2.one)
        {
            var dim = panel.GetComponent<Image>();
            if (dim == null) dim = panel.gameObject.AddComponent<Image>();
            dim.sprite = null;
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;
        }

        // Body: diamond plate like the pause / fail / revive popups
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = body;
        bgImg.type = Image.Type.Simple;
        bgImg.preserveAspect = false;
        bgImg.color = Color.white;
        bg.anchoredPosition = new Vector2(0f, -30f);
        bg.sizeDelta = new Vector2(720f, 640f);

        // Old plain "OBJECTIVE" title -> steel plaque
        foreach (Transform c in bg)
            if (c.name == "Text" && c.GetComponent<Text>() != null && c.GetComponent<Text>() != bodyText) c.gameObject.SetActive(false);
        Transform oldPlaque = bg.Find("header");
        if (oldPlaque != null) Object.DestroyImmediate(oldPlaque.gameObject);
        var plaque = Img(bg, "header", header, new Vector2(0f, 48f), new Vector2(334f, 166f));
        plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 1f);
        plaque.localScale = Vector3.one * 1.2f;
        Label(plaque, "Top", "MISSION", font, 30, PlaqueText, new Vector2(0f, 40f), new Vector2(240f, 44f));
        Label(plaque, "Bottom", "OBJECTIVE", font, 32, Gold, new Vector2(0f, 2f), new Vector2(260f, 44f));

        // Dark inset behind the objective text
        Transform oldInset = bg.Find("Inset");
        if (oldInset != null) Object.DestroyImmediate(oldInset.gameObject);
        var inset = new GameObject("Inset", typeof(RectTransform), typeof(Image));
        inset.layer = bg.gameObject.layer;
        var insetRT = (RectTransform)inset.transform;
        insetRT.SetParent(bg, false);
        insetRT.anchoredPosition = new Vector2(0f, 45f);
        insetRT.sizeDelta = new Vector2(600f, 320f);
        var insetImg = inset.GetComponent<Image>();
        insetImg.color = new Color(0f, 0f, 0f, 0.45f);
        insetImg.raycastTarget = false;

        // Objective text: centred in the inset, shrinks to fit long objectives
        if (bodyText != null)
        {
            var rt = bodyText.rectTransform;
            rt.SetParent(bg, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 45f);
            rt.sizeDelta = new Vector2(560f, 290f);
            bodyText.alignment = TextAnchor.MiddleCenter;
            bodyText.color = Color.white;
            bodyText.supportRichText = true;
            bodyText.resizeTextForBestFit = true;
            bodyText.resizeTextMinSize = 22;
            bodyText.resizeTextMaxSize = 44;
            bodyText.fontSize = 44;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            if (bodyText.GetComponent<Outline>() == null) bodyText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            rt.SetSiblingIndex(insetRT.GetSiblingIndex() + 1); // above the inset
        }

        // OKAY button: green popup button (keeps its OnObjectiveOkayPressed)
        Button okay = bg.GetComponentInChildren<Button>(true);
        if (okay != null)
        {
            var brt = (RectTransform)okay.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, 105f);
            brt.sizeDelta = new Vector2(380f, 140f);
            var img = okay.GetComponent<Image>();
            img.sprite = green;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            var t = okay.GetComponentInChildren<Text>(true);
            if (t != null)
            {
                t.text = "OKAY";
                t.fontSize = 54;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
                t.rectTransform.offsetMin = new Vector2(0f, 4f);
                t.rectTransform.offsetMax = Vector2.zero;
                var outline = t.GetComponent<Outline>();
                if (outline == null) outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.25f, 0.03f, 0.9f);
            }
            if (okay.GetComponent<PunchyButton>() == null) okay.gameObject.AddComponent<PunchyButton>();
            brt.SetAsLastSibling();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return $"Objective panel restyled ({ui.objectivePanel.name}).";
    }

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static RectTransform Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return rt;
    }

    static Text Label(Transform parent, string name, string text, Font font, int size, Color color, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
