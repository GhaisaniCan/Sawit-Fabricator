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

    // Energy (move limit). (currentMoves, maxMoves); maxMoves is 0 when the level has no move limit
    public static event System.Action<int, int> OnMovesChanged;
    // Raised whenever energy changes to a value at or below lowEnergyThreshold (but above 0)
    public static event System.Action<int> OnEnergyCritical;

    public BasePlayer selectedPlayer;

    [SerializeField] private Sprite arrowSprite; // optional, a triangle is generated when empty
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("When on, the level is only lost if no gate or valve can be clicked either, since clicking one may open a new path.")]
    [SerializeField] private bool interactablesPreventLose = true;

    [Tooltip("OnEnergyCritical fires when energy drops to this many moves or fewer")]
    [SerializeField, Min(1)] private int lowEnergyThreshold = 2;

    private static readonly Vector2[] moveDirections =
    {
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, -1),
        new Vector2(0, -1), new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1),
    };

    private readonly List<MoveArrow> arrows = new List<MoveArrow>();
    private readonly HashSet<CoreCell> activeCoreCells = new HashSet<CoreCell>();
    // In spawn order, which is also the order they step in within their group
    private readonly List<MovingCell> movingCells = new List<MovingCell>();
    private bool isMoving;

    // Energy is one pool shared by every Tech Bug on the board
    private bool movesLimited;
    private int currentMoves;
    private int maxMoves;

    public bool IsBusy => isMoving;
    public int ActiveCoreCellCount => activeCoreCells.Count;
    public bool MovesLimited => movesLimited;
    public int CurrentMoves => currentMoves;
    public int MaxMoves => maxMoves;

    private void Awake()
    {
        Instance = this;

        unitList = Resources.LoadAll<ScriptableUnit>("Units").ToList();
    }


    #region Spawning

    public void SpawnPlayers() {

        var level = GridManager.Instance.Level;

        // Before the players spawn, so their energy flashers see the starting value
        InitEnergy(level);

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
            SpawnLevelUnits<EnergyCell>(level);
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

            if (placement.overrideMovesRestored) spawnedUnit.SetMovesRestoredOnCapture(placement.movesRestoredOnCapture);

            // Must happen before the enemy's Start, which is when it builds its vines and colour marker
            if (spawnedUnit is BlockingEnemy blockingEnemy)
            {
                if (placement.overrideBlockedTiles) blockingEnemy.SetBlockedTileOffsets(placement.blockedTileOffsets);
                if (placement.chemicalColor != ChemicalColor.None) blockingEnemy.SetEnemyColor(placement.chemicalColor);
            }

            if (spawnedUnit is MovingCell movingCell && placement.overrideMoveSequence)
            {
                movingCell.SetMoveSequence(placement.moveSequence);
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

    public void RegisterMovingCell(MovingCell movingCell) {

        if (!movingCells.Contains(movingCell)) movingCells.Add(movingCell);

    }

    public void UnregisterMovingCell(MovingCell movingCell) {

        movingCells.Remove(movingCell);

    }

    #endregion


    #region Energy

    // Levels without LevelData, or with initialMoves 0, have unlimited moves
    private void InitEnergy(LevelData level) {

        movesLimited = level != null && level.HasMoveLimit;
        maxMoves = movesLimited ? Mathf.Max(level.maxMoves, level.initialMoves) : 0;
        SetMoves(movesLimited ? level.initialMoves : 0);

    }

    private void SetMoves(int value) {

        currentMoves = movesLimited ? Mathf.Clamp(value, 0, maxMoves) : 0;

        OnMovesChanged?.Invoke(currentMoves, maxMoves);
        if (movesLimited && currentMoves > 0 && currentMoves <= lowEnergyThreshold) OnEnergyCritical?.Invoke(currentMoves);

    }

    // One finished slide: pay 1 move, then add what the captured unit restores (capped at maxMoves)
    private void SpendMove(int movesRestored) {

        if (!movesLimited) return;
        SetMoves(currentMoves - 1 + movesRestored);

    }

    // A capture that isn't the Tech Bug's own slide (a passive Moving Cell stepping into it)
    private void RestoreMoves(int movesRestored) {

        if (!movesLimited || movesRestored <= 0) return;
        SetMoves(currentMoves + movesRestored);

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
    //  - hazardous vines over open ground, or a hostile Moving Cell: enter the tile and be destroyed (endsInHazard)
    //  - enemy or Core Cell: stop on its tile and capture it
    //  - stop tile (resin snare): enter it and stop there
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

            // Roots come first, even over a unit: a unit under roots can't be captured through them
            if (next.isBlocked)
            {
                // A hazardous vine over a wall still just acts as a wall
                if (next.IsHazardous && next.IsWalkableTerrain)
                {
                    path.Add(next);
                    endsInHazard = true;
                }
                break;
            }

            if (next.occupiedUnit is MovingCell movingCell && movingCell.IsHostile)
            {
                path.Add(next);
                endsInHazard = true;
                break;
            }

            if (next.occupiedUnit != null)
            {
                if (IsCapturable(next.occupiedUnit)) path.Add(next);
                break;
            }

            if (!next.walkable) break;

            path.Add(next);
            current = next;

            // Resin snare: stop on it
            if (next is StopTile) break;

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

        var capturedCoreCell = false;

        if (endsInHazard)
        {
            yield return DestroyPlayer(player);
            Debug.Log("The Tech Bug was destroyed by thorny roots or a hostile cell.");
        }
        else
        {
            var destination = path[path.Count - 1];
            var movesRestored = 0;

            if (destination.occupiedUnit != null && destination.occupiedUnit != player)
            {
                capturedCoreCell = destination.occupiedUnit is CoreCell;
                movesRestored = destination.occupiedUnit.MovesRestoredOnCapture;
                Capture(destination.occupiedUnit);
            }

            destination.SetUnit(player);
            SpendMove(movesRestored);
        }

        yield return FinishTurn(capturedCoreCell);

    }


    // After the Tech Bug's slide: win right away on the last Core Cell, otherwise let the Moving Cells
    // take their turn, then check win/lose and count the turn (SlideFinished)
    private IEnumerator FinishTurn(bool capturedCoreCell) {

        // Win/lose first, so capturing the last Core Cell wins even if vines regrow this turn
        // (and even if that move used the last of the energy)
        if (capturedCoreCell && activeCoreCells.Count == 0)
        {
            isMoving = false;
            ResolveOutcome(true);
            SlideFinished?.Invoke();
            yield break;
        }

        if (AnyPlayerLeft()) yield return MoveCells();

        isMoving = false;

        if (!AnyPlayerLeft())
        {
            GameManager.Instance.ChangeState(GameState.Lose);
            yield break;
        }

        ResolveOutcome();
        SlideFinished?.Invoke();

    }


    // Shrinks the Tech Bug away and removes it from the board (thorny roots or a hostile Moving Cell)
    private IEnumerator DestroyPlayer(BasePlayer player) {

        // Stop the idle breathing first; disabling it restores the cell's base scale for the shrink
        var idle = player.GetComponent<CellIdleAnimator>();
        if (idle != null) idle.enabled = false;

        var startScale = player.transform.localScale;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            player.transform.localScale = startScale * (1f - t / 0.25f);
            yield return null;
        }

        // A hostile cell may already have taken the tile
        if (player.OccupiedTile != null && player.OccupiedTile.occupiedUnit == player) player.OccupiedTile.occupiedUnit = null;
        Destroy(player.gameObject);

    }


    private static bool AnyPlayerLeft() {

        return GridManager.Instance.AllTiles.Any(t => t.occupiedUnit is BasePlayer);

    }


    // The enemy turn: every passive Moving Cell steps, then every hostile one
    private IEnumerator MoveCells() {

        movingCells.RemoveAll(c => c == null);
        if (movingCells.Count == 0) yield break;

        GameManager.Instance.ChangeState(GameState.EnemyTurn);

        yield return MoveCellGroup(false);
        if (AnyPlayerLeft()) yield return MoveCellGroup(true);

        if (GameManager.Instance.gameState == GameState.EnemyTurn) GameManager.Instance.ChangeState(GameState.PlayerTurn);

    }


    // Moves one group (passive or hostile) together. Steps are decided in spawn order, so a cell can move
    // into a tile another cell of the same group just left.
    private IEnumerator MoveCellGroup(bool hostile) {

        var group = movingCells.Where(c => c != null && c.IsHostile == hostile).ToList();

        var steps = new List<(MovingCell cell, Vector3 from, Vector3 to)>();
        var caughtCells = new List<MovingCell>();  // passive cells that stepped into a Tech Bug
        var victims = new List<BasePlayer>();      // Tech Bugs a hostile cell stepped into

        foreach (var cell in group)
        {
            // Only a cell still standing on its own tile acts (not one captured or removed this turn)
            if (cell == null || cell.OccupiedTile == null || cell.OccupiedTile.occupiedUnit != cell) continue;

            var step = cell.NextMove;
            cell.AdvanceSequence();
            if (step == Vector2Int.zero) continue;

            var from = cell.OccupiedTile;
            var target = GetTileAt((Vector2)from.transform.position + step);
            if (target == null) continue;

            var fromPosition = cell.transform.position;

            if (target.occupiedUnit is BasePlayer player)
            {
                if (hostile)
                {
                    // Takes the tile now; the Tech Bug is removed once the step has played
                    target.SetUnit(cell);
                    if (!victims.Contains(player)) victims.Add(player);
                }
                else
                {
                    // Captured: off the board now, destroyed once it has visibly reached the Tech Bug
                    from.occupiedUnit = null;
                    cell.OccupiedTile = null;
                    caughtCells.Add(cell);
                }
            }
            else if (target.walkable)
            {
                target.SetUnit(cell);
            }
            else
            {
                continue; // blocked: wait this turn
            }

            // SetUnit snaps the transform; put it back so the step can be animated
            cell.transform.position = fromPosition;
            steps.Add((cell, fromPosition, target.transform.position));
        }

        if (steps.Count == 0) yield break;

        var duration = 1f / moveSpeed;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            foreach (var step in steps)
            {
                if (step.cell != null) step.cell.transform.position = Vector3.Lerp(step.from, step.to, t / duration);
            }
            yield return null;
        }
        foreach (var step in steps)
        {
            if (step.cell != null) step.cell.transform.position = step.to;
        }

        foreach (var cell in caughtCells)
        {
            if (cell == null) continue;
            RestoreMoves(cell.MovesRestoredOnCapture);
            movingCells.Remove(cell);
            Destroy(cell.gameObject);
        }

        foreach (var player in victims)
        {
            yield return DestroyPlayer(player);
            Debug.Log("The Tech Bug was caught by a hostile Moving Cell.");
        }

    }


    private void Capture(BaseUnit unit) {

        // Destroy is deferred to the end of the frame, so drop the core cell from the count right away,
        // and a captured Moving Cell from the turn order (the enemy turn starts this same frame)
        if (unit is CoreCell coreCell) activeCoreCells.Remove(coreCell);
        if (unit is MovingCell movingCell) movingCells.Remove(movingCell);

        // A captured BlockingEnemy frees its tiles and retracts its vines in its own OnDestroy
        Destroy(unit.gameObject);

    }


    private static bool IsCapturable(BaseUnit unit) {

        // A hostile Moving Cell can't be captured; sliding into it destroys the Tech Bug (see GetSlidePath)
        if (unit is MovingCell movingCell && movingCell.IsHostile) return false;
        return unit is BaseEnemy || unit is CoreCell || unit is EnergyCell;

    }


    private static Tile GetTileAt(Vector2 position) {

        return GridManager.Instance.GetTileAtPosition(new Vector2(Mathf.Round(position.x), Mathf.Round(position.y)));

    }

    #endregion


    #region Win / lose

    // Win: the move just captured the last Core Cell.
    // Lose: Core Cells remain and either the energy has run out, or no Cancer Cell can slide anywhere
    // (and, if enabled, nothing can be clicked).
    private void ResolveOutcome(bool capturedCoreCell = false) {

        if (GameManager.Instance.gameState != GameState.PlayerTurn) return;

        if (capturedCoreCell && activeCoreCells.Count == 0)
        {
            GameManager.Instance.ChangeState(GameState.Win);
            return;
        }

        // No early return when no Core Cells are left: the last one can only go by capture, which wins above,
        // so an empty count means the level never had any, and it must still be losable

        if (movesLimited && currentMoves <= 0)
        {
            Debug.Log("The Tech Bug ran out of energy.");
            GameManager.Instance.ChangeState(GameState.Lose);
            return;
        }

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
