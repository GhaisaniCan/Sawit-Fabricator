using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{

    [SerializeField] private GameObject highlight;
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected bool isWalkable;

    public BaseUnit occupiedUnit;
    public bool walkable => isWalkable && (occupiedUnit == null) && !isBlocked;

    // Several Defensive Cells can grow vines over the same tile; it stays blocked until all of them retract.
    // The vine visuals belong to the BlockingEnemy (VineSegment), the tile only tracks who is blocking it.
    private readonly HashSet<BlockingEnemy> blockers = new HashSet<BlockingEnemy>();
    public bool isBlocked => blockers.Count > 0;

    // Terrain walkability, ignoring units and vines (a vine over a wall still acts as a wall)
    public bool IsWalkableTerrain => isWalkable;

    // True when any vine on this tile is hazardous: a Cancer Cell sliding in is destroyed
    public bool IsHazardous
    {
        get
        {
            foreach (var blocker in blockers)
            {
                if (blocker != null && blocker.IsHazardous) return true;
            }
            return false;
        }
    }

    private bool isSelected;

    // Interactive tiles (gates, valves) override these; plain tiles can't be interacted with
    public virtual bool CanInteract => false;
    protected virtual void Interact() { }

    public virtual void init(int x, int y)
    {

    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance.gameState != GameState.PlayerTurn || UnitManager.Instance.IsBusy) return;

        // Middle/right buttons pan the camera, so only left presses act on the board
        if (eventData.button != PointerEventData.InputButton.Left || CameraController.IsPanning) return;

        // Checked by type, not Faction, since the Faction field on unit prefabs can be wrong
        if (occupiedUnit is BasePlayer player)
        {
            UnitManager.Instance.SetSelectedPlayer(player);
        }
        else if (CanInteract)
        {
            Interact();
            UnitManager.Instance.OnBoardChanged();
        }
        else
        {
            // Clicking anywhere else clears the selection
            UnitManager.Instance.SetSelectedPlayer(null);
        }

    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        highlight.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // A selected tile keeps its highlight while the pointer is elsewhere
        if (!isSelected) highlight.SetActive(false);

    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        highlight.SetActive(selected);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }

    public void SetUnit(BaseUnit unit) {

        if (unit.OccupiedTile != null)
        {
            unit.OccupiedTile.occupiedUnit = null;
        }

        unit.transform.position = transform.position;
        occupiedUnit = unit;
        unit.OccupiedTile = this;

    }

    public void AddBlocker(BlockingEnemy blocker) {

        blockers.Add(blocker);

    }

    public void RemoveBlocker(BlockingEnemy blocker) {

        blockers.Remove(blocker);

    }

    // Child sprite drawn on this tile's sorting layer, orderOffset above the base tile sprite.
    // Public so effects from elsewhere (e.g. a chemical valve's mist) can decorate this tile.
    public SpriteRenderer CreateOverlay(string overlayName, Sprite sprite, Color color, int orderOffset) {

        var overlay = new GameObject(overlayName);
        overlay.transform.SetParent(transform, false);

        var overlayRenderer = overlay.AddComponent<SpriteRenderer>();
        overlayRenderer.sprite = sprite;
        overlayRenderer.color = color;
        overlayRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = spriteRenderer.sortingOrder + orderOffset;

        return overlayRenderer;

    }


}
