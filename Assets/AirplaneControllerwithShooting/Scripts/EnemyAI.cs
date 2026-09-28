using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AirplaneControllerwithShooting
{
    public class EnemyAI : MonoBehaviour
    {
        public int Health = 100;
        public string objName;
        public float Range = 200;
        public float Firing_Interval = 5;
        private float LastFiring_Time = 0;
        public GameObject EnemyMissile;
        public Transform Firing_Point;
        public Transform MissileLauncher;
        private bool isExploded = false;
        public GameObject SpritePointer;
        public Collider MainCollider;

        public Collider[] colliders;
        public List<Transform> PatrollingPoints;
        public bool isPatrolling = false;
        private Transform target = null;
        private float LastTimeDecision = -31;
        public float DecisionPeriod = 30;
        private NavMeshAgent agent;

        [Tooltip("Lower is Better for Better Accuracy!")]
        [Range(1, 10)]
        public float Accuracy = 5;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (AirplaneController.Instance != null && Health > 0 && Vector3.Distance(AirplaneController.Instance.transform.position, transform.position) < Range)
            {
                FireProcess();
                FollowProcess();
            }
            else
            {
                if (isPatrolling)
                {
                    if (target != null && Vector3.Distance(transform.position, target.position) <= agent.stoppingDistance)
                    {
                        target = null;
                        agent.isStopped = true;
                    }
                }
                if (Time.time > LastTimeDecision + DecisionPeriod)
                {
                    LastTimeDecision = Time.time;
                    if (isPatrolling)
                    {
                        if (target == null)
                        {
                            int decision = Random.Range(0, 2);
                            if (decision == 0)
                            {

                            }
                            else
                            {
                                if (PatrollingPoints.Count > 0)
                                {
                                    target = PatrollingPoints[Random.Range(0, PatrollingPoints.Count)];
                                    agent.SetDestination(target.position);
                                }
                            }

                        }

                    }
                }

            }
        }

        private void FollowProcess()
        {
            var rotation = Quaternion.LookRotation(AirplaneController.Instance.transform.position - MissileLauncher.position);
            MissileLauncher.rotation = Quaternion.Slerp(MissileLauncher.rotation, rotation, Time.deltaTime * 1);
        }

        void FireProcess()
        {
            if (Time.time > LastFiring_Time + Firing_Interval)
            {
                LastFiring_Time = Time.time;
                GameObject enemyMissile = Instantiate(EnemyMissile, Firing_Point.position, Quaternion.identity);
                Vector3 targettoShoot = new Vector3(AirplaneController.Instance.transform.position.x + Random.Range(-1 * (10 - Accuracy), (10 - Accuracy)), AirplaneController.Instance.transform.position.y + 5 + Random.Range(-1 * (10 - Accuracy), (10 - Accuracy)), AirplaneController.Instance.transform.position.z + Random.Range(-1 * (10 - Accuracy), (10 - Accuracy)));
                enemyMissile.transform.LookAt(targettoShoot);
                enemyMissile.GetComponentInChildren<Rigidbody>().AddForce(enemyMissile.transform.forward * 120, ForceMode.Impulse);
            }
        }

        public void GetDamage(int Damage)
        {
            Health = Health - Damage;
            if (Health <= 0 && !isExploded)
            {
                if (GameManagerMode2.instance != null)
                {
                    GameManagerMode2.instance.AddScore(250, objName);
                }
                else if (GameManager.instance != null)
                {
                    GameManager.instance.AddScore(250, objName);
                }

                isExploded = true;
                SpritePointer.SetActive(false);

                // Remove the HUD marker now instead of when the wreck is destroyed 6s later
                var hudElement = GetComponent<SickscoreGames.HUDNavigationSystem.HUDNavigationElement>();
                if (hudElement != null) hudElement.enabled = false;
                MainCollider.isTrigger = true;
                foreach (var item in colliders)
                {
                    item.enabled = true;
                }


                foreach (var rigidbody in GetComponentsInChildren<Rigidbody>())
                {
                    rigidbody.useGravity = true;
                    rigidbody.isKinematic = false;
                    rigidbody.AddExplosionForce(Random.Range(5, 20), rigidbody.transform.position, Random.Range(5, 15), Random.Range(2, 4), ForceMode.Impulse);
                    rigidbody.AddRelativeTorque(new Vector3(Random.Range(-5, 5), Random.Range(-5, 5), Random.Range(-5, 5)), ForceMode.Impulse);
                    Destroy(rigidbody.gameObject, 5);
                }

                if (RadarSystem.Instance != null)
                    RadarSystem.Instance.RemoveTarget(gameObject);
                Destroy(gameObject, 6);
            }
        }
    }
}