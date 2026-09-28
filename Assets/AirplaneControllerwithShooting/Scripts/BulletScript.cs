using System.Collections;
using UnityEngine;

namespace AirplaneControllerwithShooting
{
    public class BulletScript : MonoBehaviour
    {
        public GameObject explosionPrefab;
        public int DamagePower = 5;
        public Collider collider;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.1f);
            collider.enabled = true;
            Destroy(gameObject,3);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if ((collision.collider.CompareTag("Ground") || collision.collider.CompareTag("Enemy") || collision.collider.CompareTag("Collapsable")))
            {
                GameObject muzzle = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                muzzle.transform.eulerAngles = new Vector3(-90, 0, 0);

                if (collision.collider.CompareTag("Collapsable"))
                {
                    HouseTarget.HitPiece(collision.collider, DamagePower); // building piece: counts toward the building
                    Rigidbody rb = collision.collider.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.AddExplosionForce(10, transform.position, 1, 1f);
                        Destroy(collision.collider.gameObject, 10);
                    }
                }
                else if(collision.collider.CompareTag("Enemy"))
                {
                    // Any part of a tank / balloon (tyre, body…) damages the unit itself
                    EnemyAI enemyAI = collision.collider.GetComponentInParent<EnemyAI>();
                    if (enemyAI != null)
                    {
                        enemyAI.GetDamage(DamagePower);
                    }
                    else
                    {
                        DamageManager damageManager = collision.collider.GetComponent<DamageManager>();
                        if (damageManager != null)
                        {
                            damageManager.ApplyDirectDamage(DamagePower); // also damages its building, if any
                        }
                        else
                        {
                            HouseTarget.HitPiece(collision.collider, DamagePower);
                        }
                    }
                }
                Destroy(gameObject);
            }
        }
    }
}
