using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{

    [SerializeField] private GameObject highlight;
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] private bool isWalkable;

    public BaseUnit occupiedUnit;
    public bool walkable => isWalkable && (occupiedUnit == null) && !isBlocked;

    // Several blocking enemies can wall off the same tile; it stays blocked until all of them are gone
    private readonly HashSet<BlockingEnemy> blockers = new HashSet<BlockingEnemy>();
    private GameObject wallOverlay;
    public bool isBlocked => blockers.Count > 0;

    private bool isSelected;

    public virtual void init(int x, int y) 
    {

    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"{this.name} Down");

        if (GameManager.Instance.gameState != GameState.PlayerTurn) return;

        // Checked by type, not Faction, since the Faction field on unit prefabs can be wrong
        if (occupiedUnit is BasePlayer player)
        {
            UnitManager.Instance.SetSelectedPlayer(player);
        }
        else
        {
            // Clicking anywhere else clears the selection
            UnitManager.Instance.SetSelectedPlayer(null);
        }

    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"{this.name} Enter");

        highlight.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"{this.name} Exit");

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
        Debug.Log($"{this.name} Up");
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

    public void AddBlocker(BlockingEnemy blocker, Sprite wallSprite, Color wallColor) {

        if (!blockers.Add(blocker) || wallOverlay != null) return;

        wallOverlay = new GameObject("Wall");
        wallOverlay.transform.SetParent(transform, false);

        var wallRenderer = wallOverlay.AddComponent<SpriteRenderer>();
        wallRenderer.sprite = wallSprite;
        wallRenderer.color = wallColor;

        // Above the tile and its highlight, still below units
        wallRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        wallRenderer.sortingOrder = spriteRenderer.sortingOrder + 2;

    }

    public void RemoveBlocker(BlockingEnemy blocker) {

        if (!blockers.Remove(blocker) || isBlocked) return;

        if (wallOverlay != null) Destroy(wallOverlay);
        wallOverlay = null;

    }


}
