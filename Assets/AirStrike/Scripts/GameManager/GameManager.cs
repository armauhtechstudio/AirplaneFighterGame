using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[System.Serializable]
public class CargoWaypoint
{
    public float x;
    public float y;
    public float z;
    
    public Vector3 ToVector3() {
        return new Vector3(x, y, z);
    }
}

[System.Serializable]
public class CargoData
{
    public float speed;
    public bool loop;
    public CargoWaypoint[] waypoints;
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
}

public class GameManager : MonoBehaviour {
	public static GameManager instance;
	// basic game score
	[HideInInspector]public int Score = 0, Killed = 0;
	public Text enemiesText, generatorsText;
    public DestroyRequirement[] destroyRequirements;
    public GameObject[] levels;
    private int currentLevelIndex = 0;

    [Header("Timer & UI")]
    public Text timerText;
    private float currentTime;
    private bool isTimerRunning = false;

    [Header("Cargo Settings")]
    public GameObject cargoPrefab;
    private GameObject currentCargo;
    private int currentWaypointIndex = 0;
    bool winCond = false;
    int toKill;

    public bool isTest = false;
    public int tempLvl = 0;
    int planeToKill = 0;
    int generatorsToKill = 0;

    private void Awake()
    {
        instance = this;
    }
    void Start () {
        if (isTest)
        {
            currentLevelIndex = tempLvl;
        }
        else
        {
        }
        
        DestroyRequirement currentReq = destroyRequirements[currentLevelIndex];
        planeToKill = currentReq.planesT;
        generatorsToKill = currentReq.generatorsT;
        
        if (currentReq.useTimer) {
            currentTime = currentReq.timeLimit;
            isTimerRunning = true;
        } else {
            isTimerRunning = false;
            if (timerText != null) timerText.gameObject.SetActive(false);
        }

        if (currentReq.hasCargo) {
            InitCargo(currentReq.cargo);
        }
        
        ActivateLevel(currentLevelIndex);
        StartCounting();
        StartCoroutine(WinCondCo());
        
    }
    void InitCargo(CargoData cargoData) {
        if (cargoData.waypoints == null || cargoData.waypoints.Length == 0) return;

        if (cargoPrefab != null) {
            currentCargo = Instantiate(cargoPrefab, cargoData.waypoints[0].ToVector3(), Quaternion.identity);
        } else {
            currentCargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            currentCargo.transform.position = cargoData.waypoints[0].ToVector3();
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
                timerText.gameObject.SetActive(true);
                timerText.text = Mathf.Ceil(currentTime).ToString();
            }

            if (currentTime <= 0) {
                isTimerRunning = false;
                GameOver();
            }
        }

        if (currentReq.hasCargo && currentCargo != null) {
            if (currentWaypointIndex < currentReq.cargo.waypoints.Length) {
                Vector3 targetPos = currentReq.cargo.waypoints[currentWaypointIndex].ToVector3();
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
                        currentCargo.transform.LookAt(currentReq.cargo.waypoints[currentWaypointIndex].ToVector3());
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
    public void NextLevel()
    {
        PlayerPrefs.SetInt("CurrentLevel", PlayerPrefs.GetInt("CurrentLevel") + 1);
        currentLevelIndex = PlayerPrefs.GetInt("CurrentLevel");
        

        if (currentLevelIndex >= levels.Length)
        {
            PlayerPrefs.SetInt("CurrentLevel", 0);
            currentLevelIndex = 0; 
        }
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    public void StartCounting()
    {
        if (enemiesText != null) enemiesText.text = planeToKill.ToString();
        if (generatorsText != null) generatorsText.text = generatorsToKill.ToString();
        
        if (currentLevelIndex >= destroyRequirements.Length) return;
        DestroyRequirement currentReq = destroyRequirements[currentLevelIndex];
        
        if (currentReq.hasCargo) return; // Win handled when cargo reaches destination

        if ((planeToKill <= 0 && generatorsToKill <= 0) && winCond)
        {
            winCond = false;
            GameWin();
            print("dsa000");
        }
    }
    public void AddScore(int score, string objDestroyed){
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
		GameUI.instance.winPanel.SetActive(true);
		Time.timeScale = 0;
	}
	public void GameOver(){
		StartCoroutine(GameFailCo());
		
	}
	IEnumerator GameFailCo()
	{
		yield return new WaitForSeconds(3.5f);
        GameUI.instance.FailPanel();
		Time.timeScale = 0;
    }

}
