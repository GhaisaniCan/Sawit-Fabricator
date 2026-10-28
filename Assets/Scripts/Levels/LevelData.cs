using System;
using System.Collections.Generic;
using UnityEngine;

// A hand-made board layout: tile type per cell plus where each unit starts.
// Edit it with the grid painter in its Inspector (Assets/Editor/LevelDataEditor.cs).
[CreateAssetMenu(fileName = "New Level", menuName = "Level Data")]
public class LevelData : ScriptableObject
{
    [Serializable]
    public class TileOption
    {
        public Tile prefab;
        public Color editorColor = Color.gray; // only used by the grid painter
    }

    [Serializable]
    public class UnitPlacement
    {
        public Vector2Int position;
        public ScriptableUnit unit;

        // Only used when the unit is a BlockingEnemy
        public bool overrideBlockedTiles;
        public Vector2Int[] blockedTileOffsets = new Vector2Int[0];
        [Tooltip("Only for Defensive Cells: colour for ColorMatch chemical valves. None keeps the prefab's colour.")]
        public ChemicalColor chemicalColor;

        // Only used when the unit is a MovingCell: its steps, one per Tech Bug move, repeated in order
        public bool overrideMoveSequence;
        [Tooltip("Only for Moving Cells: one tile step per Tech Bug move, e.g. (0,1), (0,-1). (0,0) waits a turn.")]
        public Vector2Int[] moveSequence = new Vector2Int[0];

        // Any capturable unit: energy gained for capturing this one, instead of the prefab's value
        public bool overrideMovesRestored;
        [Min(0)] public int movesRestoredOnCapture;
    }

    [Min(1)] public int width = 8;
    [Min(1)] public int height = 8;

    [Tooltip("Energy the Tech Bug starts with; each slide costs 1. 0 = unlimited (no energy limit).")]
    [Min(0)] public int initialMoves = 3;
    [Tooltip("Energy cap: recharges from captures can't go above this (the \"/ max\" in an energy counter).")]
    [Min(1)] public int maxMoves = 10;

    public bool HasMoveLimit => initialMoves > 0;

    public List<TileOption> tileOptions = new List<TileOption>();

    // Index into tileOptions for each cell, stored row by row (index = y * width + x)
    [SerializeField, HideInInspector] private int[] cells = new int[64];

    public List<UnitPlacement> units = new List<UnitPlacement>();


    public int GetTileIndex(int x, int y)
    {
        EnsureCellCount();
        return cells[y * width + x];
    }

    public void SetTileIndex(int x, int y, int tileIndex)
    {
        EnsureCellCount();
        cells[y * width + x] = tileIndex;
    }

    // Distinct colours for new tile types, spread around the hue wheel by the golden ratio
    public static Color GetDefaultTileColor(int index)
    {
        return Color.HSVToRGB((0.33f + index * 0.618f) % 1f, 0.55f, 0.75f);
    }

    public void AddTileOption()
    {
        tileOptions.Add(new TileOption { editorColor = GetDefaultTileColor(tileOptions.Count) });
    }

    // Cells painted with the removed type fall back to the first type; later indices shift down
    public void RemoveTileOption(int index)
    {
        EnsureCellCount();
        tileOptions.RemoveAt(index);

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == index) cells[i] = 0;
            else if (cells[i] > index) cells[i]--;
        }
    }

    public Tile GetTilePrefab(int x, int y)
    {
        var index = GetTileIndex(x, y);
        return index >= 0 && index < tileOptions.Count ? tileOptions[index].prefab : null;
    }

    public UnitPlacement GetUnitAt(Vector2Int position)
    {
        return units.Find(u => u.position == position);
    }

    public bool IsInside(Vector2Int position)
    {
        return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
    }

    // Changes the board size, keeping cells and units that still fit
    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);
        EnsureCellCount();

        var newCells = new int[newWidth * newHeight];
        for (int x = 0; x < Mathf.Min(width, newWidth); x++)
        {
            for (int y = 0; y < Mathf.Min(height, newHeight); y++)
            {
                newCells[y * newWidth + x] = cells[y * width + x];
            }
        }

        width = newWidth;
        height = newHeight;
        cells = newCells;
        units.RemoveAll(u => !IsInside(u.position));
    }

    private void EnsureCellCount()
    {
        if (cells == null || cells.Length != width * height)
        {
            Array.Resize(ref cells, width * height);
        }
    }

    private void OnValidate()
    {
        EnsureCellCount();
        if (HasMoveLimit) maxMoves = Mathf.Max(maxMoves, initialMoves);

        // List elements added through the default Inspector start fully transparent
        for (int i = 0; i < tileOptions.Count; i++)
        {
            if (tileOptions[i].editorColor.a == 0f) tileOptions[i].editorColor = GetDefaultTileColor(i);
        }
    }
}
