using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Makes Mission Low's world 3x bigger in every direction: the original Environment tile stays in
// the middle and 8 tiles are added around it (4 sides + 4 corners).
// Each added tile is a MIRRORED copy (flipped in X, Z or both) so its terrain edge matches the
// neighbouring edge exactly — no cliffs or gaps at the seams. Trees and rocks are mirrored with it.
// Safe to re-run: the "EnvironmentExtended" root and the mirrored terrain assets are rebuilt.
public static class MissionLowEnvironmentExpand
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    const string AssetFolder = "Assets/AirplaneControllerwithShooting/Models/ExtendedTerrain";
    const string RootName = "EnvironmentExtended";
    // Children of Environment copied into each tile (not the light / airport road)
    static readonly string[] CopiedGroups = { "Trees", "Rocks" };

    [MenuItem("Tools/AirStrike/Expand Mission Low Environment (3x3)")]
    public static void RunFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Debug.Log("[MissionLowEnvironmentExpand]\n" + Build(SceneManager.GetActiveScene()));
    }

    public static string Build(Scene scene)
    {
        var log = new StringBuilder();

        Transform environment = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Environment") environment = root.transform;
        Terrain terrain = environment != null ? environment.GetComponentInChildren<Terrain>(true) : null;
        if (terrain == null)
        {
            log.AppendLine("ERROR: Environment/Terrain not found.");
            return log.ToString();
        }

        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;
        Vector3 origin = terrain.transform.position; // corner of the original tile
        log.AppendLine($"Original terrain: {size.x}x{size.z} at {origin}");
        LogSeams(data, log);

        // Rebuild
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == RootName) Object.DestroyImmediate(root);

        var extended = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(extended, scene);
        extended.tag = environment.gameObject.tag;
        extended.layer = environment.gameObject.layer;

        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(AssetFolder).Replace('\\', '/'), Path.GetFileName(AssetFolder));

        // One mirrored TerrainData per flip variant, shared by the tiles that need it
        var variants = new Dictionary<(bool, bool), TerrainData>
        {
            [(true, false)] = MakeMirroredData(data, true, false, "Terrain_MirrorX"),
            [(false, true)] = MakeMirroredData(data, false, true, "Terrain_MirrorZ"),
            [(true, true)] = MakeMirroredData(data, true, true, "Terrain_MirrorXZ"),
        };

        var groups = new List<Transform>();
        foreach (string name in CopiedGroups)
        {
            Transform g = environment.Find(name);
            if (g != null) groups.Add(g); else log.AppendLine($"NOTE: Environment/{name} not found, skipped.");
        }

        var grid = new Dictionary<(int, int), Terrain> { [(0, 0)] = terrain };
        int objects = 0;

        for (int ix = -1; ix <= 1; ix++)
        {
            for (int iz = -1; iz <= 1; iz++)
            {
                if (ix == 0 && iz == 0) continue;
                bool flipX = ix != 0, flipZ = iz != 0;

                var tile = new GameObject($"Tile_{Side(ix, iz)}");
                tile.transform.SetParent(extended.transform, false);
                tile.tag = extended.tag;
                tile.layer = extended.layer;

                // Terrain
                TerrainData tileData = variants[(flipX, flipZ)];
                GameObject tGO = Terrain.CreateTerrainGameObject(tileData);
                tGO.name = "Terrain";
                tGO.transform.SetParent(tile.transform, false);
                tGO.transform.position = origin + new Vector3(ix * size.x, 0f, iz * size.z);
                tGO.tag = terrain.gameObject.tag;
                tGO.layer = terrain.gameObject.layer;
                GameObjectUtility.SetStaticEditorFlags(tGO, GameObjectUtility.GetStaticEditorFlags(terrain.gameObject));
                Terrain t = tGO.GetComponent<Terrain>();
                CopyTerrainSettings(terrain, t);
                grid[(ix, iz)] = t;

                // Trees / rocks, mirrored across the shared edge so they sit on the mirrored ground
                foreach (Transform group in groups)
                {
                    var copyGroup = new GameObject(group.name).transform;
                    copyGroup.SetParent(tile.transform, false);
                    foreach (Transform item in group)
                    {
                        GameObject copy = Object.Instantiate(item.gameObject, copyGroup);
                        copy.name = item.name;
                        copy.transform.position = MirrorPosition(item.position, origin, size, ix, iz);
                        copy.transform.rotation = MirrorRotation(item.rotation, flipX, flipZ);
                        objects++;
                    }
                }
            }
        }

        // Tell neighbouring terrains about each other (seamless LOD at the joins)
        foreach (var kv in grid)
        {
            (int x, int z) = kv.Key;
            grid.TryGetValue((x - 1, z), out Terrain left);
            grid.TryGetValue((x, z + 1), out Terrain top);
            grid.TryGetValue((x + 1, z), out Terrain right);
            grid.TryGetValue((x, z - 1), out Terrain bottom);
            kv.Value.SetNeighbors(left, top, right, bottom);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        log.AppendLine($"Added 8 tiles -> world is now {size.x * 3}x{size.z * 3}; copied {objects} trees/rocks.");
        return log.ToString();
    }

    static string Side(int ix, int iz)
    {
        string ns = iz > 0 ? "North" : iz < 0 ? "South" : "";
        string ew = ix > 0 ? "East" : ix < 0 ? "West" : "";
        return ns + ew;
    }

    // Reflect across the edge shared with the original tile (x = origin.x + size.x for the east tile, etc.)
    static Vector3 MirrorPosition(Vector3 p, Vector3 origin, Vector3 size, int ix, int iz)
    {
        float x = p.x, z = p.z;
        if (ix != 0)
        {
            float edge = ix > 0 ? origin.x + size.x : origin.x;
            x = 2f * edge - p.x;
        }
        if (iz != 0)
        {
            float edge = iz > 0 ? origin.z + size.z : origin.z;
            z = 2f * edge - p.z;
        }
        return new Vector3(x, p.y, z);
    }

    static Quaternion MirrorRotation(Quaternion r, bool flipX, bool flipZ)
    {
        Vector3 e = r.eulerAngles;
        float yaw = e.y;
        if (flipX) yaw = -yaw;
        if (flipZ) yaw = 180f - yaw;
        return Quaternion.Euler(e.x, yaw, e.z);
    }

    static void CopyTerrainSettings(Terrain from, Terrain to)
    {
        to.materialTemplate = from.materialTemplate;
        to.heightmapPixelError = from.heightmapPixelError;
        to.basemapDistance = from.basemapDistance;
        to.shadowCastingMode = from.shadowCastingMode;
        to.drawTreesAndFoliage = from.drawTreesAndFoliage;
        to.detailObjectDistance = from.detailObjectDistance;
        to.detailObjectDensity = from.detailObjectDensity;
        to.treeDistance = from.treeDistance;
        to.groupingID = from.groupingID;
        to.allowAutoConnect = from.allowAutoConnect;
        var fromCol = from.GetComponent<TerrainCollider>();
        var toCol = to.GetComponent<TerrainCollider>();
        if (fromCol != null && toCol != null) toCol.sharedMaterial = fromCol.sharedMaterial;
    }

    // Clone the terrain asset and flip heights, textures, details and terrain trees
    static TerrainData MakeMirroredData(TerrainData source, bool flipX, bool flipZ, string name)
    {
        TerrainData d = Object.Instantiate(source);
        d.name = name;

        int hr = source.heightmapResolution;
        d.SetHeights(0, 0, Flip(source.GetHeights(0, 0, hr, hr), flipX, flipZ));

        if (source.alphamapLayers > 0)
        {
            int ar = source.alphamapResolution;
            float[,,] a = source.GetAlphamaps(0, 0, ar, ar);
            d.SetAlphamaps(0, 0, Flip3(a, flipX, flipZ));
        }

        for (int layer = 0; layer < source.detailPrototypes.Length; layer++)
        {
            int dr = source.detailResolution;
            d.SetDetailLayer(0, 0, layer, FlipInt(source.GetDetailLayer(0, 0, dr, dr, layer), flipX, flipZ));
        }

        TreeInstance[] trees = source.treeInstances;
        for (int i = 0; i < trees.Length; i++)
        {
            Vector3 p = trees[i].position; // normalized 0..1
            if (flipX) p.x = 1f - p.x;
            if (flipZ) p.z = 1f - p.z;
            trees[i].position = p;
        }
        d.treeInstances = trees;

        string path = $"{AssetFolder}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(d, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<TerrainData>(path);
    }

    // Heightmap arrays are [z, x]
    static float[,] Flip(float[,] src, bool flipX, bool flipZ)
    {
        int h = src.GetLength(0), w = src.GetLength(1);
        var dst = new float[h, w];
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
                dst[z, x] = src[flipZ ? h - 1 - z : z, flipX ? w - 1 - x : x];
        return dst;
    }

    static float[,,] Flip3(float[,,] src, bool flipX, bool flipZ)
    {
        int h = src.GetLength(0), w = src.GetLength(1), l = src.GetLength(2);
        var dst = new float[h, w, l];
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
                for (int k = 0; k < l; k++)
                    dst[z, x, k] = src[flipZ ? h - 1 - z : z, flipX ? w - 1 - x : x, k];
        return dst;
    }

    static int[,] FlipInt(int[,] src, bool flipX, bool flipZ)
    {
        int h = src.GetLength(0), w = src.GetLength(1);
        var dst = new int[h, w];
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
                dst[z, x] = src[flipZ ? h - 1 - z : z, flipX ? w - 1 - x : x];
        return dst;
    }

    // How different the opposite edges are — explains why plain copies would show seams
    static void LogSeams(TerrainData d, StringBuilder log)
    {
        int r = d.heightmapResolution;
        float[,] h = d.GetHeights(0, 0, r, r);
        float maxX = 0f, maxZ = 0f, maxH = 0f;
        for (int i = 0; i < r; i++)
        {
            maxX = Mathf.Max(maxX, Mathf.Abs(h[i, 0] - h[i, r - 1]));
            maxZ = Mathf.Max(maxZ, Mathf.Abs(h[0, i] - h[r - 1, i]));
            for (int j = 0; j < r; j++) maxH = Mathf.Max(maxH, h[i, j]);
        }
        log.AppendLine($"Terrain max height {maxH * d.size.y:0.#}m; opposite-edge mismatch X {maxX * d.size.y:0.#}m, Z {maxZ * d.size.y:0.#}m (mirroring avoids these seams).");
    }
}
