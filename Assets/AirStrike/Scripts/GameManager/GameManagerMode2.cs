using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManagerMode2 : MonoBehaviour
{
    public static GameManagerMode2 instance;

    const int ModIndex = 2;
    const string SelectedLevelKey = "Mod2_SelectedLevel";
    const string UnlockedLevelsKey = "Mod2_UnlockedLevels";

    [HideInInspector] public int Score = 0, Killed = 0;

    [System.Serializable]
    public class DestroyRequirementMode2
    {
        public int planesT;
        public int generatorsT;
        public int housesT;
        public int tanksT;
        public int balloonsT;
        public string objective;
        public bool useTimer;
        public float timeLimit;
        public bool hasCargo;
        public CargoData cargo;
        public bool isTutorial;
        public Transform playerSpawnPoint;
    }

    [Header("Mode 2 Level Requirements")]
    public DestroyRequirementMode2[] destroyRequirementsMode2;

    [Header("Mode 1 style targets (planes / generators)")]
    public Text enemiesText, generatorsText;

    [Header("Mode 2 Specific UI")]
    public Text housesText;
    public Text tanksText;
    public Text balloonsText;

    [Header("Panels")]
    public GameObject winPanel, failPanel, pausePanel;

    [Header("Home / Main Menu")]
    public string mainMenuSceneName = "Mainmenu";

    [Header("Levels")]
    public GameObject[] levels;

    [Header("Timer & UI")]
    public Text timerText;
    float currentTime;
    bool isTimerRunning = false;

    [Header("Cargo Settings")]
    public GameObject cargoPrefab;
    GameObject currentCargo;
    int currentWaypointIndex = 0;

    [Header("Open World (Mode 3)")]
    [Tooltip("Enabled instead of the levels when Mode 3 is selected in the main menu.")]
    public GameObject openWorldRoot;
    public GameObject scorePanel;
    public Text scoreText, bestScoreText;
    [Tooltip("Editor testing: play Open World without going through the main menu.")]
    public bool testOpenWorld = false;

    const int OpenWorldModIndex = 3;
    const string OpenWorldBestScoreKey = "Mod3_HighScore";
    const string OpenWorldTotalScoreKey = "Mod3_TotalScore";

    public bool IsOpenWorld { get; private set; }
    int housesDestroyed = 0, tanksDestroyed = 0, balloonsDestroyed = 0;
    int bestScore = 0;

    SickscoreGames.HUDNavigationSystem.HUDNavigationSystem hud;

    [Header("Lives")]
    public int maxLives = 3;
    int livesLeft;
    [Tooltip("Heart icon for the lives display (Assets/SS/Popup/Revive/revive_heart.png).")]
    public Sprite heartSprite;
    [Tooltip("Hearts position from the top-left corner of the screen (x right, y down is negative).")]
    public Vector2 heartsPosition = new Vector2(40f, -159f);
    RectTransform heartsRoot;
    Image[] heartImages;
    Coroutine heartsPunch;

    public bool UsesLives => maxLives > 0;

    [Header("Revive (shown when the last heart is lost)")]
    public GameObject revivePanel;
    public Text reviveTimerText;
    [Tooltip("Optional radial-filled ring that drains with the countdown.")]
    public Image reviveTimerFill;
    [Tooltip("Small message under the REVIVE button (ad not available / watch the full ad).")]
    public Text reviveStatusText;
    [Tooltip("Editor only: revive without a rewarded ad (real ads don't run in the editor).")]
    public bool reviveWithoutAdInEditor = true;
    bool reviveAdPending;   // a rewarded ad is on screen: the countdown is frozen
    bool reviveRewardEarned;
    [Tooltip("Seconds the player has to press Revive before the fail panel shows.")]
    public float reviveSeconds = 5f;
    [Tooltip("How many times per level the player may revive (0 = never offer it).")]
    public int maxRevives = 1;
    [Tooltip("Seconds to watch the explosion before the revive panel appears.")]
    public float reviveDelay = 1.5f;
    int revivesUsed = 0;
    bool reviveAnswered;

    public bool IsReviveOpen => revivePanel != null && revivePanel.activeSelf;

    [Header("Open World leaderboard (after revive, before fail)")]
    public Mod3LeaderboardManager leaderboard;
    bool IsLeaderboardOpen => leaderboard != null && leaderboard.IsOpen;

    [Tooltip("Editor only: pressing Play directly in this scene starts level tempLvl (menu / Next still pick the level).")]
    public bool isTest = false;
    public int tempLvl = 0;

    /// <summary>Set by the level-select menu and Next: the level in PlayerPrefs is the one to play.</summary>
    public static bool LevelChosenInGame;

    int currentLevelIndex = 0;
    bool winCond = false;

    int planeToKill = 0;
    int generatorsToKill = 0;
    int housesToKill = 0;
    int tanksToKill = 0;
    int balloonsToKill = 0;

    int housesTotal = 0;
    int tanksTotal = 0;
    int balloonsTotal = 0;

    void Awake()
    {
        instance = this;
    }

    [Header("Performance")]
    [Tooltip("Trees and rocks (layer \"Scenery\") are not drawn beyond this distance. The big map has " +
             "thousands of them; drawing all of them up to the camera's far plane made the game lag.")]
    public float sceneryDrawDistance = 500f;

    // Per-layer cull distance on every camera (main, FPS/TPS, radar...), spherical so turning doesn't pop
    void ApplySceneryDrawDistance()
    {
        int layer = LayerMask.NameToLayer("Scenery");
        if (layer < 0) return;
        foreach (Camera cam in FindObjectsOfType<Camera>(true))
        {
            float[] distances = cam.layerCullDistances;
            distances[layer] = sceneryDrawDistance;
            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true;
        }
    }

    void Start()
    {
        hud = SickscoreGames.HUDNavigationSystem.HUDNavigationSystem.Instance;
        IgnoreTouchesOnHudMarkers();
        ApplySceneryDrawDistance();

        IsOpenWorld =openWorldRoot != null &&
                      (testOpenWorld || PlayerPrefs.GetInt("SelectedMod", 1) == OpenWorldModIndex);
        if (scorePanel != null) scorePanel.SetActive(IsOpenWorld);
        if (openWorldRoot != null) openWorldRoot.SetActive(IsOpenWorld);

        livesLeft = maxLives;
        if (UsesLives) CreateHeartsUI();

        if (IsOpenWorld)
        {
            StartOpenWorld();
            return;
        }

        // isTest / tempLvl: only when Play is pressed directly in this scene in the Editor. A level picked
        // in the menu or reached with Next always wins, and builds never use tempLvl.
        if (isTest && Application.isEditor && !LevelChosenInGame)
        {
            currentLevelIndex = tempLvl;
        }
        else
        {
            currentLevelIndex = PlayerPrefs.GetInt(SelectedLevelKey, 0);
        }

        DestroyRequirementMode2 currentReq = destroyRequirementsMode2[currentLevelIndex];

        planeToKill = currentReq.planesT;
        generatorsToKill = currentReq.generatorsT;

        housesToKill = currentReq.housesT;
        tanksToKill = currentReq.tanksT;
        balloonsToKill = currentReq.balloonsT;

        housesTotal = currentReq.housesT;
        tanksTotal = currentReq.tanksT;
        balloonsTotal = currentReq.balloonsT;

        if (currentReq.useTimer)
        {
            currentTime = currentReq.timeLimit;
            isTimerRunning = true;
        }
        else
        {
            isTimerRunning = false;
            if (timerText != null) timerText.gameObject.SetActive(false);
        }

        if (currentReq.hasCargo)
        {
            InitCargo(currentReq.cargo);
        }

        ActivateLevel(currentLevelIndex);
        SpawnPlayer(currentReq.playerSpawnPoint);

        if (currentReq.isTutorial)
        {
            if (TutorialManager.instance != null)
            {
                TutorialManager.instance.StartTutorial();
            }
        }
        else
        {
            StartCounting();
        }

        StartCoroutine(WinCondCo());

        ShowLevelObjective(currentReq);
    }

    /// <summary>
    /// Called when the player's plane is destroyed. Takes one heart; returns true if the player
    /// still has hearts left (the plane respawns), false when it's game over (fail panel).
    /// </summary>
    public bool LoseLife()
    {
        if (!UsesLives) return false;

        livesLeft = Mathf.Max(0, livesLeft - 1);
        GameSfx.HeartLost();
        UpdateHeartsUI();
        if (heartsPunch != null) StopCoroutine(heartsPunch);
        heartsPunch = StartCoroutine(PunchHearts());
        return livesLeft > 0;
    }

    /// <summary>Called by the plane when its last heart is gone: offer a revive, else fail.</summary>
    public void OnOutOfLives()
    {
        if (revivePanel != null && revivesUsed < maxRevives)
            StartCoroutine(ReviveCountdown());
        else
            StartCoroutine(FailAfter(reviveDelay));
    }

    // Game frozen (timeScale 0) while the revive panel counts down in real time
    IEnumerator ReviveCountdown()
    {
        yield return new WaitForSeconds(reviveDelay); // let the explosion play

        reviveAnswered = false;
        reviveAdPending = false;
        SetReviveStatus("");
        revivePanel.SetActive(true);
        Time.timeScale = 0f;

        int shown = -1;
        float pop = 0f;
        for (float left = reviveSeconds; left > 0f; )
        {
            if (reviveAnswered) yield break;

            // Frozen while the rewarded ad is on screen; capped step so the first frame after the ad
            // (the app was paused) doesn't eat the remaining seconds
            if (!reviveAdPending) left -= Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            int number = Mathf.CeilToInt(left);
            if (number != shown) { shown = number; pop = 1f; if (number > 0) GameSfx.Tick(); } // pop the number each second
            pop = Mathf.Max(0f, pop - Time.unscaledDeltaTime * 5f);

            if (reviveTimerText != null)
            {
                reviveTimerText.text = number.ToString();
                reviveTimerText.transform.localScale = Vector3.one * (1f + 0.3f * pop * pop);
            }
            if (reviveTimerFill != null) reviveTimerFill.fillAmount = left / reviveSeconds;
            yield return null;
        }
        if (reviveTimerFill != null) reviveTimerFill.fillAmount = 0f;

        if (reviveAnswered) yield break;
        reviveAnswered = true;
        revivePanel.SetActive(false);
        ProceedToFail();
    }

    /// <summary>
    /// REVIVE (AD) button: shows a rewarded ad; the revive is the reward. The countdown is frozen while
    /// the ad is on screen. Closed early -> no revive, countdown continues; no ad -> message.
    /// </summary>
    public void RevivePlayer()
    {
        if (!IsReviveOpen || reviveAnswered || reviveAdPending) return;

#if UNITY_EDITOR
        if (reviveWithoutAdInEditor)
        {
            GrantRevive();
            return;
        }
#endif
        if (AdsManager.Instance == null)
        {
            SetReviveStatus("Ad not available - try again");
            return;
        }

        reviveAdPending = true;
        reviveRewardEarned = false;
        SetReviveStatus("");
        AdsManager.Instance.ShowRewardedVideo(
            onReward: () =>
            {
                reviveRewardEarned = true;
                reviveAdPending = false;
                GrantRevive();
            },
            onFailed: () =>
            {
                reviveAdPending = false;
                SetReviveStatus("Ad not available - try again");
            },
            onClosed: () =>
            {
                reviveAdPending = false;
                if (!reviveRewardEarned && IsReviveOpen) SetReviveStatus("Watch the full ad to revive");
            });
    }

    void SetReviveStatus(string message)
    {
        if (reviveStatusText != null) reviveStatusText.text = message;
    }

    /// <summary>The reward: hearts back to full, plane back at the start, game continues.</summary>
    void GrantRevive()
    {
        if (!IsReviveOpen || reviveAnswered) return;
        reviveAnswered = true;
        revivesUsed++;
        GameSfx.Revive();
        revivePanel.SetActive(false);

        livesLeft = maxLives;
        UpdateHeartsUI();
        if (heartsPunch != null) StopCoroutine(heartsPunch);
        heartsPunch = StartCoroutine(PunchHearts());

        Time.timeScale = 1f;
        if (AirplaneControllerwithShooting.AirplaneController.Instance != null)
            AirplaneControllerwithShooting.AirplaneController.Instance.Respawn();
    }

    /// <summary>"No thanks" button: skip the countdown and fail now.</summary>
    public void DeclineRevive()
    {
        if (!IsReviveOpen || reviveAnswered) return;
        reviveAnswered = true;
        revivePanel.SetActive(false);
        ProceedToFail();
    }

    IEnumerator FailAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ProceedToFail();
    }

    // Final game over after lives (and revives) are used up
    // Run over (revive declined / timed out / not offered). In Open World the leaderboard name step
    // comes first; the fail panel only shows once it's done, never at the same time.
    void ProceedToFail()
    {
        if (IsOpenWorld)
        {
            Time.timeScale = 0f; // game stays stopped while the name panel / review is open
            // Name panel first, then (on the 2nd run, once) the store review, then the fail panel
            System.Action reviewThenFail = () => StoreReview.BeforeOpenWorldFail(ShowFailPanel);
            if (leaderboard != null) leaderboard.HandleRunEnded(Score, reviewThenFail);
            else reviewThenFail();
            return;
        }
        ShowFailPanel();
    }

    // One analytics result (win / fail) per level
    bool resultLogged;

    void LogLevelResult(bool won)
    {
        if (IsOpenWorld || resultLogged) return;
        resultLogged = true;
        if (won) GameAnalytics.LevelWin(ModIndex, currentLevelIndex);
        else GameAnalytics.LevelFail(ModIndex, currentLevelIndex);
    }

    GameObject zeroScoreNote;

    // Open World run that scored nothing: no name panel was shown, so say why on the fail panel
    void ShowZeroScoreNote(bool show)
    {
        if (!show)
        {
            if (zeroScoreNote != null) zeroScoreNote.SetActive(false);
            return;
        }
        if (failPanel == null) return;

        if (zeroScoreNote == null)
        {
            Transform plate = failPanel.transform.Find("bg");
            Font font = housesText != null ? housesText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("ZeroScoreNote", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(plate != null ? plate : failPanel.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, plate != null ? 178f : 300f); // between the header and RESTART
            rt.sizeDelta = new Vector2(600f, 130f);

            NoteLine(rt, "YOUR SCORE: 0", font, 50, new Color(1f, 0.80f, 0.20f), new Vector2(0f, 28f));
            NoteLine(rt, "Score points to get on the leaderboard!", font, 24, Color.white, new Vector2(0f, -28f));
            zeroScoreNote = root;
        }
        zeroScoreNote.SetActive(true);
    }

    static void NoteLine(RectTransform parent, string text, Font font, int size, Color color, Vector2 pos)
    {
        var go = new GameObject("Line", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600f, 60f);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);
    }

    void ShowFailPanel()
    {
        LogLevelResult(false);
        ShowZeroScoreNote(IsOpenWorld && Score <= 0);
        var plane = AirplaneControllerwithShooting.AirplaneController.Instance;
        if (plane != null) Destroy(plane.gameObject);
        if (AirplaneControllerwithShooting.GameCanvas.Instance != null)
            AirplaneControllerwithShooting.GameCanvas.Instance.Hide_GameUI();

        if (failPanel != null) failPanel.SetActive(true);
        GameSfx.Fail();
        Time.timeScale = 0f;
    }

    // Hearts in the top-left corner: red = lives left, grey = lives lost
    void CreateHeartsUI()
    {
        Transform canvas = housesText != null ? housesText.canvas.rootCanvas.transform
                         : AirplaneControllerwithShooting.GameCanvas.Instance != null ? AirplaneControllerwithShooting.GameCanvas.Instance.transform
                         : null;
        if (canvas == null) return;

        var go = new GameObject("Hearts", typeof(RectTransform));
        heartsRoot = (RectTransform)go.transform;
        heartsRoot.SetParent(canvas, false);
        heartsRoot.anchorMin = heartsRoot.anchorMax = new Vector2(0f, 1f);
        heartsRoot.pivot = new Vector2(0f, 1f);
        heartsRoot.anchoredPosition = heartsPosition;
        heartsRoot.sizeDelta = new Vector2(maxLives * 78f, 72f);

        // Heart images (not a "♥" text character: many Android system fonts don't have that glyph,
        // so text hearts showed up blank on phones)
        heartImages = new Image[maxLives];
        for (int i = 0; i < maxLives; i++)
        {
            var h = new GameObject($"Heart{i + 1}", typeof(RectTransform), typeof(Image));
            var hrt = (RectTransform)h.transform;
            hrt.SetParent(heartsRoot, false);
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 0.5f);
            hrt.pivot = new Vector2(0f, 0.5f);
            hrt.anchoredPosition = new Vector2(i * 78f, 0f);
            hrt.sizeDelta = new Vector2(70f, 70f);
            var img = h.GetComponent<Image>();
            img.sprite = heartSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            heartImages[i] = img;
        }
        UpdateHeartsUI();
    }

    void UpdateHeartsUI()
    {
        if (heartImages == null) return;
        for (int i = 0; i < heartImages.Length; i++)
        {
            bool alive = i < livesLeft;
            // Without the sprite, fall back to plain red squares so lives are still visible
            heartImages[i].color = alive
                ? (heartSprite != null ? Color.white : new Color(1f, 0.23f, 0.23f))
                : new Color(0.25f, 0.25f, 0.25f, 0.75f); // lost life: dark grey
        }
    }

    IEnumerator PunchHearts()
    {
        Transform t = heartsRoot;
        if (t == null) yield break;
        for (float time = 0f; time < 0.4f; time += Time.unscaledDeltaTime)
        {
            float p = time / 0.4f;
            t.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    // Mode 3: no levels, no timer, no win — targets keep respawning (OpenWorldManager) and we count kills + score
    void StartOpenWorld()
    {
        GameAnalytics.ModeStart(OpenWorldModIndex);
        StoreReview.CountOpenWorldRun();
        DeactivateAllLevels();
        isTimerRunning = false;
        if (timerText != null) timerText.gameObject.SetActive(false);

        bestScore = PlayerPrefs.GetInt(OpenWorldBestScoreKey, 0);
        UpdateOpenWorldUI();

        if (GameUI.instance != null)
            GameUI.instance.ShowObjective("OPEN WORLD\nDestroy as many balloons, tanks and buildings as you can!");
    }

    void UpdateOpenWorldUI()
    {
        SetCounter(housesText, "Buildings", $"{housesDestroyed}", true);
        SetCounter(tanksText, "Tanks", $"{tanksDestroyed}", true);
        SetCounter(balloonsText, "Balloons", $"{balloonsDestroyed}", true);
        if (scoreText != null) scoreText.text = "Score: " + Score;
        if (bestScoreText != null) bestScoreText.text = "Best: " + bestScore;
    }

    // "Buildings: 0/3". The text sits inside its bar (…CounterBar), which is shown/hidden with it.
    static void SetCounter(Text text, string label, string value, bool show)
    {
        if (text == null) return;
        text.text = $"{label}: {value}";
        GameObject row = text.transform.parent != null && text.transform.parent.name.EndsWith("CounterBar")
            ? text.transform.parent.gameObject
            : text.gameObject;
        if (row.activeSelf != show) row.SetActive(show);
    }

    void AddOpenWorldScore(int score, string objDestroyed)
    {
        Score += score;
        if (objDestroyed == "house") housesDestroyed++;
        else if (objDestroyed == "tank") tanksDestroyed++;
        else if (objDestroyed == "balloon") balloonsDestroyed++;
        AddKills();

        PlayerPrefs.SetInt(OpenWorldTotalScoreKey, PlayerPrefs.GetInt(OpenWorldTotalScoreKey, 0) + score);
        if (Score > bestScore)
        {
            bestScore = Score;
            PlayerPrefs.SetInt(OpenWorldBestScoreKey, bestScore);
        }
        PlayerPrefs.Save();

        UpdateOpenWorldUI();
    }

    void ShowLevelObjective(DestroyRequirementMode2 req)
    {
        string textToShow = req.objective;
        if (string.IsNullOrEmpty(textToShow))
        {
            if (req.isTutorial)
            {
                textToShow = "TUTORIAL LEVEL\nFly through the checkpoints, then use ROCKETS to destroy the hot air balloons!";
            }
            else if (req.hasCargo)
            {
                textToShow = "ESCORT OBJECTIVE\nProtect and escort the cargo to the destination!";
            }
            else
            {
                textToShow = "LEVEL OBJECTIVE\nDestroy";
                bool added = false;
                if (req.planesT > 0) { textToShow += $" {req.planesT} Enemy Planes"; added = true; }
                if (req.generatorsT > 0) { textToShow += (added ? " and " : " ") + $"{req.generatorsT} Generators"; added = true; }
                if (req.housesT > 0) { textToShow += (added ? " and " : " ") + $"{req.housesT} Houses"; added = true; }
                if (req.tanksT > 0) { textToShow += (added ? " and " : " ") + $"{req.tanksT} Tanks"; added = true; }
                if (req.balloonsT > 0) { textToShow += (added ? " and " : " ") + $"{req.balloonsT} Balloons"; added = true; }

                if (!added)
                {
                    textToShow += " All Targets";
                }
                textToShow += "!";
            }
        }

        if (GameUI.instance != null)
        {
            GameUI.instance.ShowObjective(textToShow);
        }
    }

    void SpawnPlayer(Transform spawnPoint)
    {
        if (spawnPoint == null) return;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.transform.position = spawnPoint.position;

            FlightSystem flight = player.GetComponent<FlightSystem>();
            if (flight != null)
            {
                flight.SetRotation(spawnPoint.rotation);
            }
            else
            {
                player.transform.rotation = spawnPoint.rotation;
            }

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = spawnPoint.position;
                rb.rotation = spawnPoint.rotation;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            FlightView cameraView = FindObjectOfType<FlightView>();
            if (cameraView != null)
            {
                cameraView.ResetCameraPosition();
            }
        }
    }

    void InitCargo(CargoData cargoData)
    {
        if (cargoData.waypoints == null || cargoData.waypoints.Length == 0) return;

        Vector3 startPos = cargoData.GetWaypointPosition(0);
        if (cargoPrefab != null)
        {
            currentCargo = Instantiate(cargoPrefab, startPos, Quaternion.identity);
        }
        else
        {
            currentCargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            currentCargo.transform.position = startPos;
            currentCargo.name = "Cargo (Escort)";
        }
        currentWaypointIndex = 0;
    }

    // The HUD markers (off-screen arrows line up along the screen edges, over the joystick) only show
    // information: let touches go through them to the joystick / buttons
    static void IgnoreTouchesOnHudMarkers()
    {
        foreach (var hudCanvas in FindObjectsOfType<SickscoreGames.HUDNavigationSystem.HUDNavigationCanvas>(true))
            foreach (GraphicRaycaster raycaster in hudCanvas.GetComponentsInChildren<GraphicRaycaster>(true))
                raycaster.enabled = false;
    }

    void Update()
    {
        bool panelOpen = HUDVisibility.AnyOpen(pausePanel, winPanel, failPanel, revivePanel,
            leaderboard != null ? leaderboard.namePanel : null,
            GameUI.instance != null ? GameUI.instance.objectivePanel : null);
        HUDVisibility.Sync(hud, panelOpen);
        // Hearts and the Open World score sit above the popups on the canvas: hide them while one is open
        if (heartsRoot != null) HUDVisibility.ShowUnlessPanel(heartsRoot.gameObject, panelOpen);
        if (IsOpenWorld) HUDVisibility.ShowUnlessPanel(scorePanel, panelOpen);

        // Open World: fuel never drains there, so no fuel gauge (a respawn switches it back on)
        if (IsOpenWorld)
        {
            var canvas = AirplaneControllerwithShooting.GameCanvas.Instance;
            if (canvas != null && canvas.GasolineUI != null && canvas.GasolineUI.activeSelf) canvas.GasolineUI.SetActive(false);
        }

        if (IsOpenWorld) return;
        if (currentLevelIndex >= destroyRequirementsMode2.Length) return;
        DestroyRequirementMode2 currentReq = destroyRequirementsMode2[currentLevelIndex];

        if (isTimerRunning)
        {
            currentTime -= Time.deltaTime;
            if (timerText != null)
            {
                timerText.gameObject.SetActive(true);
                timerText.text = Mathf.Ceil(currentTime).ToString();
            }

            if (currentTime <= 0)
            {
                isTimerRunning = false;
                GameOver();
            }
        }

        if (currentReq.hasCargo && currentCargo != null)
        {
            if (currentWaypointIndex < currentReq.cargo.waypoints.Length)
            {
                Vector3 targetPos = currentReq.cargo.GetWaypointPosition(currentWaypointIndex);
                currentCargo.transform.position = Vector3.MoveTowards(currentCargo.transform.position, targetPos, currentReq.cargo.speed * Time.deltaTime);

                if (Vector3.Distance(currentCargo.transform.position, targetPos) < 0.1f)
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= currentReq.cargo.waypoints.Length)
                    {
                        if (currentReq.cargo.loop)
                        {
                            currentWaypointIndex = 0;
                        }
                        else
                        {
                            if (winCond)
                            {
                                winCond = false;
                                GameWin();
                            }
                        }
                    }
                    else
                    {
                        currentCargo.transform.LookAt(currentReq.cargo.GetWaypointPosition(currentWaypointIndex));
                    }
                }
            }
        }
    }

    IEnumerator WinCondCo()
    {
        yield return new WaitForSeconds(2f);
        winCond = true;
    }

    void ActivateLevel(int index)
    {
        DeactivateAllLevels();
        levels[index].SetActive(true);
    }

    void DeactivateAllLevels()
    {
        foreach (GameObject level in levels)
        {
            level.SetActive(false);
        }
    }

    public void NextLevel()
    {
        if (!StoreReview.ConsumeAdSkip()) // the review prompt took this win's ad slot
            AdsManager.ShowInterstitialIf(c => c.isInterNext);
        Time.timeScale = 1;

        // Last level finished: no next level, go back to the main menu
        int nextLevel = currentLevelIndex + 1; // the level actually played, not the last menu choice
        if (nextLevel >= levels.Length)
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        PlayerPrefs.SetInt(SelectedLevelKey, nextLevel);
        PlayerPrefs.Save();
        LevelChosenInGame = true;
        currentLevelIndex = nextLevel;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public bool IsPaused => pausePanel != null && pausePanel.activeSelf;

    public void PauseGame()
    {
        // Don't pause over the win / fail / revive screens
        if ((winPanel != null && winPanel.activeSelf) || (failPanel != null && failPanel.activeSelf) || IsReviveOpen || IsLeaderboardOpen) return;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null) player.Active = false;

        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0;
    }

    public void ResumeGame()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null) player.Active = true;

        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1;
    }

    public void TogglePause()
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    public void RestartLevel()
    {
        AdsManager.ShowInterstitialIf(c => c.isInterRestart);
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToHome()
    {
        AdsManager.ShowInterstitialIf(c => c.isInterHome);
        Time.timeScale = 1;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void StartCounting()
    {
        if (IsOpenWorld)
        {
            UpdateOpenWorldUI();
            return;
        }

        if (enemiesText != null) enemiesText.text = planeToKill.ToString();
        if (generatorsText != null) generatorsText.text = generatorsToKill.ToString();
        // Rows for target types this level doesn't have are hidden instead of showing "0/0"
        SetCounter(housesText, "Buildings", $"{housesTotal - housesToKill}/{housesTotal}", housesTotal > 0);
        SetCounter(tanksText, "Tanks", $"{tanksTotal - tanksToKill}/{tanksTotal}", tanksTotal > 0);
        SetCounter(balloonsText, "Balloons", $"{balloonsTotal - balloonsToKill}/{balloonsTotal}", balloonsTotal > 0);

        if (currentLevelIndex >= destroyRequirementsMode2.Length) return;
        DestroyRequirementMode2 currentReq = destroyRequirementsMode2[currentLevelIndex];

        if (currentReq.hasCargo || currentReq.isTutorial) return; // Win handled by cargo or tutorial manager

        if ((planeToKill <= 0 && generatorsToKill <= 0 && housesToKill <= 0 && tanksToKill <= 0 && balloonsToKill <= 0) && winCond)
        {
            winCond = false;
            Debug.Log($"[GameManagerMode2] Level {currentLevelIndex} complete — all requirements destroyed. Level Win!");
            GameWin();
        }
    }

    public void AddScore(int score, string objDestroyed)
    {
        GameSfx.Score();
        if (IsOpenWorld)
        {
            AddOpenWorldScore(score, objDestroyed);
            return;
        }

        Score += score;
        if (objDestroyed == "plane")
        {
            planeToKill -= 1;
        }
        else if (objDestroyed == "house")
        {
            housesToKill -= 1;
        }
        else if (objDestroyed == "tank")
        {
            tanksToKill -= 1;
        }
        else if (objDestroyed == "balloon")
        {
            balloonsToKill -= 1;
        }
        else
        {
            generatorsToKill -= 1;
        }
        AddKills();
        StartCounting(); // update the HUD text immediately instead of waiting for GameUI's next Update()
    }

    public void AddKills()
    {
        Killed += 1;
    }

    public void GameWin()
    {
        StartCoroutine(GameWinCo());
    }

    IEnumerator GameWinCo()
    {
        yield return new WaitForSeconds(2f);

        if (PlayerPrefs.GetInt("SelectedMod", 1) == ModIndex)
        {
            int completedLevel = currentLevelIndex;
            int currentlyUnlocked = PlayerPrefs.GetInt(UnlockedLevelsKey, 1);
            int nextLevel = completedLevel + 1;

            if (nextLevel >= currentlyUnlocked)
            {
                PlayerPrefs.SetInt(UnlockedLevelsKey, nextLevel + 1);
                PlayerPrefs.Save();
                Debug.Log($"[GameManagerMode2] Mod {ModIndex} Level {nextLevel} unlocked!");
            }
        }

        LogLevelResult(true);

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null) player.Active = false;

        if (winPanel != null) winPanel.SetActive(true);
        GameSfx.Win();
        GameNotifications.RequestPermissionIfNeeded(); // a good moment to ask (Android 13+ / iOS)
        Time.timeScale = 0;
        StoreReview.OnLevelWon(ModIndex, currentLevelIndex);
    }

    public void GameOver()
    {
        StartCoroutine(GameFailCo());
    }

    IEnumerator GameFailCo()
    {
        yield return new WaitForSeconds(3.5f);

        LogLevelResult(false);

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null) player.Active = false;

        if (failPanel != null) failPanel.SetActive(true);
        GameSfx.Fail();
        Time.timeScale = 0;
    }
}
