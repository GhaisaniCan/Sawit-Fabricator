# Sawit Fabricator — Project Context

A 2D turn-based grid tactics puzzle in Unity, themed as a **Cancer Cell infecting plant tissue**.
The player clicks the Cancer Cell and picks one of up to 8 directions, and it slides until something
stops it. Capturing every **Core Cell** wins. **Defensive Cells** wall off tiles. Clickable plant tiles
(membrane gates, deflector valves) change the board.

| Theme name      | Class                        |
|-----------------|------------------------------|
| Cancer Cell     | `BasePlayer` (`Player1`)     |
| Core Cell       | `CoreCell : BaseUnit`        |
| Defensive Cell  | `BlockingEnemy : BaseEnemy` (grows `VineSegment` vines) |
| Membrane gate   | `CellWallGateTile`           |
| Deflector valve | `DeflectorValveTile`         |
| Chemical valve  | `ChemicalValveTile` (retracts vines) |

## Tech

- **Unity 6000.6.5f1** (Unity 6.6), **URP 2D** renderer, new **Input System** (`InputSystemUIInputModule`)
- Clicks come through the EventSystem: `Physics2DRaycaster` on the camera, `BoxCollider2D` on clickables,
  and `IPointer*Handler` interfaces. There's no `Update()` polling of the mouse.
- No asmdefs for game code. Scripts compile into `Assembly-CSharp`, and `Assets/Editor/` compiles into
  `Assembly-CSharp-Editor`.
- Main scene: `Assets/Scenes/LevelScene.unity`, the only enabled build scene. `SampleScene` is left over
  from the template and disabled.

## Folder layout

```
Assets/
  Scripts/
    Managers/   GameManager, GridManager, UnitManager   (singletons via static Instance set in Awake)
    Tiles/      Tile (abstract), GrassTile, WallTile
      Plant/    PlantInteractableTile (abstract), CellWallGateTile, DeflectorValveTile, ChemicalValveTile,
                ChemicalColor (enum + colours)
    Units/      BaseUnit, ScriptableUnit, MoveArrow, CellIdleAnimator
      Players/  BasePlayer, Player1
      Enemies/  BaseEnemy, Enemy1, Enemy2, BlockingEnemy, VineSegment
      Cores/    CoreCell
    Levels/     LevelData (ScriptableObject)
    Utility/    GeneratedSprites (placeholder square / frame / triangle sprites), CameraController
  Editor/       LevelDataEditor (grid painter for LevelData)
  Levels/       Level 1, Level Test, Level 2 Infection Route, Level 3 Overgrowth (vines + chemical valve demo),
                Level 4 Chemistry (all three valve targeting modes)
  Prefabs/      Tiles/ (Base, Grass, Wall, Gate, Gate Closed, Deflector Valve, Chemical Valve [+ Area, Azure], AssetTestTile)
                Units/ (PlayerSkull, EnemyPrabowo, EnemySawit, EnemyBlocker, EnemyBlocker Thorny, Cores/CoreCell)
                FX/ (Chemical Smoke)
  Materials/    Chemical Smoke.mat
  Resources/Units/  ScriptableUnit assets (loaded with Resources.LoadAll<ScriptableUnit>("Units"))
  Sprites/      Prabowo.png, Sawit.png, Skull-Emoji.png
    Cells/      Game art: cancer/core/defensive cells, cytoplasm, cellulose wall, gates, valve, arrows
                (512 px, 512 PPU = 1 tile; SVG sources in Art/Source/ at the project root)
```

## Game flow

`GameManager.ChangeState(GameState)` drives the game:

```
Start → GenerateGrid → SpawnPlayerUnit → SpawnEnemyUnit → PlayerTurn ──→ Win / Lose
         (GridManager)   (UnitManager)     (UnitManager:      (moves and
                                            enemies + cores)   clicks repeat)
```

