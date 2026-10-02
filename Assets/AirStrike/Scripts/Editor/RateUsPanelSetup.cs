using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the Rate Us popup prefab (Assets/AirStrike/Resources/RateUsPanel.prefab) in the military-metal
// popup style: diamond-plate body, steel plaque "RATE / US", "ENJOYING THE GAME?", 5 tappable stars,
// a message line and a close X. It carries its own overlay canvas (on top of everything), so StoreReview
// can show it from any scene (Resources.Load). Art: Tools/AirStrike/Generate Rate Us Art.
// Safe to re-run: the prefab is overwritten.
public static class RateUsPanelSetup
{
    public const string PrefabFolder = "Assets/AirStrike/Resources";
    public const string PrefabPath = PrefabFolder + "/RateUsPanel.prefab";

    static readonly Color PlaqueText = new Color(0.86f, 0.88f, 0.91f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.20f);
    static readonly Color Subtle = new Color(0.82f, 0.84f, 0.87f);

    [MenuItem("Tools/AirStrike/Create Rate Us Panel")]
    public static void RunFromMenu() => Debug.Log("[RateUsPanelSetup] " + Build());

    public static string Build()
    {
        Sprite body = Load("Assets/SS/Popup/back.png"), header = Load("Assets/SS/Popup/Revive/revive_header.png"),
               close = Load("Assets/SS/Popup/RemoveAds/removeads_close.png"),
               empty = Load(RateUsArtGenerator.Folder + "/star_empty.png"), gold = Load(RateUsArtGenerator.Folder + "/star_gold.png");
        if (body == null || header == null) return "ERROR: popup art missing (back.png / Revive header).";
        if (empty == null || gold == null) return "ERROR: star art missing — run Tools/AirStrike/Generate Rate Us Art first.";
        if (close == null) return "ERROR: close button art missing — run Tools/AirStrike/Generate Remove Ads Art first.";

        Font font = null;
        foreach (string guid in AssetDatabase.FindAssets("Bulletproof t:Font"))
            if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Root: own overlay canvas, above every other UI
        var root = new GameObject("RateUsPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = LayerMask.NameToLayer("UI");
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        // Dim, blocks touches behind it
        var dim = NewUI("Dim", root.transform);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // Body
        var bg = Img(dim, "bg", body, new Vector2(0f, -25f), new Vector2(640f, 540f));

        var plaque = Img(bg, "header", header, new Vector2(0f, 48f), new Vector2(334f, 166f));
        plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 1f);
        plaque.localScale = Vector3.one * 1.15f;
        Label(plaque, "Top", "RATE", font, 32, PlaqueText, new Vector2(0f, 40f), new Vector2(240f, 44f));
        Label(plaque, "Bottom", "US", font, 36, Gold, new Vector2(0f, 2f), new Vector2(240f, 44f));

        Label(bg, "Title", "ENJOYING THE GAME?", font, 44, Gold, new Vector2(0f, 120f), new Vector2(580f, 60f));
        Label(bg, "Subtitle", "TAP A STAR TO RATE US!", font, 28, Subtle, new Vector2(0f, 62f), new Vector2(580f, 44f));

        // 5 stars
        var row = NewUI("Stars", bg);
        row.anchoredPosition = new Vector2(0f, -35f);
        row.sizeDelta = new Vector2(560f, 110f);
        var stars = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var star = Img(row, "Star" + (i + 1), empty, new Vector2(-196f + i * 98f, 0f), new Vector2(90f, 90f));
            var img = star.GetComponent<Image>();
            img.raycastTarget = true;
            var button = star.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None; // the panel animates the stars itself
            stars[i] = img;
        }

        var message = Label(bg, "Message", "", font, 36, Gold, new Vector2(0f, -140f), new Vector2(580f, 56f));
        message.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

        // Close X, top-right corner of the body
        var closeRT = Img(bg, "CloseButton", close, new Vector2(-6f, -6f), new Vector2(84f, 84f));
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.GetComponent<Image>().raycastTarget = true;
        var closeButton = closeRT.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeRT.GetComponent<Image>();

        var panel = root.AddComponent<RateUsPanel>();
        panel.body = bg;
        panel.stars = stars;
        panel.emptyStar = empty;
        panel.goldStar = gold;
        panel.messageText = message;
        panel.closeButton = closeButton;

        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/AirStrike", "Resources");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
        Object.DestroyImmediate(root);
        return ok ? $"Prefab saved: {PrefabPath} (font \"{font.name}\")" : "ERROR: prefab could not be saved.";
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
