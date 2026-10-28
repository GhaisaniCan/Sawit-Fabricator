using UnityEngine;

// Low-energy warning on the Tech Bug. Only changes SpriteRenderer.color, so it layers cleanly with
// CellIdleAnimator (which only touches scale and rotation).
//  - Pulse (energy <= lowMoveThreshold, more than 1 move left): alpha fades smoothly between pulseMinAlpha and 1
//  - Critical (1 move left): faster pulse, tint flips toward criticalTint, plus random glitch flicker
// Turns itself off (restoring the original colour) when energy rises above the threshold, the level has no
// move limit, or the game leaves PlayerTurn.
public class PlayerEnergyFlasher : MonoBehaviour
{
    private enum WarningLevel { None, Pulse, Critical }

    [SerializeField, Tooltip("Defaults to the SpriteRenderer on this object")] private SpriteRenderer targetRenderer;
    [SerializeField, Min(1)] private int lowMoveThreshold = 2;

    [Header("Pulse (low energy)")]
    [SerializeField, Min(0f), Tooltip("Fades per second")] private float pulseFrequency = 1.6f;
    [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.25f;

    [Header("Critical (last move)")]
    [SerializeField, Min(0f)] private float criticalFrequency = 4.5f;
    [SerializeField] private Color criticalTint = new Color(1f, 0.16f, 0.27f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("How strongly the tint is applied at the peak of each flash")] private float criticalTintStrength = 0.85f;
    [SerializeField, Range(0f, 1f), Tooltip("Share of the time the sprite glitches out")] private float flickerAmount = 0.18f;
    [SerializeField, Range(0f, 1f)] private float flickerAlpha = 0.1f;

    private Color baseColor;
    private bool hasBaseColor;
    private WarningLevel level = WarningLevel.None;
    private int moves;
    private int maxMoves;
    private float noiseSeed;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        noiseSeed = Random.Range(0f, 100f);
    }

    private void OnEnable()
    {
        if (targetRenderer != null)
        {
            baseColor = targetRenderer.color;
            hasBaseColor = true;
        }

        UnitManager.OnMovesChanged += OnMovesChanged;
        GameManager.StateChanged += OnStateChanged;

        // Catch up if energy was set before this cell spawned
        if (UnitManager.Instance != null) OnMovesChanged(UnitManager.Instance.CurrentMoves, UnitManager.Instance.MaxMoves);
    }

    private void OnDisable()
    {
        UnitManager.OnMovesChanged -= OnMovesChanged;
        GameManager.StateChanged -= OnStateChanged;
        SetLevel(WarningLevel.None);
    }

    private void OnMovesChanged(int currentMoves, int currentMaxMoves)
    {
        moves = currentMoves;
        maxMoves = currentMaxMoves;
        Evaluate();
    }

    private void OnStateChanged(GameState state)
    {
        Evaluate();
    }

    private void Evaluate()
    {
        var limited = maxMoves > 0;
        var playing = GameManager.Instance != null && GameManager.Instance.gameState == GameState.PlayerTurn;

        if (!limited || !playing || moves <= 0 || moves > lowMoveThreshold) SetLevel(WarningLevel.None);
        else SetLevel(moves == 1 ? WarningLevel.Critical : WarningLevel.Pulse);
    }

    private void SetLevel(WarningLevel newLevel)
    {
        level = newLevel;
        if (level == WarningLevel.None && hasBaseColor && targetRenderer != null) targetRenderer.color = baseColor;
    }

    private void Update()
    {
        if (level == WarningLevel.None || targetRenderer == null) return;

        var time = Time.time;

        if (level == WarningLevel.Pulse)
        {
            // 0..1 smooth wave
            var wave = 0.5f + 0.5f * Mathf.Sin(time * pulseFrequency * Mathf.PI * 2f);
            targetRenderer.color = WithAlpha(baseColor, Mathf.Lerp(pulseMinAlpha, 1f, wave) * baseColor.a);
            return;
        }

        // Critical: fast strobe between normal and the warning tint, with glitchy dropouts
        var strobe = 0.5f + 0.5f * Mathf.Sin(time * criticalFrequency * Mathf.PI * 2f);
        var tinted = Color.Lerp(baseColor, criticalTint, strobe * criticalTintStrength);
        var alpha = Mathf.Lerp(pulseMinAlpha, 1f, strobe);

        // Perlin noise gives irregular, clustered flickers instead of a regular beat
        var noise = Mathf.PerlinNoise(noiseSeed, time * 18f);
        if (noise < flickerAmount) alpha = flickerAlpha;

        targetRenderer.color = WithAlpha(tinted, alpha * baseColor.a);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
