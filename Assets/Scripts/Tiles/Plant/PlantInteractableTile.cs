using UnityEngine;

// Base for plant tiles the player clicks to change the board (membrane gates, deflector valves).
// Their visuals sit on TileRender order 2: above highlights and walls, below units.
public abstract class PlantInteractableTile : Tile
{
    protected const int InteractiveOrderOffset = 2;

    [SerializeField] private Color baseColor = new Color(0.55f, 0.75f, 0.35f);

    public override void init(int x, int y)
    {
        spriteRenderer.color = baseColor;
        RefreshVisual();
    }

    protected override void Interact()
    {
        Toggle();
        RefreshVisual();
    }

    // Changes the tile's state; called when the player clicks it and CanInteract is true
    protected abstract void Toggle();

    protected abstract void RefreshVisual();
}
