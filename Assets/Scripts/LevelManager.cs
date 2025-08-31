using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    [System.Serializable]
    public class TileEnemySet
    {
        public TileBase tile;            // the ground tile this enemy type belongs to
        public GameObject[] prefabs;     // one or more variations of that enemy for this tile
    }

    [System.Serializable]
    public class PropVariant
    {
        public GameObject prefab;
        [Min(0.0001f)] public float weight = 1f;   // weighted random pick
    }

    [System.Serializable]
    public class EnvPropRule
    {
        public TileBase tile;                       // which ground tile this rule applies to
        [Tooltip("Rough density: props per 100 painted cells")]
        public float densityPer100 = 5f;            // e.g., 5 props per 100 cells
        public PropVariant[] variants;              // one is chosen by weight
        [Header("Jitter & Style")]
        public Vector2 positionJitter = new Vector2(0.15f, 0.05f);
        public Vector2 uniformScaleRange = new Vector2(0.95f, 1.05f);
        public bool randomFlipX = true;
    }

    [Header("Environment Props")]
    [SerializeField] private EnvPropRule[] envPropRules;     // per-tile rules (3 entries for your ring tiles, etc.)
    [SerializeField] private GameObject[] defaultEnvProps;   // used when no rule matches a tile
    [SerializeField] private float defaultDensityPer100 = 3f;
    [SerializeField] private Transform propsContainer;       // optional parent for hierarchy tidy
    [SerializeField] private float propMinSeparation = 0.6f; // keep props from overlapping
    [SerializeField] private LayerMask propOverlapMask;      // layers considered “occupied” (e.g., Props, Walls)
    [SerializeField] private bool spawnOnInitialFill = false; // set true if you also want props on the base area

    [Header("Enemy Spawning")]
    [SerializeField] private GameObject[] commonEnemyPrefabs;   // 2 prefabs that can spawn anywhere
    [SerializeField] private TileEnemySet[] tileEnemySets;      // 3 entries, each bound to a tile type
    [SerializeField] private float minSpawnDistanceFromPlayer = 2.5f;
    [SerializeField] private float minSeparationBetweenSpawns = 1.25f;
    [SerializeField] private int maxSpawnTriesPerUnit = 30;
    [SerializeField] private Transform enemiesContainer;        // optional parent for hierarchy tidy

    public int CurrentLevelIndex => waveIndex + 1; // i = 1,2,3... (we increment waveIndex after expansion)

    [Header("Tilemaps")]
    [SerializeField] private Tilemap maskTilemap;     // initial irregular shape
    [SerializeField] private Tilemap groundTilemap;   // we paint here

    [Header("Spawning")]
    [SerializeField] private GameObject weaponPickupPrefab;   // prefab with WeaponPickup.cs
    [SerializeField] private WeaponDefinition[] spawnableWeapons;
    [SerializeField] private int defaultWeaponCount = 3;

    private bool initialized;

    [SerializeField] private StateManager m_stateManager;


    [Header("Tiles")]
    [SerializeField] private TileBase baseFillTile;    // Tile for the initial irregular area
    [SerializeField] private TileBase[] ringTiles;     // Tiles for each wave ring (colors/variants)

    [Header("Expansion Settings")]
    [Tooltip("Thickness of each wave band in cells (e.g., 10–15).")]
    [SerializeField] private int bandWidth = 12;
    [Tooltip("Use 8-neighbor connectivity (includes diagonals). 4-neighbor is more squared.")]
    [SerializeField] private bool useEightNeighbors = true;

    [Header("Bounds Scan (startup)")]
    [Tooltip("Optional bounds to scan for initial mask. If zero, use maskTilemap.cellBounds.")]
    [SerializeField] private Vector3Int scanMin; // inclusive
    [SerializeField] private Vector3Int scanMax; // inclusive

    // Internal state
    private HashSet<Vector3Int> filled = new();    // All cells we have already filled on ground
    private HashSet<Vector3Int> frontier = new();  // Outer boundary cells for next expansion
    private int waveIndex = 0;

    // Neighbor offsets in grid space (isometric layout still uses integer XY neighbors)
    private static readonly Vector3Int[] Off4 = new[]
    {
        new Vector3Int( 1,  0, 0),
        new Vector3Int(-1,  0, 0),
        new Vector3Int( 0,  1, 0),
        new Vector3Int( 0, -1, 0),
    };

    private static readonly Vector3Int[] Off8 = new[]
    {
        new Vector3Int( 1,  0, 0),
        new Vector3Int(-1,  0, 0),
        new Vector3Int( 0,  1, 0),
        new Vector3Int( 0, -1, 0),
        new Vector3Int( 1,  1, 0),
        new Vector3Int( 1, -1, 0),
        new Vector3Int(-1,  1, 0),
        new Vector3Int(-1, -1, 0),
    };

    private TileBase CurrentRingTile => (ringTiles != null && ringTiles.Length > 0)
        ? ringTiles[waveIndex % ringTiles.Length]
        : baseFillTile;

    private void Awake()
    {
        if (!maskTilemap || !groundTilemap)
        {
            Debug.LogError("[LevelManager] Assign Mask and Ground tilemaps.");
            enabled = false;
            return;
        }
    }

    /// <summary>
    /// Call this once per wave to add a new ring (bandWidth thick) around the current frontier.
    /// </summary>
    public void ExpandOneWave()
    {
        if (frontier.Count == 0)
        {
            Debug.Log("[IsometricAreaExpander] No more space to expand (frontier empty).");
            return;
        }

        var ringTile = CurrentRingTile;
        var neigh = useEightNeighbors ? Off8 : Off4;

        var currentLayer = new HashSet<Vector3Int>(frontier);
        var paintedBand = new List<Vector3Int>(); // <— collect newly painted cells

        for (int step = 0; step < Mathf.Max(1, bandWidth); step++)
        {
            if (currentLayer.Count == 0) break;

            foreach (var c in currentLayer)
            {
                groundTilemap.SetTile(c, ringTile);
                filled.Add(c);
                paintedBand.Add(c); // <— track
            }

            var nextLayer = new HashSet<Vector3Int>();
            foreach (var c in currentLayer)
            {
                foreach (var d in neigh)
                {
                    var n = c + d;
                    if (filled.Contains(n)) continue;
                    if (IsBlocked(n)) continue;
                    nextLayer.Add(n);
                }
            }

            currentLayer = nextLayer;
        }

        frontier = currentLayer;
        groundTilemap.RefreshAllTiles();

        // >>> NEW: spawn props only on this newly added band
        SpawnEnvPropsOnCells(paintedBand);

        waveIndex++;
    }

    // ---------- Helpers ----------

    private bool IsBlocked(Vector3Int cell)
    {
        // Don’t paint over blockers; also skip if a ground tile already exists.
        if (groundTilemap.HasTile(cell)) return true;
        return false;
    }

    private List<Vector3Int> GetMaskCells()
    {
        var cells = new List<Vector3Int>();

        BoundsInt bounds;
        if (scanMin == Vector3Int.zero && scanMax == Vector3Int.zero)
        {
            bounds = maskTilemap.cellBounds;
        }
        else
        {
            var min = new Vector3Int(
                Mathf.Min(scanMin.x, scanMax.x),
                Mathf.Min(scanMin.y, scanMax.y),
                0);
            var max = new Vector3Int(
                Mathf.Max(scanMin.x, scanMax.x),
                Mathf.Max(scanMin.y, scanMax.y),
                0);
            bounds = new BoundsInt(min, new Vector3Int(max.x - min.x + 1, max.y - min.y + 1, 1));
        }

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (maskTilemap.HasTile(pos))
                cells.Add(pos);
        }
        return cells;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.6f, 0.25f);
        foreach (var c in filled)
        {
            var center = groundTilemap.GetCellCenterWorld(c);
            Gizmos.DrawCube(center, Vector3.one * 0.1f);
        }

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.35f);
        foreach (var c in frontier)
        {
            var center = groundTilemap.GetCellCenterWorld(c);
            Gizmos.DrawCube(center, Vector3.one * 0.14f);
        }
    }
