using UnityEngine;
using System.Linq;

namespace AirplaneControllerwithShooting
{
    public class AirplaneController : MonoBehaviour
    {
        [HideInInspector]
        public static AirplaneController Instance;

        public bool isVirtualJoystick = false;
        public float Health = 100;
        private float TotalHealth = 100;
        public AudioSource audioSource;
        public Rigidbody Rigidbody;
        public LayerMask GroundMaskLayer = 1;
        public Transform camTarget;

        [HideInInspector]
        public float Speed = 5.0f;
        private float CacheTopSpeed = 0;
        public float RotationSpeed = 100.0f;
        public float TiltAmount = 30.0f;
        public float Smooth = 5.0f;

        private float tiltAroundZ;
        private float tiltAroundX;


        public ParticleSystem[] Airplane_Paper_Smoke_Particle;
        public ParticleSystem[] Airplane_WW1_Smoke_Particle;
        public ParticleSystem[] Airplane_WW2_Smoke_Particle;
        public ParticleSystem[] Airplane_ModernJetAircraft_Smoke_Particle;
        private ParticleSystem[] CurrentSmokeParticle;

        public bool IsOnGround = false;
        public GameObject BodyExplosionParticle;
        [HideInInspector]
        public Vector2 hMove = Vector2.zero;

        public GameObject[] AirplaneModels;
        public Transform[] FPSCamera_Points;
        public PlaneConfiguration[] PlaneConfigurations;

        public GameObject[] HighSpeedEffect;
        public MeshRenderer[] Propeller;

        [Tooltip("Seconds between exploding and reappearing at the start when the player still has lives.")]
        public float respawnDelay = 2.5f;

        // Where / how the plane started, to respawn it there after losing a life
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool spawnKinematic;

        public void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            spawnKinematic = Rigidbody != null && Rigidbody.isKinematic;
        }

        void FixedUpdate()
        {
            if (isDead) return;
            ConsumeFuel();
        }

        private int planeIndex = 0;
        private GameObject currentPlane;
        private PlaneConfiguration currentConfiguration;

        public float minYPos;

        public void Activate_AirplaneModel(PlaneType planeType)
        {
            planeIndex = (int)planeType;
            currentPlane = AirplaneModels[planeIndex];
            currentPlane.SetActive(true);
            if (!AirplaneSystemManager.Instance.ShowTrailRenderer)
            {
                TrailRenderer[] trailRenderers = currentPlane.GetComponentsInChildren<TrailRenderer>();
                if (trailRenderers != null)
                {
                    for (int i = 0; i < trailRenderers.Length; i++)
                    {
                        trailRenderers[i].enabled = false;
                    }
                }
            }
            currentConfiguration = PlaneConfigurations.Where(x => x.planeType == planeType).FirstOrDefault();
            if (currentConfiguration != null)
            {
                Health = currentConfiguration.Health;
                Gasoline.Instance.FuelCapacity = currentConfiguration.FuelTankCapacity;
                Gasoline.Instance.CurrentFuel = currentConfiguration.FuelTankCapacity;
                Gasoline.Instance.Start();
                GunController.Instance.Ammo_Machinegun = currentConfiguration.MachineGunAmmoCapacity;
                GunController.Instance.Ammo_Missile = currentConfiguration.RocketLauncherAmmoCapacity;
                GunController.Instance.Damage_Machinegun = currentConfiguration.MachineGunDamage;
                GunController.Instance.Damage_Missile = currentConfiguration.RocketLauncherDamage;

                GameCanvas.Instance.Slider_EnginePower.maxValue = currentConfiguration.TopSpeed;
                GameCanvas.Instance.Slider_EnginePower.minValue = 0;
                GunController.Instance.Start();
            }
            switch(planeType)
            {
                case PlaneType.PaperPlane:
                    CurrentSmokeParticle = Airplane_Paper_Smoke_Particle;
                    break;
                case PlaneType.Plane_Old_1920s:
                    CurrentSmokeParticle = Airplane_WW1_Smoke_Particle;
                    break;
                case PlaneType.Plane_1940s:
                    CurrentSmokeParticle = Airplane_WW2_Smoke_Particle;
                    break;
                case PlaneType.ModernJetAircraft:
                    CurrentSmokeParticle = Airplane_ModernJetAircraft_Smoke_Particle;
                    break;
            }
            for (int i = 0; i < CurrentSmokeParticle.Length; i++)
            {
                CurrentSmokeParticle[i].gameObject.SetActive(true);
            }
            TotalHealth = Health;
            Speed = 0;
        }

