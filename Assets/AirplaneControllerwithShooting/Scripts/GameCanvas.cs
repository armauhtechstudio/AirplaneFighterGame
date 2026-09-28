using UnityEngine;
using UnityEngine.UI;

namespace AirplaneControllerwithShooting
{
    public class GameCanvas : MonoBehaviour
    {
        public Button button_Missile;
        public Button button_Machinegun;
        public GameObject joystick;
        public Button button_CameraChange;
        public int isFiringUpdate = 0;
        public Slider Slider_CurrentFuel;
        public Slider Slider_EnginePower;
        public Text Text_CurrentFuel;

        public Text Text_Ammo_Machinegun;
        public Text Text_Ammo_Missile;

        public static GameCanvas Instance;
        public GameObject GameUI;
        public GameObject CockpitUI;
        public GameObject RadarUI;
        public GameObject GasolineUI;

        void Awake()
        {
            Instance = this;
        }

        public void Change_Engine()
        {
            AirplaneController.Instance.EngineLevel(Mathf.RoundToInt(Slider_EnginePower.value));
        }

        void Start()
        {
            if (AirplaneSystemManager.Instance.ShowCockpit)
            {
                CockpitUI.SetActive(true);
            }
            else
            {
                CockpitUI.SetActive(false);
            }
            if (AirplaneSystemManager.Instance.ShowRadar)
            {
                RadarUI.SetActive(true);
            }
            else
            {
                RadarUI.SetActive(false);
            }
        }

        public void Configure_For_Mobile()
        {
            joystick.gameObject.SetActive(true);
            MakeTouchFriendly();
        }

        [Header("Mobile touch areas")]
        public Vector2 joystickTouchSize = new Vector2(500f, 500f);
        [Tooltip("Extra invisible touch area around the engine slider (left/right, top/bottom).")]
        public Vector2 engineSliderTouchPadding = new Vector2(50f, 40f);
        public Vector2 engineHandleSize = new Vector2(110f, 110f);

        // Small UI is hard to hit with a thumb: add invisible touch areas (events bubble up to the
        // joystick / slider) and a bigger slider handle. Pressing anywhere on the slider's area jumps
        // the engine power to that point.
        void MakeTouchFriendly()
        {
            if (joystick != null)
            {
                RectTransform area = AddTouchArea((RectTransform)joystick.transform);
                area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
                area.sizeDelta = joystickTouchSize;
            }

            if (Slider_EnginePower != null)
            {
                RectTransform area = AddTouchArea((RectTransform)Slider_EnginePower.transform);
                area.anchorMin = Vector2.zero;
                area.anchorMax = Vector2.one;
                area.offsetMin = -engineSliderTouchPadding;
                area.offsetMax = engineSliderTouchPadding;

                if (Slider_EnginePower.GetComponent<SliderTouchDrag>() == null)
                    Slider_EnginePower.gameObject.AddComponent<SliderTouchDrag>();
                if (Slider_EnginePower.GetComponent<EngineSliderHint>() == null)
                    Slider_EnginePower.gameObject.AddComponent<EngineSliderHint>();

                RectTransform handle = Slider_EnginePower.handleRect;
                if (handle != null)
                {
                    // Handle stretches across the slider's width; grow it past the edges
                    Vector2 current = handle.rect.size;
                    handle.sizeDelta += new Vector2(Mathf.Max(0f, engineHandleSize.x - current.x),
                                                    Mathf.Max(0f, engineHandleSize.y - current.y));
                }
            }
        }

        static RectTransform AddTouchArea(RectTransform parent)
        {
            Transform existing = parent.Find("TouchArea");
            if (existing != null) return (RectTransform)existing;

            var go = new GameObject("TouchArea", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.SetAsFirstSibling(); // behind the visible parts
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f); // invisible but still receives touches
            return rt;
        }

        public void Configure_For_PCConsole()
        {
            joystick.gameObject.SetActive(false);
            button_CameraChange.GetComponentInChildren<Text>().text = "Camera (C)";
        }

        public void Click_Button_CameraSwitch()
        {
            if (button_CameraChange.IsInteractable())
            {
                if (AirplaneSystemManager.Instance.cameraFPS != null && AirplaneSystemManager.Instance.cameraFPS.activeSelf)
                {
                    AirplaneSystemManager.Instance.cameraFPS.SetActive(false);
                    AirplaneSystemManager.Instance.cameraTPS.SetActive(true);
                    CockpitUI.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 200);
                }
                else if (AirplaneSystemManager.Instance.cameraTPS != null)
                {
                    AirplaneSystemManager.Instance.cameraFPS.SetActive(true);
                    AirplaneSystemManager.Instance.cameraTPS.SetActive(false);
                    CockpitUI.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 200);
                }
            }
        }

        private void Update()
        {
            if(Input.GetKeyUp(KeyCode.C) && AirplaneSystemManager.Instance.controllerType == ControllerType.KeyboardMouse)
            {
                Click_Button_CameraSwitch();
            }
        }

        public void Hide_GameUI()
        {
            GameUI.SetActive(false);
        }


        public void Click_Button_MachineGun_Down()
        {
            isFiringUpdate = 1;
        }

        public void Click_Button_Guns_Up()
        {
            isFiringUpdate = 0;
        }

        public void Click_Button_Missle_Down()
        {
            isFiringUpdate = -1;
        }
    }
}