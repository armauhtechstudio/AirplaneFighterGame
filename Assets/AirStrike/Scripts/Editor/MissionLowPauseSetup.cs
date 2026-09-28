using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Wires the Pause / Resume / Next / Restart / Home buttons that live under
// "AdvancedAirplaneSystem" in the Mission Low scene to GameManagerMode2.
public static class MissionLowPauseSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    const string RootName = "AdvancedAirplaneSystem";

    [MenuItem("Tools/AirStrike/Wire Mission Low Pause Buttons (Mode 2)")]
    public static void RunFromMenu()
    {
        Run();
    }

    // Called via: Unity.exe -batchmode -nographics -quit -projectPath <proj> -executeMethod MissionLowPauseSetup.RunFromCLI
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

        var gm = Object.FindObjectOfType<GameManagerMode2>(true);
        if (gm == null)
        {
            log.AppendLine("ERROR: No GameManagerMode2 found in the open scene.");
            Flush(log);
            return;
        }

        Transform root = FindInScene(scene, RootName);
        if (root == null)
        {
            log.AppendLine($"ERROR: Couldn't find \"{RootName}\" in the scene.");
            Flush(log);
            return;
        }

        int wired = 0;
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            UnityAction action = ActionFor(gm, button.name);
            if (action == null) continue;

            Undo.RecordObject(button, "Wire Mode 2 pause button");

            // Remove whatever was there before (e.g. GameUI / GameManager mode 1 calls)
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);

            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
            wired++;
            log.AppendLine($"  {GetPath(button.transform, root)} -> GameManagerMode2.{action.Method.Name}()");
        }
        log.AppendLine($"Wired {wired} button(s).");

        var so = new SerializedObject(gm);
        AssignPanel(so, "pausePanel", FindPausePanel(root), log);
        AssignPanel(so, "winPanel", FindByName(root, "WinPanel"), log);
        AssignPanel(so, "failPanel", FindByName(root, "FailPanel"), log);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        log.AppendLine("Done. Save the scene (Ctrl+S) to keep the changes.");
        Flush(log);
    }

    static UnityAction ActionFor(GameManagerMode2 gm, string buttonName)
    {
        string n = buttonName.ToLowerInvariant();
        if (n.Contains("resume")) return gm.ResumeGame;
        if (n.Contains("pause")) return gm.PauseGame;
        if (n.Contains("next")) return gm.NextLevel;
        if (n.Contains("restart") || n.Contains("retry")) return gm.RestartLevel;
        if (n.Contains("home") || n.Contains("menu")) return gm.GoToHome;
        return null;
    }

    // The pause panel is the non-button object named "*pause*" that contains the resume button;
    // failing that, the resume button's parent.
    static GameObject FindPausePanel(Transform root)
    {
        Button resume = null;
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
            if (b.name.ToLowerInvariant().Contains("resume")) { resume = b; break; }
        if (resume == null) return null;

        for (Transform t = resume.transform.parent; t != null && t != root; t = t.parent)
            if (t.name.ToLowerInvariant().Contains("pause") && t.GetComponent<Button>() == null)
                return t.gameObject;

        return resume.transform.parent != null ? resume.transform.parent.gameObject : null;
    }

    static void AssignPanel(SerializedObject so, string field, GameObject panel, StringBuilder log)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop.objectReferenceValue != null)
        {
            log.AppendLine($"{field}: already set to \"{prop.objectReferenceValue.name}\" (left as is).");
            return;
        }
        if (panel == null)
        {
            log.AppendLine($"WARNING: {field} is empty and no matching panel was found — assign it manually.");
            return;
        }
        prop.objectReferenceValue = panel;
        log.AppendLine($"{field}: assigned \"{panel.name}\".");
    }

    static GameObject FindByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == name) return go.transform;
            GameObject found = FindByName(go.transform, name);
            if (found != null) return found.transform;
        }
        return null;
    }

    static string GetPath(Transform t, Transform root)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null && p != root; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    static void Flush(StringBuilder log)
    {
        Debug.Log("[MissionLowPauseSetup]\n" + log);
    }
}