- The player keeps moving during `PlayerTurn`. `EnemyTurn` exists but is unused.
- **Win:** a move captures the last Core Cell (`UnitManager.ResolveOutcome(capturedCoreCell: true)`).
- **Lose (hazard):** the last Cancer Cell slides into hazardous vines and is destroyed (`DestroyPlayerInVines`).
- **Lose (stuck):** Core Cells remain and no Cancer Cell has any safe slide in any direction. With
  `UnitManager.interactablesPreventLose` on (the default), it's also required that no gate or valve can
  be clicked, since clicking one might open a path. Turn it off for the strict rule.
- The outcome is checked after every finished slide and after every gate or valve click (`OnBoardChanged`).
- **Turns:** `UnitManager.SlideFinished` (static event) fires once per finished slide, after win/lose is
  resolved. Timed effects (vines retracted by a chemical valve) count these.
- `GameManager` Win/Lose handlers clear the selection, log, and invoke the `onWin`/`onLose` UnityEvents.
  The static `StateChanged` event fires on every state change. `RestartLevel()` reloads the scene for a Retry button.
- Input stops automatically once the state leaves `PlayerTurn`, because tiles and arrows check for it.

## Core systems

### Grid (`GridManager`)
- Tiles are stored in `Dictionary<Vector2, Tile>`, keyed by integer world position (tile (x, y) sits at
  world (x, y)). `AllTiles` lists every tile.
- `GetTileAtPosition(Vector2)` returns null when the position is off the board, which acts as the board edge.
- Camera is centred on the board after generation.
- **With a `LevelData` assigned** (the `level` field), the size and the tile in each cell come from the level.
  Without one, it falls back to `width`/`height`/`tilePrefab`, and units spawn randomly on each half of the board.
- LevelScene currently has **Level 2 Infection Route** assigned.

### Tiles (`Tile`)
- `walkable => isWalkable && occupiedUnit == null && !isBlocked`. `isWalkable` is protected, so
  subclasses (gates) can change it.
- `SetUnit(unit)` moves a unit onto the tile and clears its previous tile.
- `SetSelected(bool)` keeps the highlight on for the selected unit's tile.
- Clicking a tile: a Cancer Cell on it → select it; else if `CanInteract` → `Interact()`, then
  `UnitManager.OnBoardChanged()`; otherwise deselect.
- Blocking: `AddBlocker`/`RemoveBlocker` track a `HashSet<BlockingEnemy>`, so a tile stays blocked until
  **every** blocker retracts. The tile only tracks state; vine visuals belong to the `BlockingEnemy`.
  `IsHazardous` is true if any blocker on it is hazardous; `IsWalkableTerrain` ignores units and vines.
- `CreateOverlay(name, sprite, color, orderOffset)` adds a child sprite on the tile's sorting layer.

### Camera (`CameraController`, on Main Camera)
- Middle/right-mouse drag pans 1:1 with the cursor (after `dragThresholdPixels`), WASD/arrows pan, and the
  scroll wheel zooms toward the cursor with damping (`minOrthographicSize`..`maxOrthographicSize`).
- Clamped so the view stays within the board (`GridManager.Width`/`Height`) plus `boundsPadding`. If the view
  is bigger than that, it's centred. Input is ignored while the app is unfocused.
- Board clicks fire on **pointer down**, so `Tile` and `MoveArrow` only react to the left button and ignore
  presses while `CameraController.IsPanning`.

### Units
- `BaseUnit` has `UnitName`, `OccupiedTile`, `Faction`.
- **Check units by type (`is BasePlayer`, `is CoreCell`, `is BlockingEnemy` / `is BaseEnemy`), not by the
  `Faction` field.** Prefab faction values have been wrong before (EnemySawit was set to Player).
- `ScriptableUnit` (in `Resources/Units`) pairs a `Faction` with a `UnitPrefab`. Level placements and
  random spawning both refer to these assets. Random spawning picks by prefab type.
