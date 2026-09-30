# Feature: LevelGenerator tileset authoring tool

## Objective
Ship an Editor tool with the generator asset that imports/slices a regular-grid texture atlas, lets the user visually map slices to generator tile roles, creates compatible prefab variants, and applies those variants to a selected `LevelGenerator`.

## Scope and decisions
- Provide a Unity EditorWindow under `Assets/2DMapGenerator/Editor/`; the tool ships inside the generator folder.
- User chooses a texture already imported into the Unity project, configures cell size, offset, spacing, and pixels-per-unit, then slices the atlas as a regular grid. Default cell dimensions/PPU are 128 to match current sheets, but remain editable.
- Use the installed Unity Sprite Editor data-provider API (`SpriteDataProviderFactories`, `ISpriteEditorDataProvider`, `ISpriteNameFileIdDataProvider`, `ITextureDataProvider`); do not clone the full Sprite Editor.
- Show the resulting sprite cells visually and let the user assign multiple variants to Floor, Empty, generic Wall, and each cardinal wall role.
- Persist atlas settings, sprite-role assignments, and generated prefab references in a reusable `LevelGeneratorTileSet` profile. The user selects the template source `LevelGenerator` in the EditorWindow when generating variants.
- Generate/update prefabs from the selected generator's existing role templates where possible, changing sprite assignments while preserving renderers, colliders, sorting layers, and other components. Avoid deleting assets or overwriting unrelated prefab paths.
- Apply generated arrays to a selected `LevelGenerator` only on an explicit button click, using Unity Undo/serialization APIs. If a role has no newly assigned tiles, preserve that target's existing prefab array; fall back to the selected template source only when the target array is empty. Block Apply only when a required role would still have no valid prefab asset.
- Keep the generation runtime/prefab pipeline unchanged. No Unity Tilemap migration, no Play Mode entry by the assistant, no commits/pushes without explicit authorization.
- Existing native review lineage `review-039b2b61c6bef438` remains pending after a WebSocket failure; do not retry without authorization.

## Constraints and context
- `LevelGenerator` consumes GameObject arrays (`emptyObj`, `floorObj`, `wallObj`, `wallUpObj`, `wallDownObj`, `wallLeftObj`, `wallRightObj`). All require at least one assigned prefab.
- Existing floor and directional wall prefabs have role-specific renderer/collider/layer setup; generated variants should clone a role template rather than construct blank prefabs.
- `Packages/packages-lock.json` includes built-in `com.unity.2d.sprite` 1.0.0; local Unity 6000.5.9f1 package docs describe Sprite Editor data-provider APIs. The tool remains under `Editor/` and no asmdef is currently present for this bundle.
- The user selected regular-grid slicing. Exact irregular/freehand slicing is out of scope; users can continue using Unity's Sprite Editor for irregular shapes.
- The latest fill-mode implementation is compiled, but its new modes await the user's manual test. Keep that task pending; this tool is a separate follow-on feature.

## Tasks

### ODD-0 — Confirm tool workflow
- **Status:** done
- **Evidence:** User clarified the tool should support the generator asset and selected regular-grid slicing rather than irregular/manual sprite cuts.

### ODD-1 — Add persistent tileset profile and regular-grid slicing
- **Status:** done
- **Evidence:** Added `Assets/2DMapGenerator/LevelGeneratorTileSet.cs`; the EditorWindow creates profile-scoped grid slices through the Sprite Editor data-provider API, persists role/prefab references, preserves unrelated slices, and retains IDs for unchanged slices. Source-image dimensions come from `ITextureDataProvider.GetTextureActualWidthAndHeight`.
- **Acceptance:** A profile stores source atlas, grid settings, sprite-role assignments, and output prefab references. The Editor tool creates stable grid SpriteRects through the installed Sprite Editor data-provider API and reimports the atlas safely.

### ODD-2 — Build visual tile-role mapping window
- **Status:** done
- **Evidence:** Added `Assets/2DMapGenerator/Editor/LevelGeneratorTileSetEditorWindow.cs` with sliced-sprite thumbnails, per-slice role dropdowns, profile persistence, and explicit generation/apply actions.
- **Acceptance:** The window previews sliced cells, allows assigning multiple variants to Floor/Empty/Wall/WallUp/WallDown/WallLeft/WallRight, validates roles, and retains mapping in the profile.

### ODD-3 — Generate compatible prefabs and apply to generator
- **Status:** done
- **Evidence:** Generates from persistent role prefab templates and changes only the selected child `SpriteRenderer.sprite`. Apply is explicit, Undo-backed, includes all seven arrays, preserves existing prefab arrays for unassigned roles, and rejects non-prefab fallback references. Static review found no remaining defects in this flow.
- **Acceptance:** Tool creates or updates tracked variants from the selected category templates while preserving their components; applying is explicit, undoable, updates assigned roles, and safely preserves existing arrays for roles without new art. No required array is left empty or null.

### ODD-4 — Compile and verify the authoring flow
- **Status:** in progress
- **Evidence to date:** Unity AssetDatabase `ForceSynchronousImport` succeeded after the final changes; Console errors in the last five minutes: none. A read-only reviewer confirmed PPU freshness checks, source dimensions, persistent fallback prefabs, all seven roles, and template component preservation. User manual Editor validation is pending; assistant did not enter Play Mode and ran no automated tests.
- **Acceptance:** Unity compiles with no new errors; static/Editor checks confirm slice bounds, role mapping, stable re-slicing behavior, prefab preservation, and required-array validation. Provide manual Editor/Play-mode checks to the user; assistant does not enter Play Mode.

## Next step
The tool is ready for the user to manually test importing/slicing an atlas, assigning roles, generating prefabs, applying arrays, and Undo/Redo. Ask for feedback on grid alignment/offset/spacing, slice previews and role assignment, preservation of the template's visuals/colliders/layers, all seven arrays (especially Generic Wall), and whether Apply/Undo behaves as expected. No Play Mode entry or commits were performed.
