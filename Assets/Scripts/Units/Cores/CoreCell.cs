using UnityEngine;

// Core Cell: a stationary target. The Cancer Cell captures it by sliding onto its tile,
// and capturing every Core Cell on the board wins the level (see UnitManager).
public class CoreCell : BaseUnit
{
    // Start rather than Awake, so UnitManager.Instance is guaranteed to exist even for scene-placed cells
    private void Start()
    {
        UnitManager.Instance.RegisterCoreCell(this);
    }

    // Safety net for cells destroyed without going through capture (e.g. scene unload)
    private void OnDestroy()
    {
        if (UnitManager.Instance != null) UnitManager.Instance.UnregisterCoreCell(this);
    }
}
