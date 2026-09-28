using UnityEngine;

namespace AirplaneControllerwithShooting
{
    public class Gasoline : MonoBehaviour
    {
        public float FuelCapacity = 100;
        public float CurrentFuel = 100;
        public float FuelConsumptionRate = 0.05f;
        public AudioSource audioSource_Gasoline;
        public static Gasoline Instance;

        private void Awake()
        {
            Instance = this;
        }

        public void Start()
        {
            GameCanvas.Instance.Text_CurrentFuel.text = CurrentFuel.ToString("F0");
        }

        private void UpdateGasolineIndicators()
        {
            if (CurrentFuel <= 0) CurrentFuel = 0;
            var energyAmountPercent = (CurrentFuel * 100) / FuelCapacity;
            GameCanvas.Instance.Slider_CurrentFuel.value = energyAmountPercent;
            GameCanvas.Instance.Text_CurrentFuel.text = CurrentFuel.ToString("F0");
        }

        public void Warning_Gasoline_Empty()
        {
            if (!audioSource_Gasoline.isPlaying)
            {
                audioSource_Gasoline.Play();
            }
        }

        private void FixedUpdate()
        {
            UpdateGasolineIndicators();
        }

        public void Add_Gassoline(float Amount, AudioClip clip)
        {
            CurrentFuel += Amount;
            if (CurrentFuel > FuelCapacity)
            {
                CurrentFuel = FuelCapacity;
            }
            if (audioSource_Gasoline.isPlaying) audioSource_Gasoline.Stop();
            audioSource_Gasoline.PlayOneShot(clip);
            GameCanvas.Instance.Text_CurrentFuel.text = CurrentFuel.ToString("F0");
            var energyAmountPercent = (CurrentFuel * 100) / FuelCapacity;
            GameCanvas.Instance.Slider_CurrentFuel.value = energyAmountPercent;
        }
    }
}