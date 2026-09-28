using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MissionLowLevelSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";

    const string EnemiesAirName = "Enemies_Air";   // balloons
    const string EnemiesLandName = "Enemies_Land"; // tanks
    const string CollapsiblesName = "Collapsibles"; // buildings

    const string EnemyTag = "Enemy";
    const int DefaultHP = 150;

    [MenuItem("Tools/AirStrike/Diagnose And Fix Mission Low")]
    public static void RunFromMenu()
    {
        Run();
    }

    // Called via: Unity.exe -batchmode -nographics -quit -projectPath <proj> -executeMethod MissionLowLevelSetup.RunFromCLI
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
        log.AppendLine($"GameManagerMode2 found on GameObject \"{gm.gameObject.name}\".");

        var so = new SerializedObject(gm);

        // ---- Diagnose current state before touching anything ----
        LogArray(so, "levels", log);
        LogRequirements(so, log);
        LogTextRef(so, "housesText", log);
        LogTextRef(so, "tanksText", log);
        LogTextRef(so, "balloonsText", log);
        log.AppendLine($"isTest={so.FindProperty("isTest").boolValue}, tempLvl={so.FindProperty("tempLvl").intValue}");
        log.AppendLine($"PlayerPrefs Mod2_SelectedLevel={PlayerPrefs.GetInt("Mod2_SelectedLevel", -999)}");

        // ---- Fix: auto-wire the UI Text references if left empty ----
        AutoAssignText(so, "housesText", "HousesText", log);
        AutoAssignText(so, "tanksText", "TanksText", log);
        AutoAssignText(so, "balloonsText", "BalloonsText", log);

        // ---- Fix: levels / destroyRequirements ----
        Transform levelsRoot = FindInScene(scene, "Levels");
        if (levelsRoot == null)
        {
            log.AppendLine("ERROR: Couldn't find \"Levels\" parent GameObject.");
            Flush(log);
            return;
        }

        Transform level1 = levelsRoot.Find("Level1");
        Transform level2 = GetOrCreateChild(levelsRoot, "Level2");
        Transform level3 = GetOrCreateChild(levelsRoot, "Level3");

        if (level1 == null)
        {
            log.AppendLine("ERROR: Couldn't find \"Level1\" under \"Levels\".");
            Flush(log);
            return;
        }

        Transform enemiesAir = FindInScene(scene, EnemiesAirName);
        Transform enemiesLand = FindInScene(scene, EnemiesLandName);
        Transform collapsibles = FindInScene(scene, CollapsiblesName);

        MoveTargets(enemiesAir, level1, 2, "balloon", log);
        MoveTargets(enemiesLand, level2, 2, "tank", log);
        MoveTargets(collapsibles, level3, 3, "house", log);

        so.Update();
        var levelsProp = so.FindProperty("levels");
        if (levelsProp.arraySize < 3) levelsProp.arraySize = 3; // never shrink: MissionLowLevelsBuilder adds levels 4+
        levelsProp.GetArrayElementAtIndex(0).objectReferenceValue = level1.gameObject;
        levelsProp.GetArrayElementAtIndex(1).objectReferenceValue = level2.gameObject;
        levelsProp.GetArrayElementAtIndex(2).objectReferenceValue = level3.gameObject;

        // Each level only cares about ONE target type — explicitly zero the other two per level so a
        // stray leftover value can't make a level's UI show e.g. "0/2 Tanks" on the balloons level.
        var reqMode2Prop = so.FindProperty("destroyRequirementsMode2");
        if (reqMode2Prop.arraySize < 3) reqMode2Prop.arraySize = 3;
        SetLevelRequirement(reqMode2Prop.GetArrayElementAtIndex(0), houses: 0, tanks: 0, balloons: 2, log);
        SetLevelRequirement(reqMode2Prop.GetArrayElementAtIndex(1), houses: 0, tanks: 2, balloons: 0, log);
        SetLevelRequirement(reqMode2Prop.GetArrayElementAtIndex(2), houses: 3, tanks: 0, balloons: 0, log);

        so.ApplyModifiedProperties();

        // ---- Verify every target object under Level1/2/3 will actually receive damage ----
        VerifyTargets(level1, "balloon", log);
        VerifyTargets(level2, "tank", log);
        VerifyTargets(level3, "house", log);

        EditorSceneManager.MarkSceneDirty(scene);
        Flush(log);
    }

    static void LogArray(SerializedObject so, string propName, StringBuilder log)
    {
        var prop = so.FindProperty(propName);
        log.AppendLine($"{propName}[] size={prop.arraySize}");
        for (int i = 0; i < prop.arraySize; i++)
        {
            var el = prop.GetArrayElementAtIndex(i);
            log.AppendLine($"  [{i}] = {(el.objectReferenceValue != null ? el.objectReferenceValue.name : "NULL")}");
        }
    }

    static void LogRequirements(SerializedObject so, StringBuilder log)
    {
        var mode2Prop = so.FindProperty("destroyRequirementsMode2");
        if (mode2Prop == null)
        {
            log.AppendLine("destroyRequirementsMode2[] NOT FOUND on this component.");
            return;
        }
        log.AppendLine($"destroyRequirementsMode2[] size={mode2Prop.arraySize}");
        for (int i = 0; i < mode2Prop.arraySize; i++)
        {
            var el = mode2Prop.GetArrayElementAtIndex(i);
            int planesT = el.FindPropertyRelative("planesT").intValue;
            int generatorsT = el.FindPropertyRelative("generatorsT").intValue;
            int housesT = el.FindPropertyRelative("housesT").intValue;
            int tanksT = el.FindPropertyRelative("tanksT").intValue;
            int balloonsT = el.FindPropertyRelative("balloonsT").intValue;
            log.AppendLine($"  [{i}] planes={planesT} generators={generatorsT} houses={housesT} tanks={tanksT} balloons={balloonsT}");
        }
    }

    static void LogTextRef(SerializedObject so, string fieldName, StringBuilder log)
    {
        var prop = so.FindProperty(fieldName);
        log.AppendLine($"{fieldName} = {(prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "NULL")}");
    }

    static void AutoAssignText(SerializedObject so, string fieldName, string gameObjectName, StringBuilder log)
    {
        var prop = so.FindProperty(fieldName);
        if (prop.objectReferenceValue != null) return;

        Scene scene = ((Component)so.targetObject).gameObject.scene;
        Transform t = FindInScene(scene, gameObjectName);
        if (t == null)
        {
            log.AppendLine($"WARNING: Couldn't auto-assign {fieldName} — no GameObject named \"{gameObjectName}\" found.");
            return;
        }
        Text text = t.GetComponent<Text>();
        if (text == null)
        {
            log.AppendLine($"WARNING: \"{gameObjectName}\" has no Text component; couldn't assign {fieldName}.");
            return;
        }
        prop.objectReferenceValue = text;
        log.AppendLine($"FIXED: {fieldName} was unassigned — wired it to \"{gameObjectName}\".");
    }

    static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
        return go.transform;
    }

    static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = FindRecursive(root.transform, name);
            if (result != null) return result;
        }
        return null;
    }

    static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform result = FindRecursive(t.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    static void MoveTargets(Transform source, Transform dest, int count, string objName, StringBuilder log)
    {
        if (dest.childCount >= count)
        {
            log.AppendLine($"{dest.name} already has {dest.childCount} object(s) — re-verifying/re-wiring them instead of moving more in.");
            for (int i = 0; i < dest.childCount; i++)
            {
                WireDamage(dest.GetChild(i).gameObject, objName, log);
            }
            return;
        }

        if (source == null)
        {
            log.AppendLine($"WARNING: Couldn't find source group for \"{objName}\" targets; skipping {dest.name}.");
            return;
        }

        int needed = count - dest.childCount;
        int taken = 0;

        var candidates = new Transform[source.childCount];
        for (int i = 0; i < source.childCount; i++) candidates[i] = source.GetChild(i);

        foreach (Transform child in candidates)
        {
            if (taken >= needed) break;

            Undo.SetTransformParent(child, dest, $"Move {child.name} to {dest.name}");

            WireDamage(child.gameObject, objName, log);

            taken++;
        }

        if (taken < needed)
        {
            log.AppendLine($"WARNING: {source.name} only had {taken} spare object(s) to move into {dest.name}, needed {needed} more.");
        }
    }

    // Ensures the GameObject that actually owns the Collider has the DamageManager + Enemy tag,
    // since Damage.cs uses GameObject.SendMessage (not SendMessageUpwards) on the exact collider hit.
    static void WireDamage(GameObject root, string objName, StringBuilder log)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            log.AppendLine($"WARNING: \"{root.name}\" has no Collider anywhere in its hierarchy — weapons can never hit it.");
            // Still tag/wire the root so it's at least consistent if a collider gets added later.
            ApplyDamageManager(root, objName, log);
            return;
        }

        foreach (Collider col in colliders)
        {
            ApplyDamageManager(col.gameObject, objName, log);
        }
    }

    static void ApplyDamageManager(GameObject go, string objName, StringBuilder log)
    {
        if (go.tag != EnemyTag)
        {
            Undo.RecordObject(go, "Tag as Enemy");
            go.tag = EnemyTag;
        }

        DamageManager dm = go.GetComponent<DamageManager>();
        bool created = false;
        if (dm == null)
        {
            dm = Undo.AddComponent<DamageManager>(go);
            dm.HP = DefaultHP;
            created = true;
        }
        if (dm.objName != objName)
        {
            Undo.RecordObject(dm, "Set objName");
            dm.objName = objName;
        }

        log.AppendLine($"  {(created ? "ADDED" : "OK")} DamageManager on \"{go.name}\" (objName={dm.objName}, HP={dm.HP}, tag={go.tag}).");
    }

    static void VerifyTargets(Transform level, string expectedObjName, StringBuilder log)
    {
        log.AppendLine($"Verifying {level.name} (expect objName=\"{expectedObjName}\"):");
        for (int i = 0; i < level.childCount; i++)
        {
            GameObject child = level.GetChild(i).gameObject;
            DamageManager[] dms = child.GetComponentsInChildren<DamageManager>(true);
            if (dms.Length == 0)
            {
                log.AppendLine($"  MISSING DamageManager under \"{child.name}\".");
                continue;
            }
            foreach (var dm in dms)
            {
                bool hasCollider = dm.GetComponent<Collider>() != null;
                log.AppendLine($"  \"{dm.gameObject.name}\": objName=\"{dm.objName}\" tag={dm.gameObject.tag} hasColliderOnSameObject={hasCollider}" +
                                (dm.objName != expectedObjName ? "  <-- MISMATCH" : "") +
                                (!hasCollider ? "  <-- WILL NEVER TAKE DAMAGE (no Collider on same GameObject)" : ""));
            }
        }
    }

    static void ForceSet(SerializedProperty requirementElement, string fieldName, int value, StringBuilder log)
    {
        var prop = requirementElement.FindPropertyRelative(fieldName);
        if (prop.intValue != value)
        {
            log.AppendLine($"FIXED: {fieldName} was {prop.intValue}, setting to {value}.");
            prop.intValue = value;
        }
    }

    static void SetLevelRequirement(SerializedProperty requirementElement, int houses, int tanks, int balloons, StringBuilder log)
    {
        ForceSet(requirementElement, "housesT", houses, log);
        ForceSet(requirementElement, "tanksT", tanks, log);
        ForceSet(requirementElement, "balloonsT", balloons, log);
    }

    static void Flush(StringBuilder log)
    {
        string text = log.ToString();
        Debug.Log("[MissionLowLevelSetup]\n" + text);
        EditorUtility.DisplayDialog("Mission Low Setup Report", text, "OK");
    }
}
