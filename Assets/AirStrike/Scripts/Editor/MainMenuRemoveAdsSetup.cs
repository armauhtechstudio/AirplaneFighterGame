using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the "REMOVE ADS" button in the Mainmenu's top-right corner: a brushed-gold plate in a riveted
// steel rim (art: Tools/AirStrike/Generate Remove Ads Art), a crossed-out "AD" emblem, Bulletproof text,
// a breathing pulse + shine sweep (RemoveAdsButtonFx) and the usual press punch (PunchyButton).
// Not wired to anything yet (no purchase). Safe to re-run: the old button is replaced.
public static class MainMenuRemoveAdsSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mainmenu.unity";
    const string Art = RemoveAdsArtGenerator.Folder;
    const string ButtonName = "RemoveAdsButton";

    static readonly Color TextDark = new Color(0.20f, 0.11f, 0.02f);

    [MenuItem("Tools/AirStrike/Create Remove Ads Button (Mainmenu)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MainMenuRemoveAdsSetup] " + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        Sprite plate = Load("removeads_plate.png"), emblem = Load("removeads_emblem.png"),
               slash = Load("removeads_slash.png"), shine = Load("removeads_shine.png");
        if (plate == null || emblem == null || slash == null || shine == null)
            return "ERROR: art missing — run Tools/AirStrike/Generate Remove Ads Art first.";

        // Menu canvas: the screen-space one holding Panel/playbtn
        Transform panel = null;
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Button b in sceneRoot.GetComponentsInChildren<Button>(true))
                if (panel == null && b.name == "playbtn") panel = b.transform.parent;
        if (panel == null) return "ERROR: playbtn not found in Mainmenu.";

        // Same font as the LEADERBOARD button
        Font font = null;
        foreach (Text t in panel.GetComponentsInChildren<Text>(true))
            if (font == null && t.font != null && t.font.name == "Bulletproof") font = t.font;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Full-screen panel -> corner of the screen; otherwise use the canvas itself
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        var panelRT = (RectTransform)panel;
        RectTransform parent = panelRT.anchorMin == Vector2.zero && panelRT.anchorMax == Vector2.one
            ? panelRT : (RectTransform)canvas.transform;

        Transform old = parent.Find(ButtonName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Root: button + press punch, anchored top-right
        var root = NewUI(ButtonName, parent);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-48f, -40f);
        root.sizeDelta = new Vector2(370f, 112f);

        // Body: what pulses (the root keeps PunchyButton's scale)
        var body = NewUI("Body", root);
        Stretch(body);

        // Gold plate; its Mask clips the shine sweep to the plate
        var plateRT = NewUI("Plate", body);
        Stretch(plateRT);
        var plateImg = plateRT.gameObject.AddComponent<Image>();
        plateImg.sprite = plate;
        plateRT.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        var shineRT = NewUI("Shine", plateRT);
        shineRT.sizeDelta = new Vector2(70f, 220f);
        shineRT.localRotation = Quaternion.Euler(0f, 0f, -22f);
        var shineImg = shineRT.gameObject.AddComponent<Image>();
        shineImg.sprite = shine;
        shineImg.color = new Color(1f, 1f, 0.9f, 0.75f);
        shineImg.raycastTarget = false;
        shineRT.gameObject.SetActive(false);

        // Emblem: crossed-out "AD" on the left
        var emblemRT = NewUI("Emblem", body);
        emblemRT.anchorMin = emblemRT.anchorMax = new Vector2(0f, 0.5f);
        emblemRT.anchoredPosition = new Vector2(72f, 0f);
        emblemRT.sizeDelta = new Vector2(86f, 86f);
        var emblemImg = emblemRT.gameObject.AddComponent<Image>();
        emblemImg.sprite = emblem;
        emblemImg.raycastTarget = false;
        var adText = Label(emblemRT, "AD", "AD", font, 34, Color.white, Vector2.zero, new Vector2(86f, 86f));
        adText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        var slashRT = NewUI("Slash", emblemRT);
        Stretch(slashRT);
        var slashImg = slashRT.gameObject.AddComponent<Image>();
        slashImg.sprite = slash;
        slashImg.raycastTarget = false;

        // "REMOVE ADS" engraved in the gold
        var label = Label(body, "Label", "REMOVE ADS", font, 36, TextDark, new Vector2(48f, 0f), new Vector2(240f, 70f));
        label.gameObject.AddComponent<Shadow>().effectColor = new Color(1f, 0.95f, 0.7f, 0.55f); // light bevel under the letters
        label.GetComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);

        // Button (no click action yet)
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = plateImg;
        var colors = button.colors;
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        button.colors = colors;
        root.gameObject.AddComponent<PunchyButton>();

        var fx = root.gameObject.AddComponent<RemoveAdsButtonFx>();
        fx.pulseTarget = body;
        fx.shine = shineRT;

        EditorSceneManager.MarkSceneDirty(scene);
        return $"{ButtonName} created under \"{parent.name}\" (top-right), font \"{font.name}\", no click action.";
    }

    // ---------------------------------------------------------------- helpers

    static Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/{file}");

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text Label(Transform parent, string name, string text, Font font, int size, Color color, Vector2 pos, Vector2 box)
    {
        var rt = NewUI(name, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<Text>();
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
