using UnityEngine;

namespace AirplaneControllerwithShooting
{
    public class GunController : MonoBehaviour
    {
        [Header("Machine Gun Properties")]
        public GameObject Bullet_Machinegun;
        public Transform FiringPoint_Machinegun;
        private float LastTime_Machinegun_Fire = 0;
        public float Machinegun_Firing_Interval;
        public AudioClip AudioClip_Machinegun_Fire;

        [Header("Missile Properties")]
        public GameObject Bullet_Missile;
        public Transform[] FiringPoints_Missiles;
        private float LastTime_Missile_Fire = 0;
        public float Missile_Firing_Interval;
        private int Missile_Firing_Point_Index = 0;
        public AudioClip AudioClip_Missile_Fire;
        public ParticleSystem[] Particle_Missile_Firing_Explosion;

        [HideInInspector]
        public int Ammo_Machinegun = 240;
        [HideInInspector]
        public int Ammo_Missile = 20;

        [HideInInspector]
        public int Damage_Machinegun = 0;
        [HideInInspector]
        public int Damage_Missile = 0;

        public AudioSource AudioSource_Gun;
        public static GunController Instance;

        private void Awake()
        {
            Instance = this;
        }

        public void Start()
        {
            GameCanvas.Instance.Text_Ammo_Missile.text = Ammo_Missile.ToString();
            GameCanvas.Instance.Text_Ammo_Machinegun.text = Ammo_Machinegun.ToString();
        }

        public void Fire_Missile()
        {
            if (AirplaneController.Instance.IsOnGround) return;

            if (Time.time > LastTime_Missile_Fire + Missile_Firing_Interval && Ammo_Missile > 0)
            {
                LastTime_Missile_Fire = Time.time;
                Missile_Firing_Point_Index++;
                Ammo_Missile--;
                GameCanvas.Instance.Text_Ammo_Missile.text = Ammo_Missile.ToString();
                if (Ammo_Missile == 0) GameCanvas.Instance.Text_Ammo_Missile.color = Color.red;
                AudioSource_Gun.PlayOneShot(AudioClip_Missile_Fire);
                GameObject newMissile = Instantiate(Bullet_Missile, FiringPoints_Missiles[Missile_Firing_Point_Index % 2].position, Quaternion.identity);
                newMissile.GetComponent<MissileScript>().DamagePower = Damage_Missile;
                newMissile.transform.eulerAngles = FiringPoints_Missiles[Missile_Firing_Point_Index % 2].eulerAngles;
                newMissile.GetComponentInChildren<Rigidbody>().AddForce(FiringPoints_Missiles[Missile_Firing_Point_Index % 2].transform.forward * 130, ForceMode.Impulse);
                Particle_Missile_Firing_Explosion[Missile_Firing_Point_Index % 2].Play();
            }
        }

        public void Fire_MachineGun()
        {
            if (AirplaneController.Instance.IsOnGround) return;

            if (Time.time > LastTime_Machinegun_Fire + Machinegun_Firing_Interval && Ammo_Machinegun > 0)
            {
                LastTime_Machinegun_Fire = Time.time;
                Ammo_Machinegun--;
                GameCanvas.Instance.Text_Ammo_Machinegun.text = Ammo_Machinegun.ToString();
                if (Ammo_Machinegun == 0) GameCanvas.Instance.Text_Ammo_Machinegun.color = Color.red;
                AudioSource_Gun.PlayOneShot(AudioClip_Machinegun_Fire);
                GameObject newBullet = Instantiate(Bullet_Machinegun, FiringPoint_Machinegun.position, Quaternion.identity);
                newBullet.GetComponent<BulletScript>().DamagePower = Damage_Machinegun;
                newBullet.transform.eulerAngles = FiringPoint_Machinegun.eulerAngles;
                newBullet.GetComponentInChildren<Rigidbody>().AddForce(FiringPoint_Machinegun.transform.forward * 160, ForceMode.Impulse);
            }
        }

        void FiringProcess()
        {
            if (GameCanvas.Instance.isFiringUpdate == 1)
            {
                Fire_MachineGun();
            }
            else if (GameCanvas.Instance.isFiringUpdate == -1)
            {
                Fire_Missile();
            }
        }

        void FixedUpdate()
        {
            FiringProcess();
        }

        private void Update()
        {
            if(AirplaneSystemManager.Instance.controllerType == ControllerType.KeyboardMouse)
            {
                // Clicking/tapping a UI control (slider, pause, buttons) must not also fire the guns
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return;

                if (Input.GetMouseButton(0))
                {
                    Fire_MachineGun();
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    Fire_Missile();
                }
            }
        }

        public void Add_Ammo_MachineGun(int Amount, AudioClip clip)
        {
            Ammo_Machinegun += Amount;
            GameCanvas.Instance.Text_Ammo_Machinegun.text = Ammo_Machinegun.ToString();
            AudioSource_Gun.PlayOneShot(clip);
            GameCanvas.Instance.Text_Ammo_Machinegun.color = Color.green;
        }

        public void Add_Ammo_Missile(int Amount, AudioClip clip)
        {
            Ammo_Missile += Amount;
            GameCanvas.Instance.Text_Ammo_Missile.text = Ammo_Missile.ToString();
            AudioSource_Gun.PlayOneShot(clip);
            GameCanvas.Instance.Text_Ammo_Missile.color = Color.green;
        }
    }
}
