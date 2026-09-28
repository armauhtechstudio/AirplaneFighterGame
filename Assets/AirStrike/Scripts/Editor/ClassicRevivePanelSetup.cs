using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the same styled Revive popup as Mission Low in the Classic scene (next to GameUI's fail panel)
// and wires it to GameManager: RevivePlayer (rewarded ad) / DeclineRevive, timer text + ring, status.
// Safe to re-run: the old RevivePanel is replaced.
public static class ClassicRevivePanelSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Classic.unity";

    [MenuItem("Tools/AirStrike/Create Revive Panel (Classic)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[ClassicRevivePanelSetup]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        GameManager gm = null;
        GameUI ui = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (gm == null) gm = root.GetComponentInChildren<GameManager>(true);
            if (ui == null) ui = root.GetComponentInChildren<GameUI>(true);
        }
        if (gm == null) return "ERROR: GameManager not found.";
        if (ui == null || ui.failPanel == null) return "ERROR: GameUI or its fail panel not found.";

        // Same font as the other popups
        Text popupText = ui.failPanel.GetComponentInChildren<Text>(true);
        Font font = popupText != null ? popupText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (!MissionLowRevivePanelSetup.BuildPanel(ui.failPanel.transform.parent, font, gm.RevivePlayer, gm.DeclineRevive,
                                                   "FULL HEALTH", out MissionLowRevivePanelSetup.PanelRefs refs, out string error))
            return error;

        var so = new SerializedObject(gm);
        so.FindProperty("revivePanel").objectReferenceValue = refs.panel;
        so.FindProperty("reviveTimerText").objectReferenceValue = refs.timer;
        so.FindProperty("reviveTimerFill").objectReferenceValue = refs.fill;
        so.FindProperty("reviveStatusText").objectReferenceValue = refs.status;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        return $"RevivePanel created under \"{refs.panel.transform.parent.name}\" with font \"{font.name}\" and wired to GameManager.";
    }
}
