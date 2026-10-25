using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Which Defensive Cells a chemical valve reaches
public enum ChemicalTargetMode
{
    AreaRadius, // hosts within `radius` tiles (Chebyshev, so radius 1 = the 3x3 around the valve)
    ColorMatch, // hosts whose EnemyColor equals the valve's valveColor
    Global,     // every Defensive Cell on the board
}

// Chemical valve: an enzyme gland the player clicks to make Defensive Cells pull their vines back.
//  - TimedRetract: vines retract for retractTurns slides, then grow back (clicking again restarts the timer)
//  - Toggle: vines stay retracted until the valve is clicked again
// While its effect is active it shows a faint mist over the affected tiles and an optional smoke particle cloud.
public class ChemicalValveTile : PlantInteractableTile
{
    public enum ReleaseMode { TimedRetract, Toggle }

    private const float MistFadeSpeed = 1.5f;   // alpha per second
    private const float MistPulseSpeed = 1.4f;  // radians per second
    private const float MistScale = 1.35f;

    [Header("Effect")]
    [SerializeField] private ReleaseMode mode = ReleaseMode.TimedRetract;
    [SerializeField, Min(1)] private int retractTurns = 2;

    [Header("Targeting")]
    [SerializeField] private ChemicalTargetMode targetMode = ChemicalTargetMode.Global;
    [SerializeField, Min(0), FormerlySerializedAs("range"), Tooltip("AreaRadius only: max tiles (including diagonals) from the valve to a Defensive Cell")]
    private int radius = 1;
    [SerializeField, Tooltip("ColorMatch only: affects Defensive Cells with this EnemyColor. Also tints the mist and smoke.")]
    private ChemicalColor valveColor = ChemicalColor.None;

    [Header("Gland visuals")]
    [SerializeField] private Sprite idleSprite;     // optional, a solid square is generated when empty
    [SerializeField] private Sprite releasedSprite; // optional, a hollow frame is generated when empty
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color releasedColor = Color.white;

    [Header("Mist & smoke")]
    [SerializeField] private Sprite mistSprite; // optional, a soft dot is generated when empty
    [SerializeField, Range(0f, 1f)] private float mistAlpha = 0.4f;
    [SerializeField, Tooltip("Optional drifting cloud spawned on the valve while its effect is active")]
    private ParticleSystem smokePrefab;

    private SpriteRenderer gland;
    private SpriteRenderer colorMarker;
    private bool toggledOn;
    // TimedRetract: this valve's own countdown, so another valve retracting the same cell doesn't light this one up
    private int turnsLeft;
    private readonly List<BlockingEnemy> activeTargets = new List<BlockingEnemy>();

    // Mist overlays this valve has put on tiles (including ones still fading out) and their pulse phases
    private readonly Dictionary<Tile, SpriteRenderer> mist = new Dictionary<Tile, SpriteRenderer>();
    private readonly List<SpriteRenderer> fadingMist = new List<SpriteRenderer>();
    private readonly Dictionary<SpriteRenderer, float> mistPhase = new Dictionary<SpriteRenderer, float>();
    private Coroutine mistRoutine;
    private ParticleSystem smoke;

    // Clickable when it has something to affect, or to switch an active Toggle valve back off
    public override bool CanInteract => IsActive || GetTargets().Count > 0;

    public bool IsActive
    {
        get
        {
            if (mode == ReleaseMode.Toggle) return toggledOn;
            if (turnsLeft == 0) return false;

            // Ends early if every cell it retracted has been captured
            foreach (var target in activeTargets)
            {
                if (target != null) return true;
            }
            return false;
        }
    }

    private Color EffectColor => valveColor.ToColor();


    private void OnEnable()
    {
        BlockingEnemy.VinesChanged += OnVinesChanged;
        UnitManager.SlideFinished += OnSlideFinished;
    }

    private void OnDisable()
    {
        BlockingEnemy.VinesChanged -= OnVinesChanged;
        UnitManager.SlideFinished -= OnSlideFinished;
        ClearEffectVisuals(immediate: true);
    }

