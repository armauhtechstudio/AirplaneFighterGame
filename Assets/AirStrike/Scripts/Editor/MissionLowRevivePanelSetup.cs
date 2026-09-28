using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the Revive popup in Mission Low in the same military-metal style as the Pause / Fail popups:
// diamond-plate body (back.png) with a steel header plaque on top, a steel countdown gauge with a gold
// ring that drains, "+3 hearts", a green REVIVE button and a small red NO THANKS button.
// Art comes from Assets/SS/Popup/Revive (Tools/AirStrike/Generate Revive Popup Art).
// Wired to GameManagerMode2: RevivePlayer / DeclineRevive, reviveTimerText, reviveTimerFill.
// Safe to re-run: the old RevivePanel is replaced.
public static class MissionLowRevivePanelSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    const string Art = ReviveArtGenerator.Folder;

    static readonly Color PlaqueText = new Color(0.86f, 0.88f, 0.91f);   // engraved light steel
    static readonly Color Gold = new Color(1f, 0.80f, 0.20f);            // like the RESTART button
    static readonly Color Subtle = new Color(0.78f, 0.80f, 0.84f);

    [MenuItem("Tools/AirStrike/Create Revive Panel (Mission Low)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MissionLowRevivePanelSetup]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        GameManagerMode2 gm = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (gm == null) gm = root.GetComponentInChildren<GameManagerMode2>(true);
        if (gm == null || gm.failPanel == null) return "ERROR: GameManagerMode2 or its fail panel not found.";

        // Same font as the other popups (Bulletproof)
        Text popupText = gm.failPanel.GetComponentInChildren<Text>(true);
        Font font = popupText != null ? popupText.font : gm.housesText != null ? gm.housesText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (!BuildPanel(gm.failPanel.transform.parent, font, gm.RevivePlayer, gm.DeclineRevive, null, out PanelRefs refs, out string error))
            return error;

        var so = new SerializedObject(gm);
        so.FindProperty("revivePanel").objectReferenceValue = refs.panel;
        so.FindProperty("reviveTimerText").objectReferenceValue = refs.timer;
        so.FindProperty("reviveTimerFill").objectReferenceValue = refs.fill;
        so.FindProperty("reviveStatusText").objectReferenceValue = refs.status;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        return $"RevivePanel (styled) created under \"{refs.panel.transform.parent.name}\" with font \"{font.name}\" and wired to GameManagerMode2.";
    }

    public struct PanelRefs
    {
        public GameObject panel;
        public Text timer, status;
        public Image fill;
    }

    /// <summary>
    /// Builds the styled Revive popup under <paramref name="parent"/> (replacing an old one), wired to the
    /// given REVIVE / NO THANKS actions. rewardText null -> "+ ♥♥♥" hearts row, else one heart + that text.
    /// </summary>
    public static bool BuildPanel(Transform parent, Font font, UnityEngine.Events.UnityAction onRevive, UnityEngine.Events.UnityAction onDecline,
                                  string rewardText, out PanelRefs refs, out string error)
    {
        refs = default;
        error = null;
        Sprite body = Load("Assets/SS/Popup/back.png");
        Sprite header = Load($"{Art}/revive_header.png");
        Sprite gauge = Load($"{Art}/revive_gauge.png");
        Sprite ring = Load($"{Art}/revive_ring.png");
        Sprite heart = Load($"{Art}/revive_heart.png");
        Sprite green = Load($"{Art}/revive_button.png");
        Sprite red = Load($"{Art}/revive_nothanks.png");
        if (body == null || header == null || gauge == null || ring == null || heart == null || green == null || red == null)
        {
            error = "ERROR: revive art missing — run Tools/AirStrike/Generate Revive Popup Art first.";
            return false;
        }

        Transform old = parent.Find("RevivePanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Full screen: dark tint over the frozen game, blocks touches behind it
        RectTransform panel = NewUI("RevivePanel", parent);
        Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
        panel.SetAsLastSibling();

        // Diamond-plate body, like the pause / fail popups
        RectTransform bg = Img(panel, "bg", body, Vector2.zero, new Vector2(640f, 820f));
        bg.GetComponent<Image>().preserveAspect = false; // stretched to its box, like the other popups

        // Steel plaque on the top edge: "SECOND [CHANCE]"
        RectTransform plaque = Img(bg, "header", header, new Vector2(0f, 48f), new Vector2(334f, 166f));
        plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 1f);
        plaque.localScale = Vector3.one * 1.1f;
        Label(plaque, "Top", "SECOND", font, 30, PlaqueText, new Vector2(0f, 40f), new Vector2(220f, 44f));
        Label(plaque, "Bottom", "[CHANCE]", font, 32, PlaqueText, new Vector2(0f, 2f), new Vector2(240f, 44f));

        Label(bg, "Subtitle", "REVIVE AND KEEP FLYING!", font, 30, Subtle, new Vector2(0f, 300f), new Vector2(560f, 44f));

        // Countdown gauge: steel bezel, dark track, gold ring that drains, big number
        RectTransform gaugeRT = Img(bg, "Gauge", gauge, new Vector2(0f, 130f), new Vector2(250f, 250f));
        Image track = Img(gaugeRT, "Track", ring, Vector2.zero, new Vector2(250f, 250f)).GetComponent<Image>();
        track.color = new Color(0f, 0f, 0f, 0.6f);
        Image fill = Img(gaugeRT, "Fill", ring, Vector2.zero, new Vector2(250f, 250f)).GetComponent<Image>();
        fill.color = Gold;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        fill.fillAmount = 1f;
        Text timer = Label(gaugeRT, "Timer", "5", font, 112, Gold, new Vector2(0f, -4f), new Vector2(200f, 150f));

        // What the player gets back: "+ ♥ ♥ ♥" (lives) or "♥ <rewardText>" (e.g. FULL HEALTH)
        RectTransform hearts = NewUI("Hearts", bg);
        hearts.anchoredPosition = new Vector2(0f, -45f);
        hearts.sizeDelta = new Vector2(300f, 64f);
        if (rewardText == null)
        {
            Label(hearts, "Plus", "+", font, 54, Color.white, new Vector2(-108f, 2f), new Vector2(50f, 64f));
            for (int i = 0; i < 3; i++)
                Img(hearts, $"Heart{i + 1}", heart, new Vector2(-40f + i * 68f, 0f), new Vector2(60f, 60f));
        }
        else
        {
            Img(hearts, "Heart", heart, new Vector2(-120f, 0f), new Vector2(60f, 60f));
            Label(hearts, "RewardText", rewardText, font, 40, Color.white, new Vector2(30f, 0f), new Vector2(260f, 64f));
        }

        // Green REVIVE button (RESUME style) with a heart icon
        RectTransform reviveRT = Img(bg, "ReviveButton", green, new Vector2(0f, -160f), new Vector2(449f, 185f));
        reviveRT.localScale = Vector3.one * 0.75f;
        Img(reviveRT, "Icon", heart, new Vector2(-170f, 2f), new Vector2(76f, 76f));
        Label(reviveRT, "Text", "REVIVE", font, 64, Color.white, new Vector2(-8f, 0f), new Vector2(260f, 110f));

        // "AD" badge on the right side of the button: the revive is a rewarded ad
        Sprite gold = AssetDatabase.LoadAssetAtPath<Sprite>(LeaderboardArtGenerator.GoldPath);
        RectTransform adBadge = Img(reviveRT, "AdBadge", gold, Vector2.zero, new Vector2(104f, 74f));
        adBadge.anchorMin = adBadge.anchorMax = new Vector2(1f, 0.5f);
        adBadge.anchoredPosition = new Vector2(-80f, 0f);
        var badgeImg = adBadge.GetComponent<Image>();
        badgeImg.preserveAspect = false;
        badgeImg.type = Image.Type.Sliced;
        Text adText = Label(adBadge, "Text", "AD", font, 44, new Color(1f, 0.93f, 0.6f), Vector2.zero, new Vector2(104f, 74f));
        Button revive = MakeButton(reviveRT);

        // Small red NO THANKS button (HOME style)
        RectTransform noRT = Img(bg, "NoThanksButton", red, new Vector2(0f, -335f), new Vector2(446f, 146f));
        noRT.localScale = Vector3.one * 0.5f;
        Label(noRT, "Text", "NO THANKS", font, 64, new Color(1f, 0.86f, 0.86f), Vector2.zero, new Vector2(400f, 110f));
        Button noThanks = MakeButton(noRT);

        // Status line (ad not available / watch the full ad), empty normally
        Text status = Label(bg, "StatusText", "", font, 24, new Color(1f, 0.45f, 0.38f), new Vector2(0f, -262f), new Vector2(560f, 36f));

        UnityEventTools.AddPersistentListener(revive.onClick, onRevive);
        UnityEventTools.AddPersistentListener(noThanks.onClick, onDecline);

        panel.gameObject.AddComponent<PunchyPanel>(); // same pop-in as the other popups
        panel.gameObject.SetActive(false);

        refs = new PanelRefs { panel = panel.gameObject, timer = timer, fill = fill, status = status };
        return true;
    }

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
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

    // Engraved look: dark outline + soft drop shadow, like the text baked into the popup art
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
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -4f);
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
