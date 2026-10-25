using UnityEngine;

// Colour code linking chemical valves to the Defensive Cells they affect (ChemicalTargetMode.ColorMatch)
public enum ChemicalColor
{
    None,
    Crimson,
    Azure,
    Amber,
    Violet,
}

public static class ChemicalColors
{
    // Display colour for markers, mist and smoke
    public static Color ToColor(this ChemicalColor chemicalColor)
    {
        switch (chemicalColor)
        {
            case ChemicalColor.Crimson: return new Color(1f, 0.3f, 0.4f);
            case ChemicalColor.Azure: return new Color(0.3f, 0.75f, 1f);
            case ChemicalColor.Amber: return new Color(1f, 0.78f, 0.2f);
            case ChemicalColor.Violet: return new Color(0.75f, 0.45f, 1f);
            default: return new Color(1f, 0.96f, 0.7f); // uncoloured enzyme: pale cream, reads well over the green tiles
        }
    }
}