    // A timed retraction running out, or a target being captured, can end the effect
    private void OnVinesChanged(BlockingEnemy enemy)
    {
        if (gland != null) RefreshVisual();
    }

    // Counts down alongside the Defensive Cells' own timers (both listen to the same event)
    private void OnSlideFinished()
    {
        if (turnsLeft > 0)
        {
            turnsLeft--;
            if (turnsLeft == 0) activeTargets.Clear();
        }

        if (gland != null) RefreshVisual();
    }


    protected override void Toggle()
    {
        activeTargets.RemoveAll(target => target == null);

        if (mode == ReleaseMode.Toggle)
        {
            if (toggledOn)
            {
                foreach (var target in activeTargets) target.SetVinesActive(true);
                activeTargets.Clear();
                toggledOn = false;
            }
            else
            {
                var targets = GetTargets();
                foreach (var target in targets) target.SetVinesActive(false);
                activeTargets.AddRange(targets);
                toggledOn = targets.Count > 0;
            }
            return;
        }

        var timedTargets = GetTargets();
        if (timedTargets.Count == 0) return;

        foreach (var target in timedTargets)
        {
            target.RetractForTurns(retractTurns);
            if (!activeTargets.Contains(target)) activeTargets.Add(target);
        }
        turnsLeft = retractTurns; // clicking again restarts the countdown
    }

    protected override void RefreshVisual()
    {
        if (gland == null) gland = CreateOverlay("Gland", null, Color.white, InteractiveOrderOffset);

        var active = IsActive;
        gland.sprite = active
            ? (releasedSprite != null ? releasedSprite : GeneratedSprites.Frame)
            : (idleSprite != null ? idleSprite : GeneratedSprites.Square);
        gland.color = active ? releasedColor : idleColor;

        UpdateColorMarker();

        if (active) ShowEffectVisuals();
        else ClearEffectVisuals(immediate: false);
    }


    #region Targeting

    private List<BlockingEnemy> GetTargets()
    {
        var targets = new List<BlockingEnemy>();

        foreach (var enemy in FindObjectsByType<BlockingEnemy>(FindObjectsSortMode.None))
        {
            if (enemy.OccupiedTile != null && Affects(enemy)) targets.Add(enemy);
        }

        return targets;
    }

    private bool Affects(BlockingEnemy enemy)
    {
        switch (targetMode)
        {
            case ChemicalTargetMode.AreaRadius:
                return ChebyshevDistance(enemy.OccupiedTile.transform.position) <= radius;
            case ChemicalTargetMode.ColorMatch:
                return valveColor != ChemicalColor.None && enemy.EnemyColor == valveColor;
            default:
                return true;
        }
    }

    private int ChebyshevDistance(Vector3 position)
    {
        var offset = position - transform.position;
        return Mathf.Max(Mathf.Abs(Mathf.RoundToInt(offset.x)), Mathf.Abs(Mathf.RoundToInt(offset.y)));
    }

