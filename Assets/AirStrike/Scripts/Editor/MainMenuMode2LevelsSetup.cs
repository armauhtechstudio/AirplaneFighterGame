using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Puts the Mode 2 level buttons of the main menu into a ScrollView and grows them to 10 levels.
// Lock / unlock keeps working through MainMenuController.RefreshMod2LevelUI (Mod2_UnlockedLevels),
// because every button stays in the mod2Levels array with child 0 = lock image, child 1 = level text.
public static class MainMenuMode2LevelsSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mainmenu.unity";
    const int TargetLevelCount = 10;
    const string ScrollViewName = "Mode2LevelScrollView";

    [MenuItem("Tools/AirStrike/Main Menu - Mode 2 Levels Scroll View (10 levels)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Run();
    }

    // Called via: Unity.exe -batchmode -nographics -quit -projectPath <proj> -executeMethod MainMenuMode2LevelsSetup.RunFromCLI
    public static void RunFromCLI()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Run();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    static void Run()
    {
        var log = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        log.AppendLine($"Scene: {scene.name} ({scene.path})");

        var menu = Object.FindObjectOfType<MainMenuController>(true);
        if (menu == null)
        {
            log.AppendLine("ERROR: No MainMenuController found in the Mainmenu scene.");
            Flush(log);
            return;
        }

        var buttons = new List<RectTransform>();
        if (menu.mod2Levels != null)
            foreach (LevelEntry entry in menu.mod2Levels)
                if (entry != null && entry.levelButton != null)
                    buttons.Add((RectTransform)entry.levelButton.transform);

        if (buttons.Count == 0)
        {
            // mod2Levels was never filled in: take the "level..." buttons under the Mode 2 panel instead
            log.AppendLine("mod2Levels is empty — looking for level buttons under mod2LevelPanel.");
            if (menu.mod2LevelPanel != null)
            {
                LogHierarchy(menu.mod2LevelPanel.transform, 0, log);
                foreach (Button b in menu.mod2LevelPanel.GetComponentsInChildren<Button>(true))
                    if (b.name.ToLowerInvariant().StartsWith("level"))
                        buttons.Add((RectTransform)b.transform);
                buttons.Sort((a, b) => LevelOrder(a).CompareTo(LevelOrder(b)));
            }
        }

        if (buttons.Count == 0)
        {
            log.AppendLine("ERROR: No Mode 2 level buttons found (mod2Levels empty and none under mod2LevelPanel).");
            Flush(log);
            return;
        }
        log.AppendLine($"Found {buttons.Count} Mode 2 level button(s).");

        // ---- 1. Scroll view (skipped if the buttons are already inside one) ----
        RectTransform content;
        ScrollRect existing = buttons[0].GetComponentInParent<ScrollRect>(true);
        if (existing != null && existing.content == buttons[0].parent)
        {
            content = existing.content;
            log.AppendLine($"Buttons are already inside ScrollView \"{existing.name}\" — reusing it.");
        }
        else
        {
            content = CreateScrollView(buttons, log);
        }

        // ---- 2. Grow to 10 buttons ----
        RectTransform template = buttons[buttons.Count - 1];
        while (buttons.Count < TargetLevelCount)
        {
            GameObject copy = Object.Instantiate(template.gameObject, content);
            Undo.RegisterCreatedObjectUndo(copy, "Add Mode 2 level button");
            copy.name = $"level1 ({buttons.Count + 1})";
            buttons.Add((RectTransform)copy.transform);
            log.AppendLine($"  + {copy.name}");
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].SetSiblingIndex(i);
            SetLevelNumber(buttons[i], i + 1, log);
        }

        ApplyTopDown(content, log);

        // ---- 3. mod2Levels array ----
        var so = new SerializedObject(menu);
        SerializedProperty levelsProp = so.FindProperty("mod2Levels");
        levelsProp.arraySize = buttons.Count;
        for (int i = 0; i < buttons.Count; i++)
        {
            levelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("levelButton").objectReferenceValue =
                buttons[i].GetComponent<Button>();
        }
        so.ApplyModifiedProperties();
        log.AppendLine($"mod2Levels now has {buttons.Count} entries.");

        EditorSceneManager.MarkSceneDirty(scene);
        log.AppendLine("Done. Save the scene (Ctrl+S) to keep the changes.");
        Flush(log);
    }

    // Builds ScrollView > Viewport > Content over the area the buttons occupy now, then moves the buttons in.
    // One row of buttons -> horizontal scrolling; a grid -> vertical scrolling with the same column count.
    static RectTransform CreateScrollView(List<RectTransform> buttons, StringBuilder log)
    {
        RectTransform parent = (RectTransform)buttons[0].parent;

        // Area + layout measured from the current button placement (in the parent's space)
        Vector2 size = buttons[0].rect.size;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        var centers = new List<Vector2>();
        foreach (RectTransform b in buttons)
        {
            Vector2 c = parent.InverseTransformPoint(b.TransformPoint(b.rect.center));
            centers.Add(c);
            min = Vector2.Min(min, c - size * 0.5f);
            max = Vector2.Max(max, c + size * 0.5f);
        }

        int columns = 0;
        float rowY = centers[0].y;
        foreach (Vector2 c in centers)
            if (Mathf.Abs(c.y - rowY) < size.y * 0.5f) columns++;
        bool singleRow = columns == buttons.Count;

        float spacingX = 20f, spacingY = 20f;
        if (buttons.Count > 1)
        {
            float gap = Mathf.Abs(centers[1].x - centers[0].x);
            if (Mathf.Abs(centers[1].y - centers[0].y) < size.y * 0.5f && gap > size.x) spacingX = gap - size.x;
        }
        if (!singleRow && columns < centers.Count)
        {
            float gap = Mathf.Abs(centers[columns].y - centers[0].y);
            if (gap > size.y) spacingY = gap - size.y;
        }

        // ScrollView
        var scrollGO = new GameObject(ScrollViewName, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        Undo.RegisterCreatedObjectUndo(scrollGO, "Create Mode 2 ScrollView");
        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.SetParent(parent, false);
        scrollRT.anchorMin = scrollRT.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRT.pivot = new Vector2(0.5f, 0.5f);
        Vector2 pad = new Vector2(spacingX, spacingY) * 0.5f;
        scrollRT.sizeDelta = (max - min) + pad * 2f;
        scrollRT.anchoredPosition = (min + max) * 0.5f - (parent.rect.center);
        scrollRT.SetSiblingIndex(buttons[0].GetSiblingIndex());
        scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible, but catches drags between buttons

        // Viewport
        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        Undo.RegisterCreatedObjectUndo(viewportGO, "Create Viewport");
        var viewport = (RectTransform)viewportGO.transform;
        viewport.SetParent(scrollRT, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;

        // Content
        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        Undo.RegisterCreatedObjectUndo(contentGO, "Create Content");
        var content = (RectTransform)contentGO.transform;
        content.SetParent(viewport, false);
        var fitter = contentGO.GetComponent<ContentSizeFitter>();

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 20f;

        if (singleRow)
        {
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var layout = contentGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacingX;
            layout.padding = new RectOffset((int)pad.x, (int)pad.x, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.horizontal = true;
            scroll.vertical = false;
            log.AppendLine($"Created horizontal ScrollView \"{ScrollViewName}\" (spacing {spacingX:0}).");
        }
        else
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var grid = contentGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = size;
            grid.spacing = new Vector2(spacingX, spacingY);
            grid.padding = new RectOffset((int)pad.x, (int)pad.x, (int)pad.y, (int)pad.y);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.horizontal = false;
            scroll.vertical = true;
            log.AppendLine($"Created vertical ScrollView \"{ScrollViewName}\" ({columns} columns, spacing {spacingX:0}x{spacingY:0}).");
        }

        foreach (RectTransform b in buttons)
        {
            if (b.parent != parent)
                log.AppendLine($"  WARNING: \"{b.name}\" had a different parent than \"{buttons[0].name}\"; check its placement.");

            Vector2 buttonSize = b.rect.size;
            Undo.SetTransformParent(b, content, "Move level button into ScrollView");
            Undo.RecordObject(b, "Reset level button anchors");
            // Fixed-size anchors so the layout group keeps the button's real size
            b.anchorMin = b.anchorMax = b.pivot = new Vector2(0.5f, 0.5f);
            b.sizeDelta = buttonSize;
            b.localRotation = Quaternion.identity;
        }

        return content;
    }

    // Normal reading order: level 1 in the top row, higher levels going down, content anchored to the top.
    // (MainMenuController plays the intro: starts at the last level and slides up to level 1.)
    static void ApplyTopDown(RectTransform content, StringBuilder log)
    {
        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            log.AppendLine("NOTE: content has no GridLayoutGroup (single row) — top-down layout not applied.");
            return;
        }

        Undo.RecordObject(grid, "Top-down level grid");
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        Undo.RecordObject(content, "Anchor content to top");
        float height = content.rect.height;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, height);

        ScrollRect scroll = content.GetComponentInParent<ScrollRect>(true);
        if (scroll != null)
        {
            Undo.RecordObject(scroll, "Scroll to top");
            scroll.verticalNormalizedPosition = 1f;
        }
        log.AppendLine("Grid set top-down: level 1 at the top.");
    }

    // Numbers the button's level text 1..10 (the text child, not the lock image, in whatever order they are).
    static void SetLevelNumber(RectTransform button, int number, StringBuilder log)
    {
        Transform label = button;
        foreach (Transform child in button)
        {
            if (child.name.ToLowerInvariant().Contains("lock")) continue;
            if (child.GetComponentInChildren<Text>(true) != null || child.GetComponentInChildren<TMP_Text>(true) != null)
            {
                label = child;
                break;
            }
        }

        var text = label.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            Undo.RecordObject(text, "Set level number");
            text.text = ReplaceNumber(text.text, number);
            EditorUtility.SetDirty(text);
            return;
        }

        var tmp = label.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            Undo.RecordObject(tmp, "Set level number");
            tmp.text = ReplaceNumber(tmp.text, number);
            EditorUtility.SetDirty(tmp);
            return;
        }

        log.AppendLine($"  NOTE: \"{button.name}\" has no level Text — set its level number by hand if it uses an image.");
    }

    // "3" -> "10", "Level 3" -> "Level 10"; text without a number is left alone
    static string ReplaceNumber(string current, int number)
    {
        if (string.IsNullOrEmpty(current)) return number.ToString();
        var match = System.Text.RegularExpressions.Regex.Match(current, @"\d+");
        return match.Success
            ? current.Substring(0, match.Index) + number + current.Substring(match.Index + match.Length)
            : current;
    }

    // "level1" -> 0, "level1 (3)" -> 3: order by the Unity duplicate number, then by hierarchy position
    static float LevelOrder(RectTransform t)
    {
        var match = System.Text.RegularExpressions.Regex.Match(t.name, @"\((\d+)\)\s*$");
        int dup = match.Success ? int.Parse(match.Groups[1].Value) : 0;
        return dup * 1000 + t.GetSiblingIndex();
    }

    static void LogHierarchy(Transform t, int depth, StringBuilder log)
    {
        if (depth > 4) return;
        string components = t.GetComponent<Button>() != null ? " [Button]" : "";
        log.AppendLine($"{new string(' ', depth * 2)}- {t.name}{components}{(t.gameObject.activeSelf ? "" : " (inactive)")}");
        foreach (Transform child in t) LogHierarchy(child, depth + 1, log);
    }

    static void Flush(StringBuilder log)
    {
        Debug.Log("[MainMenuMode2LevelsSetup]\n" + log);
    }
}
