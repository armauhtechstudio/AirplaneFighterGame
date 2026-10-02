using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

/// <summary>
/// Local re-engagement notifications (no server): while the player is away, a reminder every
/// IntervalHours (20 h, 40 h, 60 h ...) with a random plane-themed message.
///
/// Cancel-all-and-reschedule: leaving the game (pause / quit) wipes everything pending and lays a fresh
/// ladder from that moment; coming back wipes it again, so a returning player never gets a stale nudge
/// and the 20 h count always runs from the last session. Wall-clock times, no quiet hours (a reminder
/// can arrive at night) — deliberate, so the cadence holds.
/// Starts by itself. Permission (Android 13+ / iOS) is asked from the first level win:
/// GameNotifications.RequestPermissionIfNeeded().
/// Settings + small icon: Tools/AirStrike/Notifications/Setup.
/// </summary>
public class GameNotifications : MonoBehaviour
{
    public const double IntervalHours = 20;   // a reminder every 20 h while away
    public const int Slots = 25;                // 7 x 20 h = ~6 days
    public const string ChannelId = "reminders";
    public const string SmallIconId = "icon_small";

    public struct Reminder
    {
        public DateTime fireTime;
        public string title;
        public string body;
    }

    static readonly string[,] Messages =
    {
        { "Your jet is fueled up!", "The skies are waiting, Ace. Come back and fly!" },
        { "Enemy planes spotted!", "Your squadron needs you. Take off and shoot them down!" },
        { "Mission briefing ready", "New targets are waiting. Ready for takeoff?" },
        { "Pilot, report to base!", "Your hangar misses you. Jump back in the cockpit!" },
        { "Rockets are loaded", "Time to blast some balloons, tanks and buildings!" },
        { "Beat your high score!", "Open World is waiting. Can you top the leaderboard?" },
        { "The skies need a hero", "Enemy forces are advancing. Fly now!" },
        { "Engines warmed up", "One more mission, Ace? Your plane is ready." },
        { "Dogfight time!", "Hop in and show them who rules the sky." },
        { "Ace Aircraft Heroes", "Your daily patrol is ready. Earn your wings today!" },
    };

    static GameNotifications instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("GameNotifications");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameNotifications>();
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(
            ChannelId, "Reminders", "Come-back reminders", Importance.Default));
#endif
        CancelAll(); // playing now: nothing pending
    }

    void OnApplicationPause(bool paused)
    {
        // Full-screen ads also background the app: same cancel + reschedule, harmless
        if (paused) { CancelAll(); ScheduleAll(DateTime.Now); }
        else CancelAll();
    }

    void OnApplicationQuit()
    {
        CancelAll();
        ScheduleAll(DateTime.Now); // Android often skips this; OnApplicationPause(true) is the reliable one
    }

    // ---------------------------------------------------------------- schedule (pure: testable in the Editor)

    /// <summary>The reminders to lay down when the player leaves at 'now': one every IntervalHours, random
    /// messages, no message twice in a row (and none repeated until all have been used).</summary>
    public static List<Reminder> BuildSchedule(DateTime now, System.Random random)
    {
        var list = new List<Reminder>();
        int count = Messages.GetLength(0);
        var deck = new List<int>();
        int last = -1;
        for (int slot = 1; slot <= Slots; slot++)
        {
            if (deck.Count == 0)
            {
                for (int i = 0; i < count; i++) if (i != last) deck.Add(i);
            }
            int pick = deck[random.Next(deck.Count)];
            deck.Remove(pick);
            last = pick;
            list.Add(new Reminder
            {
                fireTime = now.AddHours(IntervalHours * slot),
                title = Messages[pick, 0],
                body = Messages[pick, 1],
            });
        }
        return list;
    }

    static void ScheduleAll(DateTime now)
    {
        List<Reminder> reminders = BuildSchedule(now, new System.Random());
#if UNITY_ANDROID && !UNITY_EDITOR
        foreach (Reminder r in reminders)
        {
            var n = new AndroidNotification(r.title, r.body, r.fireTime) { SmallIcon = SmallIconId };
            AndroidNotificationCenter.SendNotification(n, ChannelId);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        foreach (Reminder r in reminders)
        {
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Title = r.title,
                Body = r.body,
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = r.fireTime - now, Repeats = false },
            });
        }
#endif
        Debug.Log($"[Notifications] Scheduled {reminders.Count} reminders, first at {reminders[0].fireTime:g}: \"{reminders[0].title}\"");
    }

    static void CancelAll()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS && !UNITY_EDITOR
        iOSNotificationCenter.RemoveAllScheduledNotifications();
        iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif
    }

    // ---------------------------------------------------------------- permission

    /// <summary>
    /// Asks for permission to post notifications where the OS needs it (Android 13+, iOS). Call it at a
    /// good moment (a level win), not on a cold launch. No local "already asked" flag on purpose: the OS
    /// tracks the real state and the request does nothing once it's decided.
    /// </summary>
    public static void RequestPermissionIfNeeded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed)
            new PermissionRequest();
#elif UNITY_IOS && !UNITY_EDITOR
        if (instance != null) instance.StartCoroutine(RequestIOS());
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    static IEnumerator RequestIOS()
    {
        using (var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false))
            while (!request.IsFinished) yield return null;
    }
#endif
}
