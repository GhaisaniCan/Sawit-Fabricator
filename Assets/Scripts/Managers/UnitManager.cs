using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitManager : MonoBehaviour
{

    public static UnitManager Instance;
    private List<ScriptableUnit> unitList;

    // Raised once per finished slide (one "turn"); timed effects such as retracted vines count these
    public static event System.Action SlideFinished;

    public BasePlayer selectedPlayer;

    [SerializeField] private Sprite arrowSprite; // optional, a triangle is generated when empty
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("When on, the level is only lost if no gate or valve can be clicked either, since clicking one may open a new path.")]
    [SerializeField] private bool interactablesPreventLose = true;

    private static readonly Vector2[] moveDirections =
    {
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, -1),
        new Vector2(0, -1), new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1),
    };

    private readonly List<MoveArrow> arrows = new List<MoveArrow>();
    private readonly HashSet<CoreCell> activeCoreCells = new HashSet<CoreCell>();
    private bool isMoving;

    public bool IsBusy => isMoving;
    public int ActiveCoreCellCount => activeCoreCells.Count;

    private void Awake()
    {
        Instance = this;

        unitList = Resources.LoadAll<ScriptableUnit>("Units").ToList();
    }


    #region Spawning

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
            SpawnRandomUnit<BasePlayer>(GridManager.Instance.GetPlayerHalfSpawnTile());
        }

        GameManager.Instance.ChangeState(GameState.SpawnEnemyUnit);
    }


    public void SpawnEnemies()
    {
        var level = GridManager.Instance.Level;
        if (level != null)
        {
            SpawnLevelUnits<BaseEnemy>(level);
            SpawnLevelUnits<CoreCell>(level);
            GameManager.Instance.ChangeState(GameState.PlayerTurn);
            return;
        }

        var enemyCount = 2;

        for (int i = 0; i < enemyCount; i++)
        {
            SpawnRandomUnit<BaseEnemy>(GridManager.Instance.GetEnemyHalfSpawnTile());
        }

        GameManager.Instance.ChangeState(GameState.PlayerTurn);

    }


    // Spawns the level's placed units of type T (players, enemies or core cells) on their tiles
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


    // Random setup used when GridManager has no LevelData
    private void SpawnRandomUnit<T>(Tile spawnTile) where T : BaseUnit {

        var prefab = GetRandomUnit<T>();
        if (prefab == null || spawnTile == null)
        {
            Debug.LogWarning($"Couldn't spawn a random {typeof(T).Name}: no unit asset of that type or no free tile.");
            return;
        }

        spawnTile.SetUnit(Instantiate(prefab));

    }


    // Filtered by prefab type rather than the Faction field, which can be wrong on assets
    private T GetRandomUnit<T>() where T : BaseUnit {

        return unitList.Select(u => u.UnitPrefab).OfType<T>().OrderBy(o => Random.value).FirstOrDefault();

    }

    #endregion


    #region Core cells

    public void RegisterCoreCell(CoreCell coreCell) {

        activeCoreCells.Add(coreCell);

    }

    public void UnregisterCoreCell(CoreCell coreCell) {

        activeCoreCells.Remove(coreCell);

    }

    #endregion


    #region Selection & arrows

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

    public void ClearSelection() {

        if (selectedPlayer != null) selectedPlayer.OccupiedTile.SetSelected(false);
        ClearArrows();
        selectedPlayer = null;

    }


    // Called after a gate or valve is clicked: the reachable tiles may have changed
    public void OnBoardChanged() {

        if (selectedPlayer != null)
        {
            ClearArrows();
            ShowArrows();
        }

        ResolveOutcome();

    }


    private void ShowArrows() {

        var start = selectedPlayer.OccupiedTile;

        foreach (var direction in moveDirections)
        {
            // Only offer directions where the unit would actually move
            if (GetSlidePath(start, direction, out var endsInHazard).Count == 0) continue;

            // Moves into hazardous vines are still allowed, but their arrows are tinted as a warning
            var position = (Vector2)start.transform.position + direction;
            arrows.Add(MoveArrow.Create(direction, position, arrowSprite, transform, endsInHazard));
        }

    }


    private void ClearArrows() {

        foreach (var arrow in arrows) Destroy(arrow.gameObject);
        arrows.Clear();

    }

    #endregion


    #region Movement

    public void MoveSelectedPlayer(Vector2 direction) {

        if (selectedPlayer == null || isMoving) return;

        var player = selectedPlayer;
        var path = GetSlidePath(player.OccupiedTile, direction, out var endsInHazard);
        if (path.Count == 0) return;

        ClearSelection();

        StartCoroutine(SlideAlongPath(player, path, endsInHazard));

    }


    // Every tile the slide passes through, in order; the last one is where the unit ends up.
    // Empty when the unit can't move that way at all.
    //  - board edge, wall, closed gate, vine-blocked tile or friendly unit: stop before it
    //  - hazardous vines over open ground: enter the tile and be destroyed (endsInHazard)
    //  - enemy or Core Cell: stop on its tile and capture it
    //  - deflector valve: enter it, then continue in the valve's direction
    private List<Tile> GetSlidePath(Tile start, Vector2 direction, out bool endsInHazard) {

        var path = new List<Tile>();
        var current = start;
        endsInHazard = false;

        // Valves can point at each other; stop if we'd enter the same valve heading the same way twice
        var visitedValves = new HashSet<(Tile, Vector2)>();

        while (true)
        {
            var next = GetTileAt((Vector2)current.transform.position + direction);

            if (next == null) break;

            if (next.occupiedUnit != null)
            {
                if (IsCapturable(next.occupiedUnit)) path.Add(next);
                break;
            }

            // A hazardous vine over a wall still just acts as a wall
            if (next.isBlocked && next.IsHazardous && next.IsWalkableTerrain)
            {
                path.Add(next);
                endsInHazard = true;
                break;
            }

            if (!next.walkable) break;

            path.Add(next);
            current = next;

            if (next is DeflectorValveTile valve)
            {
                direction = valve.Deflect(direction);
                if (!visitedValves.Add((valve, direction))) break;
            }
        }

        return path;

    }


    private IEnumerator SlideAlongPath(BasePlayer player, List<Tile> path, bool endsInHazard) {

        isMoving = true;

        // Step through each tile so the unit visibly turns at valves
        foreach (var tile in path)
        {
            var target = tile.transform.position;
            while ((player.transform.position - target).sqrMagnitude > 0.0001f)
            {
                player.transform.position = Vector3.MoveTowards(player.transform.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        if (endsInHazard)
        {
            yield return DestroyPlayerInVines(player);
            yield break;
        }

        var destination = path[path.Count - 1];
        var capturedCoreCell = false;

        if (destination.occupiedUnit != null && destination.occupiedUnit != player)
        {
            capturedCoreCell = destination.occupiedUnit is CoreCell;
            Capture(destination.occupiedUnit);
        }

        destination.SetUnit(player);

        isMoving = false;

        // Win/lose first, so capturing the last Core Cell wins even if vines regrow this turn
        ResolveOutcome(capturedCoreCell);
        SlideFinished?.Invoke();

    }


    // The Cancer Cell slid into hazardous vines: shrink it away, then lose unless another Cancer Cell is left
    private IEnumerator DestroyPlayerInVines(BasePlayer player) {

        var startScale = player.transform.localScale;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            player.transform.localScale = startScale * (1f - t / 0.25f);
            yield return null;
        }

        if (player.OccupiedTile != null) player.OccupiedTile.occupiedUnit = null;
        Destroy(player.gameObject);

        isMoving = false;
        Debug.Log("The Cancer Cell was destroyed by hazardous vines.");

        var anotherPlayerLeft = GridManager.Instance.AllTiles.Any(t => t.occupiedUnit is BasePlayer);
        if (!anotherPlayerLeft)
        {
            GameManager.Instance.ChangeState(GameState.Lose);
            yield break;
        }

        ResolveOutcome();
        SlideFinished?.Invoke();

    }


    private void Capture(BaseUnit unit) {

        // Destroy is deferred to the end of the frame, so drop the core cell from the count right away
        if (unit is CoreCell coreCell) activeCoreCells.Remove(coreCell);

        // A captured BlockingEnemy frees its tiles and retracts its vines in its own OnDestroy
        Destroy(unit.gameObject);

    }


    private static bool IsCapturable(BaseUnit unit) {

        return unit is BaseEnemy || unit is CoreCell;

    }


    private static Tile GetTileAt(Vector2 position) {

        return GridManager.Instance.GetTileAtPosition(new Vector2(Mathf.Round(position.x), Mathf.Round(position.y)));

    }

    #endregion


    #region Win / lose

    // Win: the move just captured the last Core Cell.
    // Lose: Core Cells remain but no Cancer Cell can slide anywhere (and, if enabled, nothing can be clicked).
    private void ResolveOutcome(bool capturedCoreCell = false) {

        if (GameManager.Instance.gameState != GameState.PlayerTurn) return;

        if (capturedCoreCell && activeCoreCells.Count == 0)
        {
            GameManager.Instance.ChangeState(GameState.Win);
            return;
        }

        if (activeCoreCells.Count == 0) return;

        if (AnyPlayerCanMove()) return;
        if (interactablesPreventLose && GridManager.Instance.AllTiles.Any(t => t.CanInteract)) return;

        GameManager.Instance.ChangeState(GameState.Lose);

    }


    private bool AnyPlayerCanMove() {

        foreach (var tile in GridManager.Instance.AllTiles)
        {
            if (!(tile.occupiedUnit is BasePlayer)) continue;

            // A slide into hazardous vines doesn't count as a way out
            foreach (var direction in moveDirections)
            {
                if (GetSlidePath(tile, direction, out var endsInHazard).Count > 0 && !endsInHazard) return true;
            }
        }

        return false;

    }

    #endregion


}
