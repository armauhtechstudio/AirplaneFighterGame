using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the "REMOVE ADS" offer popup in the Mainmenu, in the military-metal popup style (diamond-plate
// body, steel header plaque, green button):
//   header REMOVE / ADS, close X (top-right, wired to Close), a steel-framed screen with the crossed-out
//   AD emblem and three glowing gold chevrons, three green-check benefits, and a CLAIM NOW button
//   (NOT wired: hook it to the purchase, then MainMenuController.RemoveAdsFromGame()).
// A RemoveAdsOfferTrigger on the mode selection panel opens it every time that panel shows.
// Art: Tools/AirStrike/Generate Remove Ads Art. Safe to re-run: the old panel is replaced.
public static class MainMenuRemoveAdsOfferSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mainmenu.unity";
    const string PanelName = "RemoveAdsOffer";
    const string Art = RemoveAdsArtGenerator.Folder;

    static readonly Color PlaqueText = new Color(0.86f, 0.88f, 0.91f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.20f);
    static readonly Color RowText = new Color(0.93f, 0.93f, 0.90f);

    [MenuItem("Tools/AirStrike/Create Remove Ads Offer Panel (Mainmenu)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MainMenuRemoveAdsOfferSetup] " + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        Sprite body = Load("Assets/SS/Popup/back.png"), header = Load("Assets/SS/Popup/Revive/revive_header.png"),
               green = Load("Assets/SS/Popup/Revive/revive_button.png"),
               close = Load($"{Art}/removeads_close.png"), check = Load($"{Art}/removeads_check.png"),
               chevron = Load($"{Art}/removeads_chevron.png"), screen = Load($"{Art}/removeads_screen.png"),
               emblem = Load($"{Art}/removeads_emblem.png"), slash = Load($"{Art}/removeads_slash.png");
        if (body == null || header == null || green == null)
            return "ERROR: popup art missing (back.png / Revive art).";
        if (close == null || check == null || chevron == null || screen == null || emblem == null || slash == null)
            return "ERROR: Remove Ads art missing — run Tools/AirStrike/Generate Remove Ads Art first.";

        MainMenuController controller = null;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (controller == null) controller = r.GetComponentInChildren<MainMenuController>(true);
        if (controller == null || controller.modSelectionPanel == null) return "ERROR: MainMenuController / modSelectionPanel not found.";

        Font font = null;
        foreach (Text t in controller.modSelectionPanel.transform.root.GetComponentsInChildren<Text>(true))
            if (font == null && t.font != null && t.font.name == "Bulletproof") font = t.font;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Transform parent = controller.modSelectionPanel.transform.parent;
        Transform old = parent.Find(PanelName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Full-screen dim, blocks touches behind it
        var panel = NewUI(PanelName, parent);
        Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        panel.SetAsLastSibling();

        // Diamond-plate body like the other popups
        var bg = Img(panel, "bg", body, new Vector2(0f, -20f), new Vector2(640f, 800f));

        // Steel plaque: REMOVE / ADS
        var plaque = Img(bg, "header", header, new Vector2(0f, 48f), new Vector2(334f, 166f));
        plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 1f);
        plaque.localScale = Vector3.one * 1.2f;
        Label(plaque, "Top", "REMOVE", font, 30, PlaqueText, new Vector2(0f, 40f), new Vector2(240f, 44f));
        Label(plaque, "Bottom", "ADS", font, 36, Gold, new Vector2(0f, 2f), new Vector2(240f, 44f));

        // Picture: steel-framed screen, crossed-out AD emblem, three chevrons pointing at it
        var screenRT = Img(bg, "Screen", screen, new Vector2(0f, 180f), new Vector2(540f, 260f));
        var emblemRT = Img(screenRT, "Emblem", emblem, new Vector2(-105f, 0f), new Vector2(190f, 190f));
        var ad = Label(emblemRT, "AD", "AD", font, 72, Color.white, Vector2.zero, new Vector2(190f, 190f));
        ad.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        var slashRT = NewUI("Slash", emblemRT);
        Stretch(slashRT);
        slashRT.gameObject.AddComponent<Image>().sprite = slash;
        slashRT.GetComponent<Image>().raycastTarget = false;

        var chevrons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var c = Img(screenRT, "Chevron" + (i + 1), chevron, new Vector2(60f + i * 62f, 0f), new Vector2(64f, 80f));
            chevrons[i] = c.GetComponent<Image>();
        }

        // Benefits card
        var card = NewUI("Benefits", bg);
        card.anchoredPosition = new Vector2(0f, -80f);
        card.sizeDelta = new Vector2(540f, 230f);
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.color = new Color(0f, 0f, 0f, 0.45f);
        cardImg.raycastTarget = false;
        string[] rows = { "Remove automatic ads", "Enjoy a banner-free game", "Reward ads remain optional" };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = 76f - i * 76f;
            Img(card, "Check" + (i + 1), check, new Vector2(-220f, y), new Vector2(50f, 50f));
            var row = Label(card, "Row" + (i + 1), rows[i], font, 28, RowText, new Vector2(30f, y), new Vector2(430f, 60f));
            row.alignment = TextAnchor.MiddleLeft;
            if (i < rows.Length - 1)
            {
                var line = NewUI("Separator" + (i + 1), card);
                line.anchoredPosition = new Vector2(0f, y - 38f);
                line.sizeDelta = new Vector2(480f, 2f);
                var li = line.gameObject.AddComponent<Image>();
                li.color = new Color(1f, 1f, 1f, 0.15f);
                li.raycastTarget = false;
            }
        }

        // CLAIM NOW (not wired)
        var claim = Img(bg, "ClaimButton", green, new Vector2(0f, -295f), new Vector2(380f, 140f));
        claim.GetComponent<Image>().raycastTarget = true;
        var claimText = Label(claim, "Text", "CLAIM NOW", font, 52, Color.white, new Vector2(0f, 4f), new Vector2(360f, 120f));
        claimText.gameObject.AddComponent<Outline>().effectColor = new Color(0.05f, 0.25f, 0.03f, 0.9f);
        var claimButton = claim.gameObject.AddComponent<Button>();
        claimButton.targetGraphic = claim.GetComponent<Image>();
        claim.gameObject.AddComponent<PunchyButton>();

        // Close X (top-right corner of the body)
        var closeRT = Img(bg, "CloseButton", close, new Vector2(-6f, -6f), new Vector2(96f, 96f));
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.GetComponent<Image>().raycastTarget = true;
        var closeButton = closeRT.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeRT.GetComponent<Image>();
        closeRT.gameObject.AddComponent<PunchyButton>();

        var offer = panel.gameObject.AddComponent<RemoveAdsOfferPanel>();
        offer.claimButton = claim;
        offer.closeButton = closeRT;
        offer.emblem = emblemRT;
        offer.chevrons = chevrons;
        UnityEventTools.AddPersistentListener(closeButton.onClick, offer.Close);

        panel.gameObject.AddComponent<PunchyPanel>(); // same pop-in as the other popups
        panel.gameObject.SetActive(false);

        // Show on the mode selection, every time it opens
        var trigger = controller.modSelectionPanel.GetComponent<RemoveAdsOfferTrigger>();
        if (trigger == null) trigger = controller.modSelectionPanel.AddComponent<RemoveAdsOfferTrigger>();
        trigger.offer = offer;
        EditorUtility.SetDirty(trigger);

        var so = new SerializedObject(controller);
        so.FindProperty("removeAdsOffer").objectReferenceValue = offer;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        return $"{PanelName} created under \"{parent.name}\" (font \"{font.name}\"); trigger on \"{controller.modSelectionPanel.name}\"; CLAIM NOW not wired.";
    }

    // ---------------------------------------------------------------- helpers

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

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

    static RectTransform Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var rt = NewUI(name, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return rt;
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
