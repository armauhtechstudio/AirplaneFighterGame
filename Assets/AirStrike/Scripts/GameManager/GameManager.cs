using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[System.Serializable]
public class CargoData
{
    public float speed;
    public bool loop;
    public Transform[] waypoints;

    public Vector3 GetWaypointPosition(int index) {
        if (waypoints != null && index >= 0 && index < waypoints.Length && waypoints[index] != null) {
            return waypoints[index].position;
        }
        return Vector3.zero;
    }
}

[System.Serializable]
public class DestroyRequirement
{
    public int planesT;
    public int generatorsT;
    public string objective;
    public bool useTimer;
    public float timeLimit;
    public bool hasCargo;
    public CargoData cargo;
    public bool isTutorial;
    public Transform playerSpawnPoint;
}

public class GameManager : MonoBehaviour {
	public static GameManager instance;
	// basic game score
	[HideInInspector]public int Score = 0, Killed = 0;
	public Text enemiesText, generatorsText;
    public DestroyRequirement[] destroyRequirements;
    public GameObject[] levels;
    public string mainMenuSceneName = "Mainmenu";
    protected int currentLevelIndex = 0;

    [Header("Timer & UI")]
    public Text timerText;
    protected float currentTime;
    protected bool isTimerRunning = false;

    [Header("Cargo Settings")]
    public GameObject cargoPrefab;
    protected GameObject currentCargo;
    protected int currentWaypointIndex = 0;
    protected bool winCond = false;
    protected int toKill;

    public bool isTest = false;
    public int tempLvl = 0;
    protected int planeToKill = 0;
    protected int generatorsToKill = 0;

    protected virtual void Awake()
    {
        instance = this;
    }

    protected virtual int SelectedModIndex => 1;
    protected virtual string SelectedLevelKey => "Mod1_SelectedLevel";
    protected virtual string UnlockedLevelsKey => "Mod1_UnlockedLevels";
    protected virtual void Start () {
        if (isTest)
        {
            currentLevelIndex = tempLvl;
        }
        else
        {
            currentLevelIndex = PlayerPrefs.GetInt(SelectedLevelKey, 0);
        }

        DestroyRequirement currentReq = destroyRequirements[currentLevelIndex];
        planeToKill = currentReq.planesT;
        generatorsToKill = currentReq.generatorsT;
        
        if (currentReq.useTimer) {
            currentTime = currentReq.timeLimit;
            isTimerRunning = true;
        } else {
            isTimerRunning = false;
            SetTimerVisible(false);
        }

        if (currentReq.hasCargo) {
            InitCargo(currentReq.cargo);
        }
        
        ActivateLevel(currentLevelIndex);
        SpawnPlayer(currentReq.playerSpawnPoint);
        RecordPlayerStart();

        if (currentReq.isTutorial) {
            if (TutorialManager.instance != null) {
                TutorialManager.instance.StartTutorial();
            }
        } else {
            StartCounting();
        }
        
        StartCoroutine(WinCondCo());

        ShowLevelObjective(currentReq);
    }

    protected virtual void ShowLevelObjective(DestroyRequirement req) {
        string textToShow = req.objective;
        if (string.IsNullOrEmpty(textToShow)) {
            if (req.isTutorial) {
                textToShow = "TUTORIAL LEVEL\nComplete the checkpoints & hot air balloon target practice!";
            } else if (req.hasCargo) {
                textToShow = "ESCORT OBJECTIVE\nProtect and escort the cargo to the destination!";
            } else {
                textToShow = $"LEVEL OBJECTIVE\nDestroy {req.planesT} Enemy Planes";
                if (req.generatorsT > 0) {
                    textToShow += $" and {req.generatorsT} Generators";
                }
                textToShow += "!";
            }
        }

        if (GameUI.instance != null) {
            GameUI.instance.ShowObjective(textToShow);
        }
    }

