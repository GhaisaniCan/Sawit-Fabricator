using UnityEngine;

// Membrane gate: clicking it toggles between Open (walkable) and Closed (blocks like a wall).
public class CellWallGateTile : PlantInteractableTile
{
    [SerializeField] private bool startsOpen = true;
    [SerializeField] private Sprite openSprite;   // optional, a hollow frame is generated when empty
    [SerializeField] private Sprite closedSprite; // optional, a solid square is generated when empty
    [SerializeField] private Color openColor = new Color(0.75f, 0.95f, 0.6f, 0.9f);
    [SerializeField] private Color closedColor = new Color(0.3f, 0.5f, 0.15f, 1f);

    private SpriteRenderer membrane;

    public bool IsOpen => isWalkable;

    // A gate can't close on a unit sitting in it
    public override bool CanInteract => !IsOpen || occupiedUnit == null;


    public override void init(int x, int y)
    {
        isWalkable = startsOpen;
        base.init(x, y);
    }

    protected override void Toggle()
    {
        isWalkable = !isWalkable;
    }

    protected override void RefreshVisual()
    {
        if (membrane == null) membrane = CreateOverlay("Membrane", null, Color.white, InteractiveOrderOffset);

        membrane.sprite = IsOpen
            ? (openSprite != null ? openSprite : GeneratedSprites.Frame)
            : (closedSprite != null ? closedSprite : GeneratedSprites.Square);
        membrane.color = IsOpen ? openColor : closedColor;
    }
}
