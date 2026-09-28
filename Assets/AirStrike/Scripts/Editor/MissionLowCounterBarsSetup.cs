using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Puts Mission Low's target counters (Buildings / Balloons / Tanks) on bars in the same style as the
// Classic scene's objective bars: Assets/SS/obj.png with the red bracket frame (obj panel bar.png).
// Each text is moved inside its "<name>CounterBar"; GameManagerMode2 shows/hides the whole bar.
// Safe to re-run.
public static class MissionLowCounterBarsSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    static readonly Vector2 BarSize = new Vector2(300f, 64f);
    const float LeftMargin = 24f, Spacing = 74f, TopY = 74f;

    [MenuItem("Tools/AirStrike/Counter Bars (Mission Low)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MissionLowCounterBarsSetup]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        var log = new StringBuilder();
        GameManagerMode2 gm = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (gm == null) gm = root.GetComponentInChildren<GameManagerMode2>(true);
        if (gm == null) return "ERROR: GameManagerMode2 not found.";

        Sprite bar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SS/obj.png");
        Sprite brackets = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SS/obj panel bar.png");
        if (bar == null || brackets == null) return "ERROR: Assets/SS/obj.png or 'obj panel bar.png' not found.";

        // Top to bottom, in the order they already appear
        Text[] texts = { gm.housesText, gm.balloonsText, gm.tanksText };
        string[] labels = { "Buildings: 0/0", "Balloons: 0/0", "Tanks: 0/0" };
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null) { log.AppendLine($"NOTE: counter {i} not assigned on GameManagerMode2, skipped."); continue; }

            Transform existing = text.transform.parent;
            RectTransform barRT;
            if (existing != null && existing.name == text.name + "CounterBar")
            {
                barRT = (RectTransform)existing;
            }
            else
            {
                var go = new GameObject(text.name + "CounterBar", typeof(RectTransform), typeof(Image));
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
                sImg.color = new Color(1f, 0.016f, 0f); // same red as Classic's bars
                sImg.raycastTarget = false;

                text.transform.SetParent(barRT, false);
            }

            // Bar: left edge of the screen, stacked
            barRT.anchorMin = barRT.anchorMax = new Vector2(0f, 0.5f);
            barRT.pivot = new Vector2(0f, 0.5f);
            barRT.sizeDelta = BarSize;
            barRT.anchoredPosition = new Vector2(LeftMargin, TopY - i * Spacing);
            var barImg = barRT.GetComponent<Image>();
            barImg.sprite = bar;
            barImg.raycastTarget = false;

            // Text fills the bar with some padding, like "Destroy Enemies Planes: 12"
            var tRT = (RectTransform)text.transform;
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.pivot = new Vector2(0.5f, 0.5f);
            tRT.offsetMin = new Vector2(26f, 8f);
            tRT.offsetMax = new Vector2(-26f, -8f);
            tRT.localScale = Vector3.one;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 34;
            text.text = labels[i];
            text.raycastTarget = false;
            PrefabOverrides.Record(text, tRT, barRT, barImg);

            log.AppendLine($"{text.name}: bar at y={TopY - i * Spacing}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }
}
