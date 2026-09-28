using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AirplaneControllerwithShooting;

// Mode 3 (Open World): keeps a fixed number of balloons / tanks / buildings alive at random spawn
// points and respawns each one some seconds after it is destroyed. Scoring/counting is done by
// GameManagerMode2.AddScore, which the targets already call when they die.
public class OpenWorldManager : MonoBehaviour
{
    [System.Serializable]
    public class SpawnGroup
    {
        public string label = "Balloon";
        [Tooltip("Inactive target to clone (keeps its EnemyAI / DamageManager / HouseTarget / HUD marker setup).")]
        public GameObject template;
        [Tooltip("Children of this transform are the spawn points.")]
        public Transform spawnPointsRoot;
        public int maxAlive = 6;

        [System.NonSerialized] public List<Spawned> alive = new List<Spawned>();
    }

    public class Spawned
    {
        public GameObject go;
        public Transform point;
    }

    public SpawnGroup[] groups;
    public float respawnDelay = 12f;
    [Tooltip("Destroyed buildings are removed after this many seconds (their loose pieces fall first).")]
    public float wreckCleanupDelay = 10f;

    const float CheckInterval = 0.5f;
    float nextCheck;

    void Start()
    {
        foreach (SpawnGroup g in groups)
        {
            if (g.alive == null) g.alive = new List<Spawned>();
            for (int i = 0; i < g.maxAlive; i++)
                Spawn(g);
        }
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + CheckInterval;

        foreach (SpawnGroup g in groups)
        {
            for (int i = g.alive.Count - 1; i >= 0; i--)
            {
                Spawned s = g.alive[i];
                if (!IsDead(s.go)) continue;

                g.alive.RemoveAt(i);
                if (s.go != null && s.go.GetComponent<HouseTarget>() != null)
                    Destroy(s.go, wreckCleanupDelay); // EnemyAI targets clean themselves up

                StartCoroutine(RespawnAfterDelay(g));
            }
        }
    }

    IEnumerator RespawnAfterDelay(SpawnGroup g)
    {
        yield return new WaitForSeconds(respawnDelay);
        if (g.alive.Count < g.maxAlive) Spawn(g);
    }

    static bool IsDead(GameObject go)
    {
        if (go == null) return true;

        HouseTarget house = go.GetComponent<HouseTarget>();
        if (house != null) return house.IsDestroyed;

        EnemyAI ai = go.GetComponent<EnemyAI>();
        if (ai != null) return ai.Health <= 0;

        return false; // plain DamageManager targets are destroyed outright -> null above
    }

    void Spawn(SpawnGroup g)
    {
        if (g.template == null || g.spawnPointsRoot == null || g.spawnPointsRoot.childCount == 0)
        {
            Debug.LogWarning($"[OpenWorldManager] {g.label}: missing template or spawn points.");
            return;
        }

        Transform point = PickFreePoint(g);
        // Created under the (inactive) templates parent first, so it can be set up before it wakes up
        GameObject go = Instantiate(g.template, point.position, point.rotation, g.template.transform.parent);
        go.name = $"{g.label} (spawned)";
        KeepOffNavMeshUnitsStill(go);
        go.transform.SetParent(transform, true);
        go.SetActive(true);
        g.alive.Add(new Spawned { go = go, point = point });
    }

    // The NavMesh only covers the original central area. A unit spawned outside it can't use its
    // NavMeshAgent (errors) — it stays where it is and still aims and fires at the player.
    static void KeepOffNavMeshUnitsStill(GameObject go)
    {
        NavMeshAgent agent = go.GetComponentInChildren<NavMeshAgent>(true);
        if (agent == null) return;
        if (NavMesh.SamplePosition(go.transform.position, out _, 5f, NavMesh.AllAreas)) return;

        agent.enabled = false;
        foreach (EnemyAI ai in go.GetComponentsInChildren<EnemyAI>(true))
            ai.isPatrolling = false;
    }

    // A random spawn point nobody from this group is standing on (any point if all are taken)
    Transform PickFreePoint(SpawnGroup g)
    {
        var free = new List<Transform>();
        foreach (Transform p in g.spawnPointsRoot)
        {
            bool taken = false;
            foreach (Spawned s in g.alive)
                if (s.point == p) { taken = true; break; }
            if (!taken) free.Add(p);
        }

        if (free.Count == 0)
            return g.spawnPointsRoot.GetChild(Random.Range(0, g.spawnPointsRoot.childCount));
        return free[Random.Range(0, free.Count)];
    }
}
