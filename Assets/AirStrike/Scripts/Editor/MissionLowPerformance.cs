using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Puts Mission Low's trees and rocks (original + expanded tiles) on a "Scenery" layer so cameras can
// stop drawing them beyond GameManagerMode2.sceneryDrawDistance. With the 3x3 world the camera was
// drawing thousands of far-away trees every frame (~10,000 renderers), which is what made it lag.
public static class MissionLowPerformance
{
    public const string SceneryLayer = "Scenery";
    static readonly string[] SceneryGroups = { "Trees", "Rocks" };

    public static string Apply(Scene scene)
    {
        var log = new StringBuilder();
        int layer = EnsureLayer(SceneryLayer, log);
        if (layer < 0) return log.ToString();

        int changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Environment" && root.name != "EnvironmentExtended") continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(SceneryGroups, t.name) < 0) continue;
                foreach (Transform item in t.GetComponentsInChildren<Transform>(true))
                {
                    if (item.gameObject.layer == layer) continue;
                    item.gameObject.layer = layer;
                    changed++;
                }
            }
        }
        log.AppendLine($"Moved {changed} tree/rock objects to layer \"{SceneryLayer}\" ({layer}).");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    // Adds the layer to the first free user slot (8..31) if it doesn't exist yet
    static int EnsureLayer(string name, StringBuilder log)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;
            slot.stringValue = name;
            tagManager.ApplyModifiedProperties();
            log.AppendLine($"Added layer \"{name}\" at index {i}.");
            return i;
        }
        log.AppendLine("ERROR: no free layer slot for \"" + name + "\".");
        return -1;
    }
}
