using System.Collections.Generic;
using UnityEngine;

// While alive, walls off tiles around itself; they act like the board edge.
// Capturing (destroying) this enemy frees every tile it blocked.
public class BlockingEnemy : BaseEnemy
{
    // Offsets from this enemy's tile, e.g. (1, 0) is the tile to its right
    [SerializeField] private Vector2Int[] blockedTileOffsets =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
    };
    [SerializeField] private Sprite wallSprite; // optional, a plain square is generated when empty
    [SerializeField] private Color wallColor = new Color(0.45f, 0.3f, 0.15f, 0.9f);

    private static Sprite generatedWallSprite;

    private readonly List<Tile> blockedTiles = new List<Tile>();

    public Vector2Int[] BlockedTileOffsets => blockedTileOffsets;

    // Per-level override, set by UnitManager before Start runs
    public void SetBlockedTileOffsets(Vector2Int[] offsets)
    {
        blockedTileOffsets = offsets;
    }


    // Runs after spawning, once Tile.SetUnit has given this enemy its tile
    private void Start()
    {
        ProjectWalls();
    }

    private void OnDestroy()
    {
        FreeWalls();
    }


    private void ProjectWalls()
    {
        if (OccupiedTile == null) return;

        var origin = OccupiedTile.transform.position;
        var sprite = wallSprite != null ? wallSprite : GetGeneratedWallSprite();

        foreach (var offset in blockedTileOffsets)
        {
            if (offset == Vector2Int.zero) continue;

            var tile = GridManager.Instance.GetTileAtPosition(new Vector2(Mathf.Round(origin.x) + offset.x, Mathf.Round(origin.y) + offset.y));
            if (tile == null) continue; // off the board

            tile.AddBlocker(this, sprite, wallColor);
            blockedTiles.Add(tile);
        }
    }

    private void FreeWalls()
    {
        foreach (var tile in blockedTiles)
        {
            // Tiles may already be gone when the scene unloads
            if (tile != null) tile.RemoveBlocker(this);
        }

        blockedTiles.Clear();
    }


    private static Sprite GetGeneratedWallSprite()
    {
        if (generatedWallSprite != null) return generatedWallSprite;

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        generatedWallSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        return generatedWallSprite;
    }
}
