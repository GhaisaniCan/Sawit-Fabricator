using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Grid painter for LevelData: pick a brush, then click or drag on the board.
// Left click paints, right click removes the unit in that cell.
[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private enum BrushKind { Tile, Unit, EraseUnit }

    // Static so the chosen brush survives switching between level assets
    private static BrushKind brushKind = BrushKind.Tile;
    private static int brushTileIndex;
    private static ScriptableUnit brushUnit;

    private const float MinCellSize = 22f;
    private const float MaxCellSize = 48f;

    private static readonly Color emptyCellColor = new Color(0.15f, 0.15f, 0.15f);
    private static readonly Color wallOverlayColor = new Color(0.45f, 0.3f, 0.15f, 0.75f);
    private static readonly Color playerOutline = new Color(0.3f, 0.8f, 1f);
    private static readonly Color enemyOutline = new Color(1f, 0.35f, 0.35f);
    private static readonly Color coreOutline = new Color(0.4f, 1f, 0.45f);

    private static Color GetOutline(ScriptableUnit unit)
    {
        if (unit.UnitPrefab is BasePlayer) return playerOutline;
        if (unit.UnitPrefab is CoreCell) return coreOutline;
        return enemyOutline;
    }

    private List<ScriptableUnit> projectUnits = new List<ScriptableUnit>();


    private void OnEnable()
    {
        projectUnits = AssetDatabase.FindAssets("t:ScriptableUnit")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableUnit>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(u => u != null)
            .OrderBy(u => u.Faction).ThenBy(u => u.name)
            .ToList();
    }

    public override void OnInspectorGUI()
    {
        var level = (LevelData)target;
        serializedObject.Update();

        DrawSize(level);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawTileOptions(level);

        EditorGUILayout.Space();
        DrawBrushes(level);

        EditorGUILayout.Space();
        DrawGrid(level);
        DrawWarnings(level);

        EditorGUILayout.Space();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("units"), new GUIContent("Units (edit blocker overrides here)"), true);
        serializedObject.ApplyModifiedProperties();
    }


    private void DrawSize(LevelData level)
    {
        EditorGUILayout.LabelField("Board", EditorStyles.boldLabel);

        // Delayed fields only resize once the value is committed, not on every keystroke
        var newWidth = EditorGUILayout.DelayedIntField("Width", level.width);
        var newHeight = EditorGUILayout.DelayedIntField("Height", level.height);

        if (newWidth != level.width || newHeight != level.height)
        {
            Undo.RecordObject(level, "Resize Level");
            level.Resize(newWidth, newHeight);
            EditorUtility.SetDirty(level);
        }
    }

    // One row per tile type: painter colour, tile prefab, remove button
    private void DrawTileOptions(LevelData level)
    {
        EditorGUILayout.LabelField("Tile Types", EditorStyles.boldLabel);

        for (int i = 0; i < level.tileOptions.Count; i++)
        {
            var option = level.tileOptions[i];

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();

            var color = EditorGUILayout.ColorField(GUIContent.none, option.editorColor, true, false, false, GUILayout.Width(50));
            color.a = 1f; // painter colours are always opaque so swatches never vanish
            var prefab = (Tile)EditorGUILayout.ObjectField(option.prefab, typeof(Tile), false);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Edit Tile Type");
                option.editorColor = color;
                option.prefab = prefab;
                EditorUtility.SetDirty(level);
            }

            var remove = GUILayout.Button("-", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();

            if (remove)
            {
                Undo.RecordObject(level, "Remove Tile Type");
                level.RemoveTileOption(i);
                brushTileIndex = Mathf.Clamp(brushTileIndex, 0, Mathf.Max(0, level.tileOptions.Count - 1));
                EditorUtility.SetDirty(level);
                GUIUtility.ExitGUI(); // the list changed mid-layout
            }
        }

        if (GUILayout.Button("Add Tile Type"))
        {
            Undo.RecordObject(level, "Add Tile Type");
            level.AddTileOption();
            EditorUtility.SetDirty(level);
        }
    }

    private void DrawBrushes(LevelData level)
    {
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

        if (level.tileOptions.Count == 0)
        {
            EditorGUILayout.HelpBox("Add a tile type (e.g. Grass Tile, Wall Tile) above to paint the board.", MessageType.Info);
        }

        // Tile brushes
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < level.tileOptions.Count; i++)
        {
            var option = level.tileOptions[i];
            var label = option.prefab != null ? option.prefab.name : $"Tile {i}";
            if (BrushButton(label, brushKind == BrushKind.Tile && brushTileIndex == i, option.editorColor, null))
            {
                brushKind = BrushKind.Tile;
                brushTileIndex = i;
            }
        }
        EditorGUILayout.EndHorizontal();

        // Unit brushes, every ScriptableUnit in the project
        EditorGUILayout.BeginHorizontal();
        foreach (var unit in projectUnits)
        {
            var outline = GetOutline(unit);
            if (BrushButton(unit.name, brushKind == BrushKind.Unit && brushUnit == unit, outline, GetSprite(unit)))
            {
                brushKind = BrushKind.Unit;
                brushUnit = unit;
            }
        }
        if (BrushButton("Erase Unit", brushKind == BrushKind.EraseUnit, Color.gray, null))
        {
            brushKind = BrushKind.EraseUnit;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill Board With Tile Brush") && brushTileIndex < level.tileOptions.Count)
        {
            Undo.RecordObject(level, "Fill Level");
            for (int x = 0; x < level.width; x++)
                for (int y = 0; y < level.height; y++)
                    level.SetTileIndex(x, y, brushTileIndex);
            EditorUtility.SetDirty(level);
        }
        if (GUILayout.Button("Clear Units"))
        {
            Undo.RecordObject(level, "Clear Units");
            level.units.Clear();
            EditorUtility.SetDirty(level);
        }
        EditorGUILayout.EndHorizontal();
    }

    private bool BrushButton(string label, bool selected, Color swatch, Sprite sprite)
    {
        var rect = GUILayoutUtility.GetRect(64, 46, GUILayout.ExpandWidth(false));
        var clicked = GUI.Toggle(rect, selected, GUIContent.none, "Button") && !selected;

        var swatchRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, 22);
        EditorGUI.DrawRect(swatchRect, swatch);
        if (sprite != null) DrawSprite(swatchRect, sprite);

        GUI.Label(new Rect(rect.x, rect.y + 26, rect.width, 18), label, EditorStyles.centeredGreyMiniLabel);
        return clicked;
    }


    private void DrawGrid(LevelData level)
    {
        EditorGUILayout.LabelField("Board (left click: paint, right click: remove unit)", EditorStyles.boldLabel);

        var availableWidth = EditorGUIUtility.currentViewWidth - 40;
        var cellSize = Mathf.Clamp(availableWidth / level.width, MinCellSize, MaxCellSize);
        var gridRect = GUILayoutUtility.GetRect(cellSize * level.width, cellSize * level.height, GUILayout.ExpandWidth(false));

        var blockedCells = GetBlockedCells(level);

        for (int x = 0; x < level.width; x++)
        {
            for (int y = 0; y < level.height; y++)
            {
                var cellRect = GetCellRect(gridRect, cellSize, level.height, x, y);
                DrawCell(level, cellRect, new Vector2Int(x, y), blockedCells);
            }
        }

        HandleGridMouse(level, gridRect, cellSize);
    }

    // y = 0 is the bottom row in the game, so draw rows top-down from the highest y
    private static Rect GetCellRect(Rect gridRect, float cellSize, int height, int x, int y)
    {
        return new Rect(gridRect.x + x * cellSize, gridRect.y + (height - 1 - y) * cellSize, cellSize, cellSize);
    }

    private void DrawCell(LevelData level, Rect cellRect, Vector2Int position, HashSet<Vector2Int> blockedCells)
    {
        var inner = new Rect(cellRect.x + 1, cellRect.y + 1, cellRect.width - 2, cellRect.height - 2);

        var tileIndex = level.GetTileIndex(position.x, position.y);
        var tileColor = tileIndex >= 0 && tileIndex < level.tileOptions.Count ? level.tileOptions[tileIndex].editorColor : emptyCellColor;
        EditorGUI.DrawRect(inner, tileColor);

        if (blockedCells.Contains(position))
        {
            EditorGUI.DrawRect(inner, wallOverlayColor);
            GUI.Label(inner, "#", EditorStyles.centeredGreyMiniLabel);
        }

        var placement = level.GetUnitAt(position);
        if (placement != null && placement.unit != null)
        {
            DrawOutline(inner, GetOutline(placement.unit));

            var sprite = GetSprite(placement.unit);
            if (sprite != null) DrawSprite(new Rect(inner.x + 3, inner.y + 3, inner.width - 6, inner.height - 6), sprite);
            else GUI.Label(inner, placement.unit.name.Substring(0, 1), EditorStyles.centeredGreyMiniLabel);
        }

        GUI.Label(new Rect(inner.x + 1, inner.y + inner.height - 12, inner.width, 12), $"{position.x},{position.y}", EditorStyles.miniLabel);
    }

    private void HandleGridMouse(LevelData level, Rect gridRect, float cellSize)
    {
        var e = Event.current;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if (!gridRect.Contains(e.mousePosition)) return;

        var x = Mathf.FloorToInt((e.mousePosition.x - gridRect.x) / cellSize);
        var y = level.height - 1 - Mathf.FloorToInt((e.mousePosition.y - gridRect.y) / cellSize);
        var position = new Vector2Int(x, y);
        if (!level.IsInside(position)) return;

        // Units are placed on click only; dragging would stamp copies across the board
        var isDrag = e.type == EventType.MouseDrag;

        if (e.button == 1)
        {
            RemoveUnit(level, position);
        }
        else if (e.button == 0)
        {
            switch (brushKind)
            {
                case BrushKind.Tile:
                    if (brushTileIndex < level.tileOptions.Count && level.GetTileIndex(x, y) != brushTileIndex)
                    {
                        Undo.RecordObject(level, "Paint Tile");
                        level.SetTileIndex(x, y, brushTileIndex);
                        EditorUtility.SetDirty(level);
                    }
                    break;

                case BrushKind.Unit:
                    if (!isDrag && brushUnit != null) PlaceUnit(level, position, brushUnit);
                    break;

                case BrushKind.EraseUnit:
                    RemoveUnit(level, position);
                    break;
            }
        }

        e.Use();
        Repaint();
    }

    private static void PlaceUnit(LevelData level, Vector2Int position, ScriptableUnit unit)
    {
        Undo.RecordObject(level, "Place Unit");
        level.units.RemoveAll(u => u.position == position);
        level.units.Add(new LevelData.UnitPlacement { position = position, unit = unit });
        EditorUtility.SetDirty(level);
    }

    private static void RemoveUnit(LevelData level, Vector2Int position)
    {
        if (level.GetUnitAt(position) == null) return;

        Undo.RecordObject(level, "Remove Unit");
        level.units.RemoveAll(u => u.position == position);
        EditorUtility.SetDirty(level);
    }


    private void DrawWarnings(LevelData level)
    {
        if (!level.units.Any(u => u.unit != null && u.unit.UnitPrefab is BasePlayer))
        {
            EditorGUILayout.HelpBox("No player unit placed.", MessageType.Warning);
        }

        if (!level.units.Any(u => u.unit != null && u.unit.UnitPrefab is CoreCell))
        {
            EditorGUILayout.HelpBox("No Core Cell placed, so this level can never be won.", MessageType.Warning);
        }

        foreach (var placement in level.units)
        {
            if (placement.unit == null)
            {
                EditorGUILayout.HelpBox($"Unit at {placement.position} has no unit asset and will be skipped.", MessageType.Warning);
                continue;
            }

            var tilePrefab = level.GetTilePrefab(placement.position.x, placement.position.y);
            if (tilePrefab != null && !IsWalkablePrefab(tilePrefab))
            {
                EditorGUILayout.HelpBox($"{placement.unit.name} at {placement.position} is on a non-walkable tile ({tilePrefab.name}).", MessageType.Warning);
            }
        }
    }


    // Tiles walled off by blocking enemies, using each placement's override when set
    private static HashSet<Vector2Int> GetBlockedCells(LevelData level)
    {
        var blocked = new HashSet<Vector2Int>();

        foreach (var placement in level.units)
        {
            if (placement.unit == null || !(placement.unit.UnitPrefab is BlockingEnemy blockingEnemy)) continue;

            var offsets = placement.overrideBlockedTiles ? placement.blockedTileOffsets : blockingEnemy.BlockedTileOffsets;
            if (offsets == null) continue;

            foreach (var offset in offsets)
            {
                var cell = placement.position + offset;
                if (offset != Vector2Int.zero && level.IsInside(cell)) blocked.Add(cell);
            }
        }

        return blocked;
    }

    private static bool IsWalkablePrefab(Tile tilePrefab)
    {
        var walkableProperty = new SerializedObject(tilePrefab).FindProperty("isWalkable");
        return walkableProperty == null || walkableProperty.boolValue;
    }

    private static Sprite GetSprite(ScriptableUnit unit)
    {
        if (unit == null || unit.UnitPrefab == null) return null;
        var spriteRenderer = unit.UnitPrefab.GetComponentInChildren<SpriteRenderer>();
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    // Draws a sprite inside rect, keeping its aspect ratio
    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        var texture = sprite.texture;
        var textureRect = sprite.textureRect;
        var uv = new Rect(textureRect.x / texture.width, textureRect.y / texture.height,
                          textureRect.width / texture.width, textureRect.height / texture.height);

        var aspect = textureRect.width / textureRect.height;
        var fitted = rect;
        if (aspect > rect.width / rect.height)
        {
            fitted.height = rect.width / aspect;
            fitted.y += (rect.height - fitted.height) / 2;
        }
        else
        {
            fitted.width = rect.height * aspect;
            fitted.x += (rect.width - fitted.width) / 2;
        }

        GUI.DrawTextureWithTexCoords(fitted, texture, uv);
    }

    private static void DrawOutline(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color);
    }
}
