using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

// Firebase Analytics events for level results:
//   Mode_1_WinLevel_3, Mode_2_FailLevel_5, Mode_3_Start
// Levels are 1-based. Events logged before Firebase is ready are queued and sent once it is.
public static class GameAnalytics
{
    static bool ready;
    static bool checking;
    static readonly List<string> pending = new List<string>();

    public static void LevelWin(int mode, int levelIndex) => Log($"Mode_{mode}_WinLevel_{levelIndex + 1}");
    public static void LevelFail(int mode, int levelIndex) => Log($"Mode_{mode}_FailLevel_{levelIndex + 1}");
    public static void ModeStart(int mode) => Log($"Mode_{mode}_Start");

    public static void Log(string eventName)
    {
        Debug.Log("[GameAnalytics] " + eventName);
#if !UNITY_EDITOR
        if (ready)
        {
            FirebaseAnalytics.LogEvent(eventName);
            return;
        }
        pending.Add(eventName);
        Init();
#endif
    }

    static void Init()
    {
        if (checking) return;
        checking = true;
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            checking = false;
            if (task.IsFaulted || task.IsCanceled || task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning("[GameAnalytics] Firebase not available: " + (task.IsFaulted ? task.Exception?.Message : task.IsCanceled ? "canceled" : task.Result.ToString()));
                return; // events stay queued; the next Log tries again
            }

            ready = true;
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            foreach (string name in pending) FirebaseAnalytics.LogEvent(name);
            pending.Clear();
        });
    }
}
