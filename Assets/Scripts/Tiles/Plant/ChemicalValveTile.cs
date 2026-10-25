using System.Collections.Generic;
using UnityEngine;

// Chemical valve: an enzyme gland the player clicks to make Defensive Cells pull their vines back.
//  - TimedRetract: vines retract for retractTurns slides, then grow back (clicking again restarts the timer)
//  - Toggle: vines stay retracted until the valve is clicked again
// Which Defensive Cells it reaches is set by range (0 = every one on the board).
public class ChemicalValveTile : PlantInteractableTile
{
    public enum ReleaseMode { TimedRetract, Toggle }

    [SerializeField] private ReleaseMode mode = ReleaseMode.TimedRetract;
    [SerializeField, Min(1)] private int retractTurns = 2;
    [SerializeField, Min(0), Tooltip("Max tiles (including diagonals) from this valve to a Defensive Cell. 0 = every Defensive Cell on the board.")]
    private int range;
    [SerializeField] private Sprite idleSprite;     // optional, a solid square is generated when empty
    [SerializeField] private Sprite releasedSprite; // optional, a hollow frame is generated when empty
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color releasedColor = Color.white;

    private SpriteRenderer gland;
    private bool toggledOn;

    // Nothing to affect (e.g. every Defensive Cell in range captured) means nothing to click
    public override bool CanInteract => GetTargets().Count > 0;

    // Released while any Defensive Cell it reaches is retracted because of it
    private bool IsReleased
    {
        get
        {
            if (mode == ReleaseMode.Toggle) return toggledOn;
            foreach (var target in GetTargets())
            {
                if (target.RetractTurnsLeft > 0) return true;
            }
            return false;
        }
    }


    private void OnEnable()
    {
        BlockingEnemy.VinesChanged += OnVinesChanged;
    }

    private void OnDisable()
    {
        BlockingEnemy.VinesChanged -= OnVinesChanged;
    }

    // A timed retraction running out flips this valve back to idle
    private void OnVinesChanged(BlockingEnemy enemy)
    {
        if (gland != null) RefreshVisual();
    }

    protected override void Toggle()
    {
        var targets = GetTargets();

        if (mode == ReleaseMode.Toggle)
        {
            toggledOn = !toggledOn;
            foreach (var target in targets) target.SetVinesActive(!toggledOn);
        }
        else
        {
            foreach (var target in targets) target.RetractForTurns(retractTurns);
        }
    }

    protected override void RefreshVisual()
    {
        if (gland == null) gland = CreateOverlay("Gland", null, Color.white, InteractiveOrderOffset);

        var released = IsReleased;
        gland.sprite = released
            ? (releasedSprite != null ? releasedSprite : GeneratedSprites.Frame)
            : (idleSprite != null ? idleSprite : GeneratedSprites.Square);
        gland.color = released ? releasedColor : idleColor;
    }


    private List<BlockingEnemy> GetTargets()
    {
        var targets = new List<BlockingEnemy>();
        var origin = transform.position;

        foreach (var enemy in FindObjectsByType<BlockingEnemy>(FindObjectsSortMode.None))
        {
            if (enemy.OccupiedTile == null) continue;

            var offset = enemy.OccupiedTile.transform.position - origin;
            var distance = Mathf.Max(Mathf.Abs(Mathf.RoundToInt(offset.x)), Mathf.Abs(Mathf.RoundToInt(offset.y)));
            if (range == 0 || distance <= range) targets.Add(enemy);
        }

        return targets;
    }
}
