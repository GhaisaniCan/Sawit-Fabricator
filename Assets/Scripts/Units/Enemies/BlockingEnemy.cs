using System;
using System.Collections.Generic;
using UnityEngine;

// Defensive Cell: a host node that grows vines onto the tiles at blockedTileOffsets.
// Tiles under its vines are blocked (like the board edge), or deadly if the vines are hazardous.
// Vines can be retracted without killing the host (SetVinesActive / RetractForTurns, e.g. by a ChemicalValveTile),
// and capturing the host retracts them for good.
public class BlockingEnemy : BaseEnemy
{
    // Raised whenever this enemy's vines extend, retract or tick down a timed retraction
    public static event Action<BlockingEnemy> VinesChanged;

    // Offsets from this enemy's tile, e.g. (1, 0) is the tile to its right
    [SerializeField] private Vector2Int[] blockedTileOffsets =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
    };

    [Tooltip("Chemical valves in ColorMatch mode only affect Defensive Cells of their colour. None = no marker, never colour-matched.")]
    [SerializeField] private ChemicalColor enemyColor = ChemicalColor.None;
    [SerializeField] private Sprite colorMarkerSprite; // optional, a soft dot is generated when empty

    [Header("Vines")]
    [Tooltip("When on, a Cancer Cell sliding into these vines is destroyed instead of stopping in front of them.")]
    [SerializeField] private bool hazardous;
    [SerializeField] private Sprite stemSprite;       // optional, thin bars are drawn when empty
    [SerializeField] private Sprite hazardStemSprite; // optional, used instead of stemSprite when hazardous
    [SerializeField] private Sprite knotSprite;
    [SerializeField] private Sprite tipSprite;
    [SerializeField] private Color vineColor = Color.white;
    [SerializeField, Min(0f)] private float growDuration = 0.25f;
    [SerializeField, Min(0f), Tooltip("Extra delay per step away from the host, so vines grow outward and retract inward")]
    private float growStagger = 0.06f;

    // Board position → tile the vines cover, and every vine piece (including the root on the host's tile)
    private readonly Dictionary<Vector2Int, Tile> blockedTiles = new Dictionary<Vector2Int, Tile>();
    private readonly Dictionary<Vector2Int, VineSegment> segments = new Dictionary<Vector2Int, VineSegment>();
    private int maxDepth;

    private bool vinesActive;
    private bool toggledOff;
    private int retractTurnsLeft;

    public Vector2Int[] BlockedTileOffsets => blockedTileOffsets;
    public bool IsHazardous => hazardous;
    public bool VinesActive => vinesActive;
    public int RetractTurnsLeft => retractTurnsLeft;
    public IReadOnlyDictionary<Vector2Int, VineSegment> Segments => segments;
    public ChemicalColor EnemyColor => enemyColor;

    // Tiles this cell's vines cover (whether or not they're currently out)
    public IEnumerable<Tile> CoveredTiles => blockedTiles.Values;

    // Per-level override, set by UnitManager before Start runs
    public void SetBlockedTileOffsets(Vector2Int[] offsets)
    {
        blockedTileOffsets = offsets;
    }

    // Per-level override, set by UnitManager before Start runs
    public void SetEnemyColor(ChemicalColor color)
    {
        enemyColor = color;
    }


    private void OnEnable()
    {
        UnitManager.SlideFinished += OnSlideFinished;
    }

    private void OnDisable()
    {
        UnitManager.SlideFinished -= OnSlideFinished;
    }

    // Runs after spawning, once Tile.SetUnit has given this enemy its tile
    private void Start()
    {
        CreateColorMarker();
        BuildVines();
        ApplyVineState();
    }

    // Small coloured dot on the cell so players can see which colour-coded valve affects it
    private void CreateColorMarker()
    {
        if (enemyColor == ChemicalColor.None) return;

        var marker = new GameObject("Chemical Marker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0.3f, 0.3f, -0.01f);
        marker.transform.localScale = Vector3.one * 0.3f;

        var markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = colorMarkerSprite != null ? colorMarkerSprite : GeneratedSprites.SoftDot;
        markerRenderer.color = enemyColor.ToColor();
        markerRenderer.sortingLayerName = "Units";
        markerRenderer.sortingOrder = 1; // just above the cell itself
    }

    // Captured (or unloaded): free every tile and pull the vines back in for good
    private void OnDestroy()
    {
        foreach (var tile in blockedTiles.Values)
        {
            // Tiles may already be gone when the scene unloads
            if (tile != null) tile.RemoveBlocker(this);
        }

        foreach (var segment in segments.Values)
        {
            if (segment != null) segment.Retract(RetractDelay(segment), growDuration, destroyAfter: true);
        }

        blockedTiles.Clear();
        segments.Clear();
    }


    #region Vine state

    // Retracts (false) or extends (true) the vines without destroying the host. Extending also cancels a timed retraction.
    public void SetVinesActive(bool active)
    {
        toggledOff = !active;
        if (active) retractTurnsLeft = 0;
        ApplyVineState();
    }

    // Retracts the vines until the Cancer Cell has finished this many more slides
    public void RetractForTurns(int turns)
    {
        retractTurnsLeft = Mathf.Max(retractTurnsLeft, turns);
        ApplyVineState();
        VinesChanged?.Invoke(this);
    }

    private void OnSlideFinished()
    {
        if (retractTurnsLeft == 0) return;

        retractTurnsLeft--;
        if (retractTurnsLeft == 0 && ApplyVineState())
        {
            // The vines grew back on their own, so arrows and win/lose need refreshing
            UnitManager.Instance.OnBoardChanged();
        }
        VinesChanged?.Invoke(this);
    }

    // Brings tiles and visuals in line with the current state; returns true if the vines changed
    private bool ApplyVineState()
    {
        var shouldBeActive = !toggledOff && retractTurnsLeft == 0;
        if (shouldBeActive == vinesActive) return false;

        vinesActive = shouldBeActive;

        foreach (var tile in blockedTiles.Values)
        {
            if (vinesActive) tile.AddBlocker(this);
            else tile.RemoveBlocker(this);
        }

        foreach (var segment in segments.Values)
        {
            if (vinesActive) segment.Grow(segment.Depth * growStagger, growDuration);
            else segment.Retract(RetractDelay(segment), growDuration, destroyAfter: false);
        }

        VinesChanged?.Invoke(this);
        return true;
    }

    private float RetractDelay(VineSegment segment)
    {
        return (maxDepth - segment.Depth) * growStagger;
    }

    #endregion


    #region Building the vine network

    private const int StraightStepCost = 2;
    private const int DiagonalStepCost = 5;

    private static readonly Vector2Int[] neighbourSteps =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(-1, -1), new Vector2Int(1, -1),
    };

    private void BuildVines()
    {
        if (OccupiedTile == null) return;

        var host = RoundToBoard(OccupiedTile.transform.position);

        foreach (var offset in blockedTileOffsets)
        {
            if (offset == Vector2Int.zero) continue;

            var position = host + offset;
            var tile = GridManager.Instance.GetTileAtPosition(position);
            if (tile != null) blockedTiles[position] = tile; // off-board offsets are skipped
        }

        // Cheapest-path search from the host through covered tiles gives every tile a parent closer to the host.
        // A diagonal step costs more than two straight ones, so vines only cut corners when there's no straight route.
        var parents = new Dictionary<Vector2Int, Vector2Int>();
        var depths = new Dictionary<Vector2Int, int> { [host] = 0 };
        var costs = new Dictionary<Vector2Int, int> { [host] = 0 };
        var open = new List<Vector2Int> { host };

        while (open.Count > 0)
        {
            // The sets are a handful of tiles, so a linear scan for the cheapest is fine
            var current = open[0];
            foreach (var candidate in open)
            {
                if (costs[candidate] < costs[current]) current = candidate;
            }
            open.Remove(current);

            foreach (var step in neighbourSteps)
            {
                var next = current + step;
                if (!blockedTiles.ContainsKey(next)) continue;

                var cost = costs[current] + (step.x != 0 && step.y != 0 ? DiagonalStepCost : StraightStepCost);
                if (costs.TryGetValue(next, out var known) && known <= cost) continue;

                costs[next] = cost;
                parents[next] = current;
                depths[next] = depths[current] + 1;
                if (!open.Contains(next)) open.Add(next);
            }
        }

        // Offsets that don't touch the rest of the vine still get a stub pointing back toward the host
        foreach (var position in blockedTiles.Keys)
        {
            if (depths.ContainsKey(position)) continue;

            var towardHost = host - position;
            parents[position] = position + new Vector2Int(Math.Sign(towardHost.x), Math.Sign(towardHost.y));
            depths[position] = Mathf.Max(Mathf.Abs(towardHost.x), Mathf.Abs(towardHost.y));
        }

        var children = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var pair in parents)
        {
            if (!children.TryGetValue(pair.Value, out var list)) children[pair.Value] = list = new List<Vector2Int>();
            list.Add(pair.Key - pair.Value);
        }

        var visuals = new VineVisuals
        {
            stem = hazardous && hazardStemSprite != null ? hazardStemSprite : stemSprite,
            knot = knotSprite,
            tip = tipSprite,
            color = vineColor,
            sortingLayerId = SortingLayer.NameToID("TileRender"),
            sortingOrder = 1, // TileRender 1: above the base tile, below interactive tile visuals
        };

        // The root on the host's tile draws the stems leaving the host (under the host's own sprite)
        if (children.TryGetValue(host, out var rootChildren))
        {
            segments[host] = VineSegment.Create(OccupiedTile, host, Vector2Int.zero, rootChildren, 0, visuals);
        }

        foreach (var pair in blockedTiles)
        {
            var position = pair.Key;
            var childDirections = children.TryGetValue(position, out var list) ? list : new List<Vector2Int>();
            segments[position] = VineSegment.Create(pair.Value, position, parents[position] - position, childDirections, depths[position], visuals);
            maxDepth = Mathf.Max(maxDepth, depths[position]);
        }
    }

    private static Vector2Int RoundToBoard(Vector3 position)
    {
        return new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
    }

    #endregion
}
