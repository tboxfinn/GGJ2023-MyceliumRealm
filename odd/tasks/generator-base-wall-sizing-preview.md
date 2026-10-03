# Feature: Base wall, topology sections, diagonal-only walls, and PPU-independent sizing

## Objective
Clarify the wall assignment workflow and generated output: expose the brown Base Wall/Background as a visible target, separate cardinal-only/diagonal-only/mixed patterns, spawn diagonal-only wall cells, center Sprite previews, and control exact visual size independently of PPU.

## Confirmed user decisions
- The brown area in the map is the base wall/background; expose it prominently as `Base Wall / Background`, backed by the existing `LevelGeneratorTileRole.Empty` cells. It is not a zero-neighbor topology mask.
- Split topology assignments into **Cardinal Walls** (15 patterns), **Diagonal-only** (15 patterns), and **Walls + Diagonals** (225 patterns), preserving all existing mask bit values.
- Generate wall cells that touch Floor only diagonally, with no cardinal Floor neighbor.
- The shown `WallTiles_15` is a pure NE diagonal and belongs to mask 16, not mixed mask 17 (N + NE). Move only that profile assignment; preserve other mappings.
- Sprite visual size is exact X/Y world units, independent of importer PPU. Set the selected `WallTiles.png` importer to PPU 100 without re-slicing.
- Base Wall is collapsible; topology groups use actual tabs, defaulting to Diagonal-only.
- `Direct Profile Workflow` and `Import Existing Sliced Sprites` should be compact foldouts, collapsed by default, with current controls preserved when expanded.
- Do not show all 225 raw Mixed masks. Show one representative for each distinct neighbor silhouette and reuse existing assignments when a diagonal Floor neighbor is side-connected to a cardinal Floor neighbor. User confirmed canonicalization: mask17 (N + NE) aliases cardinal mask1 (N); the hand-drawn S + NE mask20 remains distinct. Preserve existing exact profile assignments.

## Constraints
- Preserve `Assets/Scenes/Lvl1.unity`, atlas slice rects/IDs/names, prefab structures/transforms, collider geometry, existing cardinal wall placement, and random variant selection.
- Only `WallTiles.png` PPU changes, via Unity TextureImporter/AssetDatabase; no Slice/Re-slice or raw `.meta` edits.
- Visual-size changes apply to spawned SpriteRenderers only, not colliders or prefab transforms.
- No Play Mode, automated tests, commits, pushes, or publishing. User owns final visual and collider validation.

## Completed implementation
- Centered the selected Sprite preview and added Base Wall role labeling, semantic descriptors, and profile SpriteRenderSizeWorldUnits `(1,1)` control.
- Set `WallTiles.png` importer PPU 16 → 100 through Unity. Prior worker verification found 27 existing Sprite subassets (`WallTiles_0`…`WallTiles_26`, 16×16), rects/IDs/local IDs persisted, and sampled importer settings unchanged.
- Added a visible Base Wall/Background target, split the full mask set into three groups, added diagonal-only wall generation, and moved `WallTiles_15` from mask17 to mask16 via Unity AssetDatabase.
- Replaced stacked groups with real Cardinal/Diagonals/Mixed tabs and made Base Wall collapsible. ForceSynchronousImport completed; recent Error/Exception queries were empty.

## Design
- Base Wall / Background is backed by the existing `Empty` role; it has a first-position foldout whose collapsed header shows assigned-variant count.
- Topology tabs are Cardinal-only (15), Diagonal-only (15), and Mixed (225); only one group is visible at a time, defaulting to Diagonal-only.
- Mixed masks are optional overrides. If no mixed Sprite is assigned, runtime falls back to the matching cardinal-only mask when one exists; do not imply all 225 need art.
- For an unassigned mixed pattern, display the inherited cardinal mask and sprite when available; otherwise describe the existing generic/legacy fallback.
- Mixed topology uses user-confirmed canonical visual patterns, not 225 raw cards: NE is redundant if N or E exists; SE if E or S; SW if S or W; NW if W or N. Runtime preserves exact raw overrides, then resolves representative assignments before cardinal fallback. UI shows unique representatives and aliases (17→1; 20 remains distinct; 49→33). No profile/atlas assignments changed. Static source review passed; Unity reported `compiling:false`, `compilationFailed:false`, and zero current Console errors; `recompile` returned `up_to_date` (no fresh compiler invocation), ForceSynchronousImport was unavailable, and Exception could not be queried separately.
- Upper `Direct Profile Workflow` and `Import Existing Sliced Sprites` sections remain expanded in the current Builder; user requests compact foldouts so these controls can be hidden until needed.
- Generate diagonal-only walls after `RemoveSingleWalls` and before mask recomputation, only in still-empty cells with diagonal Floor neighbor(s) and no cardinal Floor neighbors.
- Keep exact masks, filter, assignment/drop/remove, View, semantic labels, preview and visual sizing working across tabs.