#endif

    // Weighted random from variants
    GameObject PickVariant(PropVariant[] variants)
    {
        if (variants == null || variants.Length == 0) return null;
        float sum = 0f;
        for (int i = 0; i < variants.Length; i++) sum += Mathf.Max(0.0001f, variants[i].weight);
        float r = Random.value * sum;
        for (int i = 0; i < variants.Length; i++)
        {
            r -= Mathf.Max(0.0001f, variants[i].weight);
            if (r <= 0f) return variants[i].prefab;
        }
        return variants[variants.Length - 1].prefab;
    }

    EnvPropRule FindRule(TileBase tile)
    {
        if (envPropRules == null) return null;
        for (int i = 0; i < envPropRules.Length; i++)
            if (envPropRules[i] != null && envPropRules[i].tile == tile)
                return envPropRules[i];
        return null;
    }

    bool ValidPropSpot(Vector3 pos)
    {
        // Don't place on top of something else
        var hits = Physics2D.OverlapCircleAll(pos, propMinSeparation, propOverlapMask);
        return hits == null || hits.Length == 0;
    }

    void PlaceProp(GameObject prefab, Vector3 worldPos, EnvPropRule rule)
    {
        if (!prefab) return;
        var parent = propsContainer ? propsContainer : transform;
        var go = Instantiate(prefab, worldPos, Quaternion.identity, parent);

        // small jitter/scale/flip for variety
        if (rule != null)
        {
            Vector2 j = rule.positionJitter;
            go.transform.position += (Vector3)new Vector2(Random.Range(-j.x, j.x), Random.Range(-j.y, j.y));

            float s = Random.Range(rule.uniformScaleRange.x, rule.uniformScaleRange.y);
            go.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

            if (rule.randomFlipX)
            {
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                if (sr) sr.flipX = (Random.value < 0.5f);
            }
        }

        // optional: add SortByY if props should depth-sort by Y
        if (!go.GetComponent<SortingGroup>() && !go.GetComponent<SortByY>())
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr) go.AddComponent<SortByY>();
        }
    }

    void SpawnEnvPropsOnCells(List<Vector3Int> bandCells)
    {
        if (bandCells == null || bandCells.Count == 0) return;

        // Group the just-painted cells by tile type
        var byTile = new Dictionary<TileBase, List<Vector3Int>>();
        foreach (var c in bandCells)
        {
            var t = groundTilemap.GetTile(c);
            if (!t) continue;
            if (!byTile.TryGetValue(t, out var list))
            {
                list = new List<Vector3Int>();
                byTile[t] = list;
            }
            list.Add(c);
        }

        // For each tile type, decide how many props, then place them
        foreach (var kv in byTile)
        {
            var tile = kv.Key;
            var cells = kv.Value;
            var rule = FindRule(tile);

            float density = rule != null ? rule.densityPer100 : defaultDensityPer100;
            int targetCount = Mathf.RoundToInt(Mathf.Clamp(density, 0f, 100f) / 100f * cells.Count);
            if (targetCount <= 0) continue;

            // Shuffle cells so placement looks organic
            for (int i = 0; i < cells.Count; i++)
            {
                int j = Random.Range(i, cells.Count);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }

            int spawned = 0;
            int tries = 0;
            int maxTries = targetCount * 40;

            while (spawned < targetCount && tries++ < maxTries)
            {
                var cell = cells[Random.Range(0, cells.Count)];
                Vector3 pos = groundTilemap.GetCellCenterWorld(cell);

                if (!ValidPropSpot(pos)) continue;

                GameObject prefab = null;
                if (rule != null)
                {
                    prefab = PickVariant(rule.variants);
                }
                if (!prefab && defaultEnvProps != null && defaultEnvProps.Length > 0)
                {
                    prefab = defaultEnvProps[Random.Range(0, defaultEnvProps.Length)];
                }

                if (!prefab) break;

                PlaceProp(prefab, pos, rule);
                spawned++;
            }
        }
    }


    // ---- call this from Idle state ENTER ----
    public void InitializeFromMaskOnce()
    {
        if (initialized) return;
        if (!maskTilemap || !groundTilemap || !baseFillTile)
        {
            Debug.LogError("[LevelManager] Assign mask/ground tilemaps and baseFillTile.");
            return;
        }

        // Copy mask into ground as base fill
        foreach (var cell in maskTilemap.cellBounds.allPositionsWithin)
        {
            if (!maskTilemap.HasTile(cell)) continue;
            groundTilemap.SetTile(cell, baseFillTile);
            filled.Add(cell);
        }
        groundTilemap.RefreshAllTiles();

        // Build initial frontier (neighbors around the base)
        var neigh = useEightNeighbors ? Off8 : Off4;
        foreach (var c in filled)
        {
            foreach (var d in neigh)
            {
                var n = c + d;
                if (filled.Contains(n)) continue;
                if (groundTilemap.HasTile(n)) continue;
                frontier.Add(n);
            }
        }

        if (spawnOnInitialFill)
        {
            // Collect all currently filled cells as a list and spawn props on them once
            var baseCells = new List<Vector3Int>(filled);
            SpawnEnvPropsOnCells(baseCells);
        }

        initialized = true;
    }

    public void SpawnWeaponsOnGround(int count = -1)
    {
        if (!initialized) InitializeFromMaskOnce();
        if (!weaponPickupPrefab || spawnableWeapons == null || spawnableWeapons.Length == 0) return;

        int toSpawn = (count > 0) ? count : defaultWeaponCount;
        var freeCells = new List<Vector3Int>(filled);

        // simple shuffle
        for (int i = 0; i < freeCells.Count; i++)
        {
            int j = Random.Range(i, freeCells.Count);
            (freeCells[i], freeCells[j]) = (freeCells[j], freeCells[i]);
        }

        int spawned = 0;
        foreach (var cell in freeCells)
        {
            var pos = groundTilemap.GetCellCenterWorld(cell);
            // sanity: don’t stack pickups—raycast for existing WeaponPickup nearby
            var hits = Physics2D.OverlapCircleAll(pos, 0.2f);
            bool occupied = false;
            foreach (var h in hits) if (h.GetComponentInParent<WeaponPickup>()) { occupied = true; break; }
            if (occupied) continue;

            var prefab = weaponPickupPrefab;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            // assign a random weapon def
            var wp = go.GetComponent<WeaponPickup>();
            if (wp && wp.weapon == null) wp.weapon = spawnableWeapons[Random.Range(0, spawnableWeapons.Length)];

            spawned++;
            if (spawned >= toSpawn) break;
        }
    }

    public void SpawnEnemiesForCombat(Transform player)
    {
        if (!initialized) InitializeFromMaskOnce();

        int i = Mathf.Max(1, CurrentLevelIndex);

        // ---- Common enemies: 2 + i (any ground cell) ----
        int commonCount = 2 + i;
        SpawnCommon(commonCount, player);

        // ---- Tile-specific: floor(i/3) + 1  (per tile type) ----
        int perTileCount = (i / 3) + 1;
        foreach (var set in tileEnemySets)
            SpawnOnTileType(set, perTileCount, player);
    }

    // ---------- helpers ----------
    void SpawnCommon(int count, Transform player)
    {
        if (commonEnemyPrefabs == null || commonEnemyPrefabs.Length == 0) return;
        if (filled == null || filled.Count == 0) return;

        var cells = new List<Vector3Int>(filled);
        Shuffle(cells);

        var taken = new List<Vector3>(); // ensure enemies aren't stacked
        int spawned = 0, tries = 0, maxTries = count * maxSpawnTriesPerUnit;

        while (spawned < count && tries++ < maxTries)
        {
            var cell = cells[Random.Range(0, cells.Count)];
            if (!groundTilemap.HasTile(cell)) continue;

            Vector3 pos = groundTilemap.GetCellCenterWorld(cell);
            if (!ValidSpawnPosition(pos, player, taken)) continue;

            var prefab = commonEnemyPrefabs[Random.Range(0, commonEnemyPrefabs.Length)];
            InstantiateEnemy(prefab, pos, ref taken);
            spawned++;
        }
    }

    void SpawnOnTileType(TileEnemySet set, int count, Transform player)
    {
        if (set == null || set.tile == null || set.prefabs == null || set.prefabs.Length == 0) return;

        // collect cells that are exactly this tile type
        var cells = new List<Vector3Int>();
        foreach (var c in filled)
            if (groundTilemap.GetTile(c) == set.tile)
                cells.Add(c);

        if (cells.Count == 0) return;
        Shuffle(cells);

        var taken = new List<Vector3>();
        int spawned = 0, tries = 0, maxTries = count * maxSpawnTriesPerUnit;

        while (spawned < count && tries++ < maxTries)
        {
            var cell = cells[Random.Range(0, cells.Count)];
            Vector3 pos = groundTilemap.GetCellCenterWorld(cell);

            if (!ValidSpawnPosition(pos, player, taken)) continue;

            var prefab = set.prefabs[Random.Range(0, set.prefabs.Length)];
            InstantiateEnemy(prefab, pos, ref taken);
            spawned++;
        }
    }

    bool ValidSpawnPosition(Vector3 pos, Transform player, List<Vector3> taken)
    {
        // keep away from player start
        if (player && Vector2.Distance(player.position, pos) < minSpawnDistanceFromPlayer) return false;

        // keep enemies separated
        foreach (var p in taken)
            if (Vector2.Distance(p, pos) < minSeparationBetweenSpawns) return false;

        // avoid placing right on colliders (small overlap check)
        var coll = Physics2D.OverlapCircleAll(pos, 0.25f);
        foreach (var c in coll)
            if (c.attachedRigidbody == null || c.attachedRigidbody.bodyType != RigidbodyType2D.Static)
                return false;

        return true;
    }

    void InstantiateEnemy(GameObject prefab, Vector3 pos, ref List<Vector3> taken)
    {
        var go = (enemiesContainer ? Instantiate(prefab, pos, Quaternion.identity, enemiesContainer)
                                   : Instantiate(prefab, pos, Quaternion.identity));
        taken.Add(pos);
    }

    // tiny util
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void EnterCombatMode()
    {
        StartCoroutine("IdleCoolDown");
    }

    IEnumerator IdleCoolDown()
    {
        yield return new WaitForSeconds(5f);
        GameStateCombatPhase combatPhase = new GameStateCombatPhase(m_stateManager);
        m_stateManager.SetNewState(combatPhase);
    }

    public void ManageCombatPhase()
    {
        StartCoroutine("CombatCoolDown");
    }
    IEnumerator CombatCoolDown()
    {
        yield return new WaitForSeconds(30f);
        ExpandOneWave();
        GameStateIdlePhase combatPhase = new GameStateIdlePhase(m_stateManager);
        m_stateManager.SetNewState(combatPhase);
    }


}
