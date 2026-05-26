using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(FlightSystem))]

public class GameUI : MonoBehaviour
{
    FlightView View;
    public GUISkin skin;
	public Texture2D Logo;
	public int Mode;
	private GameManager game;
	private PlayerController play;
	private WeaponController weapon;

	public Text killsText, scoreText, healthText, weaponReloadingText;
	public Image weaponsIcons;
    public GameObject failPanel, winPanel, pausePanel;

	void Start ()
	{
		game = (GameManager)GameObject.FindObjectOfType (typeof(GameManager));
		play = (PlayerController)GameObject.FindObjectOfType (typeof(PlayerController));
		weapon = play.GetComponent<WeaponController> ();
        View = (FlightView)GameObject.FindObjectOfType(typeof(FlightView));
        // define player

    }
    private void Update()
    {
        if (ControlFreak2.CF2Input.GetKeyDown(KeyCode.Escape))
        {
            Pause();
        }

		Gameplay();
    }

	public void Gameplay()
	{
		if (play)
		{
			play.Active = true;

			killsText.text = "Kills: " + game.Killed.ToString();
			scoreText.text = "Score: " + game.Score.ToString();
			healthText.text = "ARMOR: " + play.GetComponent<DamageManager>().HP;
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
        }
        else
        {
            play = (PlayerController)GameObject.FindObjectOfType(typeof(PlayerController));
            weapon = play.GetComponent<WeaponController>();
        }
    }
    
    public void ChangeCamera()
    {
        if (View)
            View.SwitchCameras();
    }

	public void FailPanel()
	{
        if (play)
            play.Active = false;

        MouseLock.MouseLocked = false;
    }
	public void Pause()
	{
        if (play)
            play.Active = false;
        Time.timeScale = 0;
		Gameplay();
        Time.timeScale = 1;
    }

	public void Resume()
	{
        Gameplay();
        Time.timeScale = 1;
    }

    public void Restart()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

	public void Home()
	{
        Time.timeScale = 1;
        Gameplay();
        SceneManager.LoadScene("Mainmenu");
    }
}
