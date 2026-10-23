using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitManager : MonoBehaviour
{

    public static UnitManager Instance;
    private List<ScriptableUnit> unitList;

    public BasePlayer selectedPlayer;

    [SerializeField] private Sprite arrowSprite; // optional, a triangle is generated when empty
    [SerializeField] private float moveSpeed = 8f;

    private static readonly Vector2[] moveDirections =
    {
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, -1),
        new Vector2(0, -1), new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1),
    };

    private readonly List<MoveArrow> arrows = new List<MoveArrow>();
    private bool isMoving;

    private void Awake()
    {
        Instance = this;

        unitList = Resources.LoadAll<ScriptableUnit>("Units").ToList();
    }


    public void SpawnPlayers() {

        var level = GridManager.Instance.Level;
        if (level != null)
        {
            SpawnLevelUnits<BasePlayer>(level);
            GameManager.Instance.ChangeState(GameState.SpawnEnemyUnit);
            return;
        }

        var playerCount = 1;

        for (int i = 0; i < playerCount; i++)
        {
            var randomPrefab = GetRandomUnit<BasePlayer>(Faction.Player);
            var spawnedPlayer = Instantiate(randomPrefab);
            var randomSpawnTile = GridManager.Instance.GetPlayerHalfSpawnTile();

            randomSpawnTile.SetUnit(spawnedPlayer);

        }

        GameManager.Instance.ChangeState(GameState.SpawnEnemyUnit);
    }


    public void SpawnEnemies()
    {
        var level = GridManager.Instance.Level;
        if (level != null)
        {
            SpawnLevelUnits<BaseEnemy>(level);
            GameManager.Instance.ChangeState(GameState.PlayerTurn);
            return;
        }

        var enemyCount = 2;

        for (int i = 0; i < enemyCount; i++)
        {
            var randomPrefab = GetRandomUnit<BaseEnemy>(Faction.Enemy);
            var spawnedEnemy = Instantiate(randomPrefab);
            var randomSpawnTile = GridManager.Instance.GetEnemyHalfSpawnTile();

            randomSpawnTile.SetUnit(spawnedEnemy);

        }

        GameManager.Instance.ChangeState(GameState.PlayerTurn);

    }


    // Spawns the level's placed units of type T (players or enemies) on their tiles
    private void SpawnLevelUnits<T>(LevelData level) where T : BaseUnit {

        foreach (var placement in level.units)
        {
            if (placement.unit == null || !(placement.unit.UnitPrefab is T)) continue;

            var tile = GridManager.Instance.GetTileAtPosition(placement.position);
            if (tile == null || tile.occupiedUnit != null)
            {
                Debug.LogWarning($"{level.name}: can't place {placement.unit.name} at {placement.position}, the tile is missing or taken.");
                continue;
            }

            var spawnedUnit = Instantiate(placement.unit.UnitPrefab);

            // Must happen before the enemy's Start, which is when it builds its walls
            if (placement.overrideBlockedTiles && spawnedUnit is BlockingEnemy blockingEnemy)
            {
                blockingEnemy.SetBlockedTileOffsets(placement.blockedTileOffsets);
            }

            tile.SetUnit(spawnedUnit);
        }

    }


    private T GetRandomUnit<T>(Faction faction) where T : BaseUnit{
    
        return (T)unitList.Where(u=> u.Faction == faction).OrderBy(o=>Random.value).First().UnitPrefab;
        
    }


    // Passing null, or the already selected player, clears the selection
    public void SetSelectedPlayer(BasePlayer player) {

        if (isMoving) return;

        var newSelection = player == selectedPlayer ? null : player;

        if (selectedPlayer != null) selectedPlayer.OccupiedTile.SetSelected(false);
        ClearArrows();

        selectedPlayer = newSelection;
        if (selectedPlayer == null) return;

        selectedPlayer.OccupiedTile.SetSelected(true);
        ShowArrows();

    }


    public void MoveSelectedPlayer(Vector2 direction) {

        if (selectedPlayer == null || isMoving) return;

        var player = selectedPlayer;
        var destination = GetMoveDestination(player.OccupiedTile, direction);

        player.OccupiedTile.SetSelected(false);
        ClearArrows();
        selectedPlayer = null;

        StartCoroutine(SlideToTile(player, destination));
        //GameManager.Instance.ChangeState(GameState.EnemyTurn);

    }


    private void ShowArrows() {

        var start = selectedPlayer.OccupiedTile;

        foreach (var direction in moveDirections)
        {
            // Only offer directions where the unit would actually move
            if (GetMoveDestination(start, direction) == start) continue;

            var position = (Vector2)start.transform.position + direction;
            arrows.Add(MoveArrow.Create(direction, position, arrowSprite, transform));
        }

    }


    private void ClearArrows() {

        foreach (var arrow in arrows) Destroy(arrow.gameObject);
        arrows.Clear();

    }


    // Slides in a straight line: stops at the board edge or before a blocking tile/friendly unit,
    // and stops on an enemy's tile to capture it
    private Tile GetMoveDestination(Tile start, Vector2 direction) {

        var current = start;

        while (true)
        {
            var nextPosition = (Vector2)current.transform.position + direction;
            var next = GridManager.Instance.GetTileAtPosition(new Vector2(Mathf.Round(nextPosition.x), Mathf.Round(nextPosition.y)));

            if (next == null) return current;

            if (next.occupiedUnit != null) return next.occupiedUnit is BaseEnemy ? next : current;

            if (!next.walkable) return current;

            current = next;
        }

    }


    private IEnumerator SlideToTile(BasePlayer player, Tile destination) {

        isMoving = true;

        var target = destination.transform.position;
        while ((player.transform.position - target).sqrMagnitude > 0.0001f)
        {
            player.transform.position = Vector3.MoveTowards(player.transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        if (destination.occupiedUnit is BaseEnemy capturedEnemy)
        {
            Destroy(capturedEnemy.gameObject);
        }

        destination.SetUnit(player);

        isMoving = false;

    }


}
