using UnityEngine;

// Gentle organic idle motion for cells (Cancer Cell, Core Cells, Defensive Cells):
// a sine-wave "breathing" scale pulse plus a slow rotation sway.
// It animates relative to the scale/rotation the cell had when enabled, so prefab scales are kept,
// and both are restored when it's disabled. Disable it before animating the transform yourself
// (UnitManager does this before the hazard death shrink).
public class CellIdleAnimator : MonoBehaviour
{
    [Header("Breathing (scale)")]
    [SerializeField, Min(0f), Tooltip("Breaths per second")] private float scaleFrequency = 0.6f;
    [SerializeField, Range(0f, 0.3f), Tooltip("Fraction of the base scale, e.g. 0.05 = ±5%")] private float scaleAmplitude = 0.05f;
    [SerializeField, Tooltip("Stretch one axis while the other shrinks, like a soft membrane, instead of a uniform pulse")]
    private bool squashAndStretch = true;

    [Header("Sway (rotation)")]
    [SerializeField, Min(0f), Tooltip("Sways per second")] private float rotationFrequency = 0.35f;
    [SerializeField, Range(0f, 30f), Tooltip("Max degrees either side of the base rotation")] private float rotationAngleMax = 4f;

    private Vector3 baseScale;
    private Quaternion baseRotation;
    private float scalePhase;
    private float rotationPhase;
    private bool hasBase;

    private void Awake()
    {
        // Random phases so cells on the same board don't breathe in unison
        scalePhase = Random.Range(0f, Mathf.PI * 2f);
        rotationPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnEnable()
    {
        baseScale = transform.localScale;
        baseRotation = transform.localRotation;
        hasBase = true;
    }

    private void OnDisable()
    {
        if (!hasBase) return;

        transform.localScale = baseScale;
        transform.localRotation = baseRotation;
        hasBase = false;
    }

    private void Update()
    {
        var time = Time.time;

        var breath = Mathf.Sin(time * scaleFrequency * Mathf.PI * 2f + scalePhase) * scaleAmplitude;
        transform.localScale = squashAndStretch
            ? new Vector3(baseScale.x * (1f + breath), baseScale.y * (1f - breath), baseScale.z)
            : baseScale * (1f + breath);

        var sway = Mathf.Sin(time * rotationFrequency * Mathf.PI * 2f + rotationPhase) * rotationAngleMax;
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, sway);
    }
}
