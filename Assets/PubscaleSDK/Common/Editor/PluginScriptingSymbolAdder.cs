using UnityEditor;
using UnityEngine;

public class PluginScriptingSymbolAdder : AssetPostprocessor
{
    // Define the folder to detect and the scripting symbols to add
    private static readonly string PluginFolder = "Assets/PubscaleSDK"; // Replace with your plugin folder
    private static readonly string[] ScriptingSymbols = { "VUPLEX_WEBVIEW", "VUPLEX_ANDROID_DISABLE_AUTOMATIC_PAUSING" }; // Replace with your symbols

    // This method is called whenever assets are imported, deleted, or moved
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            if (assetPath.StartsWith(PluginFolder))
            {
                AddScriptingSymbols();
                break;
            }
        }
    }

    private static void AddScriptingSymbols()
    {
        // Iterate over all BuildTargetGroups
        foreach (BuildTargetGroup buildTargetGroup in System.Enum.GetValues(typeof(BuildTargetGroup)))
        {
            // Skip invalid or obsolete groups
            if (buildTargetGroup == BuildTargetGroup.Unknown || IsObsolete(buildTargetGroup))
                continue;

            // Get current scripting define symbols
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            // Add the new scripting symbols if not already present
            foreach (string symbol in ScriptingSymbols)
            {
                if (!defines.Contains(symbol))
                {
                    defines = string.IsNullOrEmpty(defines) ? symbol : defines + ";" + symbol;
                }
            }

            // Update the scripting define symbols
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
        }
    }

    private static bool IsObsolete(BuildTargetGroup buildTargetGroup)
    {
        // Check for obsolete BuildTargetGroups using reflection
        var attributes = typeof(BuildTargetGroup).GetField(buildTargetGroup.ToString()).GetCustomAttributes(typeof(System.ObsoleteAttribute), false);
        return attributes.Length > 0;
    }
}