    // Tiles that get mist: the whole area for AreaRadius, otherwise each affected cell and its vine tiles
    private HashSet<Tile> GetMistTiles()
    {
        var tiles = new HashSet<Tile>();

        if (targetMode == ChemicalTargetMode.AreaRadius)
        {
            var centre = transform.position;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var tile = GridManager.Instance.GetTileAtPosition(new Vector2(Mathf.Round(centre.x) + dx, Mathf.Round(centre.y) + dy));
                    if (tile != null) tiles.Add(tile);
                }
            }
            return tiles;
        }

        foreach (var target in activeTargets)
        {
            if (target == null || target.OccupiedTile == null) continue;
            tiles.Add(target.OccupiedTile);
            foreach (var tile in target.CoveredTiles) tiles.Add(tile);
        }
        return tiles;
    }

    #endregion


    #region Mist & smoke visuals

    private void ShowEffectVisuals()
    {
        var wanted = GetMistTiles();
        var color = EffectColor;

        // Fade out mist on tiles no longer affected (e.g. a target was captured)
        foreach (var tile in new List<Tile>(mist.Keys))
        {
            if (wanted.Contains(tile)) continue;
            fadingMist.Add(mist[tile]);
            mist.Remove(tile);
        }

        foreach (var tile in wanted)
        {
            if (mist.ContainsKey(tile)) continue;

            // TileRender order 1 like the vines, nudged forward so the mist sits over them
            var overlay = tile.CreateOverlay("Chemical Mist", mistSprite != null ? mistSprite : GeneratedSprites.SoftDot,
                                             new Color(color.r, color.g, color.b, 0f), 1);
            overlay.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            // Larger than a tile so neighbouring puffs merge into one cloud
            overlay.transform.localScale = Vector3.one * MistScale;
            mist[tile] = overlay;
            mistPhase[overlay] = Random.Range(0f, Mathf.PI * 2f);
        }

        if (smoke == null && smokePrefab != null)
        {
            smoke = Instantiate(smokePrefab, transform.position + new Vector3(0f, 0f, -0.1f), Quaternion.identity, transform);
            var main = smoke.main;
            main.startColor = new Color(color.r, color.g, color.b, main.startColor.color.a);
            if (targetMode == ChemicalTargetMode.AreaRadius)
            {
                var shape = smoke.shape;
                shape.radius = Mathf.Max(shape.radius, radius * 0.8f); // drift over the whole area
            }
            smoke.Play();
        }

        if (mistRoutine == null) mistRoutine = StartCoroutine(AnimateMist());
    }

    private void ClearEffectVisuals(bool immediate)
    {
        foreach (var overlay in mist.Values) fadingMist.Add(overlay);
        mist.Clear();

        if (smoke != null)
        {
            // Let existing puffs drift away instead of popping out
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(smoke.gameObject, immediate ? 0f : smoke.main.startLifetime.constantMax + 0.5f);
            smoke = null;
        }

        if (immediate)
        {
            foreach (var overlay in fadingMist)
            {
                if (overlay != null) Destroy(overlay.gameObject);
            }
            fadingMist.Clear();
            mistPhase.Clear();
        }
    }

    // Fades mist in, pulses it gently while active, and fades out (then destroys) removed mist
    private IEnumerator AnimateMist()
    {
        while (mist.Count > 0 || fadingMist.Count > 0)
        {
            var step = MistFadeSpeed * Time.deltaTime;

            foreach (var overlay in mist.Values)
            {
                var pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * MistPulseSpeed + mistPhase[overlay]);
                SetAlpha(overlay, Mathf.MoveTowards(overlay.color.a, mistAlpha * pulse, step));
            }

            for (int i = fadingMist.Count - 1; i >= 0; i--)
            {
                var overlay = fadingMist[i];
                if (overlay == null) { fadingMist.RemoveAt(i); continue; }

                SetAlpha(overlay, Mathf.MoveTowards(overlay.color.a, 0f, step));
                if (overlay.color.a > 0f) continue;

                mistPhase.Remove(overlay);
                Destroy(overlay.gameObject);
                fadingMist.RemoveAt(i);
            }

            yield return null;
        }

        mistRoutine = null;
    }

    private static void SetAlpha(SpriteRenderer target, float alpha)
    {
        var color = target.color;
        color.a = alpha;
        target.color = color;
    }

    // ColorMatch valves wear a dot of their colour, matching the dot on the Defensive Cells they affect
    private void UpdateColorMarker()
    {
        var showMarker = targetMode == ChemicalTargetMode.ColorMatch && valveColor != ChemicalColor.None;

        if (showMarker && colorMarker == null)
        {
            colorMarker = CreateOverlay("Chemical Marker", GeneratedSprites.SoftDot, EffectColor, InteractiveOrderOffset);
            colorMarker.transform.localPosition = new Vector3(0.3f, 0.3f, -0.01f);
            colorMarker.transform.localScale = Vector3.one * 0.3f;
        }

        if (colorMarker != null) colorMarker.enabled = showMarker;
    }

    #endregion
}
