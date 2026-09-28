using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(FlightSystem))]

public class GameUI : MonoBehaviour
{
    public static GameUI instance;
    FlightView View;
	public int Mode;
	private GameManager game;
	private PlayerController play;
	private WeaponController weapon;

	// Continuous fire flags for held-down shoot buttons
	private bool isSimpleFiring   = false;
	private bool isMultipleFiring = false;

    public Text killsText, scoreText, healthText, weaponReloadingText;
	public Image weaponsIcons;
	public Image rocketCooldownImage;
    public GameObject failPanel, winPanel, pausePanel;
    public Slider slider;

    [Header("Objective Panel")]
    public GameObject objectivePanel;
    public Text objectiveText;

    SickscoreGames.HUDNavigationSystem.HUDNavigationSystem hud;

    private void Awake()
    {
        instance = this;
    }
    void Start ()
	{
		hud = SickscoreGames.HUDNavigationSystem.HUDNavigationSystem.Instance;
		game =(GameManager)GameObject.FindObjectOfType (typeof(GameManager));
		play = (PlayerController)GameObject.FindObjectOfType (typeof(PlayerController));
		weapon = play.GetComponent<WeaponController> ();
        View = (FlightView)GameObject.FindObjectOfType(typeof(FlightView));
        // define player

    }
    private void Update()
    {
        // Mode 2/3 scenes: GameManagerMode2 handles this with its own panels
        if (GameManagerMode2.instance == null)
            HUDVisibility.Sync(hud, pausePanel, winPanel, failPanel, objectivePanel, game != null ? game.revivePanel : null);

        if (ControlFreak2.CF2Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManagerMode2.instance != null)
                GameManagerMode2.instance.TogglePause();
            else
                Pause();
        }

        // Shot down, waiting for the revive answer: no firing from the wreck
        if (game != null && game.HoldingPlayerForRevive) return;

        // Continuous fire while buttons are held (weapon's own delay/cooldown still applies)
        if (isSimpleFiring && weapon != null)
            weapon.FireWeaponAtIndex(0);
        if (isMultipleFiring && weapon != null)
            weapon.FireWeaponAtIndex(1);

