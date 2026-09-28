using UnityEditor;
using UnityEngine;

// Edits made from code to objects inside a prefab instance (e.g. AdvancedAirplaneSystem in Mission Low)
// are only kept when saved if they're recorded as prefab overrides; otherwise the prefab's values win.
public static class PrefabOverrides
{
    public static void Record(params Object[] objects)
    {
        foreach (Object o in objects)
        {
            if (o == null) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(o))
                PrefabUtility.RecordPrefabInstancePropertyModifications(o);
            EditorUtility.SetDirty(o);
        }
    }
}
