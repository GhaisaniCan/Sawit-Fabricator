using UnityEngine;

// Placeholder sprites built in code, so features work before real art exists.
// Each is created once and cached; all are white so they can be tinted with SpriteRenderer.color.
public static class GeneratedSprites
{
    private static Sprite square;
    private static Sprite frame;
    private static Sprite triangle;

    // Solid 1x1 world-unit square
    public static Sprite Square
    {
        get
        {
            if (square != null) return square;

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            return square;
        }
    }

    // Hollow 1x1 world-unit square outline
    public static Sprite Frame
    {
        get
        {
            if (frame != null) return frame;

            const int size = 32;
            const int border = 3;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var onBorder = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = onBorder ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            frame = CreateSprite(pixels, size);
            return frame;
        }
    }

    // 1x1 world-unit triangle pointing right (0°), rotate it to point elsewhere
    public static Sprite Triangle
    {
        get
        {
            if (triangle != null) return triangle;

            const int size = 64;
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

            triangle = CreateSprite(pixels, size);
            return triangle;
        }
    }

    private static Sprite CreateSprite(Color32[] pixels, int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
