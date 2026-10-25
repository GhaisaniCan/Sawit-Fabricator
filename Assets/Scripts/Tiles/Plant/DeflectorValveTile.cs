using System;
using System.Collections.Generic;
using UnityEngine;

// Deflector valve: a sliding Cancer Cell that enters this tile continues in the valve's direction,
// whichever way it came in. Clicking the valve rotates it to the next allowed direction.
public class DeflectorValveTile : PlantInteractableTile
{
    [Flags]
    public enum ValveDirections
    {
        Right = 1, // 0°
        Up = 2,    // 90°
        Left = 4,  // 180°
        Down = 8,  // 270°
    }

    [SerializeField] private ValveDirections allowedDirections = ValveDirections.Right | ValveDirections.Up | ValveDirections.Left | ValveDirections.Down;
    [SerializeField, Tooltip("0 = right, 90 = up, 180 = left, 270 = down. Snapped to the nearest allowed direction.")]
    private int startAngle;
    [SerializeField] private Sprite arrowSprite; // optional, a triangle is generated when empty
    [SerializeField] private Color arrowColor = new Color(0.95f, 0.85f, 0.3f, 1f);

    private int currentAngle;
    private SpriteRenderer arrow;

    public int CurrentAngle => currentAngle;

    // Unit vector the valve sends sliding cells along
    public Vector2 OutputDirection
    {
        get
        {
            var radians = currentAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Round(Mathf.Cos(radians)), Mathf.Round(Mathf.Sin(radians)));
        }
    }

    // Rotating only does something when more than one direction is allowed
    public override bool CanInteract => GetAllowedAngles().Count > 1;


    public override void init(int x, int y)
    {
        currentAngle = ClosestAllowedAngle(startAngle);
        base.init(x, y);
    }

    public Vector2 Deflect(Vector2 incomingDirection)
    {
        return OutputDirection;
    }

    protected override void Toggle()
    {
        var angles = GetAllowedAngles();
        var index = angles.IndexOf(currentAngle);
        currentAngle = angles[(index + 1) % angles.Count];
    }

    protected override void RefreshVisual()
    {
        if (arrow == null)
        {
            arrow = CreateOverlay("Valve Arrow", arrowSprite != null ? arrowSprite : GeneratedSprites.Triangle, arrowColor, InteractiveOrderOffset);
            arrow.transform.localScale = Vector3.one * 0.6f;
        }

        arrow.transform.localRotation = Quaternion.Euler(0, 0, currentAngle);
    }


    // Allowed angles in rotation order: 0, 90, 180, 270
    private List<int> GetAllowedAngles()
    {
        var angles = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            if (((int)allowedDirections & (1 << i)) != 0) angles.Add(i * 90);
        }

        if (angles.Count == 0) angles.Add(0); // nothing ticked, behave as a fixed right-pointing valve
        return angles;
    }

    private int ClosestAllowedAngle(int angle)
    {
        var normalized = ((angle % 360) + 360) % 360;
        var allowed = GetAllowedAngles();

        var best = allowed[0];
        foreach (var candidate in allowed)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(normalized, candidate)) < Mathf.Abs(Mathf.DeltaAngle(normalized, best))) best = candidate;
        }

        return best;
    }

    private void OnValidate()
    {
        startAngle = Mathf.Clamp(Mathf.RoundToInt(startAngle / 90f) * 90, 0, 270);
    }
}
