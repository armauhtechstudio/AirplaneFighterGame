/// <summary>
/// Spawner. this scripts just a spawner object.
/// </summary>
using UnityEngine;
using System.Collections;

public class Spawner : MonoBehaviour
{
	private Transform Objectman = null;// object to spawn 
	private float timeSpawn = 0;
	private int timeSpawnMax = 0;
	private float enemyCount = 0;
	public int radian = 0;

	private void Start ()
	{
		if (GetComponent<Renderer>())
			GetComponent<Renderer>().enabled = false;

	}

	private void Update ()
	{
		// find the spawned objects
		GameObject[] gos = GameObject.FindGameObjectsWithTag (Objectman.tag);
		timeSpawn += 1;
		if (gos.Length < enemyCount) {
			int timespawnmax = timeSpawnMax;
			if (timespawnmax <= 0) {
				timespawnmax = 10;
			}
            if (timeSpawn >= timespawnmax)
            {
                // Assuming Objectman is a Transform, get its corresponding GameObject.
                GameObject objectmanPrefab = Objectman.gameObject;

                // Instantiate a new GameObject (enemy) based on the prefab.
                GameObject enemyCreated = Instantiate(
                    objectmanPrefab,
                    transform.position + new Vector3(Random.Range(-radian, radian), 20, Random.Range(-radian, radian)),
                    Quaternion.identity
                );

                // Set the scale of the new enemy.
                enemyCreated.transform.localScale = new Vector3(
                    Random.Range(5, 20),
                    enemyCreated.transform.localScale.y,
                    enemyCreated.transform.localScale.z
                );

                // Reset the timeSpawn variable to 0, indicating that a new enemy has been spawned.
                timeSpawn = 0;
            }

        }

    }

}
