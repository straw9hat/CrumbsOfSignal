using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap maskTilemap;      // Paint initial shape here in editor
    [SerializeField] private Tilemap groundTilemap;    // Expansion gets painted here
    [SerializeField] private Tilemap blockersTilemap;  // Optional: walls/obstacles to avoid

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
            Debug.LogError("[IsometricAreaExpander] Assign Mask and Ground tilemaps.");
            enabled = false;
            return;
        }

        // Build initial region from Mask and paint base fill
        var maskCells = GetMaskCells();
        if (maskCells.Count == 0)
        {
            Debug.LogWarning("[IsometricAreaExpander] No cells found in Mask. Did you paint the irregular shape?");
        }

        // Fill base area
        foreach (var c in maskCells)
        {
            groundTilemap.SetTile(c, baseFillTile);
            filled.Add(c);
        }

        // Compute initial frontier: neighbors of the base area that are not filled and not blocked
        var neigh = useEightNeighbors ? Off8 : Off4;
        foreach (var c in maskCells)
        {
            foreach (var d in neigh)
            {
                var n = c + d;
                if (filled.Contains(n)) continue;
                if (IsBlocked(n)) continue;
                frontier.Add(n);
            }
        }

        // Optional: refresh collider/composite here if you use TilemapCollider2D on ground
        groundTilemap.RefreshAllTiles();
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

        // We'll grow outward bandWidth steps, painting and updating the frontier as we go.
        var currentLayer = new HashSet<Vector3Int>(frontier);

        for (int step = 0; step < Mathf.Max(1, bandWidth); step++)
        {
            if (currentLayer.Count == 0) break;

            // Paint this layer
            foreach (var c in currentLayer)
            {
                groundTilemap.SetTile(c, ringTile);
                filled.Add(c);
            }

            // Build next layer from neighbors of this layer
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

        // New frontier is whatever remains just outside the band we painted
        frontier = currentLayer;

        groundTilemap.RefreshAllTiles();
        waveIndex++;
    }

    // ---------- Helpers ----------

    private bool IsBlocked(Vector3Int cell)
    {
        // Don’t paint over blockers; also skip if a ground tile already exists.
        if (blockersTilemap && blockersTilemap.HasTile(cell)) return true;
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
}
