using UnityEditor;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using PubScale.SdkOne;
using System.Text.RegularExpressions;


public class PubScaleBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private static object _compilationContext;
    private static int _compilationErrorCount;

    private static PubScaleSettings psSettings = null;


    static void LoadPubScaleSettings()
    {
        psSettings = AssetDatabase.LoadAssetAtPath<PubScaleSettings>(PubEditorUX.PackageSettingsPath);

        if (psSettings == null)
        {
            psSettings = ScriptableObject.CreateInstance<PubScaleSettings>();
            AssetDatabase.CreateAsset(psSettings, PubEditorUX.PackageSettingsPath);
        }

        PubScaleSettings.Instance = (PubScaleSettings)AssetDatabase.LoadAssetAtPath(PubEditorUX.PackageSettingsPath, typeof(PubScaleSettings));
    }



    public void OnPreprocessBuild(BuildReport report)
    {
        _compilationContext = null;
        _compilationErrorCount = 0;

        CompilationPipeline.compilationStarted += CompilationPipelineOnCompilationStarted;
        CompilationPipeline.assemblyCompilationFinished += CompilationPipelineOnAssemblyCompilationFinished;
        CompilationPipeline.compilationFinished += CompilationPipelineOnCompilationFinished;

        LoadPubScaleSettings();
        PerformPubScaleBuildChecks();
    }

    private static void CompilationPipelineOnCompilationStarted(object compilationContext)
    {
        _compilationContext = compilationContext;
        _compilationErrorCount = 0;
    }

    private static void CompilationPipelineOnAssemblyCompilationFinished(string path, CompilerMessage[] messages)
    {
        for (int i = messages?.Length ?? 0; --i >= 0;)
        {
            if (messages[i].type == CompilerMessageType.Error)
                ++_compilationErrorCount;
        }
    }

    private static void CompilationPipelineOnCompilationFinished(object compilationContext)
    {
        if (compilationContext != _compilationContext)
            return;

        _compilationContext = null;

        CompilationPipeline.compilationStarted -= CompilationPipelineOnCompilationStarted;
        CompilationPipeline.assemblyCompilationFinished -= CompilationPipelineOnAssemblyCompilationFinished;
        CompilationPipeline.compilationFinished -= CompilationPipelineOnCompilationFinished;

        if (_compilationErrorCount > 0)
        {
            Debug.LogError($"Compilation finished with errors ({_compilationErrorCount})");
            // Custom compilation failure processing
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (_compilationErrorCount > 0)
        {
            throw new BuildFailedException("Sorry");
        }
    }



    void PerformPubScaleBuildChecks()
    {
        int pubScaleErrors = 0;

        if (psSettings == null)
        {
            Debug.LogError("PubScale SDK settings not found");
            pubScaleErrors++;
        }
        else
        {

#if UNITY_ANDROID

            string appID = psSettings.GetAndroidAppID();

            if (string.IsNullOrEmpty(appID))
            {
                Debug.LogWarning($"PubScale Android App ID is Empty. Please assign your SDK App ID in the PubScale Window.");
                pubScaleErrors++;
            }
            else if (IsNumeric(appID) == false)
            {
                Debug.LogWarning($"PubScale Android App ID is expected to have only numbers and no characters. Please recheck PubScale App ID from Dashboard");
                pubScaleErrors++;
            }


            if (psSettings.UseTestMode == false)
            {
                if (string.IsNullOrEmpty(psSettings.Fallback_NativeAdID_Android))
                {
                    Debug.LogWarning($"PUBSCALE: Please provide a Default Native ID in PubScale Settings Window for Live Ads");
                    pubScaleErrors++;
                }
                else if (IsValidAdUnitId(psSettings.Fallback_NativeAdID_Android) == false)
                {
                    Debug.LogWarning($"PUBSCALE: Invalid Default Native ID format detected in PubScale Settings Window. Please enter a valid Admob Native AD Unit ID");
                    pubScaleErrors++;
                }
            }
#endif

#if UNITY_IOS

            string appID = psSettings.GetIOSAppID();

            if (string.IsNullOrEmpty(appID))
            {
                Debug.LogWarning($"PubScale IOS App ID is Empty. Please assign your SDK App ID in the PubScale Window.");
                pubScaleErrors++;
            }
            else if (IsNumeric(appID) == false)
            {
                Debug.LogWarning($"PubScale IOS App ID is expected to have only numbers and no characters. Please recheck PubScale App ID from Dashboard");
                pubScaleErrors++;
            }

            if (psSettings.UseTestMode == false)
            {
                if (string.IsNullOrEmpty(psSettings.Fallback_NativeAdID_IOS))
                {
                    Debug.LogWarning($"PUBSCALE: Please provide a Default Native ID in PubScale Settings Window for Live Ads");
                    pubScaleErrors++;
                }
                else if (IsValidAdUnitId(psSettings.Fallback_NativeAdID_IOS) == false)
                {
                    Debug.LogWarning($"PUBSCALE: Invalid Default Native ID format detected in PubScale Settings Window. Please enter a valid Admob Native AD Unit ID");
                    pubScaleErrors++;
                }
            }
#endif

        }

        if (pubScaleErrors > 0)
        {
            throw new BuildFailedException("PubScaleSDK has detected some build issues. Please check Warning messages in Console.");
        }


    }


    bool IsNumeric(string input)
    {
        // IsNumeric
        foreach (char c in input)
        {
            if (char.IsDigit(c) == false)
            {
                return false; // Return false if any character is not a digit
            }
        }

        return true; // All characters are digits
    }


    private readonly Regex adUnitIdPattern = new Regex(@"^ca-app-pub-\d{16}/\d{10}$");

    bool IsValidAdUnitId(string adUnitId)
    {
        // if (string.IsNullOrEmpty(adUnitId))
        // {
        //     return false;
        // }

        // // Check if the ad unit ID matches the pattern
        // return adUnitIdPattern.IsMatch(adUnitId);

        return true;
    }



}
