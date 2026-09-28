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
            EngineSound_source.Play();
            EngineSound_source.loop = true;
            EngineSound_source.volume = 0;
        }

        void Update()
        {
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

            if (AirplaneController.Instance.Speed > 10)
            {
                EngineSound_source.volume = Mathf.Min(1, AirplaneController.Instance.Speed / GameCanvas.Instance.Slider_EnginePower.maxValue);
                EngineSound_source.pitch = (AirplaneController.Instance.Speed / GameCanvas.Instance.Slider_EnginePower.maxValue);
                EngineSound_source.loop = true;
            }
            else
            {
                EngineSound_source.volume = 0;
            }

            if (AirplaneController.Instance.Speed > 10f)
            {
              
            }
            else
            {
                EngineSound_source.volume = 0;
            }
            last_rpm = AirplaneController.Instance.Speed;
        }
    }
}
