using System.Collections;
using UnityEngine;

namespace AirplaneControllerwithShooting
{
    public class MissileScript : MonoBehaviour
    {
        public GameObject explosionPrefab;
        public int DamagePower = 25;
        public Transform particle_following;
        public bool isEnemyMissile = false;
        public Collider collider;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.1f);
            collider.enabled = true;
            Destroy(gameObject, 4);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if(isEnemyMissile)
            {
                if (collision.collider.CompareTag("Ground") || collision.collider.CompareTag("Airplane"))
                {
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                    if (particle_following != null)
                    {
                        particle_following.parent = null;
                    }
                    if(collision.collider.CompareTag("Airplane"))
                    {
                        AirplaneController.Instance.GetDamage(DamagePower);
                    }
                    Destroy(gameObject);
                }
            }
            else
            {
                if (collision.collider.CompareTag("Ground") || collision.collider.CompareTag("Enemy"))
                {
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                    if (particle_following != null)
                    {
                        particle_following.parent = null;
                    }
                    Vector3 explosionPos = transform.position;
                    Collider[] colliders = Physics.OverlapSphere(explosionPos, 15);
                    // A tank / balloon is several colliders: damage each unit once, via its EnemyAI
                    var damagedUnits = new System.Collections.Generic.HashSet<EnemyAI>();
                    foreach (Collider hit in colliders)
                    {
                        if (hit.CompareTag("Enemy"))
                        {
                            EnemyAI enemyAI = hit.GetComponentInParent<EnemyAI>();
                            if (enemyAI != null)
                            {
                                if (damagedUnits.Add(enemyAI)) enemyAI.GetDamage(DamagePower);
                            }
                            else
                            {
                                DamageManager damageManager = hit.GetComponent<DamageManager>();
                                if (damageManager != null)
                                {
                                    damageManager.ApplyDirectDamage(DamagePower, false); // buildings: once below
                                }
                            }
                        }
                    }
                    HouseTarget.HitArea(colliders, DamagePower); // each building in the blast, once
                    Destroy(gameObject);
                }
                else if (collision.collider.CompareTag("Collapsable"))
                {
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                    Vector3 explosionPos = transform.position;
                    Collider[] colliders = Physics.OverlapSphere(explosionPos, 10);
                    HouseTarget.HitArea(colliders, DamagePower); // building pieces: count toward the building
                    foreach (Collider hit in colliders)
                    {
                        Rigidbody rb = hit.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.isKinematic = false;
                            rb.useGravity = true;
                            rb.AddExplosionForce(100, explosionPos, 10, 3.0F);
                            Destroy(hit.gameObject, 10);
                        }
                    }
                    Destroy(gameObject);
                }
            }
        }
    }
}