- **`CellIdleAnimator`** (on PlayerSkull, CoreCell, EnemyBlocker and its Thorny variant): sine "breathing"
  scale (`scaleFrequency`, `scaleAmplitude`, optional `squashAndStretch`) and Z sway (`rotationFrequency`,
  `rotationAngleMax`), random phases per cell. It animates relative to the scale/rotation captured in
  `OnEnable` and restores them in `OnDisable`, so **disable it before animating the unit's transform
  yourself** (`DestroyPlayerInVines` does). Slides only move position, so they don't conflict.

### Movement (`UnitManager` + `MoveArrow`)
- Arrows appear on neighbouring tiles for each of the 8 directions where the slide path isn't empty.
- `GetSlidePath(start, direction)` returns every tile passed through; the last one is where the unit ends up:
  - off the board → stop on the current tile
  - an enemy or Core Cell (`IsCapturable`) → stop **on** its tile (capture)
  - a friendly unit, non-walkable tile (wall, closed gate) or vine-blocked tile → stop **before** it
  - hazardous vines over walkable terrain → enter the tile and be destroyed (`endsInHazard`); these
    moves still get arrows, tinted red, and don't count as a way out for the lose check
  - a deflector valve → enter it, then continue in the valve's `OutputDirection`. Entering the same
    valve with the same heading twice ends the slide, which stops valve loops from running forever.
- `SlideAlongPath` animates tile by tile (`moveSpeed`, default 8) so turns at valves are visible, then
  captures, calls `SetUnit` and resolves win/lose. Input is ignored while `IsBusy`.
- `Capture` removes a Core Cell from the count right away (`Destroy` is deferred), then destroys the unit.
- `MoveArrow` uses `GeneratedSprites.Triangle` unless `arrowSprite` is set on UnitManager. It must be on
  the **"Units"** sorting layer, or the tiles cover it and catch its clicks.

### Core Cells (`CoreCell : BaseUnit`)
- Stationary targets. They register with `UnitManager` in `Start` and unregister in `OnDestroy`.
- `UnitManager.ActiveCoreCellCount` is the number still on the board. A level with none can't be won,
  and the level painter warns about this.
- Spawned from `LevelData` placements (`SpawnEnemies` spawns `BaseEnemy` and then `CoreCell` placements).
  They don't spawn in the random fallback mode.
- Prefab: `Prefabs/Units/Cores/CoreCell` (`core_cell` art). Unit asset:
  `Resources/Units/Cores/CoreUnit 1`.

### Defensive Cells and vines (`BlockingEnemy : BaseEnemy`, `VineSegment`)
- The Defensive Cell is a host that grows **vines** onto the tiles at `blockedTileOffsets` (relative to its
  own tile; default the 4 neighbours). Covered tiles are blocked. A level placement can override the offsets
  via `SetBlockedTileOffsets`, which must be called before `Start`.
- **Building (in `Start`):** a cheapest-path search from the host through the covered tiles gives each tile a
  parent. A diagonal step costs more than two straight ones, so vines run straight and only cut corners when
  they must. Offsets not touching the rest of the vine get a stub pointing toward the host.
- **`VineSegment`** = one tile's piece, a child of the *tile* (so it can animate away after the host dies).
  It draws `stemSprite` (a half stem from the centre to the right edge, rotated toward each connection and
  stretched on diagonals) plus a `tipSprite` (end, pointing away from its parent) or a `knotSprite`
  (corner/branch). Shapes: Root (host tile, under the host sprite), Tip, Straight, Corner, Branch.
  Drawn on TileRender order 1, stems ×1.6 thick, knots/tips ×1.3. Missing sprites fall back to thin bars.
- **State:** vines are out unless toggled off (`SetVinesActive(false)`) or retracted for N turns
  (`RetractForTurns(n)`, counted on `UnitManager.SlideFinished`). Changing state adds/removes the tiles'
  blockers immediately and animates the pieces (grow outward / retract inward, `growDuration`, `growStagger`).
  When a timed retraction runs out, the vines regrow and call `UnitManager.OnBoardChanged()`.
