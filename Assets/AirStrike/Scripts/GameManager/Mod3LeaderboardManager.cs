using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Database;
using UnityEngine;
using UnityEngine.UI;

// Mod 3 (Open World) leaderboard name submission, shown after the revive flow and before the fail panel.
//
//  First run (name not submitted yet): the name panel opens.
//    Submit  -> name trimmed + validated, saved locally (name, submitted flag, best score), then the
//               name + score are saved to the player's own Firebase record; panel closes; fail panel shows.
//    Not Now -> nothing saved or uploaded; the panel will be offered again next time.
//  Returning player (name already submitted): no panel. Only a score higher than the locally saved
//  best is saved and uploaded (with the saved name); equal or lower scores are not uploaded.
//
// Local data is always saved before uploading, and an upload failure never blocks the fail panel.
public class Mod3LeaderboardManager : MonoBehaviour
{
    public const string KeyNameSubmitted = "Mod3_LeaderboardNameSubmitted";
    public const string KeyPlayerName = "Mod3_LeaderboardPlayerName";
    public const string KeyBestScore = "Mod3_LeaderboardBestScore";
    public const string DatabasePath = "leaderboards/mod3";
    public const int MaxNameLength = 20;

    [Header("Name panel (Mod3LeaderboardNamePanel)")]
    public GameObject namePanel;
    public InputField playerNameInput;
    public Text validationText;
    public Text scoreText;
    public Button submitButton;
    public Button laterButton;

    public static bool NameSubmitted => PlayerPrefs.GetInt(KeyNameSubmitted, 0) == 1;
    public static string SavedName => PlayerPrefs.GetString(KeyPlayerName, "");
    public static int SavedBestScore => PlayerPrefs.GetInt(KeyBestScore, 0);

    public bool IsOpen => namePanel != null && namePanel.activeSelf;

    int pendingScore;
    Action onDone;

    void Awake()
    {
        if (namePanel != null) namePanel.SetActive(false);
        if (playerNameInput != null) playerNameInput.characterLimit = MaxNameLength;
    }

    /// <summary>
    /// Call when a Mod 3 run has ended (after the revive flow). Calls <paramref name="done"/> once the
    /// leaderboard step is finished, which is when the fail panel may be shown.
    /// </summary>
    public void HandleRunEnded(int finalScore, Action done)
    {
        pendingScore = finalScore;
        onDone = done;

        if (!NameSubmitted && namePanel != null)
        {
            OpenNamePanel();
            return;
        }

        // Returning player: upload only a new best, with the saved name
        if (NameSubmitted && finalScore > SavedBestScore)
        {
            PlayerPrefs.SetInt(KeyBestScore, finalScore);
            PlayerPrefs.Save();
            Upload(SavedName, finalScore);
        }
        Finish();
    }

    void OpenNamePanel()
    {
        if (validationText != null) validationText.text = "";
        if (scoreText != null) scoreText.text = "YOUR SCORE: " + pendingScore;
        if (playerNameInput != null)
        {
            playerNameInput.characterLimit = MaxNameLength;
            playerNameInput.text = SavedName; // prefill if a name was typed before but never submitted
        }
        namePanel.SetActive(true);
    }

