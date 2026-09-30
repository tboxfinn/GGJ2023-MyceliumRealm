# Feature: Live editor preview for LevelGenerator

## Objective
Show the generated map in the Unity Editor without entering Play Mode, and refresh it automatically when relevant Inspector values change (including fixed seed and generation parameters).

## Scope and decisions
- Use an editor-only custom Inspector and preview lifecycle for `LevelGenerator`; keep runtime `Start()` generation intact.
- The map preview is temporary, non-persistent content: it must not become serialized scene content or dirty the saved scene merely by previewing.
- Auto-refresh after relevant Inspector edits; coalesce edits so a single inspector change does not trigger repeated generations. Changing `fixedSeed` must update the map when fixed-seed mode is enabled.
- Keep gameplay scene `Assets/Scenes/Lvl1.unity` untouched and do not enable preview there by default. Enable the preview only for the dedicated `GeneratorTest` component; other instances can opt in.
- Current persisted `GeneratorTest` baseline: 20x20, fixed seed enabled with seed 10. Earlier in-memory reads of 20x12/seed disabled were transient and did not survive reload; do not overwrite the saved baseline without a separate user decision.
- No commit, push, or publication without explicit user authorization.

## Constraints and context
- The user ran Play-mode testing and confirmed the generator works. The assistant did not enter Play Mode; user owns any future Play-mode regressions.
- Previous native review lineage `review-039b2b61c6bef438` remains pending after a WebSocket failure. The user has not authorized a retry; do not retry or claim review approval.
- Unity AssetDatabase/editor APIs must be used for asset-folder creation and scene edits.

## Tasks

### ODD-0 — Confirm preview behavior and baseline
- **Status:** done
- **Evidence:** User selected a temporary preview that is regenerated in the Editor but not saved into the scene. Fresh Unity and YAML reads agree on the current 20x20, fixed-seed-10 scene baseline.

### ODD-1 — Implement edit-mode preview lifecycle and automatic Inspector refresh
- **Status:** done
- **Acceptance:** Preview appears without Play; relevant serialized Inspector changes refresh it once; preview preserves global random state, cleans up safely on scene/play transitions, and does not serialize generated objects.
- **Evidence:** Added a custom Inspector and edit-mode lifecycle with coalesced refresh, fixed-seed support, a 10,000-cell preview cap, RNG/generator-state restoration, owner-marked transient roots, and cleanup for disabled/inactive components. Static verification confirmed preview roots are not matched by name alone and no scene dirty flag is cleared. Unity compilation succeeds after switching to the Unity 6 `EntityId` API.

### ODD-2 — Enable preview in GeneratorTest only
- **Status:** done
- **Acceptance:** `GeneratorTest` opts into live preview; `Lvl1` and other scenes remain unaffected unless explicitly enabled.
- **Evidence:** Set and saved `livePreview: true` only in `Assets/2DMapGenerator/Scenes/GeneratorTest.unity`; the field defaults off for other generator instances. The scene remains valid and clean with two saved roots.

### ODD-3 — Prepare manual test checklist and receive feedback
- **Status:** done
- **Acceptance:** User checks preview appearance, seed and other parameter refresh, and toggle cleanup; user may verify runtime behavior in Play Mode.
- **Evidence:** User confirmed the map appears in the Scene view with Live Preview enabled, changing the seed changes the preview, changing `Percent To Fill` visibly changes the map, toggling Live Preview off/on works, and the runtime generator works in Play Mode.

## Next step
No further preview implementation is pending. If the user wants the generator packaged for reuse/publication, first choose UPM-package or Unity Asset Store distribution; the previous native review lineage also remains pending after a WebSocket failure.