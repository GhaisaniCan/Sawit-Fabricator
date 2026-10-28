using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class GridManager : MonoBehaviour
{

    public static GridManager Instance;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private Tile tilePrefab;
    [SerializeField, FormerlySerializedAs("camera")] private Transform cam;

    // When set, the board and units come from this level; width, height and tilePrefab above are ignored
    [SerializeField] private LevelData level;
    public LevelData Level => level;

    // Optional Fabricator border drawn around the board. Must be a 9-sliced sprite whose
    // border region lies entirely outside the board (its inner edge hugs the tiles).
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private string frameSortingLayer = "TileRender";
    [SerializeField] private int frameSortingOrder = -5;
    private SpriteRenderer frame;

    // Board size in tiles; taken from the level once GenerateGrid has run
    public int Width => width;
    public int Height => height;


    private Dictionary<Vector2, Tile> tiles;

    public IEnumerable<Tile> AllTiles => tiles != null ? tiles.Values : Enumerable.Empty<Tile>();


    private void Awake()
    {
        Instance = this;
    }

    public void GenerateGrid() {
        tiles = new Dictionary<Vector2, Tile>();

        if (level != null)
        {
            width = level.width;
            height = level.height;
        }

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++)
            {
                // Cells with no tile type assigned fall back to tilePrefab
                var prefab = level != null && level.GetTilePrefab(x, y) != null ? level.GetTilePrefab(x, y) : tilePrefab;
                var spawnedTile = Instantiate(prefab, new Vector3(x, y), Quaternion.identity);
                spawnedTile.name = $"Tile {x} {y}";

                
                spawnedTile.init(x, y);

                tiles[new Vector2(x, y)] = spawnedTile;
            }
        }


        cam.position = new Vector3((float)width/2 - 0.5f, (float)height / 2 - 0.5f, -10);

        BuildFrame();

        GameManager.Instance.ChangeState(GameState.SpawnPlayerUnit);
    }


    private void BuildFrame()
    {
        if (frameSprite == null) return;

        if (frame == null)
        {
            frame = new GameObject("Fabricator Frame").AddComponent<SpriteRenderer>();
            frame.transform.SetParent(transform, false);
        }

        frame.sprite = frameSprite;
        frame.drawMode = SpriteDrawMode.Sliced;
        frame.sortingLayerName = frameSortingLayer;
        frame.sortingOrder = frameSortingOrder;

        // Left and right borders are equal (as are bottom and top), so the frame is the board plus one border on each side
        var border = frameSprite.border / frameSprite.pixelsPerUnit;
        frame.size = new Vector2(width + border.x + border.z, height + border.y + border.w);
        frame.transform.position = new Vector3((float)width / 2 - 0.5f, (float)height / 2 - 0.5f, 0);
    }

    public Tile GetPlayerHalfSpawnTile() {

        // Returns null when no walkable tile is left in this half
        return tiles.Where(t=>t.Key.x < width/2 && t.Value.walkable).OrderBy(t=>Random.value).Select(t=>t.Value).FirstOrDefault();


    }

    public Tile GetEnemyHalfSpawnTile()
    {

        // Returns null when no walkable tile is left in this half
        return tiles.Where(t => t.Key.x > width / 2 && t.Value.walkable).OrderBy(t => Random.value).Select(t => t.Value).FirstOrDefault();


    }


    public Tile GetTileAtPosition(Vector2 position) { 
    
        if (tiles.TryGetValue(position, out var tile)) { 
        
            return tile; 
        
        } return null;
        

    }

}