        public void EngineLevel(int power)
        {
            CacheTopSpeed = power;

            if(Speed > 5)
            {
                for (int i = 0; i < CurrentSmokeParticle.Length; i++)
                {
                    CurrentSmokeParticle[i].Play();
                }
            }
            else
            {
                for (int i = 0; i < CurrentSmokeParticle.Length; i++)
                {
                    CurrentSmokeParticle[i].Stop();
                }
            }
        }


        void Update()
        {
            if (isDead) return; // crashed, waiting to respawn

            if (AirplaneSystemManager.Instance.controllerType == ControllerType.KeyboardMouse)
            {
                if(Input.GetKey(KeyCode.Space))
                {
                    GameCanvas.Instance.Slider_EnginePower.value = GameCanvas.Instance.Slider_EnginePower.value + 15 * Time.deltaTime;
                }
                else if (Input.GetKey(KeyCode.LeftShift))
                {
                    GameCanvas.Instance.Slider_EnginePower.value = GameCanvas.Instance.Slider_EnginePower.value - 15 * Time.deltaTime;
                }
                hMove.x = Input.GetAxis("Horizontal");
                hMove.y = Input.GetAxis("Vertical");
            }
            else
            {
                hMove.x = SimpleJoystick.Instance.HorizontalValue;
                hMove.y = SimpleJoystick.Instance.VerticalValue;
            }


            Speed = Mathf.Lerp(Speed, CacheTopSpeed, Time.deltaTime);
            transform.Translate(Vector3.forward * Speed * Time.deltaTime);
            if (!hasTakenOff && transform.position.y > spawnPosition.y + TakeoffHeight)
                hasTakenOff = true;

            if(transform.position.y < minYPos)
            {
                transform.position = new Vector3(transform.position.x, minYPos, transform.position.z);
                if (hasTakenOff) { ExplodePlane(); return; } // flew down into the ground
            }
            if(CacheTopSpeed < 5)
            {
                return;
            }
            transform.Rotate(Vector3.up, hMove.x * RotationSpeed * Time.deltaTime);
            camTarget.localPosition = new Vector3(Mathf.Lerp(camTarget.localPosition.x, hMove.x * 1.5f, Time.deltaTime * 2f), camTarget.localPosition.y, 0);

            tiltAroundZ = Mathf.Lerp(tiltAroundZ, hMove.x * -TiltAmount, Time.deltaTime * Smooth);
            tiltAroundX = Mathf.Lerp(tiltAroundX, hMove.y * TiltAmount, Time.deltaTime * Smooth);

            Quaternion target = Quaternion.Euler(tiltAroundX, transform.eulerAngles.y, tiltAroundZ);
            transform.rotation = Quaternion.Lerp(transform.rotation, target, Time.deltaTime * Smooth);
        }

        void ConsumeFuel()
        {
            if (Gasoline.Instance.CurrentFuel > 0)
            {
                if(currentConfiguration.planeType == PlaneType.Plane_Old_1920s)
                {
                    if (Speed > currentConfiguration.TopSpeed / 3)
                    {
                        HighSpeedEffect[0].SetActive(true);
                        Propeller[0].enabled = false;
                    }
                    else
                    {
                        HighSpeedEffect[0].SetActive(false);
                        Propeller[0].enabled = true;
                    }
                }
                else if (currentConfiguration.planeType == PlaneType.Plane_1940s)
                {
                    if (Speed > currentConfiguration.TopSpeed / 3)
                    {
                        HighSpeedEffect[1].SetActive(true);
                        Propeller[1].enabled = false;
                    }
                    else
                    {
                        HighSpeedEffect[1].SetActive(false);
                        Propeller[1].enabled = true;
                    }
                }

                if (CacheTopSpeed > 10 && Speed < 10)
                {
                    
                }
                else if(Speed > 10)
                {
                    // Mode 3 (Open World): long runs and no fuel pickups, so the tank never drains there
                    bool openWorld = GameManagerMode2.instance != null && GameManagerMode2.instance.IsOpenWorld;
                    if (!openWorld)
                        Gasoline.Instance.CurrentFuel = Gasoline.Instance.CurrentFuel - Speed * Time.deltaTime * Gasoline.Instance.FuelConsumptionRate;
                    Rigidbody.useGravity = false;
                }
                else if (Speed <= 10)
                {
                    Rigidbody.useGravity = true;
                    if(!IsOnGround)
                    {
                        Gasoline.Instance.Warning_Gasoline_Empty();
                    }
                }
            }
            else
            {
                Rigidbody.useGravity = true;
                Rigidbody.isKinematic = false;
                if (!IsOnGround)
                {
                    Gasoline.Instance.Warning_Gasoline_Empty();
                }
            }
        }