- **Capture:** `OnDestroy` frees every tile and retracts + destroys all its pieces.
- **Hazardous** (`hazardous`, uses `hazardStemSprite`): sliding into these vines destroys the Cancer Cell.
- `BlockingEnemy.VinesChanged` (static event) fires on every extend/retract/timer tick.
- **`enemyColor`** (`ChemicalColor`) links it to ColorMatch chemical valves and shows a coloured dot on the
  cell (Units order 1). Per-level override: `LevelData.UnitPlacement.chemicalColor` → `SetEnemyColor` (before Start).
  `CoveredTiles` lists the tiles its vines cover.
- Prefabs: `EnemyBlocker` (normal) and `EnemyBlocker Thorny` (variant: hazardous, crimson-tinted host),
  unit assets `EnemyUnit Blocker` / `EnemyUnit Thorny Blocker`.

### Plant tiles (`PlantInteractableTile : Tile`)
- `PlantInteractableTile` turns `Tile`'s `CanInteract`/`Interact()` into `Toggle()` + `RefreshVisual()`,
  and tints the base sprite with `baseColor` in `init`.
- **`CellWallGateTile`:** clicking it toggles `isWalkable` (Open ↔ Closed). `startsOpen` sets the
  starting state. A gate can't close while a unit is on it. Visuals: a hollow frame when open, a solid
  square when closed (`openSprite`/`closedSprite` can replace them).
- **`DeflectorValveTile`:** `allowedDirections` is a flags mask (Right 0°, Up 90°, Left 180°, Down 270°)
  and `startAngle` is the starting direction. Clicking it rotates to the next allowed angle. It can only
  be clicked when 2 or more directions are allowed, so a single direction makes a fixed deflector. The
  output direction is absolute: it doesn't depend on which way the unit came in. Shown as a triangle arrow.
- **`ChemicalValveTile`:** an enzyme gland that makes Defensive Cells retract their vines.
  - `mode`: TimedRetract (`retractTurns`, default 2; clicking again restarts the timer) or Toggle
    (retracted until clicked again).
  - `targetMode` (`ChemicalTargetMode`): **AreaRadius** (hosts within `radius` tiles, Chebyshev, so 1 = 3×3),
    **ColorMatch** (hosts whose `EnemyColor` == `valveColor`), **Global** (every Defensive Cell).
  - `CanInteract`: there's a target, or the valve is active (so a Toggle valve can always be switched off).
  - Each valve tracks **its own** active state (Toggle flag, or its own `turnsLeft` counted on
    `SlideFinished`), so two valves hitting the same cell don't light each other up.
  - While active: `releasedSprite`, a pulsing **mist** overlay (`mistSprite`, `mistAlpha`, tinted by
    `valveColor`) on the affected tiles (AreaRadius: the whole area; otherwise each target's host + vine
    tiles), and an optional `smokePrefab` particle cloud on the valve. Mist fades in and out; smoke stops
    emitting and is destroyed once its puffs have died. Everything is cleaned up when the effect ends.
  - ColorMatch valves show a colour dot matching the dot on the Defensive Cells they affect.
- **`ChemicalColor`** (enum + `ToColor()`): None, Crimson, Azure, Amber, Violet. None = no marker, never
  colour-matched; its mist colour is pale cream.
- Prefabs `Prefabs/Tiles/Gate Tile`, `Gate Tile Closed`, `Deflector Valve Tile`, `Chemical Valve Tile` (Global,
  Timed), `Chemical Valve Area` (radius 1, Timed) and `Chemical Valve Azure` (ColorMatch Azure, Toggle) are
  variants of Base Tile. Add them as tile types in a LevelData to paint them. Smoke FX:
  `Prefabs/FX/Chemical Smoke` (material `Materials/Chemical Smoke`, Sprites/Default + mist texture).

### Level authoring (`LevelData` + `LevelDataEditor`)
- Create one with *Create → Level Data*.
- Holds `width`, `height`, `tileOptions` (tile prefab + painter colour), `cells` (one int per cell,
  `index = y * width + x`, indexing into `tileOptions`), and `units` (position + ScriptableUnit +
  optional Defensive Cell overrides: vine offsets and `chemicalColor`).
