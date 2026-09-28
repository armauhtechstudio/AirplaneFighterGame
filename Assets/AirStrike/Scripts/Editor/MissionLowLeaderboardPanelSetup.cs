using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds "Mod3LeaderboardNamePanel" in Mission Low (military-metal style like the Revive popup) and a
// Mod3LeaderboardManager on the GameManagerMode2 object, wired together:
//   plaque "JOIN THE [LEADERBOARD]", description, "YOUR SCORE", name box (PlayerNameInput, max 20),
//   validation message, SUBMIT (SubmitButton) and NOT NOW (LaterButton).
// Safe to re-run.
public static class MissionLowLeaderboardPanelSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    static readonly Color PlaqueText = new Color(0.86f, 0.88f, 0.91f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.20f);
    static readonly Color Subtle = new Color(0.78f, 0.80f, 0.84f);

    [MenuItem("Tools/AirStrike/Create Leaderboard Name Panel (Mission Low)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MissionLowLeaderboardPanelSetup]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        GameManagerMode2 gm = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (gm == null) gm = root.GetComponentInChildren<GameManagerMode2>(true);
        if (gm == null || gm.failPanel == null) return "ERROR: GameManagerMode2 or its fail panel not found.";

        Sprite body = Load("Assets/SS/Popup/back.png");
        Sprite header = Load($"{ReviveArtGenerator.Folder}/revive_header.png");
        Sprite green = Load($"{ReviveArtGenerator.Folder}/revive_button.png");
        Sprite red = Load($"{ReviveArtGenerator.Folder}/revive_nothanks.png");
        Sprite box = Load(LeaderboardArtGenerator.Teal); // rectangular teal bar (9-sliced)
        if (body == null || header == null || green == null || red == null || box == null)
            return "ERROR: popup art missing (Assets/SS/Popup/back.png, Assets/SS/Popup/Revive/*, Assets/SS/obj.png).";

        Text popupText = gm.failPanel.GetComponentInChildren<Text>(true);
        Font font = popupText != null ? popupText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Bulletproof

        Transform parent = gm.failPanel.transform.parent;
        Transform old = parent.Find("Mod3LeaderboardNamePanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Full-screen dark tint over the frozen game
        RectTransform panel = NewUI("Mod3LeaderboardNamePanel", parent);
        Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
        panel.SetAsLastSibling();

        RectTransform bg = Img(panel, "bg", body, Vector2.zero, new Vector2(640f, 640f));
        bg.GetComponent<Image>().preserveAspect = false;

        RectTransform plaque = Img(bg, "header", header, new Vector2(0f, 48f), new Vector2(334f, 166f));
        plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 1f);
        plaque.localScale = Vector3.one * 1.1f;
        Label(plaque, "Top", "JOIN THE", font, 30, PlaqueText, new Vector2(0f, 40f), new Vector2(220f, 44f));
        Label(plaque, "Bottom", "[LEADERBOARD]", font, 26, PlaqueText, new Vector2(0f, 2f), new Vector2(250f, 44f));

        Text desc = Label(bg, "Description", "Submit your name to compete on the leaderboard and see how your score compares with other players.",
                          font, 24, Subtle, new Vector2(0f, 205f), new Vector2(540f, 100f));
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;

        Text score = Label(bg, "ScoreText", "YOUR SCORE: 0", font, 36, Gold, new Vector2(0f, 118f), new Vector2(540f, 56f));

        // Name box (teal HUD bar) with InputField
        RectTransform field = Img(bg, "PlayerNameInput", box, new Vector2(0f, 28f), new Vector2(480f, 92f));
        field.GetComponent<Image>().preserveAspect = false;
        field.GetComponent<Image>().type = Image.Type.Sliced;
        field.GetComponent<Image>().raycastTarget = true;
        Text input = Label(field, "Text", "", font, 34, Color.white, Vector2.zero, Vector2.zero);
        Stretch((RectTransform)input.transform, 34f, 12f);
        input.supportRichText = false;
        input.horizontalOverflow = HorizontalWrapMode.Overflow;
        Text placeholder = Label(field, "Placeholder", "Enter your name", font, 30, new Color(0.85f, 0.9f, 0.9f, 0.45f), Vector2.zero, Vector2.zero);
        Stretch((RectTransform)placeholder.transform, 34f, 12f);
        var inputField = field.gameObject.AddComponent<InputField>();
        inputField.textComponent = input;
        inputField.placeholder = placeholder;
        inputField.characterLimit = Mod3LeaderboardManager.MaxNameLength;
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.contentType = InputField.ContentType.Standard;
        inputField.caretColor = Gold;
        inputField.customCaretColor = true;
        inputField.caretWidth = 3;
        inputField.selectionColor = new Color(1f, 0.8f, 0.2f, 0.35f);
        inputField.targetGraphic = field.GetComponent<Image>();

        Text validation = Label(bg, "ValidationText", "", font, 24, new Color(1f, 0.38f, 0.32f), new Vector2(0f, -42f), new Vector2(540f, 40f));

        RectTransform submitRT = Img(bg, "SubmitButton", green, new Vector2(0f, -140f), new Vector2(449f, 185f));
        submitRT.localScale = Vector3.one * 0.72f;
        Label(submitRT, "Text", "SUBMIT", font, 76, Color.white, new Vector2(0f, 0f), new Vector2(360f, 110f));
        Button submit = MakeButton(submitRT);

        RectTransform laterRT = Img(bg, "LaterButton", red, new Vector2(0f, -250f), new Vector2(446f, 146f));
        laterRT.localScale = Vector3.one * 0.5f;
        Label(laterRT, "Text", "NOT NOW", font, 64, new Color(1f, 0.86f, 0.86f), Vector2.zero, new Vector2(400f, 110f));
        Button later = MakeButton(laterRT);

        // Manager on the GameManagerMode2 object
        var manager = gm.GetComponent<Mod3LeaderboardManager>();
        if (manager == null) manager = gm.gameObject.AddComponent<Mod3LeaderboardManager>();
        manager.namePanel = panel.gameObject;
        manager.playerNameInput = inputField;
        manager.validationText = validation;
        manager.scoreText = score;
        manager.submitButton = submit;
        manager.laterButton = later;
        UnityEventTools.AddPersistentListener(submit.onClick, manager.Submit);
        UnityEventTools.AddPersistentListener(later.onClick, manager.Later);

        var so = new SerializedObject(gm);
        so.FindProperty("leaderboard").objectReferenceValue = manager;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);

        panel.gameObject.AddComponent<PunchyPanel>();
        panel.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        return $"Mod3LeaderboardNamePanel created under \"{parent.name}\", Mod3LeaderboardManager on \"{gm.name}\".";
    }

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    static RectTransform Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewUI(name, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rt;
    }

    static Text Label(Transform parent, string name, string value, Font font, int size, Color color, Vector2 pos, Vector2 box)
    {
        RectTransform rt = NewUI(name, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var text = rt.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = value;
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.05f, 0.06f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    static Button MakeButton(RectTransform rt)
    {
        var image = rt.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rt.gameObject.AddComponent<PunchyButton>();
        return button;
    }
}
