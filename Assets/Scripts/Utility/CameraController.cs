using UnityEngine;
using UnityEngine.InputSystem;

// Pans (mouse drag / WASD / arrows) and zooms (scroll wheel) the orthographic main camera,
// keeping the view inside the board plus some padding. Goes on the Main Camera.
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public enum PanButton { Middle, Right, MiddleOrRight }

    [Header("Zoom")]
    [SerializeField] private float minOrthographicSize = 3f;
    [SerializeField] private float maxOrthographicSize = 12f;
    [SerializeField] private float zoomSensitivity = 1.5f;   // orthographic size per scroll notch
    [SerializeField] private float zoomDamping = 10f;        // higher = snappier
    [SerializeField] private bool zoomTowardCursor = true;

    [Header("Pan")]
    [SerializeField] private PanButton panButton = PanButton.MiddleOrRight;
    [SerializeField] private float dragThresholdPixels = 5f;
    [SerializeField] private bool keyboardPan = true;
    [SerializeField] private float keyboardPanSpeed = 1.5f;  // in screen heights per second, so it feels the same at any zoom

    [Header("Edge Scrolling")]
    [SerializeField] private bool enableEdgeScrolling = true;
    [SerializeField] private float edgeScrollMarginPixels = 15f;
    [SerializeField] private float edgeScrollSpeed = 10f;    // world units per second

    [Header("Player Focus (Space)")]
    [SerializeField] private float recenterDuration = 0.25f;

    [Header("Bounds")]
    [SerializeField] private float boundsPadding = 2f;       // world units the view may go past the board edge

    // True while a pan drag is past the threshold. Tiles and arrows ignore presses while it's set.
    public static bool IsPanning { get; private set; }

    private Camera cam;
    private float targetSize;

    private bool panHeld;
    private Vector2 panStartScreen;
    private Vector3 panAnchorWorld;

    private bool focusing;
    private float focusElapsed;
    private Vector3 focusFrom;
    private Vector3 focusTo;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        targetSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
    }

    private void OnDisable()
    {
        panHeld = false;
        IsPanning = false;
    }

    private void LateUpdate()
    {
        // Ignore input that arrives while another window has focus (scrolling elsewhere shouldn't zoom)
        var mouse = Application.isFocused ? Mouse.current : null;

        if (mouse == null)
        {
            panHeld = false;
            IsPanning = false;
        }

        if (mouse != null)
        {
            HandleDrag(mouse);
            HandleZoom(mouse);
        }
        else
        {
            ApplyZoom(null);
        }

        if (Application.isFocused) HandleKeyboard();

        // Edge scrolling never fights a held drag button or a running focus move
        if (mouse != null && enableEdgeScrolling && !panHeld && !focusing) HandleEdgeScroll(mouse);

        UpdateFocus();
        ClampToBoard();
    }

    private void HandleEdgeScroll(Mouse mouse)
    {
        Vector2 screenPos = mouse.position.ReadValue();

        // Outside the game view (e.g. over another Editor panel) counts as no edge
        if (!IsOnScreen(screenPos)) return;

        Vector2 input = Vector2.zero;
        if (screenPos.x <= edgeScrollMarginPixels) input.x -= 1f;
        else if (screenPos.x >= Screen.width - edgeScrollMarginPixels) input.x += 1f;
        if (screenPos.y <= edgeScrollMarginPixels) input.y -= 1f;
        else if (screenPos.y >= Screen.height - edgeScrollMarginPixels) input.y += 1f;

        if (input == Vector2.zero) return;

        transform.position += (Vector3)(input.normalized * edgeScrollSpeed * Time.unscaledDeltaTime);
    }

    // Starts a smooth move that centres the view on the Cancer Cell (the selected one, else any on the board)
    public void FocusOnPlayer()
    {
        var player = FindFocusPlayer();
        if (player == null) return;

        focusFrom = transform.position;
        focusTo = ClampPosition(new Vector3(player.transform.position.x, player.transform.position.y, transform.position.z));
        focusElapsed = 0f;
        focusing = true;
    }

    private static BasePlayer FindFocusPlayer()
    {
        var units = UnitManager.Instance;
        if (units != null && units.selectedPlayer != null) return units.selectedPlayer;

        return FindFirstObjectByType<BasePlayer>();
    }

    private void UpdateFocus()
    {
        if (!focusing) return;

        focusElapsed += Time.unscaledDeltaTime;
        float t = recenterDuration > 0f ? Mathf.Clamp01(focusElapsed / recenterDuration) : 1f;

        transform.position = Vector3.Lerp(focusFrom, focusTo, Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) focusing = false;
    }

    private void HandleDrag(Mouse mouse)
    {
        Vector2 screenPos = mouse.position.ReadValue();

        if (PanPressedThisFrame(mouse))
        {
            panHeld = true;
            panStartScreen = screenPos;
            panAnchorWorld = ScreenToWorld(screenPos);
            focusing = false;   // a manual drag takes over from the Space focus move
        }

        if (panHeld && !PanHeld(mouse))
        {
            panHeld = false;
            IsPanning = false;
            return;
        }

        if (!panHeld) return;

        if (!IsPanning && (screenPos - panStartScreen).sqrMagnitude >= dragThresholdPixels * dragThresholdPixels)
        {
            IsPanning = true;
        }

        if (IsPanning)
        {
            // Move so the world point grabbed at press time stays under the cursor (1:1 drag)
            transform.position += panAnchorWorld - ScreenToWorld(screenPos);
        }
    }

    private void HandleZoom(Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y;
        Vector2 screenPos = mouse.position.ReadValue();
        bool onScreen = IsOnScreen(screenPos);

        // Only the sign is used: scroll magnitudes differ between platforms and Input System settings
        if (onScreen && !Mathf.Approximately(scroll, 0f))
        {
            targetSize = Mathf.Clamp(targetSize - Mathf.Sign(scroll) * zoomSensitivity, minOrthographicSize, maxOrthographicSize);
        }

        ApplyZoom(zoomTowardCursor && !IsPanning && onScreen ? screenPos : (Vector2?)null);
    }

    private void ApplyZoom(Vector2? focusScreen)
    {
        if (Mathf.Approximately(cam.orthographicSize, targetSize)) return;

        Vector3 focusBefore = focusScreen.HasValue ? ScreenToWorld(focusScreen.Value) : Vector3.zero;

        // Frame-rate independent damping
        float t = 1f - Mathf.Exp(-zoomDamping * Time.unscaledDeltaTime);
        cam.orthographicSize = Mathf.Abs(cam.orthographicSize - targetSize) < 0.001f
            ? targetSize
            : Mathf.Lerp(cam.orthographicSize, targetSize, t);

        // Keep the point under the cursor fixed while zooming
        if (focusScreen.HasValue)
        {
            transform.position += focusBefore - ScreenToWorld(focusScreen.Value);
        }
    }

    private void HandleKeyboard()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame) FocusOnPlayer();

        if (!keyboardPan || IsPanning) return;

        Vector2 input = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;

        if (input == Vector2.zero) return;

        focusing = false;   // manual keys take over from the Space focus move
        float speed = keyboardPanSpeed * cam.orthographicSize * 2f;
        transform.position += (Vector3)(input.normalized * speed * Time.unscaledDeltaTime);
    }

    private void ClampToBoard()
    {
        transform.position = ClampPosition(transform.position);
    }

    private Vector3 ClampPosition(Vector3 pos)
    {
        var grid = GridManager.Instance;
        if (grid == null || grid.Width <= 0 || grid.Height <= 0) return pos;

        // Tile (x, y) is centred on world (x, y), so the board spans -0.5 .. size - 0.5
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        pos.x = ClampAxis(pos.x, -0.5f - boundsPadding, grid.Width - 0.5f + boundsPadding, halfWidth);
        pos.y = ClampAxis(pos.y, -0.5f - boundsPadding, grid.Height - 0.5f + boundsPadding, halfHeight);
        return pos;
    }

    // Keeps the view edges inside [min, max]; when the view is wider than that range it's centred instead
    private static float ClampAxis(float value, float min, float max, float halfExtent)
    {
        float low = min + halfExtent;
        float high = max - halfExtent;
        return low > high ? (min + max) * 0.5f : Mathf.Clamp(value, low, high);
    }

    private static bool IsOnScreen(Vector2 screenPos)
    {
        return screenPos.x >= 0f && screenPos.y >= 0f && screenPos.x <= Screen.width && screenPos.y <= Screen.height;
    }

    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -transform.position.z));
        world.z = 0f;
        return world;
    }

    private bool PanPressedThisFrame(Mouse mouse)
    {
        return (UsesMiddle && mouse.middleButton.wasPressedThisFrame) || (UsesRight && mouse.rightButton.wasPressedThisFrame);
    }

    private bool PanHeld(Mouse mouse)
    {
        return (UsesMiddle && mouse.middleButton.isPressed) || (UsesRight && mouse.rightButton.isPressed);
    }

    private bool UsesMiddle => panButton != PanButton.Right;
    private bool UsesRight => panButton != PanButton.Middle;
}