    /// <summary>SubmitButton</summary>
    public void Submit()
    {
        if (!IsOpen) return;

        string raw = playerNameInput != null ? playerNameInput.text : "";
        string name = raw.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            ShowValidation("Please enter your name.");
            return;
        }
        if (name.Length == 0)
        {
            ShowValidation("Please enter a valid name.");
            return;
        }
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);

        // Save locally first
        PlayerPrefs.SetString(KeyPlayerName, name);
        PlayerPrefs.SetInt(KeyNameSubmitted, 1);
        PlayerPrefs.SetInt(KeyBestScore, pendingScore);
        PlayerPrefs.Save();

        Upload(name, pendingScore);
        namePanel.SetActive(false);
        Finish();
    }

    /// <summary>LaterButton ("Not Now"): nothing saved or uploaded; asked again next run.</summary>
    public void Later()
    {
        if (!IsOpen) return;
        namePanel.SetActive(false);
        Finish();
    }

    void ShowValidation(string message)
    {
        if (validationText != null) validationText.text = message;
    }

    void Finish()
    {
        Action done = onDone;
        onDone = null;
        done?.Invoke();
    }

    // ================================================================ Firebase (static: also used by the main menu)
    //
    // One record per player:  leaderboards/mod3/<playerId> = { playerId, playerName, score }
    // (no timestamp). Ties are broken by playerId so every load gives the same ranks.

    public const string KeyPlayerId = "Mod3_LeaderboardPlayerId";
    public const int TopCount = 100;
    const int TimeoutMs = 10000;

    public struct Entry
    {
        public string playerId;
        public string playerName;
        public long score;
    }

    /// <summary>Stable unique id for this player (created once, reused). Never the name.</summary>
    public static string PlayerId
    {
        get
        {
            string id = PlayerPrefs.GetString(KeyPlayerId, "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(KeyPlayerId, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    static Task<DependencyStatus> dependencyCheck;

    static async Task<DatabaseReference> Board()
    {
        if (dependencyCheck == null) dependencyCheck = FirebaseApp.CheckAndFixDependenciesAsync();
        DependencyStatus status = await dependencyCheck;
        if (status != DependencyStatus.Available)
        {
            dependencyCheck = null; // try again next time
            throw new Exception("Firebase not available: " + status);
        }
        return FirebaseDatabase.DefaultInstance.GetReference(DatabasePath);
    }

    // Offline, Firebase requests can wait forever: give up after a while so the UI can show an error
    static async Task<T> WithTimeout<T>(Task<T> task)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeoutMs)) != task) throw new TimeoutException("Leaderboard request timed out");
        return await task;
    }

    // Fire-and-forget: creates / updates this player's single record (callers only call it for a new best)
    static async void Upload(string playerName, int score)
    {
        try
        {
            DatabaseReference board = await Board();
            string id = PlayerId;
            var data = new Dictionary<string, object>
            {
                { "playerId", id },
                { "playerName", playerName },
                { "score", score },
            };
            await WithTimeout(board.Child(id).SetValueAsync(data).ContinueWith(t => { if (t.IsFaulted) throw t.Exception; return true; }));
            Debug.Log($"[Mod3Leaderboard] Saved {playerName}: {score} ({id})");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Mod3Leaderboard] Upload failed, score kept locally: {e.Message}");
        }
    }

    /// <summary>Global Top 100, highest score first (ties: playerId).</summary>
    public static async Task<List<Entry>> LoadTopAsync()
    {
        DatabaseReference board = await Board();
        DataSnapshot snap = await WithTimeout(board.OrderByChild("score").LimitToLast(TopCount).GetValueAsync());
        var list = new List<Entry>();
        foreach (DataSnapshot child in snap.Children)
            if (TryParse(child, out Entry e)) list.Add(e);
        list.Sort(Compare);
        return list;
    }

    /// <summary>This player's record from Firebase, or null if they don't have one.</summary>
    public static async Task<Entry?> LoadMineAsync()
    {
        DatabaseReference board = await Board();
        DataSnapshot snap = await WithTimeout(board.Child(PlayerId).GetValueAsync());
        return snap.Exists && TryParse(snap, out Entry e) ? e : (Entry?)null;
    }

    /// <summary>
    /// Global rank = players with a higher score + players with the same score and a smaller playerId + 1.
    /// Works outside the Top 100 too (reads only the records scoring at least as much as the player).
    /// </summary>
    public static async Task<int> LoadRankAsync(Entry me)
    {
        DatabaseReference board = await Board();
        DataSnapshot snap = await WithTimeout(board.OrderByChild("score").StartAt(me.score).GetValueAsync());
        int ahead = 0;
        foreach (DataSnapshot child in snap.Children)
        {
            if (!TryParse(child, out Entry e) || e.playerId == me.playerId) continue;
            if (Compare(e, me) < 0) ahead++;
        }
        return ahead + 1;
    }

    /// <summary>Sort order: score descending, then playerId ascending (deterministic ties).</summary>
    public static int Compare(Entry a, Entry b)
    {
        int byScore = b.score.CompareTo(a.score);
        return byScore != 0 ? byScore : string.CompareOrdinal(a.playerId, b.playerId);
    }

    static bool TryParse(DataSnapshot snap, out Entry entry)
    {
        entry = default;
        if (snap == null || !snap.Exists) return false;
        object score = snap.Child("score").Value;
        if (score == null) return false;
        try { entry.score = Convert.ToInt64(score); } catch { return false; }
        entry.playerId = snap.Child("playerId").Value as string ?? snap.Key;
        entry.playerName = snap.Child("playerName").Value as string ?? "Pilot";
        return true;
    }
}
