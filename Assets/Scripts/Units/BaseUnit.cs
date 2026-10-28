using UnityEngine;

public class BaseUnit : MonoBehaviour
{
    public string UnitName;
    public Tile OccupiedTile;
    public Faction Faction;

    // Energy (moves) the Tech Bug gains when it captures this unit. Set per prefab; a level placement can override it.
    // Only matters in levels with a move limit.
    [SerializeField, Min(0), Tooltip("Energy (moves) the Tech Bug gains for capturing this unit")]
    private int movesRestoredOnCapture;

    public int MovesRestoredOnCapture => movesRestoredOnCapture;

    // Per-level override, set by UnitManager when spawning
    public void SetMovesRestoredOnCapture(int moves)
    {
        movesRestoredOnCapture = Mathf.Max(0, moves);
    }
}
