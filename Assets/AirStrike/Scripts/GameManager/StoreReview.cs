using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Review;
#endif

// Native "rate this app" prompt (Google Play In-App Review / iOS SKStoreReviewController), requested
// when the win panel of a milestone level shows (level 2 in Mode 1 and Mode 2). On the first genuine
// attempt it takes the slot of the interstitial on the Next button.
//
// The stores rate-limit (Play: ~1 prompt per user per month) and never say whether a dialog was shown,
// so a request is only a request. In the Editor nothing is shown.
public class StoreReview : MonoBehaviour
{
    [Tooltip("1-based levels whose WIN asks for a review (each mode has its own milestones).")]
    public int[] reviewAfterLevels = { 2 };
    [Tooltip("Seconds (real time) after the win panel shows before asking.")]
    public float delaySeconds = 0.6f;

    [Tooltip("Mode 3 (Open World): ask before the fail panel of this run number (once).")]
    public int openWorldRunsBeforeReview = 2;
    [Tooltip("Longest wait (real seconds) for the review flow before the fail panel shows anyway.")]
    public float maxWaitBeforeFailPanel = 60f;

    const string KeyLastLevel = "StoreReview_LastLevel_Mode"; // + mode: highest milestone already asked
    const string KeyAdSkipUsed = "StoreReview_AdSkipUsed";     // the interstitial was skipped once already
    const string KeyOpenWorldRuns = "StoreReview_OpenWorldRuns";
    const string KeyOpenWorldAsked = "StoreReview_OpenWorldAsked";

    static StoreReview instance;
    static bool skipNextAd;
    bool requesting;

    static StoreReview Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<StoreReview>();
                if (instance == null) instance = new GameObject("StoreReview").AddComponent<StoreReview>();
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject); // the request can still be running when Next loads a scene
    }

    /// <summary>Call when a level is won (only wins: never ask after a fail). levelIndex is 0-based.</summary>
    public static void OnLevelWon(int mode, int levelIndex)
    {
        int level = levelIndex + 1;
        StoreReview review = Instance;
        if (review.requesting || !review.IsMilestone(level)) return;

        // Levels can be replayed: ask once per milestone, not every time it's won
        string key = KeyLastLevel + mode;
        if (PlayerPrefs.GetInt(key, 0) >= level) return;
        PlayerPrefs.SetInt(key, level);

        // Only the first genuine attempt takes the ad's place: a later request may be quota-blocked and
        // show nothing, and then the player would get neither
        if (PlayerPrefs.GetInt(KeyAdSkipUsed, 0) == 0)
        {
            PlayerPrefs.SetInt(KeyAdSkipUsed, 1);
            skipNextAd = true;
        }
        PlayerPrefs.Save();

        Debug.Log($"[StoreReview] Mode {mode} level {level} won: requesting review (ad skipped: {skipNextAd})");
        review.StartCoroutine(review.Request());
    }

    /// <summary>Mode 3: call when an Open World run starts.</summary>
    public static void CountOpenWorldRun()
    {
        PlayerPrefs.SetInt(KeyOpenWorldRuns, PlayerPrefs.GetInt(KeyOpenWorldRuns, 0) + 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Mode 3, run over: on the configured run (once ever) asks for a review and calls showFailPanel when
    /// the review flow is done. Otherwise calls showFailPanel right away.
    /// </summary>
    public static void BeforeOpenWorldFail(System.Action showFailPanel)
    {
        StoreReview review = Instance;
        bool ask = !review.requesting
                   && PlayerPrefs.GetInt(KeyOpenWorldAsked, 0) == 0
                   && PlayerPrefs.GetInt(KeyOpenWorldRuns, 0) >= review.openWorldRunsBeforeReview;
        if (!ask)
        {
            showFailPanel?.Invoke();
            return;
        }

        PlayerPrefs.SetInt(KeyOpenWorldAsked, 1);
        PlayerPrefs.Save();
        Debug.Log($"[StoreReview] Open World run {PlayerPrefs.GetInt(KeyOpenWorldRuns, 0)} over: requesting review before the fail panel");
        review.StartCoroutine(review.RequestThen(showFailPanel));
    }

    IEnumerator RequestThen(System.Action onDone)
    {
        StartCoroutine(Request());
        float waited = 0f;
        while (requesting && waited < maxWaitBeforeFailPanel) // the flow ends when the dialog closes
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (requesting) Debug.LogWarning("[StoreReview] Review flow still running: showing the fail panel anyway");
        onDone?.Invoke();
    }

    /// <summary>True once if the review took this win's interstitial slot (the Next button then shows no ad).</summary>
    public static bool ConsumeAdSkip()
    {
        bool skip = skipNextAd;
        skipNextAd = false;
        return skip;
    }

    bool IsMilestone(int level)
    {
        foreach (int l in reviewAfterLevels)
            if (l == level) return true;
        return false;
    }

    IEnumerator Request()
    {
        requesting = true;
        yield return new WaitForSecondsRealtime(delaySeconds); // the win panel is up with timeScale 0

#if UNITY_ANDROID && !UNITY_EDITOR
        // ReviewInfo expires: request and launch back to back
        var manager = new ReviewManager();
        var requestFlow = manager.RequestReviewFlow();
        yield return requestFlow;
        if (requestFlow.Error != ReviewErrorCode.NoError)
        {
            Debug.LogWarning("[StoreReview] RequestReviewFlow failed: " + requestFlow.Error);
            requesting = false;
            yield break;
        }

        var launchFlow = manager.LaunchReviewFlow(requestFlow.GetResult());
        yield return launchFlow;
        // NoError = the flow finished; Play may still have shown nothing (quota)
        Debug.Log("[StoreReview] LaunchReviewFlow finished: " + launchFlow.Error);
#elif UNITY_IOS && !UNITY_EDITOR
        UnityEngine.iOS.Device.RequestStoreReview();
#else
        Debug.Log("[StoreReview] (Editor) a review would be requested here.");
#endif
        requesting = false;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/AirStrike/Store Review/Reset Review State")]
    static void ResetState()
    {
        for (int mode = 1; mode <= 3; mode++) PlayerPrefs.DeleteKey(KeyLastLevel + mode);
        PlayerPrefs.DeleteKey(KeyAdSkipUsed);
        PlayerPrefs.DeleteKey(KeyOpenWorldRuns);
        PlayerPrefs.DeleteKey(KeyOpenWorldAsked);
        PlayerPrefs.Save();
        Debug.Log("[StoreReview] Review state cleared: milestones will ask again.");
    }
#endif
}
