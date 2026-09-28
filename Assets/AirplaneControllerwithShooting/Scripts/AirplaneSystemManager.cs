using UnityEngine;
using UnityEngine.Events;

namespace AirplaneControllerwithShooting
{
    public class AirplaneSystemManager : MonoBehaviour
    {
        public ControllerType controllerType;
        [Tooltip("On phones/tablets use Mobile controls (joystick + buttons) whatever controllerType says. " +
                 "KeyboardMouse on a phone fires the gun on every touch and hides the joystick.")]
        public bool autoDetectMobile = true;
        [Tooltip("Mobile only: engine power (0-1 of top speed) at start, so the plane flies without touching the slider first.")]
        [Range(0f, 1f)] public float mobileStartThrottle = 0.6f;
        public CameraType cameraType;
        public PlaneType planeType;

        public bool ShowCockpit = true;
        public bool ShowRadar = true;
        public bool ShowTrailRenderer = true;
        public bool HaveWeapons = true;

        public GameObject cameraFPS;
        public GameObject cameraTPS;
        public static AirplaneSystemManager Instance;

        public UnityEvent EventToInvokeWhenAirplaneExploded;
        public UnityEvent EventToInvokeWhenAllEnemiesDestroyed;

        public void Awake()
        {
            Instance = this;
            if (autoDetectMobile && Application.isMobilePlatform)
                controllerType = ControllerType.Mobile;
        }

        private void Start()
        {
            if (controllerType == ControllerType.KeyboardMouse)
            {
                GameCanvas.Instance.Configure_For_PCConsole();
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else if (controllerType == ControllerType.Mobile)
            {
                GameCanvas.Instance.Configure_For_Mobile();
            }

            AirplaneController.Instance.Activate_AirplaneModel(planeType);

            if (controllerType == ControllerType.Mobile)
            {
                var engine = GameCanvas.Instance.Slider_EnginePower;
                float start = engine.maxValue * mobileStartThrottle;
                if (engine.value < start) engine.value = start; // onValueChanged -> Change_Engine
            }

            if (cameraType == CameraType.Interior_FPS)
            {
                cameraFPS.SetActive(true);
                cameraTPS.SetActive(false);
                GameCanvas.Instance.CockpitUI.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 200);
            }
            else if (cameraType == CameraType.Outdoor_TPS)
            {
                cameraFPS.SetActive(false);
                cameraTPS.SetActive(true);
                GameCanvas.Instance.CockpitUI.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 200);
            }
            cameraFPS.transform.position = AirplaneController.Instance.FPSCamera_Points[(int)planeType].position;
            GameCanvas.Instance.button_Machinegun.gameObject.SetActive(HaveWeapons);
            GameCanvas.Instance.button_Missile.gameObject.SetActive(HaveWeapons);
            GunController.Instance.enabled = HaveWeapons;
        }

        public Transform GetCamera()
        {
            if (cameraType == CameraType.Interior_FPS)
            {
                return cameraFPS.transform;
            }
            else
            {
                return cameraTPS.transform;
            }
        }
    }

    public enum ControllerType
    {
        KeyboardMouse,
        Mobile
    }

    public enum CameraType
    {
        Interior_FPS,
        Outdoor_TPS
    }

    public enum PlaneType
    {
        PaperPlane,
        Plane_Old_1920s,
        Plane_1940s,
        ModernJetAircraft
    }
}