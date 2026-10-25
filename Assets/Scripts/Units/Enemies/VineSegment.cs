using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sprites and tint shared by every segment of one Defensive Cell's vines
public struct VineVisuals
{
    public Sprite stem;  // half segment, tile centre → right edge (rotated per connection)
    public Sprite knot;  // drawn at the centre where a vine bends or branches
    public Sprite tip;   // drawn at the centre of a tendril's end, pointing away from its stem
    public Color color;
    public int sortingLayerId;
    public int sortingOrder;
}

// One tile's piece of a Defensive Cell's vine, built and owned by BlockingEnemy.
// It lives on the tile it covers (not on the enemy), so it can still animate away after the enemy is destroyed.
public class VineSegment : MonoBehaviour
{
    public enum Shape { Root, Tip, Straight, Corner, Branch }

    private const float Diagonal = 1.41421356f;

    // The art is drawn thin; scale it up so a covered tile clearly reads as blocked
    private const float StemThickness = 1.6f;
    private const float CentrePieceScale = 1.3f;

    public Vector2Int BoardPosition { get; private set; }
    public Vector2Int ParentDirection { get; private set; } // toward the host cell; zero on the host's own tile
    public Shape SegmentShape { get; private set; }
    public int Depth { get; private set; }                  // steps along the vine from the host

    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private Coroutine scaleRoutine;


    public static VineSegment Create(Tile tile, Vector2Int boardPosition, Vector2Int parentDirection,
                                     IReadOnlyList<Vector2Int> childDirections, int depth, VineVisuals visuals)
    {
        var segmentObject = new GameObject($"Vine {boardPosition}");
        segmentObject.transform.SetParent(tile.transform, false);

        var segment = segmentObject.AddComponent<VineSegment>();
        segment.BoardPosition = boardPosition;
        segment.ParentDirection = parentDirection;
        segment.Depth = depth;
        segment.SegmentShape = GetShape(parentDirection, childDirections);
        segment.Build(childDirections, visuals);

        // Starts retracted; BlockingEnemy grows it
        segmentObject.transform.localScale = Vector3.zero;
        segment.SetVisible(false);
        return segment;
    }

    private static Shape GetShape(Vector2Int parentDirection, IReadOnlyList<Vector2Int> childDirections)
    {
        if (parentDirection == Vector2Int.zero) return Shape.Root;
        if (childDirections.Count == 0) return Shape.Tip;
        if (childDirections.Count > 1) return Shape.Branch;
        return childDirections[0] == -parentDirection ? Shape.Straight : Shape.Corner;
    }

    private void Build(IReadOnlyList<Vector2Int> childDirections, VineVisuals visuals)
    {
        if (ParentDirection != Vector2Int.zero) AddStem(ParentDirection, visuals);
        foreach (var child in childDirections) AddStem(child, visuals);

        switch (SegmentShape)
        {
            case Shape.Tip:
                // Points away from the stem it grows out of
                AddCentrePiece("Tip", visuals.tip, -ParentDirection, visuals);
                break;
            case Shape.Corner:
            case Shape.Branch:
                AddCentrePiece("Knot", visuals.knot, Vector2Int.right, visuals);
                break;
            // Straight pieces join seamlessly; the root sits under the host cell's own sprite
        }
    }

    // A half stem from the tile centre toward one neighbour; diagonals are stretched to reach the corner
    private void AddStem(Vector2Int direction, VineVisuals visuals)
    {
        var length = direction.x != 0 && direction.y != 0 ? Diagonal : 1f;
        var stem = AddRenderer("Stem", visuals.stem != null ? visuals.stem : GeneratedSprites.Square, visuals, 0f);
        stem.transform.localRotation = Quaternion.Euler(0, 0, Angle(direction));

        if (visuals.stem != null)
        {
            stem.transform.localScale = new Vector3(length, StemThickness, 1f);
        }
        else
        {
            // Placeholder: a thin bar from the centre to the edge
            stem.transform.localPosition = (Vector2)direction * 0.25f;
            stem.transform.localScale = new Vector3(0.5f * length + 0.08f, 0.16f, 1f);
        }
    }

    private void AddCentrePiece(string pieceName, Sprite sprite, Vector2Int facing, VineVisuals visuals)
    {
        // Slightly in front of the stems so it covers the joint
        var piece = AddRenderer(pieceName, sprite != null ? sprite : GeneratedSprites.Square, visuals, -0.01f);
        piece.transform.localRotation = Quaternion.Euler(0, 0, Angle(facing));
        piece.transform.localScale = Vector3.one * (sprite == null ? 0.3f : CentrePieceScale);
    }

    private SpriteRenderer AddRenderer(string pieceName, Sprite sprite, VineVisuals visuals, float z)
    {
        var piece = new GameObject(pieceName);
        piece.transform.SetParent(transform, false);
        piece.transform.localPosition = new Vector3(0, 0, z);

        var pieceRenderer = piece.AddComponent<SpriteRenderer>();
        pieceRenderer.sprite = sprite;
        pieceRenderer.color = visuals.color;
        pieceRenderer.sortingLayerID = visuals.sortingLayerId;
        pieceRenderer.sortingOrder = visuals.sortingOrder;
        renderers.Add(pieceRenderer);
        return pieceRenderer;
    }

    private static float Angle(Vector2Int direction)
    {
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }


    public void Grow(float delay, float duration)
    {
        if (!isActiveAndEnabled) return;
        SetVisible(true);
        StartScale(1f, delay, duration, null);
    }

    public void Retract(float delay, float duration, bool destroyAfter)
    {
        // Can't animate while being torn down (e.g. scene unload)
        if (!isActiveAndEnabled)
        {
            if (destroyAfter && this != null) Destroy(gameObject);
            return;
        }

        StartScale(0f, delay, duration, () =>
        {
            if (destroyAfter) Destroy(gameObject);
            else SetVisible(false);
        });
    }

    private void StartScale(float target, float delay, float duration, System.Action onDone)
    {
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleTo(target, delay, duration, onDone));
    }

    private IEnumerator ScaleTo(float target, float delay, float duration, System.Action onDone)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var start = transform.localScale.x;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Ease out, so tendrils snap into place
            var eased = 1f - Mathf.Pow(1f - t / duration, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(start, target, eased);
            yield return null;
        }

        transform.localScale = Vector3.one * target;
        scaleRoutine = null;
        onDone?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        foreach (var pieceRenderer in renderers) pieceRenderer.enabled = visible;
    }
}
