using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Main menu Mod 3 leaderboard.
//  Leaderboard button -> name never submitted: registration panel ("Play Mod 3" / "Close")
//                     -> name submitted:       leaderboard panel, loaded from Firebase
//  Leaderboard panel: Top 100 (highest first) in a scroll view, the player's row highlighted with a
//  "YOU" tag, and a "Your Rank" section that also works when the player is outside the Top 100.
//  Loading / empty / error (Retry) states; Firebase problems never crash the menu.
public class Mod3LeaderboardUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject registrationPanel;        // Mod3LeaderboardRegistrationPanel
    public GameObject leaderboardPanel;         // Mod3LeaderboardPanel
    public GameObject loadingPanel;             // LeaderboardLoadingPanel
    public GameObject emptyState;               // "No scores yet..."
    public GameObject errorPanel;               // "Leaderboard Unavailable" + Retry / Close
    public CanvasGroup leaderboardInteraction;  // disabled while loading

    [Header("List")]
    public ScrollRect scrollView;               // LeaderboardScrollView
    public RectTransform content;
    public Mod3LeaderboardEntry entryPrefab;    // Mod3LeaderboardEntry

    [Header("Your Rank")]
    public GameObject currentPlayerSection;     // CurrentPlayerRankSection
    public Text yourRankText, yourNameText, yourScoreText;

    [Header("Main menu")]
    public MainMenuController mainMenu;
    public int mod3Index = 3;

    readonly List<Mod3LeaderboardEntry> rows = new List<Mod3LeaderboardEntry>();
    int loadVersion;

    void Awake()
    {
        HideAll();
    }

    /// <summary>Mod3LeaderboardButton</summary>
    public void OpenMod3Leaderboard()
    {
        HideAll();
        if (!Mod3LeaderboardManager.NameSubmitted)
        {
            if (registrationPanel != null) registrationPanel.SetActive(true);
            return;
        }
        if (leaderboardPanel != null) leaderboardPanel.SetActive(true);
        ReloadLeaderboard();
    }

    /// <summary>CloseButton / CloseLeaderboardButton / error Close</summary>
    public void ClosePanel()
    {
        loadVersion++; // ignore any request still in flight
        HideAll();
    }

    /// <summary>PlayMod3Button</summary>
    public void CloseLeaderboardPanelAndStartMod3()
    {
        ClosePanel();
        if (mainMenu != null) mainMenu.SelectMode(mod3Index);
    }

    /// <summary>Retry</summary>
    public async void ReloadLeaderboard()
    {
        int version = ++loadVersion;
        SetLoading(true);
        if (errorPanel != null) errorPanel.SetActive(false);
        if (emptyState != null) emptyState.SetActive(false);
        if (currentPlayerSection != null) currentPlayerSection.SetActive(false);

        List<Mod3LeaderboardManager.Entry> top;
        Mod3LeaderboardManager.Entry? mine = null;
        int rank = -1;
        try
        {
            top = await Mod3LeaderboardManager.LoadTopAsync();
            mine = await Mod3LeaderboardManager.LoadMineAsync();
            if (mine.HasValue)
            {
                int index = top.FindIndex(e => e.playerId == mine.Value.playerId);
                rank = index >= 0 ? index + 1 : await Mod3LeaderboardManager.LoadRankAsync(mine.Value);
            }
        }
        catch (Exception e)
        {
            if (version != loadVersion) return;
            Debug.LogWarning("[Mod3LeaderboardUI] Load failed: " + e.Message);
            SetLoading(false);
            ShowRows(null, null);
            if (errorPanel != null) errorPanel.SetActive(true);
            return;
        }
        if (version != loadVersion || this == null) return; // closed while loading

        SetLoading(false);
        ShowLeaderboard(top, mine, rank);
    }

    /// <summary>Fills the list and the Your Rank section (public so it can be previewed with test data).</summary>
    public void ShowLeaderboard(List<Mod3LeaderboardManager.Entry> top, Mod3LeaderboardManager.Entry? mine, int rank)
    {
        string myId = Mod3LeaderboardManager.PlayerId;
        ShowRows(top, myId);
        if (emptyState != null) emptyState.SetActive(top == null || top.Count == 0);

        // Your Rank: Firebase record if found, else the locally saved name / best (rank unknown -> "--",
        // never 0 or -1)
        string name = mine.HasValue ? mine.Value.playerName : Mod3LeaderboardManager.SavedName;
        long score = mine.HasValue ? mine.Value.score : Mod3LeaderboardManager.SavedBestScore;
        if (currentPlayerSection != null) currentPlayerSection.SetActive(true);
        if (yourRankText != null) yourRankText.text = rank > 0 ? "#" + rank : "#--";
        if (yourNameText != null) yourNameText.text = string.IsNullOrEmpty(name) ? "You" : name;
        if (yourScoreText != null) yourScoreText.text = score.ToString("N0");

        // Start at the top. The rows were just created, so lay them out first, and repeat next frame
        // in case the panel's intro animation / layout was still settling.
        if (scrollView != null && isActiveAndEnabled) StartCoroutine(ScrollToTop());
    }

    System.Collections.IEnumerator ScrollToTop()
    {
        for (int i = 0; i < 2; i++)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scrollView.StopMovement();
            scrollView.verticalNormalizedPosition = 1f;
            yield return null;
        }
    }

    void ShowRows(List<Mod3LeaderboardManager.Entry> top, string myId)
    {
        int count = top == null ? 0 : Mathf.Min(top.Count, Mod3LeaderboardManager.TopCount);
        while (rows.Count < count) rows.Add(Instantiate(entryPrefab, content));
        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;
            rows[i].gameObject.SetActive(used);
            if (used) rows[i].Set(i + 1, top[i].playerName, top[i].score, top[i].playerId == myId);
        }
    }

    void SetLoading(bool loading)
    {
        if (loadingPanel != null) loadingPanel.SetActive(loading);
        if (leaderboardInteraction != null)
        {
            leaderboardInteraction.interactable = !loading;
            leaderboardInteraction.blocksRaycasts = !loading;
        }
    }

    void HideAll()
    {
        if (registrationPanel != null) registrationPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);
    }
}