        public void GetDamage(int Damage)
        {
            Health = Health - Damage;
            if (Health <= 0)
            {
                Gasoline.Instance.CurrentFuel = 0;
                GameCanvas.Instance.GasolineUI.SetActive(false);
            }
        }

        // The plane is moved by its transform, not by physics, so touching the ground often registers
        // almost no impact force (e.g. on the expanded map tiles). Once airborne, any ground contact
        // is a crash; before take-off (rolling down the runway) it isn't.
        const float TakeoffHeight = 6f;
        private bool hasTakenOff = false;

        private void OnCollisionStay(Collision collision)
        {
            if (hasTakenOff && collision.collider.CompareTag("Ground")) ExplodePlane();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.CompareTag("Ground"))
            {
                IsOnGround = true;
                if (hasTakenOff) { ExplodePlane(); return; }
            }
            if (Gasoline.Instance.CurrentFuel == 0)
            {
                ExplodePlane();
            }
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce > 3)
            {
                ExplodePlane();
            }
            else if (collision.collider.CompareTag("Enemy"))
            {
                ExplodePlane();
                collision.collider.GetComponent<EnemyAI>().GetDamage(collision.collider.GetComponent<EnemyAI>().Health);
            }
        }

        bool isDead = false;
        /// <summary>Crashed, waiting to respawn (the plane model is hidden).</summary>
        public bool IsDead => isDead;
        void ExplodePlane()
        {
            if (isDead) return;
            isDead = true;

            if (AirplaneSystemManager.Instance.cameraFPS.activeSelf)
            {
                AirplaneSystemManager.Instance.cameraFPS.SetActive(false);
                AirplaneSystemManager.Instance.cameraTPS.SetActive(true);
            }
            Instantiate(BodyExplosionParticle, transform.position, Quaternion.identity);
            for (int i = 0; i < CurrentSmokeParticle.Length; i++)
            {
                CurrentSmokeParticle[i].Stop();
            }
            if(AirplaneSystemManager.Instance.EventToInvokeWhenAirplaneExploded != null)
            {
                AirplaneSystemManager.Instance.EventToInvokeWhenAirplaneExploded.Invoke();
            }

            // Lives: lose a heart and come back at the start while any are left; on the last one the
            // game manager offers a revive (and shows the fail panel itself if it's declined)
            var gm = GameManagerMode2.instance;
            if (gm != null && gm.UsesLives)
            {
                HideWreck();
                if (gm.LoseLife()) StartCoroutine(RespawnAfterDelay());
                else gm.OnOutOfLives();
                return;
            }

            Destroy(gameObject, 0.1f);
            GameCanvas.Instance.Hide_GameUI();

            if (GameManagerMode2.instance != null)
                GameManagerMode2.instance.GameOver();
            else if (GameManager.instance != null)
                GameManager.instance.GameOver();
        }

        // Wreck: hide the plane and freeze it where it crashed
        void HideWreck()
        {
            if (currentPlane != null) currentPlane.SetActive(false);
            if (Rigidbody != null)
            {
                Rigidbody.velocity = Vector3.zero;
                Rigidbody.angularVelocity = Vector3.zero;
                Rigidbody.isKinematic = true;
            }
            Speed = 0f;
        }

        System.Collections.IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            Respawn();
        }

        /// <summary>Back at the start, repaired and refuelled (after losing a life, or a revive).</summary>
        public void Respawn()
        {
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (Rigidbody != null)
            {
                Rigidbody.isKinematic = spawnKinematic;
                Rigidbody.useGravity = false;
                Rigidbody.velocity = Vector3.zero;
                Rigidbody.angularVelocity = Vector3.zero;
            }
            tiltAroundX = tiltAroundZ = 0f;
            hMove = Vector2.zero;
            IsOnGround = false;

            Health = TotalHealth;
            Gasoline.Instance.CurrentFuel = Gasoline.Instance.FuelCapacity;
            GameCanvas.Instance.GasolineUI.SetActive(true);

            if (currentPlane != null) currentPlane.SetActive(true);
            if (CurrentSmokeParticle != null)
                foreach (ParticleSystem smoke in CurrentSmokeParticle) smoke.Play();
            EngineLevel(Mathf.RoundToInt(GameCanvas.Instance.Slider_EnginePower.value));

            hasTakenOff = false; // back on the runway: the take-off roll is safe again
            isDead = false;
        }

        private void OnCollisionExit()
        {
            IsOnGround = false;
        }
    }
}