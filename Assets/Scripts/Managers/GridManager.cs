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

        GameManager.Instance.ChangeState(GameState.SpawnPlayerUnit);
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