## Tasks

### ODD-1 — First-pass Base Wall labeling and topology descriptions
- **Status:** completed
- Existing `Empty` role is labeled Base Wall/Background and topology cards have semantic descriptions.

### ODD-2 — Center the selected Sprite preview
- **Status:** completed
- The selected Sprite is drawn centered in a fit-to-canvas preview; no art/pivot/import changes. Visual confirmation remains user-owned.

### ODD-3 — Add exact PPU-independent visual size
- **Status:** completed
- Added `(1,1)` profile world size; runtime profile overrides apply it only to SpriteRenderers, with legacy fallback. Prefab transforms and colliders remain untouched.

### ODD-4 — Set WallTiles importer PPU to 100 without re-slicing
- **Status:** completed
- Unity TextureImporter PPU set to 100; prior query found all 27 Sprite names, rects, IDs, and local file IDs unchanged.

### ODD-5 — First-pass compile/import check
- **Status:** completed
- ForceSynchronousImport settled with no compile failure; recent Error/Exception queries were empty. No Play Mode/tests.

### ODD-6 — Promote Base Wall and split topology sections
- **Status:** completed
- Added first-position Base Wall/Background backed by `Empty`; split masks into Cardinal-only (15), Diagonal-only (15), and Mixed (225). Moved `WallTiles_15` from mask17 to NE-only mask16 via Unity AssetDatabase.

### ODD-7 — Generate diagonal-only wall cells
- **Status:** completed
- Added a pass after `RemoveSingleWalls` and before mask recomputation. It changes only empty cells with diagonal Floor neighbor(s) and no cardinal Floor neighbors; bounds-safe.

### ODD-8 — Verify initial grouped workflow
- **Status:** completed; interactive visual/collider confirmation remains user-owned
- Source check and ForceSynchronousImport passed; recent Error/Exception queries empty. Unity readback confirmed mask16, PPU100, Sprite/Multiple, 27 sprites, render size `(1,1)`, and grid-slice PPU128. No Play Mode/tests.

### ODD-9 — Add collapsible Base Wall and actual topology tabs
- **Status:** completed; user visual confirmation remains pending
- Base Wall foldout shows assigned count. Cardinal (15), Diagonals (15), and Mixed (225) are actual tabs; Diagonals default; tab changes reset scroll. Source check and ForceSynchronousImport passed; recent Error/Exception queries empty. No interactive UI check.

### ODD-10 — Reuse canonical wall-topology masks at runtime
- **Status:** completed
- `GetCanonicalWallMask` removes diagonals connected to adjacent cardinal Floor bits. `SpawnWall` preserves exact raw profile/prefab priority, then tries canonical profile/prefab, cardinal fallback, and existing legacy behavior. Static examples: 17→1, 20→20, 49→33.

### ODD-11 — Show canonical Mixed patterns and aliases
- **Status:** completed
- Mixed now shows 16 canonical patterns plus any directly assigned noncanonical exact overrides. Cardinal cards list aliases such as mask17; canonical Mixed cards list raw-mask aliases. Filter searches representatives and aliases. Existing mask255 assignment remains visible and unchanged.

### ODD-12 — Verify canonical mask reuse
- **Status:** completed with verification limitations
- Independent static review confirmed representative/alias mappings, exact-override precedence, and table behavior. A later Unity `recompile` completed with `failed:false`, `errors:[]`, and `compilationFailed:false`; Editor is not compiling, domain reload settled, and current Console error count is zero. ForceSynchronousImport and a separate Exception query were unavailable; historical error-buffer entries predated the successful recompile. No Play Mode, automated tests, or asset/profile edits; manual visual confirmation remains user-owned.

### ODD-13 — Collapse upper workflow sections
- **Status:** completed; visual UI confirmation remains user-owned
- `Direct Profile Workflow` and `Import Existing Sliced Sprites` are serialized foldout headers, collapsed by default on a fresh window. All prior controls/actions remain inside balanced expanded bodies. Unity script recompile completed with no failure and current Console error count zero; no asset/profile/scene changes. Manual visual confirmation remains pending.

## Manual checks still owned by the user
- Confirm the Direct Profile Workflow and Import Existing Sliced Sprites foldouts start collapsed and reveal their controls when opened.
- Confirm Base Wall foldout and topology tabs in the Builder.
- Confirm mixed fallback labels, diagonal-only placement, PPU100 appearance, centered artwork, and unchanged colliders in GeneratorTest.
