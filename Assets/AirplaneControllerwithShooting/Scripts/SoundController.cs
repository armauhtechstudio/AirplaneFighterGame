using UnityEngine;

namespace AirplaneControllerwithShooting
{
    public class SoundController : MonoBehaviour
    {
        public AudioClip EngineSound_Propeller_clip;
        public AudioClip EngineSound_Jet_clip;
        public AudioClip EngineSound_Paper_clip;
        public AudioClip EngineStart_Propeller_clip;
        public AudioClip EngineStart_Jet_clip;
        public AudioSource EngineSound_source;
        public AudioSource EngineStarter_source;
        public float ReferenceSoundRPM;
        private float last_rpm;

        [Header("Engine loop (always on while flying, not too loud)")]
        [Range(0f, 1f)] public float idleVolume = 0.25f;
        [Range(0f, 1f)] public float maxVolume = 0.45f;
        public float idlePitch = 0.85f;
        public float maxPitch = 1.15f;
        [Tooltip("How fast volume / pitch follow the throttle (per second).")]
        public float smoothing = 2f;

        void Start()
        {
            EngineSound_source.playOnAwake = false;
            if(AirplaneSystemManager.Instance.planeType == PlaneType.Plane_Old_1920s || AirplaneSystemManager.Instance.planeType == PlaneType.Plane_1940s)
            {
                EngineSound_source.clip = EngineSound_Propeller_clip;
            }
            else if(AirplaneSystemManager.Instance.planeType == PlaneType.ModernJetAircraft)
            {
                EngineSound_source.clip = EngineSound_Jet_clip;
            }
            else if (AirplaneSystemManager.Instance.planeType == PlaneType.PaperPlane)
            {
                EngineSound_source.clip = EngineSound_Paper_clip;
            }
            EngineSound_source.loop = true;
            EngineSound_source.spatialBlend = 0f; // the player's own engine: same loudness whatever the camera distance
            EngineSound_source.volume = 0;
            EngineSound_source.Play();
        }

        void Update()
        {
            // Plane gone (the fail panel destroys it): just fade out
            if (AirplaneController.Instance == null || GameCanvas.Instance == null)
            {
                EngineSound_source.volume = Mathf.MoveTowards(EngineSound_source.volume, 0f, smoothing * 0.5f * Time.unscaledDeltaTime);
                return;
            }

            if (last_rpm == 0 && AirplaneController.Instance.Speed > 0)
            {
                if (AirplaneSystemManager.Instance.planeType == PlaneType.Plane_Old_1920s || AirplaneSystemManager.Instance.planeType == PlaneType.Plane_1940s)
                {
                    EngineStarter_source.PlayOneShot(EngineStart_Propeller_clip, 1);
                }
                else if (AirplaneSystemManager.Instance.planeType == PlaneType.ModernJetAircraft)
                {
                    EngineStarter_source.PlayOneShot(EngineStart_Jet_clip, 1);
                }
            }

            // Engine loop: never left stopped (hiding the plane model on a crash stops its AudioSource, and
            // nothing restarted it after the respawn), a steady hum that rises gently with the throttle,
            // faded out only while the plane is a wreck
            if (EngineSound_source.isActiveAndEnabled && !EngineSound_source.isPlaying)
            {
                EngineSound_source.loop = true;
                EngineSound_source.Play();
            }

            float maxSpeed = Mathf.Max(1f, GameCanvas.Instance.Slider_EnginePower.maxValue);
            float throttle = Mathf.Clamp01(AirplaneController.Instance.Speed / maxSpeed);
            // Silent while crashed, and while the game is frozen (pause / win / fail / revive panels)
            bool silent = AirplaneController.Instance.IsDead || Time.timeScale == 0f;
            float targetVolume = silent ? 0f : Mathf.Lerp(idleVolume, maxVolume, throttle);
            float targetPitch = Mathf.Lerp(idlePitch, maxPitch, throttle);
            float step = smoothing * Time.unscaledDeltaTime;
            EngineSound_source.volume = Mathf.MoveTowards(EngineSound_source.volume, targetVolume, step * 0.5f);
            EngineSound_source.pitch = Mathf.MoveTowards(EngineSound_source.pitch, targetPitch, step);

            last_rpm = AirplaneController.Instance.Speed;
        }
    }
}
