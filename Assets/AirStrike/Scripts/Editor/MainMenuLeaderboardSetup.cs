using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main menu Mod 3 leaderboard, in the game's military-metal popup style:
//  - "LEADERBOARD" button under PLAY (Mod3LeaderboardButton, same plate art as PLAY)
//  - Mod3LeaderboardRegistrationPanel: "Play Mod 3" / "Close"
//  - Mod3LeaderboardPanel: RANK / PLAYER / SCORE header, LeaderboardScrollView (Top 100 rows from the
//    Mod3LeaderboardEntry prefab), CurrentPlayerRankSection, Close, LeaderboardLoadingPanel,
//    empty state and error state (Retry / Close)
//  - Mod3LeaderboardUI wired to all of it; row prefab saved to Assets/AirStrike/Prefabs/UI.
// Safe to re-run.
public static class MainMenuLeaderboardSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mainmenu.unity";
    const string PrefabFolder = "Assets/AirStrike/Prefabs/UI";
    const string PrefabPath = PrefabFolder + "/Mod3LeaderboardEntry.prefab";

    static readonly Color PlaqueText = new Color(0.86f, 0.88f, 0.91f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.20f);
    static readonly Color Subtle = new Color(0.78f, 0.80f, 0.84f);

    static Font font;
    static Sprite body, plaque, green, red, bar, barGold, knob, ring;

    [MenuItem("Tools/AirStrike/Main Menu Leaderboard (Mod 3)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MainMenuLeaderboardSetup]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        MainMenuController menu = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (menu == null) menu = root.GetComponentInChildren<MainMenuController>(true);
        if (menu == null || menu.menuPanel == null) return "ERROR: MainMenuController / menuPanel not found.";

        body = Load("Assets/SS/Popup/back.png");
        plaque = Load($"{ReviveArtGenerator.Folder}/revive_header.png");
        green = Load($"{ReviveArtGenerator.Folder}/revive_button.png");
        red = Load($"{ReviveArtGenerator.Folder}/revive_nothanks.png");
        bar = Load(LeaderboardArtGenerator.Teal);        // rectangular teal bar (9-sliced)
        barGold = Load(LeaderboardArtGenerator.GoldPath); // gold: the player's own row / Your Rank
        ring = Load($"{ReviveArtGenerator.Folder}/revive_ring.png");
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (body == null || plaque == null || green == null || red == null || bar == null || barGold == null || ring == null)
            return "ERROR: art missing (run Generate Revive Popup Art and Generate Leaderboard Bar Art first).";

        Transform canvas = menu.menuPanel.GetComponentInParent<Canvas>(true).rootCanvas.transform;
        Text anyText = canvas.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.font != null && t.font.name == "Bulletproof");
        font = anyText != null ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        foreach (string old in new[] { "Mod3LeaderboardUI", "Mod3LeaderboardRegistrationPanel", "Mod3LeaderboardPanel" })
        {
            Transform t = canvas.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        var uiGO = new GameObject("Mod3LeaderboardUI", typeof(RectTransform));
        uiGO.transform.SetParent(canvas, false);
        var ui = uiGO.AddComponent<Mod3LeaderboardUI>();
        ui.mainMenu = menu;
        ui.entryPrefab = BuildEntryPrefab();

        // ---- Leaderboard button under PLAY ----
        Button leaderboardButton = BuildMenuButton(menu, ui);

        // ---- Registration panel ----
        RectTransform reg = Overlay("Mod3LeaderboardRegistrationPanel", canvas);
        RectTransform regBg = Img(reg, "bg", body, new Vector2(0f, -10f), new Vector2(760f, 560f), false);
        Plaque(regBg, "MOD 3", "[LEADERBOARD]");
        Label(regBg, "Description", "Play Mod 3 and submit your high score to compete on the global leaderboard!", 30, Subtle,
              new Vector2(0f, 120f), new Vector2(640f, 150f)).horizontalOverflow = HorizontalWrapMode.Wrap;
        Button play = TextButton(regBg, "PlayMod3Button", green, "PLAY MOD 3", new Vector2(0f, -70f), 0.75f, Color.white);
        Button regClose = TextButton(regBg, "CloseButton", red, "CLOSE", new Vector2(0f, -190f), 0.5f, new Color(1f, 0.86f, 0.86f));
        ui.registrationPanel = reg.gameObject;

        // ---- Leaderboard panel ----
        RectTransform lb = Overlay("Mod3LeaderboardPanel", canvas);
        RectTransform bg = Img(lb, "bg", body, new Vector2(0f, -20f), new Vector2(980f, 920f), false);
        Plaque(bg, "MOD 3", "[LEADERBOARD]");

        var interaction = NewUI("Content", bg);
        Stretch(interaction);
        ui.leaderboardInteraction = interaction.gameObject.AddComponent<CanvasGroup>();

        // Column header
        Label(interaction, "HeaderRank", "RANK", 26, Subtle, new Vector2(-350f, 370f), new Vector2(120f, 40f));
        Label(interaction, "HeaderName", "PLAYER", 26, Subtle, new Vector2(-150f, 370f), new Vector2(240f, 40f)).alignment = TextAnchor.MiddleLeft;
        Label(interaction, "HeaderScore", "SCORE", 26, Subtle, new Vector2(300f, 370f), new Vector2(200f, 40f)).alignment = TextAnchor.MiddleRight;

        // Scroll view with the Top 100
        RectTransform scrollRT = NewUI("LeaderboardScrollView", interaction);
        scrollRT.anchoredPosition = new Vector2(0f, 60f);
        scrollRT.sizeDelta = new Vector2(880f, 580f);
        var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
        scrollRT.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        RectTransform viewport = NewUI("Viewport", scrollRT);
        Stretch(viewport, 8f, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = NewUI("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;
        ui.scrollView = scroll;
        ui.content = content;

        Text empty = Label(interaction, "EmptyState", "No scores yet. Be the first to set a high score!", 30, Subtle, new Vector2(0f, 60f), new Vector2(700f, 120f));
        empty.horizontalOverflow = HorizontalWrapMode.Wrap;
        ui.emptyState = empty.gameObject;

        // Your Rank
        RectTransform you = Img(interaction, "CurrentPlayerRankSection", barGold, new Vector2(0f, -285f), new Vector2(880f, 92f), false);
        you.GetComponent<Image>().type = Image.Type.Sliced;
        Label(you, "Title", "YOUR RANK", 24, Gold, new Vector2(-340f, 0f), new Vector2(170f, 40f));
        ui.yourRankText = Label(you, "YourRankText", "#--", 40, Color.white, new Vector2(-205f, 0f), new Vector2(130f, 60f));
        ui.yourNameText = Label(you, "YourNameText", "You", 32, Color.white, new Vector2(20f, 0f), new Vector2(300f, 60f));
        ui.yourNameText.alignment = TextAnchor.MiddleLeft;
        ui.yourScoreText = Label(you, "YourScoreText", "0", 36, Gold, new Vector2(300f, 0f), new Vector2(200f, 60f));
        ui.yourScoreText.alignment = TextAnchor.MiddleRight;
        ui.currentPlayerSection = you.gameObject;

        Button close = TextButton(bg, "CloseLeaderboardButton", red, "CLOSE", new Vector2(0f, -400f), 0.5f, new Color(1f, 0.86f, 0.86f));

        // Loading overlay
        RectTransform loading = NewUI("LeaderboardLoadingPanel", bg);
        Stretch(loading, 24f, 24f);
        loading.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform spinner = Img(loading, "Spinner", ring, new Vector2(0f, 40f), new Vector2(120f, 120f), true);
        spinner.GetComponent<Image>().color = Gold;
        spinner.GetComponent<Image>().type = Image.Type.Filled;
        spinner.GetComponent<Image>().fillMethod = Image.FillMethod.Radial360;
        spinner.GetComponent<Image>().fillAmount = 0.75f;
        spinner.gameObject.AddComponent<UISpin>();
        Label(loading, "LoadingText", "Loading leaderboard...", 32, Color.white, new Vector2(0f, -70f), new Vector2(600f, 60f));
        ui.loadingPanel = loading.gameObject;

        // Error overlay
        RectTransform error = NewUI("ErrorPanel", bg);
        Stretch(error, 24f, 24f);
        error.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 0.92f);
        Label(error, "Title", "LEADERBOARD UNAVAILABLE", 40, Gold, new Vector2(0f, 150f), new Vector2(800f, 70f));
        Label(error, "Message", "Unable to load the leaderboard. Please check your internet connection and try again.", 28, Subtle,
              new Vector2(0f, 40f), new Vector2(720f, 130f)).horizontalOverflow = HorizontalWrapMode.Wrap;
        Button retry = TextButton(error, "RetryButton", green, "RETRY", new Vector2(0f, -110f), 0.7f, Color.white);
        Button errorClose = TextButton(error, "CloseButton", red, "CLOSE", new Vector2(0f, -230f), 0.5f, new Color(1f, 0.86f, 0.86f));
        ui.errorPanel = error.gameObject;
        ui.leaderboardPanel = lb.gameObject;

        // ---- Wiring ----
        Wire(leaderboardButton, ui.OpenMod3Leaderboard);
        Wire(play, ui.CloseLeaderboardPanelAndStartMod3);
        Wire(regClose, ui.ClosePanel);
        Wire(close, ui.ClosePanel);
        Wire(retry, ui.ReloadLeaderboard);
        Wire(errorClose, ui.ClosePanel);

        reg.gameObject.SetActive(false);
        lb.gameObject.SetActive(false);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        return $"Main menu leaderboard built (button under PLAY, registration + leaderboard panels, row prefab {PrefabPath}).";
    }

    // Same plate art family as PLAY (the blank plate in the pause.png sheet), gold text
    static Button BuildMenuButton(MainMenuController menu, Mod3LeaderboardUI ui)
    {
        Transform panel = menu.menuPanel.transform;
        Transform old = panel.Find("Mod3LeaderboardButton");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Image playImg = panel.GetComponentsInChildren<Button>(true).Select(b => b.GetComponent<Image>())
                             .FirstOrDefault(i => i != null && i.gameObject.activeSelf && i.sprite != null);
        Sprite plate = null;
        if (playImg != null)
        {
            string sheet = AssetDatabase.GetAssetPath(playImg.sprite);
            plate = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>()
                .Where(s => s != playImg.sprite && s.rect.width > 300f && s.rect.height < 130f && s.rect.x > 1000f)
                .OrderByDescending(s => s.rect.width).FirstOrDefault();
        }

        RectTransform rt = NewUI("Mod3LeaderboardButton", panel);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -275f);
        rt.sizeDelta = new Vector2(402f, 96f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = plate != null ? plate : bar;
        if (plate == null) img.type = Image.Type.Sliced;
        Label(rt, "Text", "LEADERBOARD", 40, new Color(0.96f, 0.80f, 0.36f), Vector2.zero, new Vector2(380f, 90f));
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        rt.gameObject.AddComponent<PunchyButton>();
        return button;
    }

    // Row prefab: teal bar, rank badge, name, "YOU" tag, score
    static Mod3LeaderboardEntry BuildEntryPrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/AirStrike/Prefabs", "UI");

        var root = new GameObject("Mod3LeaderboardEntry", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(860f, 74f);
        root.AddComponent<LayoutElement>().preferredHeight = 74f;
        var bgImg = root.AddComponent<Image>();
        bgImg.sprite = bar;
        bgImg.type = Image.Type.Sliced;
        bgImg.raycastTarget = false;

        var entry = root.AddComponent<Mod3LeaderboardEntry>();
        entry.background = bgImg;
        entry.normalBar = bar;
        entry.highlightBar = barGold;

        RectTransform badge = Img(rt, "RankBadge", knob, Vector2.zero, new Vector2(58f, 58f), true);
        badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
        badge.anchoredPosition = new Vector2(62f, 0f);
        entry.rankBadge = badge.GetComponent<Image>();
        entry.rankText = Label(badge, "RankText", "1", 28, Color.white, Vector2.zero, new Vector2(70f, 58f));

        entry.playerNameText = Label(rt, "PlayerNameText", "Player", 30, Color.white, Vector2.zero, Vector2.zero);
        var nRT = (RectTransform)entry.playerNameText.transform;
        nRT.anchorMin = new Vector2(0f, 0f);
        nRT.anchorMax = new Vector2(0.62f, 1f);
        nRT.offsetMin = new Vector2(115f, 0f);
        nRT.offsetMax = Vector2.zero;
        entry.playerNameText.alignment = TextAnchor.MiddleLeft;
        entry.playerNameText.horizontalOverflow = HorizontalWrapMode.Wrap;   // long names (up to 20) shrink to fit
        entry.playerNameText.verticalOverflow = VerticalWrapMode.Truncate;
        entry.playerNameText.resizeTextForBestFit = true;
        entry.playerNameText.resizeTextMinSize = 16;
        entry.playerNameText.resizeTextMaxSize = 30;

        RectTransform you = Img(rt, "CurrentPlayerIndicator", knob, Vector2.zero, new Vector2(74f, 36f), true);
        you.anchorMin = you.anchorMax = new Vector2(0.66f, 0.5f);
        you.GetComponent<Image>().color = Gold;
        you.GetComponent<Image>().type = Image.Type.Sliced;
        Label(you, "Text", "YOU", 22, new Color(0.12f, 0.08f, 0.02f), Vector2.zero, new Vector2(74f, 36f)).GetComponent<Outline>().enabled = false;
        entry.currentPlayerIndicator = you.gameObject;

        entry.scoreText = Label(rt, "ScoreText", "0", 32, Gold, Vector2.zero, Vector2.zero);
        var sRT = (RectTransform)entry.scoreText.transform;
        sRT.anchorMin = new Vector2(0.7f, 0f);
        sRT.anchorMax = new Vector2(1f, 1f);
        sRT.offsetMin = Vector2.zero;
        sRT.offsetMax = new Vector2(-30f, 0f);
        entry.scoreText.alignment = TextAnchor.MiddleRight;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Mod3LeaderboardEntry>();
    }

    // ------------------------------------------------------------------ helpers

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static void Wire(Button b, UnityAction action) => UnityEventTools.AddPersistentListener(b.onClick, action);

    static RectTransform Overlay(string name, Transform canvas)
    {
        RectTransform rt = NewUI(name, canvas);
        Stretch(rt);
        rt.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // dims + blocks the menu
        rt.gameObject.AddComponent<PunchyPanel>();
        rt.SetAsLastSibling();
        return rt;
    }

    static void Plaque(RectTransform parent, string top, string bottom)
    {
        RectTransform p = Img(parent, "header", plaque, new Vector2(0f, 48f), new Vector2(334f, 166f), true);
        p.anchorMin = p.anchorMax = new Vector2(0.5f, 1f);
        p.localScale = Vector3.one * 1.15f;
        Label(p, "Top", top, 30, PlaqueText, new Vector2(0f, 40f), new Vector2(220f, 44f));
        Label(p, "Bottom", bottom, 26, PlaqueText, new Vector2(0f, 2f), new Vector2(250f, 44f));
    }

    static Button TextButton(RectTransform parent, string name, Sprite sprite, string text, Vector2 pos, float scale, Color textColor)
    {
        RectTransform rt = Img(parent, name, sprite, pos, sprite == green ? new Vector2(449f, 185f) : new Vector2(446f, 146f), true);
        rt.localScale = Vector3.one * scale;
        Label(rt, "Text", text, sprite == green ? 70 : 64, textColor, Vector2.zero, new Vector2(400f, 110f));
        var image = rt.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rt.gameObject.AddComponent<PunchyButton>();
        return button;
    }

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

    static RectTransform Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, bool preserve)
    {
        RectTransform rt = NewUI(name, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserve;
        image.raycastTarget = false;
        return rt;
    }

    static Text Label(Transform parent, string name, string value, int size, Color color, Vector2 pos, Vector2 box)
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
}