		Gameplay();
    }

    public void Gameplay()
	{
		if (play)
		{
			if (game != null && game.HoldingPlayerForRevive) return;
			play.Active = true;
            GameManager.instance?.StartCounting();
            killsText.text = "Kills: " + game.Killed.ToString();
			scoreText.text = "Score: " + game.Score.ToString();
			healthText.text = "" + play.GetComponent<DamageManager>().HP;
            if (weapon.WeaponLists[weapon.CurrentWeapon].Icon)
			{
				weaponsIcons.sprite = weapon.WeaponLists[weapon.CurrentWeapon].Icon;
            }
            if (weapon.WeaponLists[weapon.CurrentWeapon].Ammo <= 0 && weapon.WeaponLists[weapon.CurrentWeapon].ReloadingProcess > 0)
            {
                if (!weapon.WeaponLists[weapon.CurrentWeapon].InfinityAmmo)
                    weaponReloadingText.text = "Reloading " + Mathf.Floor((1 - weapon.WeaponLists[weapon.CurrentWeapon].ReloadingProcess) * 100) + "%";
            }
            else
            {
                if (!weapon.WeaponLists[weapon.CurrentWeapon].InfinityAmmo)
                    weaponReloadingText.text = weapon.WeaponLists[weapon.CurrentWeapon].Ammo.ToString();
            }

            // Update Rocket Cooldown Image
            if (rocketCooldownImage != null && weapon.WeaponLists.Length > 2 && weapon.WeaponLists[2] != null)
            {
                var rocketLauncher = weapon.WeaponLists[2];
                if (rocketLauncher.Reloading)
                {
                    if (!rocketCooldownImage.gameObject.activeSelf)
                        rocketCooldownImage.gameObject.SetActive(true);
                    
                    rocketCooldownImage.fillAmount = rocketLauncher.ReloadingProcess;
                }
                else
                {
                    if (rocketCooldownImage.gameObject.activeSelf)
                        rocketCooldownImage.gameObject.SetActive(false);
                    
                    rocketCooldownImage.fillAmount = 0f;
                }
            }
        }
        else
        {
            //play = (PlayerController)GameObject.FindObjectOfType(typeof(PlayerController));
            //weapon = play.GetComponent<WeaponController>();
        }
    }
    
    public void ChangeCamera()
    {
        if (View)
            View.SwitchCameras();
    }

    // ── Dedicated shoot buttons ──────────────────────────────────────────

    /// <summary>
    /// Call this on Pointer DOWN of the Shoot button → starts continuous fire (index 0).
    /// Wire to: EventTrigger > PointerDown → GameUI.OnSimpleFireDown()
    /// </summary>
    public void OnSimpleFireDown()
    {
        isSimpleFiring = true;
    }

    /// <summary>
    /// Call this on Pointer UP of the Shoot button → stops continuous fire.
    /// Wire to: EventTrigger > PointerUp → GameUI.OnSimpleFireUp()
    /// </summary>
    public void OnSimpleFireUp()
    {
        isSimpleFiring = false;
    }

    /// <summary>
    /// Call this on Pointer DOWN of the Multiple Bullets button → starts continuous fire (index 1).
    /// Wire to: EventTrigger > PointerDown → GameUI.OnMultipleFireDown()
    /// </summary>
    public void OnMultipleFireDown()
    {
        isMultipleFiring = true;
    }

    /// <summary>
    /// Call this on Pointer UP of the Multiple Bullets button → stops continuous fire.
    /// Wire to: EventTrigger > PointerUp → GameUI.OnMultipleFireUp()
    /// </summary>
    public void OnMultipleFireUp()
    {
        isMultipleFiring = false;
    }

    /// <summary>Called by the "Rockets" button → fires weapon index 2.</summary>
    public void FireRocket()
    {
        if (weapon != null)
            weapon.FireWeaponAtIndex(2);
    }
    // ────────────────────────────────────────────────────────────────────

	public void FailPanel()
	{
        if (play)
            play.Active = false;

        failPanel.SetActive(true);
        Time.timeScale = 0;

    }
	public void Pause()
	{
        if (GameManagerMode2.instance != null)
        {
            GameManagerMode2.instance.PauseGame();
            return;
        }

        // Don't pause over the revive / fail screens
        if ((game != null && game.HoldingPlayerForRevive) || (failPanel != null && failPanel.activeSelf)) return;

        if (play)
            play.Active = false;
        
        pausePanel.SetActive(true);
        Time.timeScale = 0;
		//Gameplay();
    }

	public void Resume()
	{
        if (GameManagerMode2.instance != null)
        {
            GameManagerMode2.instance.ResumeGame();
            return;
        }

        Gameplay();
        pausePanel.SetActive(false);
        Time.timeScale = 1;
    }

    public void Restart()
    {
        if (GameManagerMode2.instance != null)
        {
            GameManagerMode2.instance.RestartLevel(); // shows the ad itself
            return;
        }

        AdsManager.ShowInterstitialIf(c => c.isInterRestart);
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

	public void ShowObjective(string text)
	{
		if (objectivePanel != null)
		{
			if (objectiveText != null)
			{
				objectiveText.text = text;
			}
			objectivePanel.SetActive(true);
			Time.timeScale = 0;
		}
	}

	public void OnObjectiveOkayPressed()
	{
		if (objectivePanel != null)
		{
			objectivePanel.SetActive(false);
		}
		Time.timeScale = 1;
	}

	public void Home()
	{
        if (GameManagerMode2.instance != null)
        {
            GameManagerMode2.instance.GoToHome(); // shows the ad itself
            return;
        }

        AdsManager.ShowInterstitialIf(c => c.isInterHome);
        Time.timeScale = 1;
        Gameplay();
        SceneManager.LoadScene("Mainmenu");
    }
}