- The Inspector painter has:
  - **Tile Types:** rows with a colour picker and prefab field, plus Add and Remove buttons.
  - **Brushes:** one for each tile type, one for each ScriptableUnit in the project, and Erase Unit.
    Outlines: blue for the Cancer Cell, green for Core Cells, red for enemies.
  - **Grid:** left-click or drag paints, right-click removes a unit. Row 0 is at the bottom.
  - **Wall preview:** tiles a Defensive Cell will wall off are shown on the grid.
  - **Warnings:** no player placed, no Core Cell placed, unit on a non-walkable tile, placement with no unit asset.
- Resize with the Width and Height fields, or `LevelData.Resize` in code. Changing `width`/`height`
  directly would scramble the cells.
- Removing a tile type re-maps the cells (`RemoveTileOption`).

## Rendering / sorting layers

Sorting layers, bottom to top: **Default → TileRender → Units**. The sorting layer always outranks the
sorting order, both for drawing and for which object gets a click.

| Thing                           | Layer      | Order |
|---------------------------------|------------|-------|
| Tile sprite                     | TileRender | 0     |
| Tile highlight                  | TileRender | 1     |
| Vine segments (stems, knots, tips) | TileRender | 1  |
| Chemical mist overlay (z −0.02, over vines) | TileRender | 1 |
| Gate membrane / valve arrow / chemical gland / valve colour dot | TileRender | 2 |
| Chemical smoke particles        | TileRender | 3     |
| Defensive Cell colour dot       | Units      | 1     |
| Units (Cancer, Core, Defensive) | Units      | 0     |
| Move arrows                     | Units      | 10    |

The Global Light 2D lights TileRender and Units. Sprites on a layer it doesn't light will render black.

## Conventions & gotchas

- **Unity CLI is connected** (`unity status`). Prefer `unity command` / `script.run` over hand-editing
  `.prefab`/`.unity`/`.asset` YAML. Play mode crawls when the Editor is unfocused; for scripted play tests use
  `unity command editor.playmode.set_run_in_background --enable true`, and turn it off afterwards.

- **Rename or move scripts and assets inside Unity** (or move the `.meta` file with them) so GUIDs and
  prefab references survive.
- **One MonoBehaviour per file, with the file named after the class**, or Unity can't attach it. That's
  why the gate and valve each have their own file.
- **Don't use `using NUnit.Framework;` in runtime scripts.** It compiles in the Editor but breaks player
  builds. Some IDEs add it automatically.
- Positions are rounded (`Mathf.Round`) before dictionary lookups to avoid float mismatches.
- Code style: Allman-ish braces, `PascalCase` public fields on units (`OccupiedTile`), `camelCase`
  private/serialized fields, short comments only where the reason isn't obvious.

## Not done yet / ideas

- Win/lose UI: the `onWin`/`onLose` events and `RestartLevel()` are ready, but no panel exists yet
- Real dead-end detection. Lose currently ignores whether clicking a gate or valve could actually help
  (see `interactablesPreventLose`)
- Enemy turn logic (unused `EnemyTurn` state)
- Level selection or loading more than the one level assigned on GridManager
- Art for the generic enemies (EnemyPrabowo, EnemySawit still use their old images)

## Art notes

- Sprites in `Assets/Sprites/Cells` are on the prefabs: units at scale 0.85–0.9, tiles on the Square child,
  gate `openSprite`/`closedSprite`, valve `arrowSprite`, UnitManager `arrowSprite` (in LevelScene).
- Tints are white so the art shows its own colours. GrassTile's checkerboard uses a very light offset tint.
- Vines use `vine_stem` / `vine_stem_thorny` / `vine_knot` / `vine_tip`; the chemical valve uses
  `chemical_valve_idle` / `chemical_valve_released`. Same 512 px / 512 PPU setup, SVG sources in Art/Source.
- Because vines draw on TileRender 1, a gate or valve under a vine draws its own visual (order 2) over it.
- `MoveArrow` is scaled 0.8 because the `move_arrow` art has padding (so it draws at about half a tile).
