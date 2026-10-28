using UnityEngine;

// Drifting cell: steps one tile after every Tech Bug move, cycling through moveSequence (e.g. up, down, up, down).
// UnitManager moves every passive cell first, then every hostile one, then hands the turn back to the player.
//  - passive: captured on contact, whether the Tech Bug slides into it or it steps into the Tech Bug
//  - hostile: destroys the Tech Bug on contact, either way round
// A step into a wall, closed gate, root, other unit or the board edge is skipped (the cell waits), but the
// sequence still advances. A zero entry is a deliberate wait.
public class MovingCell : BaseEnemy
{
    [SerializeField] private bool hostile;
    [Tooltip("One entry per Tech Bug move, repeated in order. Each component is clamped to -1..1 (one tile, diagonals allowed).")]
    [SerializeField] private Vector2Int[] moveSequence = { Vector2Int.up, Vector2Int.down };
    [SerializeField] private Color indicatorColor = new Color(1f, 0.85f, 0.3f, 0.95f);

    private int stepIndex;
    private SpriteRenderer indicator;

    public bool IsHostile => hostile;
    public Vector2Int[] MoveSequence => moveSequence;
    public bool HasMoves => moveSequence != null && moveSequence.Length > 0;

    // The step this cell takes on the next enemy turn
    public Vector2Int NextMove => HasMoves ? Clamp(moveSequence[stepIndex % moveSequence.Length]) : Vector2Int.zero;

    // Per-level override; must be called before Start
    public void SetMoveSequence(Vector2Int[] sequence)
    {
        moveSequence = sequence ?? new Vector2Int[0];
        stepIndex = 0;
    }

    public void AdvanceSequence()
    {
        if (HasMoves) stepIndex = (stepIndex + 1) % moveSequence.Length;
        RefreshIndicator();
    }

    public static Vector2Int Clamp(Vector2Int step)
    {
        return new Vector2Int(Mathf.Clamp(step.x, -1, 1), Mathf.Clamp(step.y, -1, 1));
    }

    // Start rather than Awake, so UnitManager.Instance is guaranteed to exist
    private void Start()
    {
        UnitManager.Instance.RegisterMovingCell(this);
        CreateIndicator();
        RefreshIndicator();
    }

    private void OnDestroy()
    {
        if (UnitManager.Instance != null) UnitManager.Instance.UnregisterMovingCell(this);
    }

    // Small arrow on the cell's edge showing where it will step next, so the player can plan around it
    private void CreateIndicator()
    {
        var indicatorObject = new GameObject("Next Move");
        indicatorObject.transform.SetParent(transform, false);
        indicatorObject.transform.localScale = Vector3.one * 0.3f;

        indicator = indicatorObject.AddComponent<SpriteRenderer>();
        indicator.sprite = GeneratedSprites.Triangle;
        indicator.color = indicatorColor;
        indicator.sortingLayerName = "Units";
        indicator.sortingOrder = 2;
    }

    private void RefreshIndicator()
    {
        if (indicator == null) return;

        var next = NextMove;
        indicator.enabled = next != Vector2Int.zero;
        if (!indicator.enabled) return;

        var direction = ((Vector2)next).normalized;
        indicator.transform.localPosition = direction * 0.55f;
        indicator.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }
}
