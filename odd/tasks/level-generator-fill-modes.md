# Feature: Selectable LevelGenerator fill modes

## Objective
Add selectable floor-generation algorithms to `LevelGenerator` while preserving the current Random Walk behavior as the default.

## Scope and decisions
- Add three modes: **Random Walk** (existing behavior), **Rooms + Corridors**, and **Cellular Automata**.
- Keep Random Walk selected by default so existing scenes preserve their current generation behavior.
- Each mode produces the same floor/empty grid states, then reuses the existing wall creation, cleanup, prefab spawning, fixed-seed behavior, and transient editor preview pipeline.
- Keep generated floor cells connected: room layouts connect rooms with corridors; cellular layouts connect surviving floor regions with narrow corridors.
- Keep mode-specific parameters understandable in the Inspector: existing walker settings for Random Walk; room count and size controls for Rooms + Corridors; initial fill chance, smoothing iterations, and neighbor threshold for Cellular Automata. `Percent To Fill` retains its existing meaning for Random Walk.
- Preserve the outer wall border, generation safety limits, and deterministic fixed-seed behavior. Do not migrate this generator to Unity Tilemap or change assets/scenes.
- User owns Play-mode testing; do not enter Play Mode or run Play-mode tests. No commit/push without explicit user authorization.
- Existing native review lineage `review-039b2b61c6bef438` is pending after a WebSocket failure; do not retry without authorization.

## Constraints and context
- Current code is in `Assets/2DMapGenerator/LevelGenerator.cs`; `LevelGeneratorEditor` is a custom Inspector in `Assets/2DMapGenerator/Editor/LevelGeneratorEditor.cs` and explicitly tracks preview-affecting properties.
- No generator-specific tests or test assembly definitions were found under `Assets`. Strict TDD is disabled; use Unity compilation and focused static/Editor checks, without entering Play Mode.
- Keep the change limited to the generator script and its custom Inspector unless exploration discovers a necessary additional surface.

## Tasks

### ODD-0 — Confirm modes and compatibility defaults
- **Status:** done
- **Evidence:** User approved adding the three discussed algorithms and a selector. Existing Random Walk remains the default; the other modes receive their own Inspector parameters.

### ODD-1 — Implement fill strategies and selector
- **Status:** done
- **Acceptance:** Inspector selector dispatches to Random Walk, Rooms + Corridors, or Cellular Automata; each strategy produces valid floor/empty grid data and reuses common wall/spawn code. New mode settings validate safely and honor fixed seeds and limits.
- **Evidence:** Added three dispatchable strategies. The original Random Walk loop remains as the default. Room placement is bounded, rooms connect with L-corridors, and Cellular Automata smooths the interior then joins disconnected floor regions. Mode settings validate by selected mode and retain grid, work, and placement limits.

### ODD-2 — Integrate Inspector controls and preview refresh
- **Status:** done
- **Acceptance:** Inspector shows the selector and relevant mode-specific settings; changing the mode or any strategy parameter refreshes the live preview. Existing preview controls and default scenes continue to behave as before.
- **Evidence:** The custom Inspector now shows clear mode labels/help, displays only settings for the selected mode, and tracks the selector and all mode-specific fields as preview inputs.

### ODD-3 — Compile and verify behavior boundaries
- **Status:** pending
- **Acceptance:** Unity compiles; static/read-only checks confirm all modes feed the shared wall/spawn pipeline, respect the reserved map border, and refresh deterministically with fixed seeds. Provide the user a short manual Play-mode checklist; the assistant does not enter Play Mode.
- **Evidence (partial):** Unity AssetDatabase synchronous refresh completed successfully; Console returned no errors in the last five minutes. Independent static verification confirmed the shared pipeline, reserved borders, mode-specific validation, connectivity repair, deterministic fixed-seed path, and preview refresh bindings. Runtime behavior for the new modes remains unverified until the user tests it.

## Next step
Waiting for the user to manually select each mode in `GeneratorTest`, verify the map and preview refresh, and confirm fixed-seed repeatability. The assistant did not enter Play Mode. The native review preflight is also waiting on a human selection of intended untracked paths; the previous lineage `review-039b2b61c6bef438` remains pending after a WebSocket failure and must not be retried without authorization.