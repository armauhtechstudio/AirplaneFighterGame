using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Main Menu Controller
///
/// HOW TO SET UP IN THE INSPECTOR:
/// ----------------------------------------------------------
/// 1. Attach this script to a GameObject in your Main Menu scene.
///
/// 2. Mod Panels:
///    - modSelectionPanel   : The root panel showing Mod 1 / Mod 2 / Mod 3 buttons.
///    - mod1LevelPanel      : The level-selection panel for Mod 1.
///    - mod2LevelPanel      : The level-selection panel for Mod 2.
///
/// 3. Mod 1 Levels (mod1Levels array):
///    - Set Size to the number of levels in Mod 1.
///    - For each element, assign:
///        levelButton : The Button GameObject for that level.
///    - The lock Image is automatically found as the FIRST CHILD of the button GameObject.
///      Make sure the lock Image child exists on every button.
///
/// 4. Mod 2 Levels (mod2Levels array): same structure as Mod 1.
///
/// 5. Scene Names:
///    - mod1SceneName : Gameplay scene for Mod 1 (e.g. "Classic").
///    - mod2SceneName : Gameplay scene for Mod 2.
///    - mod3SceneName : Scene to load directly for Mod 3.
///
/// 6. PlayerPrefs Keys used internally:
///    "SelectedMod"         -> stores the chosen mod index (1 / 2 / 3)
///    "Mod1_SelectedLevel"  -> stores the chosen level index for Mod 1
///    "Mod2_SelectedLevel"  -> stores the chosen level index for Mod 2
///    "Mod1_UnlockedLevels" -> how many Mod 1 levels are unlocked (starts at 1)
///    "Mod2_UnlockedLevels" -> how many Mod 2 levels are unlocked (starts at 1)
/// ----------------------------------------------------------
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // Singleton – lets GameManager call MainMenuController.Instance.UnlockMod1Level()
    public static MainMenuController Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------

    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject modSelectionPanel;
    public GameObject mod1LevelPanel;
    public GameObject mod2LevelPanel;

    [Header("Mod 1 Levels")]
    public LevelEntry[] mod1Levels;

    [Header("Mod 2 Levels")]
    public LevelEntry[] mod2Levels;

    [Header("Scene Names")]
    [Tooltip("Gameplay scene for Mod 1 (e.g. \"Classic\").")]
    public string mod1SceneName = "Classic";

    [Tooltip("Gameplay scene for Mod 2.")]
    public string mod2SceneName = "Mod2Scene";          // <-- replace with your scene name

    [Tooltip("Scene to load directly when Mod 3 is selected.")]
    public string mod3SceneName = "Mod3Scene";          // <-- replace with your scene name

    [Header("Logo")]
    [Tooltip("Optional: the menu Logo. Gets a PunchyLogo component added automatically for a punchy pop-in + idle kick.")]
    public RectTransform logo;

    [Header("Remove Ads")]
    [Tooltip("The REMOVE ADS button: hidden once ads are removed.")]
    public GameObject removeAdsButton;
    [Tooltip("The REMOVE ADS offer popup (shown on the mode selection): closed once ads are removed.")]
    public RemoveAdsOfferPanel removeAdsOffer;

    [Header("Loading Screen")]
    public GameObject loadingPanel;
    public Image loadingFillImage;
    public float loadingDuration = 6f;
    [Tooltip("How many discrete 'shots' fill the bar, instead of one smooth sweep.")]
    public int loadingTicks = 12;
    public Color loadingTickFlashColor = Color.white;
    Color loadingBarBaseColor = Color.white;
    bool isLoading = false;

    // -------------------------------------------------------------------------
    // PlayerPrefs Keys (private constants shared with static helpers)
    // -------------------------------------------------------------------------

    private const string KEY_SELECTED_MOD        = "SelectedMod";
    private const string KEY_MOD1_SELECTED_LEVEL = "Mod1_SelectedLevel";
    private const string KEY_MOD2_SELECTED_LEVEL = "Mod2_SelectedLevel";
    private const string KEY_MOD1_UNLOCKED       = "Mod1_UnlockedLevels";
    private const string KEY_MOD2_UNLOCKED       = "Mod2_UnlockedLevels";

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
    }

    // -------------------------------------------------------------------------
    // Remove Ads
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call when the Remove Ads purchase succeeds: saves RemoveAds = 1, takes down the banners / MREC and
    /// hides the REMOVE ADS button. From then on only rewarded ads (the player's choice) are shown.
    /// Not wired to anything: call it from your purchase code.
    /// </summary>
    public void RemoveAdsFromGame()
    {
        if (AdsManager.Instance != null)
            AdsManager.Instance.RemoveAdsNow(); // saves the flag + destroys banners, hides the MREC
        else
        {
            PlayerPrefs.SetInt(AdsManager.RemoveAdsKey, 1);
            PlayerPrefs.Save();
        }

        if (removeAdsButton != null) removeAdsButton.SetActive(false);
        if (removeAdsOffer != null) removeAdsOffer.Close();
        Debug.Log("[MainMenuController] Ads removed.");
    }

    private void Start()
    {
        // Already bought: no Remove Ads button
        if (removeAdsButton != null && AdsManager.AdsRemoved) removeAdsButton.SetActive(false);

        // Ensure level 1 is always unlocked on first run
        if (!PlayerPrefs.HasKey(KEY_MOD1_UNLOCKED))
            PlayerPrefs.SetInt(KEY_MOD1_UNLOCKED, 1);

        if (!PlayerPrefs.HasKey(KEY_MOD2_UNLOCKED))
            PlayerPrefs.SetInt(KEY_MOD2_UNLOCKED, 1);

        PlayerPrefs.Save();

        // Wire Mod 1 level buttons dynamically so you only need to assign the
        // Button reference in the Inspector without adding onClick listeners manually.
        for (int i = 0; i < mod1Levels.Length; i++)
        {
            int idx = i; // capture for closure
            mod1Levels[i].levelButton.onClick.AddListener(() => OnMod1LevelSelected(idx));
        }

        // Wire Mod 2 level buttons
        for (int i = 0; i < mod2Levels.Length; i++)
        {
            int idx = i;
            mod2Levels[i].levelButton.onClick.AddListener(() => OnMod2LevelSelected(idx));
        }

        if (loadingFillImage != null)
            loadingBarBaseColor = loadingFillImage.color;

        if (logo != null && logo.GetComponent<PunchyLogo>() == null)
        {
            logo.gameObject.AddComponent<PunchyLogo>();
        }
    }

    // -------------------------------------------------------------------------
    // Mod Selection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Hook this up to your Mod 1, Mod 2, and Mod 3 UI buttons.
    /// Pass modIndex = 1, 2, or 3.
    /// </summary>
    public void SelectMode(int modIndex)
    {
        // Validate
        if (modIndex < 1 || modIndex > 3)
        {
            Debug.LogWarning($"[MainMenuController] SelectMod called with invalid index: {modIndex}");
            return;
        }

        ShowInterstitialIf(c => c.isInterMode);

        // Persist the selection
        PlayerPrefs.SetInt(KEY_SELECTED_MOD, modIndex);
        PlayerPrefs.Save();

        Debug.Log($"[MainMenuController] Mod {modIndex} selected.");

        StartCoroutine(ShowLoadingScreenRoutine(() => {
            switch (modIndex)
            {
                case 1: OpenMod1LevelSelection(); break;
                case 2: OpenMod2LevelSelection(); break;
                case 3: LoadMod3Scene();          break;
            }
        }, loadsScene: modIndex == 3));
    }

    // -------------------------------------------------------------------------
    // Mod 1 – Level Selection
    // -------------------------------------------------------------------------

    private void OpenMod1LevelSelection()
    {
        menuPanel.SetActive(false);
        modSelectionPanel.SetActive(false);
        mod2LevelPanel.SetActive(false);
        mod1LevelPanel.SetActive(true);
        RefreshMod1LevelUI();
    }

    /// <summary>
    /// Refreshes lock/unlock images and button interactability for Mod 1 levels.
    /// Call this whenever the Mod 1 level panel becomes visible.
    /// </summary>
    private void RefreshMod1LevelUI()
    {
        // Number of levels that are currently unlocked (default: only level 1)
        int unlocked = PlayerPrefs.GetInt(KEY_MOD1_UNLOCKED, 1);

        for (int i = 0; i < mod1Levels.Length; i++)
        {
            ApplyLockState(mod1Levels[i].levelButton, i < unlocked);
        }
    }

    /// <summary>
    /// Lock image visible + text hidden + not clickable when LOCKED, the opposite when UNLOCKED.
    /// The lock is the child named "lock..." and the text the child with a Text component, in any order;
    /// falls back to child 0 = lock, child 1 = text.
    /// </summary>
    private static void ApplyLockState(Button button, bool isUnlocked)
    {
        Transform btn = button.transform;
        Transform lockImage = null, levelText = null;

        foreach (Transform child in btn)
        {
            if (lockImage == null && child.name.ToLowerInvariant().Contains("lock"))
                lockImage = child;
            else if (levelText == null && (child.GetComponent<Text>() != null || child.name.ToLowerInvariant().Contains("text")))
                levelText = child;
        }
        if (lockImage == null && btn.childCount > 0) lockImage = btn.GetChild(0);
        if (levelText == null && btn.childCount > 1) levelText = btn.GetChild(1);

        if (lockImage != null) lockImage.gameObject.SetActive(!isUnlocked);
        if (levelText != null && levelText != lockImage) levelText.gameObject.SetActive(isUnlocked);

        button.interactable = isUnlocked;
    }

    /// <summary>
    /// Called when the player taps a Mod 1 level button.
    /// Saves the selected level index then loads the Mod 1 gameplay scene.
    /// </summary>
    private void OnMod1LevelSelected(int levelIndex)
    {
        ShowInterstitialIf(c => c.isInterLevelSelect);

        PlayerPrefs.SetInt(KEY_MOD1_SELECTED_LEVEL, levelIndex);
        PlayerPrefs.Save();
        GameManager.LevelChosenInGame = true; // overrides the scene's isTest / tempLvl

        Debug.Log($"[MainMenuController] Mod 1 – Level {levelIndex} selected. Loading: {mod1SceneName}");
        StartCoroutine(ShowLoadingScreenRoutine(() => {
            SceneManager.LoadScene(mod1SceneName);
        }, loadsScene: true));
    }

    // -------------------------------------------------------------------------
    // Mod 2 – Level Selection
    // -------------------------------------------------------------------------

    private void OpenMod2LevelSelection()
    {
        modSelectionPanel.SetActive(false);
        mod1LevelPanel.SetActive(false);
        mod2LevelPanel.SetActive(true);
        RefreshMod2LevelUI();
    }

    /// <summary>
    /// Refreshes lock/unlock images and button interactability for Mod 2 levels.
    /// </summary>
    private void RefreshMod2LevelUI()
    {
        int unlocked = PlayerPrefs.GetInt(KEY_MOD2_UNLOCKED, 1);

        for (int i = 0; i < mod2Levels.Length; i++)
        {
            ApplyLockState(mod2Levels[i].levelButton, i < unlocked);
        }

        // Intro: open on the last level (bottom of the list), then slide up to level 1
        if (mod2Levels.Length > 0 && mod2Levels[0].levelButton != null)
        {
            ScrollRect scroll = mod2Levels[0].levelButton.GetComponentInParent<ScrollRect>(true);
            if (scroll != null)
            {
                if (mod2ScrollIntro != null) StopCoroutine(mod2ScrollIntro);
                mod2ScrollIntro = StartCoroutine(PlayMod2ScrollIntro(scroll));
            }
        }
    }

    [Header("Mode 2 Level List Intro")]
    [Tooltip("Seconds on the last level before sliding up (lets the panel pop in first).")]
    public float mod2ScrollIntroDelay = 0.6f;
    [Tooltip("Seconds the slide from the last level up to level 1 takes.")]
    public float mod2ScrollIntroDuration = 1.4f;
    private Coroutine mod2ScrollIntro;

    private IEnumerator PlayMod2ScrollIntro(ScrollRect scroll)
    {
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 0f; // bottom = last level

        // Hold on the last level; the player touching the list takes over at any moment
        for (float t = 0f; t < mod2ScrollIntroDelay; t += Time.unscaledDeltaTime)
        {
            if (PlayerIsTouching()) yield break;
            yield return null;
        }

        for (float t = 0f; t < mod2ScrollIntroDuration; t += Time.unscaledDeltaTime)
        {
            if (PlayerIsTouching()) yield break;
            float p = Mathf.Clamp01(t / mod2ScrollIntroDuration);
            float eased = p * p * (3f - 2f * p); // smooth start and stop
            scroll.verticalNormalizedPosition = eased; // 0 (last level) -> 1 (level 1)
            yield return null;
        }
        scroll.verticalNormalizedPosition = 1f;
        mod2ScrollIntro = null;
    }

    private static bool PlayerIsTouching()
    {
        return Input.touchCount > 0 || Input.GetMouseButton(0);
    }

    /// <summary>
    /// Called when the player taps a Mod 2 level button.
    /// </summary>
    private void OnMod2LevelSelected(int levelIndex)
    {
        ShowInterstitialIf(c => c.isInterLevelSelect);

        PlayerPrefs.SetInt(KEY_MOD2_SELECTED_LEVEL, levelIndex);
        PlayerPrefs.Save();
        GameManagerMode2.LevelChosenInGame = true; // overrides the scene's isTest / tempLvl

        Debug.Log($"[MainMenuController] Mod 2 – Level {levelIndex} selected. Loading: {mod2SceneName}");
        StartCoroutine(ShowLoadingScreenRoutine(() => {
            SceneManager.LoadScene(mod2SceneName);
        }, loadsScene: true));
    }

    // -------------------------------------------------------------------------
    // Mod 3 – Direct Scene Load
    // -------------------------------------------------------------------------

    private void LoadMod3Scene()
    {
        Debug.Log($"[MainMenuController] Mod 3 selected. Loading: {mod3SceneName}");
        SceneManager.LoadScene(mod3SceneName);
    }

    // -------------------------------------------------------------------------
    // Level Unlock – Mod 1 ONLY
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unlocks the next Mod 1 level based on whichever level the player just finished.
    /// 
    /// HOW TO CALL FROM GameManager.GameWin():
    ///   MainMenuController.Instance.UnlockMod1Level();
    ///
    /// This function ONLY affects Mod 1 progression – it never touches Mod 2 or Mod 3.
    /// </summary>
    public void UnlockMod1Level()
    {
        // Read the level the player just played
        int completedLevel    = PlayerPrefs.GetInt(KEY_MOD1_SELECTED_LEVEL, 0);
        int currentlyUnlocked = PlayerPrefs.GetInt(KEY_MOD1_UNLOCKED, 1);
        int nextLevel         = completedLevel + 1;

        // Only advance if the next level hasn't been unlocked yet
        if (nextLevel >= currentlyUnlocked)
        {
            PlayerPrefs.SetInt(KEY_MOD1_UNLOCKED, nextLevel + 1);
            PlayerPrefs.Save();
            Debug.Log($"[MainMenuController] Mod 1 – Level {nextLevel} unlocked!");
        }
        else
        {
            Debug.Log($"[MainMenuController] Mod 1 – Level {nextLevel} was already unlocked.");
        }
    }

    // -------------------------------------------------------------------------
    // Level Unlock Progression – Static helpers
    // (Alternative: call these directly without needing an Instance reference)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Static version – unlocks the next Mod 1 level.
    /// Pass the index of the level the player just completed.
    /// Example: MainMenuController.UnlockNextMod1Level(MainMenuController.GetMod1SelectedLevel());
    /// This ONLY affects Mod 1.
    /// </summary>
    public static void UnlockNextMod1Level(int completedLevelIndex)
    {
        int currentlyUnlocked = PlayerPrefs.GetInt(KEY_MOD1_UNLOCKED, 1);
        int nextLevel         = completedLevelIndex + 1;

        if (nextLevel >= currentlyUnlocked)
        {
            PlayerPrefs.SetInt(KEY_MOD1_UNLOCKED, nextLevel + 1);
            PlayerPrefs.Save();
            Debug.Log($"[MainMenuController] Mod 1 – Level {nextLevel} is now unlocked.");
        }
    }

    /// <summary>
    /// Static version – unlocks the next Mod 2 level.
    /// Pass the index of the level the player just completed.
    /// Example: MainMenuController.UnlockNextMod2Level(MainMenuController.GetMod2SelectedLevel());
    /// This ONLY affects Mod 2.
    /// </summary>
    public static void UnlockNextMod2Level(int completedLevelIndex)
    {
        int currentlyUnlocked = PlayerPrefs.GetInt(KEY_MOD2_UNLOCKED, 1);
        int nextLevel         = completedLevelIndex + 1;

        if (nextLevel >= currentlyUnlocked)
        {
            PlayerPrefs.SetInt(KEY_MOD2_UNLOCKED, nextLevel + 1);
            PlayerPrefs.Save();
            Debug.Log($"[MainMenuController] Mod 2 – Level {nextLevel} is now unlocked.");
        }
    }

    // -------------------------------------------------------------------------
    // Panel Navigation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows the mod selection panel and hides level panels.
    /// Wire this to a "Back" button on each level panel.
    /// </summary>
    /// <summary>
    /// Main menu PLAY button: interstitial first when remote config isInterMenu is on (AdsManager also
    /// checks IsInterstialAd / enableInterstitial), then the mode selection as before.
    /// Separate from ShowModSelectionPanel so the level panels' Back buttons don't show an ad.
    /// </summary>
    public void OnPlayButton()
    {
        ShowInterstitialIf(c => c.isInterMenu);
        ShowModSelectionPanel();
    }

    static void ShowInterstitialIf(System.Func<GameConfigData, bool> flag) => AdsManager.ShowInterstitialIf(flag);

    public void ShowModSelectionPanel()
    {
        StartCoroutine(ShowLoadingScreenRoutine(() => {
            menuPanel.SetActive(false);
            mod1LevelPanel.SetActive(false);
            mod2LevelPanel.SetActive(false);
            modSelectionPanel.SetActive(true);
        }));
    }

    // -------------------------------------------------------------------------
    // Loading Screen
    // -------------------------------------------------------------------------

    /// <param name="loadsScene">True when onComplete loads a scene: the loading panel then stays up until
    /// the new scene replaces the menu, instead of closing and flashing the level selection panel.</param>
    private IEnumerator ShowLoadingScreenRoutine(System.Action onComplete, bool loadsScene = false)
    {
        // Ignore extra taps while a loading screen is already running
        if (isLoading) yield break;
        isLoading = true;

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (loadingFillImage != null)
            loadingFillImage.fillAmount = 0f;

        int ticks = Mathf.Max(1, loadingTicks);
        float tickDuration = loadingDuration / ticks;

        for (int i = 1; i <= ticks; i++)
        {
            float targetFill = (float)i / ticks;
            yield return StartCoroutine(FillTick(targetFill, tickDuration));
        }

        if (loadingFillImage != null)
        {
            loadingFillImage.fillAmount = 1f;
            loadingFillImage.color = loadingBarBaseColor;
        }

        // Show the next panel (or start the scene load) BEFORE hiding the loading panel,
        // so the previous panel never flashes in between.
        onComplete?.Invoke();

        if (!loadsScene)
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            isLoading = false;
        }
    }

    // Elastic-feeling overshoot: shoots past the target then settles back, like recoil.
    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = (overshoot - 1f) * 2f + 1f;
        float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    // Each tick snaps the bar forward with a tiny overshoot + a bright flash tint that fades back
    // out — like a round being chambered — instead of one continuous linear sweep.
    IEnumerator FillTick(float targetFill, float duration)
    {
        if (loadingFillImage == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float startFill = loadingFillImage.fillAmount;

        float snapDuration = duration * 0.35f;
        float t = 0f;
        while (t < snapDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / snapDuration);
            loadingFillImage.fillAmount = Mathf.Lerp(startFill, targetFill, EaseOutBack(p, 1.4f));
            loadingFillImage.color = Color.Lerp(loadingTickFlashColor, loadingBarBaseColor, p);
            yield return null;
        }
        loadingFillImage.fillAmount = targetFill;

        float rest = duration - snapDuration;
        float fadeT = 0f;
        while (fadeT < rest)
        {
            fadeT += Time.deltaTime;
            loadingFillImage.color = Color.Lerp(loadingTickFlashColor, loadingBarBaseColor, Mathf.Clamp01(fadeT / Mathf.Max(0.01f, rest)));
            yield return null;
        }
        loadingFillImage.color = loadingBarBaseColor;
    }

    // -------------------------------------------------------------------------
    // Static Getters (read from GameManager or any other script)
    // -------------------------------------------------------------------------

    /// <summary>Returns the mod the player selected (1, 2, or 3).</summary>
    public static int GetSelectedMod()       => PlayerPrefs.GetInt(KEY_SELECTED_MOD, 1);

    /// <summary>Returns the Mod 1 level index the player selected.</summary>
    public static int GetMod1SelectedLevel() => PlayerPrefs.GetInt(KEY_MOD1_SELECTED_LEVEL, 0);

    /// <summary>Returns the Mod 2 level index the player selected.</summary>
    public static int GetMod2SelectedLevel() => PlayerPrefs.GetInt(KEY_MOD2_SELECTED_LEVEL, 0);
}

// =============================================================================
// Data Structure – assign per level in the Inspector
// =============================================================================

/// <summary>
/// Represents one level slot in the level selection UI.
/// 
/// Hierarchy expected:
///   [Button GameObject]  <-- assign this as levelButton
///       └─ [Lock Image]  <-- first child; enabled when LOCKED, disabled when UNLOCKED
/// </summary>
[System.Serializable]
public class LevelEntry
{
    [Tooltip("The Button GameObject for this level. Its FIRST CHILD must be the lock Image.")]
    public Button levelButton;
}