    void SpawnPlayer(Transform spawnPoint) {
        if (spawnPoint == null) return;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null) {
            player.transform.position = spawnPoint.position;

            FlightSystem flight = player.GetComponent<FlightSystem>();
            if (flight != null) {
                flight.SetRotation(spawnPoint.rotation);
            } else {
                player.transform.rotation = spawnPoint.rotation;
            }

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.position = spawnPoint.position;
                rb.rotation = spawnPoint.rotation;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            FlightView cameraView = FindObjectOfType<FlightView>();
            if (cameraView != null) {
                cameraView.ResetCameraPosition();
            }
        }
    }
    void InitCargo(CargoData cargoData) {
        if (cargoData.waypoints == null || cargoData.waypoints.Length == 0) return;

        Vector3 startPos = cargoData.GetWaypointPosition(0);
        if (cargoPrefab != null) {
            currentCargo = Instantiate(cargoPrefab, startPos, Quaternion.identity);
        } else {
            currentCargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            currentCargo.transform.position = startPos;
            currentCargo.name = "Cargo (Escort)";
        }
        currentWaypointIndex = 0;
    }

    void Update() {
        if (currentLevelIndex >= destroyRequirements.Length) return;
        DestroyRequirement currentReq = destroyRequirements[currentLevelIndex];

        if (isTimerRunning) {
            currentTime -= Time.deltaTime;
            if (timerText != null) {
                SetTimerVisible(true);
                timerText.text = "Time: " + Mathf.Ceil(currentTime);
            }

            if (currentTime <= 0) {
                isTimerRunning = false;
                GameOver(false); // out of time: a revive wouldn't help
            }
        }

        if (currentReq.hasCargo && currentCargo != null) {
            if (currentWaypointIndex < currentReq.cargo.waypoints.Length) {
                Vector3 targetPos = currentReq.cargo.GetWaypointPosition(currentWaypointIndex);
                currentCargo.transform.position = Vector3.MoveTowards(currentCargo.transform.position, targetPos, currentReq.cargo.speed * Time.deltaTime);

                if (Vector3.Distance(currentCargo.transform.position, targetPos) < 0.1f) {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= currentReq.cargo.waypoints.Length) {
                        if (currentReq.cargo.loop) {
                            currentWaypointIndex = 0;
                        } else {
                            if (winCond) {
                                winCond = false;
                                GameWin();
                            }
                        }
                    } else {
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
        // Deactivate all levels
        DeactivateAllLevels();

        // Activate the level at the specified index
        levels[index].SetActive(true);
        
    }
    void DeactivateAllLevels()
    {
        // Deactivate all levels in the array
        foreach (GameObject level in levels)
        {
            level.SetActive(false);
        }
    }
    // The timer text sits inside its HUD bar (…Bar): show / hide the whole bar with it
    void SetTimerVisible(bool show)
    {
        if (timerText == null) return;
        Transform bar = timerText.transform.parent;
        GameObject row = bar != null && bar.name.EndsWith("Bar") ? bar.gameObject : timerText.gameObject;
        if (row.activeSelf != show) row.SetActive(show);
        if (row != timerText.gameObject && !timerText.gameObject.activeSelf) timerText.gameObject.SetActive(true);
    }

    public void NextLevel()
    {
        if (!StoreReview.ConsumeAdSkip()) // the review prompt took this win's ad slot
            AdsManager.ShowInterstitialIf(c => c.isInterNext);
        Time.timeScale = 1;

        // Last level finished: no next level, go back to the main menu
        int nextLevel = PlayerPrefs.GetInt(SelectedLevelKey, 0) + 1;
        if (nextLevel >= levels.Length)
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        PlayerPrefs.SetInt(SelectedLevelKey, nextLevel);
        currentLevelIndex = nextLevel;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    public virtual void StartCounting()
    {
        if (enemiesText != null) enemiesText.text = planeToKill.ToString();
        if (generatorsText != null) generatorsText.text = generatorsToKill.ToString();
        
        if (currentLevelIndex >= destroyRequirements.Length) return;
        DestroyRequirement currentReq = destroyRequirements[currentLevelIndex];
        
        if (currentReq.hasCargo || currentReq.isTutorial) return; // Win handled by cargo or tutorial manager

        if ((planeToKill <= 0 && generatorsToKill <= 0) && winCond)
        {
            winCond = false;
            GameWin();
            print("dsa000");
        }
    }
    public virtual void AddScore(int score, string objDestroyed){
		Score += score;
        if (objDestroyed == "plane")
        {
            planeToKill -= 1;
        }
        else
        {
            generatorsToKill -= 1;
        }
        AddKills();
    }
	public void AddKills(){
		Killed +=1;
	}
	public void GameWin()
	{
		StartCoroutine(GameWinCo());
	}
	IEnumerator GameWinCo()
	{
		yield return new WaitForSeconds(2f);

		// Unlock the next level for whichever mod this GameManager belongs to, directly via PlayerPrefs
		// (MainMenuController lives in the Main Menu scene, so we write PlayerPrefs here directly)
		if (PlayerPrefs.GetInt("SelectedMod", 1) == SelectedModIndex)
		{
			int completedLevel    = PlayerPrefs.GetInt(SelectedLevelKey, 0);
			int currentlyUnlocked = PlayerPrefs.GetInt(UnlockedLevelsKey, 1);
			int nextLevel         = completedLevel + 1;

			if (nextLevel >= currentlyUnlocked)
			{
				PlayerPrefs.SetInt(UnlockedLevelsKey, nextLevel + 1);
				PlayerPrefs.Save();
				Debug.Log($"[GameManager] Mod {SelectedModIndex}  Level {nextLevel} unlocked!");
			}
		}

		LogLevelResult(true);
		GameUI.instance.winPanel.SetActive(true);
		Time.timeScale = 0;
		StoreReview.OnLevelWon(SelectedModIndex, currentLevelIndex);
	}

    // One analytics result (win / fail) per level
    bool resultLogged;

    void LogLevelResult(bool won)
    {
        if (resultLogged) return;
        resultLogged = true;
        if (won) GameAnalytics.LevelWin(SelectedModIndex, currentLevelIndex);
        else GameAnalytics.LevelFail(SelectedModIndex, currentLevelIndex);
    }
	/// <summary>
	/// Player died (allowRevive) or the level was lost another way (timer ran out -> allowRevive false).
	/// A death first offers the Revive panel (rewarded ad) if one is left, else the fail panel shows.
	/// </summary>
	public void GameOver(bool allowRevive = true){
		if (gameOverStarted) return;
		gameOverStarted = true;
		timerWasRunning = isTimerRunning; // a revive continues the level timer
		isTimerRunning = false;

		if (allowRevive && revivePanel != null && revivesUsed < maxRevives)
		{
			HoldingPlayerForRevive = true; // DamageManager keeps the plane as a hidden wreck
			StartCoroutine(ReviveCountdown());
		}
		else
			StartCoroutine(GameFailCo());
	}
	IEnumerator GameFailCo()
	{
		yield return new WaitForSeconds(3.5f);
        LogLevelResult(false);
        GameUI.instance.FailPanel();
		Time.timeScale = 0;
    }

    [Header("Revive (shown when the player is shot down)")]
    public GameObject revivePanel;
    public Text reviveTimerText;
    public Image reviveTimerFill;
    public Text reviveStatusText;
    [Tooltip("Editor only: revive without a rewarded ad (real ads don't run in the editor).")]
    public bool reviveWithoutAdInEditor = true;
    [Tooltip("Seconds the player has to press Revive before the fail panel shows.")]
    public float reviveSeconds = 5f;
    [Tooltip("How many times per level the player may revive (0 = never offer it).")]
    public int maxRevives = 1;
    [Tooltip("Seconds to watch the explosion before the revive panel appears.")]
    public float reviveDelay = 1.5f;

    /// <summary>True from the player's death until the revive is granted / declined.</summary>
    public bool HoldingPlayerForRevive { get; private set; }
    public bool IsReviveOpen => revivePanel != null && revivePanel.activeSelf;

    bool gameOverStarted;
    int revivesUsed;
    bool reviveAnswered;
    bool reviveAdPending;   // a rewarded ad is on screen: the countdown is frozen
    bool reviveRewardEarned;
    bool timerWasRunning;

    // Where the player starts the level: the revive puts the plane back here
    Vector3 playerStartPos;
    Quaternion playerStartRot;
    bool hasPlayerStart;

    void RecordPlayerStart()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null) return;
        playerStartPos = player.transform.position;
        playerStartRot = player.transform.rotation;
        hasPlayerStart = true;
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
            if (number != shown) { shown = number; pop = 1f; } // pop the number each second
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

    /// <summary>The reward: full health, plane back at the level start, game continues.</summary>
    void GrantRevive()
    {
        if (!IsReviveOpen || reviveAnswered) return;
        reviveAnswered = true;
        revivesUsed++;
        revivePanel.SetActive(false);

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            DamageManager damage = player.GetComponent<DamageManager>();
            if (damage != null) damage.Revive();

            if (hasPlayerStart)
            {
                player.transform.position = playerStartPos;
                FlightSystem flight = player.GetComponent<FlightSystem>();
                if (flight != null) flight.SetRotation(playerStartRot);
                else player.transform.rotation = playerStartRot;

                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = playerStartPos;
                    rb.rotation = playerStartRot;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                FlightView cameraView = FindObjectOfType<FlightView>();
                if (cameraView != null) cameraView.ResetCameraPosition();
            }
            player.Active = true;
        }

        HoldingPlayerForRevive = false;
        gameOverStarted = false;
        isTimerRunning = timerWasRunning;
        Time.timeScale = 1f;
    }

    /// <summary>"No thanks" button: skip the countdown and fail now.</summary>
    public void DeclineRevive()
    {
        if (!IsReviveOpen || reviveAnswered) return;
        reviveAnswered = true;
        revivePanel.SetActive(false);
        ProceedToFail();
    }

    void ProceedToFail()
    {
        HoldingPlayerForRevive = false;
        LogLevelResult(false);
        GameUI.instance.FailPanel();
        Time.timeScale = 0;
    }

}
