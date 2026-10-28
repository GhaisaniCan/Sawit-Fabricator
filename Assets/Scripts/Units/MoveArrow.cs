using UnityEngine;
using UnityEngine.EventSystems;

public class MoveArrow : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color safeColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    private static readonly Color dangerColor = new Color(1f, 0.25f, 0.3f, 0.95f);
    private static readonly Color hoverColor = Color.white;

    private Vector2 direction;
    private SpriteRenderer spriteRenderer;
    private Color idleColor;


    // dangerous: this move ends in hazardous vines, so the arrow is tinted red as a warning
    public static MoveArrow Create(Vector2 direction, Vector2 position, Sprite sprite, Transform parent, bool dangerous = false)
    {
        var arrowObject = new GameObject($"Arrow {direction}");
        arrowObject.transform.SetParent(parent);

        // In front of tiles and units so it wins the click
        arrowObject.transform.position = new Vector3(position.x, position.y, -1);
        arrowObject.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        // The move_arrow art has padding around it, so 0.8 draws it at about half a tile
        arrowObject.transform.localScale = Vector3.one * 0.8f;

        var arrow = arrowObject.AddComponent<MoveArrow>();
        arrow.direction = direction;

        arrow.spriteRenderer = arrowObject.AddComponent<SpriteRenderer>();
        arrow.spriteRenderer.sprite = sprite != null ? sprite : GeneratedSprites.Triangle;
        // Sorting layer outranks sorting order, so it must be on the topmost layer (TileRender < Units)
        arrow.spriteRenderer.sortingLayerName = "Units";
        arrow.spriteRenderer.sortingOrder = 10;
        arrow.idleColor = dangerous ? dangerColor : safeColor;
        arrow.spriteRenderer.color = arrow.idleColor;

        arrowObject.AddComponent<BoxCollider2D>();

        return arrow;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance.gameState != GameState.PlayerTurn) return;
        if (eventData.button != PointerEventData.InputButton.Left || CameraController.IsPanning) return;

        UnitManager.Instance.MoveSelectedPlayer(direction);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        spriteRenderer.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        spriteRenderer.color = idleColor;
    }
}
