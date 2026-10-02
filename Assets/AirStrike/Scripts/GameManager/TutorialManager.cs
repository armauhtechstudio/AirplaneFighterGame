using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager instance;

    [Header("Checkpoint Settings")]
    public Transform[] checkpoints;
    public float checkpointRadius = 15f;
    public GameObject checkpointEffect;

    [Header("Hot Air Balloon Settings")]
    public GameObject[] hotAirBalloons; // 3 hot air balloons for the tutorial

    [Header("UI Instructions")]
    public Text tutorialText;

    [Header("Rockets Button Focus")]
    [Tooltip("Highlighted (rest of the screen dimmed) when the balloon part starts. Found by its FireRocket click action (or name) if left empty.")]
    public Button shootButton;
    public string shootButtonName = "Rockets";
    private TutorialShootFocus shootFocus;

    [Header("Tutorial State")]
    public bool isTutorialActive = false;
    public int currentCheckpointIndex = 0;
    public int currentBalloonIndex = 0;

    public enum TutorialState
    {
        Inactive,
        Checkpoints,
        HotAirBalloons,
        Completed
    }

    public TutorialState currentState = TutorialState.Inactive;

    private PlayerController playerController;
    private FlightSystem flightSystem;
    private WeaponController weaponController;
    private int lastWeaponIndex = -1;
    private bool weaponSwitchedForCurrentTarget = false;

    private void Awake()
    {
        instance = this;
    }

    public void StartTutorial()
    {
        isTutorialActive = true;
        currentState = TutorialState.Checkpoints;
        currentCheckpointIndex = 0;
        currentBalloonIndex = 0;

        FindPlayer();

        // Ensure checkpoints are visible
        UpdateCheckpointVisibility();

        // Ensure hot air balloons are hidden initially during checkpoint phase
        if (hotAirBalloons != null)
        {
            foreach (var balloon in hotAirBalloons)
            {
                if (balloon != null) balloon.SetActive(false);
            }
        }

        UpdateUI();
    }

    private void FindPlayer()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                flightSystem = playerController.GetComponent<FlightSystem>();
                weaponController = playerController.GetComponent<WeaponController>();
            }
        }
    }

    private void Update()
    {
        if (!isTutorialActive || currentState == TutorialState.Inactive || currentState == TutorialState.Completed)
            return;

        FindPlayer();
        if (playerController == null) return;

        if (currentState == TutorialState.Checkpoints)
        {
            ProcessCheckpoints();
        }
        else if (currentState == TutorialState.HotAirBalloons)
        {
            ProcessHotAirBalloons();
        }
    }

    private void ProcessCheckpoints()
    {
        if (checkpoints == null || checkpoints.Length == 0 || currentCheckpointIndex >= checkpoints.Length)
        {
            StartHotAirBalloonPhase();
            return;
        }

        Transform currentCp = checkpoints[currentCheckpointIndex];
        if (currentCp != null)
        {
            float distance = Vector3.Distance(playerController.transform.position, currentCp.position);
            if (distance <= checkpointRadius)
            {
                if (checkpointEffect != null)
                {
                    Instantiate(checkpointEffect, currentCp.position, Quaternion.identity);
                }

                currentCheckpointIndex++;
                GameSfx.Checkpoint();

                if (currentCheckpointIndex >= 4 || currentCheckpointIndex >= checkpoints.Length)
                {
                    StartHotAirBalloonPhase();
                }
                else
                {
                    UpdateCheckpointVisibility();
                    UpdateUI();
                }
            }
        }
    }

    private void StartHotAirBalloonPhase()
    {
        currentState = TutorialState.HotAirBalloons;
        currentBalloonIndex = 0;

        // Freeze player forward movement so player won't fly past targets while shooting
        if (flightSystem != null)
        {
            flightSystem.DisableForwardMovement = true;
        }

        if (weaponController != null)
        {
            lastWeaponIndex = weaponController.CurrentWeapon;
            weaponSwitchedForCurrentTarget = false;
        }

        ActivateCurrentBalloon();
        UpdateUI();

        // Last checkpoint passed: now the player has to fire rockets — spotlight the ROCKETS button
        shootFocus = TutorialShootFocus.Show(FindShootButton(), "Press the ROCKETS button to fire!");
    }

    // The ROCKETS button: the one whose click calls GameUI.FireRocket (any name), else by name
    private Button FindShootButton()
    {
        if (shootButton != null) return shootButton;
        Button[] buttons = FindObjectsOfType<Button>();
        foreach (Button b in buttons)
        {
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == "FireRocket") { shootButton = b; return shootButton; }

            var trigger = b.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger != null)
                foreach (var entry in trigger.triggers)
                    for (int i = 0; i < entry.callback.GetPersistentEventCount(); i++)
                        if (entry.callback.GetPersistentMethodName(i) == "FireRocket") { shootButton = b; return shootButton; }
        }
        foreach (Button b in buttons)
            if (b.name == shootButtonName) { shootButton = b; return shootButton; }

        Debug.LogWarning($"[TutorialManager] Rockets button (FireRocket / \"{shootButtonName}\") not found; no highlight.");
        return null;
    }

    private void ActivateCurrentBalloon()
    {
        if (hotAirBalloons == null) return;

        for (int i = 0; i < hotAirBalloons.Length; i++)
        {
            if (hotAirBalloons[i] != null)
            {
                hotAirBalloons[i].SetActive(i == currentBalloonIndex);
            }
        }
    }

    private void ProcessHotAirBalloons()
    {
        // Detect weapon switch by player
        if (weaponController != null)
        {
            if (weaponController.CurrentWeapon != lastWeaponIndex)
            {
                weaponSwitchedForCurrentTarget = true;
            }
        }

        if (hotAirBalloons != null && currentBalloonIndex < hotAirBalloons.Length)
        {
            GameObject activeBalloon = hotAirBalloons[currentBalloonIndex];

            // Check if current hot air balloon is destroyed or inactive
            if (activeBalloon == null || !activeBalloon.activeInHierarchy)
            {
                currentBalloonIndex++;
                weaponSwitchedForCurrentTarget = false;

                if (weaponController != null)
                {
                    lastWeaponIndex = weaponController.CurrentWeapon;
                }

                if (currentBalloonIndex >= 3 || currentBalloonIndex >= hotAirBalloons.Length)
                {
                    CompleteTutorial();
                }
                else
                {
                    ActivateCurrentBalloon();
                    UpdateUI();
                }
            }
            else
            {
                UpdateUI();
            }
        }
        else
        {
            CompleteTutorial();
        }
    }

    private void CompleteTutorial()
    {
        currentState = TutorialState.Completed;

        if (shootFocus != null) shootFocus.Hide();

        // Restore normal player movement speed
        if (flightSystem != null)
        {
            flightSystem.DisableForwardMovement = false;
        }

        if (tutorialText != null)
        {
            tutorialText.text = "TUTORIAL COMPLETE!";
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.GameWin();
        }
    }

    private void UpdateCheckpointVisibility()
    {
        if (checkpoints == null) return;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] != null)
            {
                checkpoints[i].gameObject.SetActive(i == currentCheckpointIndex);
            }
        }
    }

    private void UpdateUI()
    {
        if (tutorialText == null) return;

        if (currentState == TutorialState.Checkpoints)
        {
            tutorialText.text = $"TUTORIAL: Pass through Checkpoint ({currentCheckpointIndex + 1}/4)";
        }
        else if (currentState == TutorialState.HotAirBalloons)
        {
            tutorialText.text = $"TUTORIAL: Balloon ({currentBalloonIndex + 1}/3) - Press ROCKETS to destroy the Hot Air Balloon!";
        }
    }
}
