using System.Text;
using AirplaneControllerwithShooting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Re-skins Mission Low's Machinegun / Rocket Launcher buttons with the Classic scene's weapon button art
// (Assets/SS/Gameplay: multiple.png = "SHOOT MULTIPLE BULLETS", rockets.png = "SHOOT ROCKETS").
// The buttons keep their EventTrigger firing (hold to fire), and the ammo counters stay as badges.
// Safe to re-run.
public static class MissionLowWeaponButtonsSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    static readonly Vector2 ButtonSize = new Vector2(300f, 121f);   // 465x188 art at ~65%
    const float RightEdge = -300f;   // clear of the engine slider and its (mobile) touch area
    const float BottomY = 56f, Gap = 18f;

    [MenuItem("Tools/AirStrike/Weapon Buttons (Mission Low)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MissionLowWeaponButtonsSetup]\n" + Build(SceneManager.GetActiveScene()));
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
        if (gc == null) return "ERROR: GameCanvas not found.";

        Sprite multiple = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SS/Gameplay/multiple.png");
        Sprite rockets = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SS/Gameplay/rockets.png");
        if (multiple == null || rockets == null) return "ERROR: Assets/SS/Gameplay/multiple.png or rockets.png not found.";
        Font font = gm != null && gm.housesText != null ? gm.housesText.font : null; // Bulletproof

        var log = new StringBuilder();
        Skin(gc.button_Missile, gc.Text_Ammo_Missile, rockets, new Vector2(RightEdge, BottomY), font, log);
        Skin(gc.button_Machinegun, gc.Text_Ammo_Machinegun, multiple, new Vector2(RightEdge, BottomY + ButtonSize.y + Gap), font, log);

        EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    static void Skin(Button button, Text ammo, Sprite sprite, Vector2 position, Font font, StringBuilder log)
    {
        if (button == null) { log.AppendLine("NOTE: a weapon button isn't assigned on GameCanvas, skipped."); return; }

        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = ButtonSize;
        rt.localScale = Vector3.one;

        var image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = true;

        // Full-strength art; slightly darker while pressed (the old round button used see-through tints)
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.interactable = true; // was off in the prefab, which drew the art with the 60% "disabled" tint

        PrefabOverrides.Record(rt, image, button);

        // The new art already has the icon and "SHOOT ..." label: hide the old icon and label
        foreach (Transform child in button.transform)
        {
            if (ammo != null && child == ammo.transform) continue;
            child.gameObject.SetActive(false);
            PrefabOverrides.Record(child.gameObject);
        }

        // Ammo count: small badge over the art's icon square (bottom-left), colour still set by GunController
        if (ammo != null)
        {
            var aRT = (RectTransform)ammo.transform;
            aRT.anchorMin = aRT.anchorMax = new Vector2(0f, 0f);
            aRT.pivot = new Vector2(0.5f, 0.5f);
            aRT.anchoredPosition = new Vector2(68f, 24f);
            aRT.sizeDelta = new Vector2(110f, 40f);
            aRT.localScale = Vector3.one;
            if (font != null) ammo.font = font;
            ammo.fontSize = 28;
            ammo.alignment = TextAnchor.MiddleCenter;
            ammo.horizontalOverflow = HorizontalWrapMode.Overflow;
            ammo.raycastTarget = false;
            var outline = ammo.GetComponent<Outline>();
            if (outline == null) outline = ammo.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            ammo.transform.SetAsLastSibling();
            PrefabOverrides.Record(aRT, ammo, outline);
        }

        if (button.GetComponent<PunchyButton>() == null) button.gameObject.AddComponent<PunchyButton>();
        log.AppendLine($"{button.name}: {sprite.name} at {position}");
    }
}
