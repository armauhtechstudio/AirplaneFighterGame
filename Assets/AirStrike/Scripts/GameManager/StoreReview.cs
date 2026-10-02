using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Review;
#endif

// "Rate us" flow, at the win panel of a milestone level (level 2 in Mode 1 and Mode 2) and before the
// fail panel of the 2nd Open World run: the RateUsPanel (5 stars) shows first. 4-5 stars -> the native
// store review (Google Play In-App Review / iOS SKStoreReviewController) opens; 1-3 stars -> thanks,
// the game continues. Once the player has rated (any number of stars) they are never asked again.
// On the first genuine attempt it takes the slot of the interstitial on the Next button.
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
    const string KeyRated = "StoreReview_Rated";           // the player picked a star rating: never ask again
    const string KeyRating = "StoreReview_Rating";         // the stars they gave (1-5)

    /// <summary>The player has rated in the Rate Us panel (any number of stars).</summary>
    public static bool HasRated => PlayerPrefs.GetInt(KeyRated, 0) == 1;

    static StoreReview instance;
    static bool skipNextAd;
    bool requesting;      // a rate-us flow is running
    bool nativeRunning;   // the store review flow is running

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
        if (HasRated || review.requesting || !review.IsMilestone(level)) return;

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

        Debug.Log($"[StoreReview] Mode {mode} level {level} won: asking for a rating (ad skipped: {skipNextAd})");
        review.StartCoroutine(review.AskRating(null));
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
        bool ask = !HasRated && !review.requesting
                   && PlayerPrefs.GetInt(KeyOpenWorldAsked, 0) == 0
                   && PlayerPrefs.GetInt(KeyOpenWorldRuns, 0) >= review.openWorldRunsBeforeReview;
        if (!ask)
        {
            showFailPanel?.Invoke();
            return;
        }

        PlayerPrefs.SetInt(KeyOpenWorldAsked, 1);
        PlayerPrefs.Save();
        Debug.Log($"[StoreReview] Open World run {PlayerPrefs.GetInt(KeyOpenWorldRuns, 0)} over: asking for a rating before the fail panel");
        review.StartCoroutine(review.AskRating(showFailPanel));
    }

    // Rate Us panel -> (4-5 stars) store review; onDone runs once the panel is closed and the store review
    // (if any) has finished
    IEnumerator AskRating(System.Action onDone)
    {
        requesting = true;
        yield return new WaitForSecondsRealtime(delaySeconds); // let the win / fail moment land first

        bool panelClosed = false;
        bool shown = RateUsPanel.Show(
            onRated: stars =>
            {
                PlayerPrefs.SetInt(KeyRated, 1);
                PlayerPrefs.SetInt(KeyRating, stars);
                PlayerPrefs.Save();
                Debug.Log($"[StoreReview] Rated {stars} star(s)" + (stars >= 4 ? ": opening the store review" : ""));
                if (stars >= 4) StartCoroutine(NativeReview()); // while the stars fill
            },
            onClosed: () => panelClosed = true);

        if (!shown) // no Rate Us prefab: straight to the store review, as before
        {
            Debug.LogWarning("[StoreReview] Resources/" + RateUsPanel.ResourcePath + " missing: opening the store review directly");
            StartCoroutine(NativeReview());
            panelClosed = true;
        }

        while (!panelClosed) yield return null;

        float waited = 0f;
        while (nativeRunning && waited < maxWaitBeforeFailPanel) // the store flow ends when its dialog closes
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (nativeRunning) Debug.LogWarning("[StoreReview] Store review still running: continuing anyway");

        requesting = false;
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

    // Native store review (Google Play In-App Review / iOS)
    IEnumerator NativeReview()
    {
        nativeRunning = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        // ReviewInfo expires: request and launch back to back
        var manager = new ReviewManager();
        var requestFlow = manager.RequestReviewFlow();
        yield return requestFlow;
        if (requestFlow.Error != ReviewErrorCode.NoError)
        {
            Debug.LogWarning("[StoreReview] RequestReviewFlow failed: " + requestFlow.Error);
            nativeRunning = false;
            yield break;
        }

        var launchFlow = manager.LaunchReviewFlow(requestFlow.GetResult());
        yield return launchFlow;
        // NoError = the flow finished; Play may still have shown nothing (quota)
        Debug.Log("[StoreReview] LaunchReviewFlow finished: " + launchFlow.Error);
#elif UNITY_IOS && !UNITY_EDITOR
        UnityEngine.iOS.Device.RequestStoreReview();
        yield return null;
#else
        Debug.Log("[StoreReview] (Editor) a review would be requested here.");
        yield return null;
#endif
        nativeRunning = false;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/AirStrike/Store Review/Reset Review State")]
    static void ResetState()
    {
        for (int mode = 1; mode <= 3; mode++) PlayerPrefs.DeleteKey(KeyLastLevel + mode);
        PlayerPrefs.DeleteKey(KeyAdSkipUsed);
        PlayerPrefs.DeleteKey(KeyOpenWorldRuns);
        PlayerPrefs.DeleteKey(KeyOpenWorldAsked);
        PlayerPrefs.DeleteKey(KeyRated);
        PlayerPrefs.DeleteKey(KeyRating);
        PlayerPrefs.Save();
        Debug.Log("[StoreReview] Review state cleared: milestones will ask again.");
    }
#endif
}
