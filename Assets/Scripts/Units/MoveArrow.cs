using UnityEngine;
using UnityEngine.EventSystems;

public class MoveArrow : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color idleColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    private static readonly Color hoverColor = Color.white;
    private static Sprite generatedSprite;

    private Vector2 direction;
    private SpriteRenderer spriteRenderer;


    public static MoveArrow Create(Vector2 direction, Vector2 position, Sprite sprite, Transform parent)
    {
        var arrowObject = new GameObject($"Arrow {direction}");
        arrowObject.transform.SetParent(parent);

        // In front of tiles and units so it wins the click
        arrowObject.transform.position = new Vector3(position.x, position.y, -1);
        arrowObject.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        arrowObject.transform.localScale = Vector3.one * 0.5f;

        var arrow = arrowObject.AddComponent<MoveArrow>();
        arrow.direction = direction;

        arrow.spriteRenderer = arrowObject.AddComponent<SpriteRenderer>();
        arrow.spriteRenderer.sprite = sprite != null ? sprite : GetGeneratedSprite();
        // Sorting layer outranks sorting order, so it must be on the topmost layer (TileRender < Units)
        arrow.spriteRenderer.sortingLayerName = "Units";
        arrow.spriteRenderer.sortingOrder = 10;
        arrow.spriteRenderer.color = idleColor;

        arrowObject.AddComponent<BoxCollider2D>();

        return arrow;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance.gameState != GameState.PlayerTurn) return;

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


    // Right-pointing triangle, used when no arrow sprite is assigned on UnitManager
    private static Sprite GetGeneratedSprite()
    {
        if (generatedSprite != null) return generatedSprite;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Half-height of the triangle shrinks linearly from the left edge to the tip
                var halfHeight = (size / 2f) * (1f - (float)x / size);
                var inside = Mathf.Abs(y + 0.5f - size / 2f) <= halfHeight;
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        generatedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedSprite;
    }
}
