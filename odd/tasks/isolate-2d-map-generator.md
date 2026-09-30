# Feature: Isolate and harden 2DMapGenerator

## Objective
Move the existing procedural 2D level generator and its current resource bundle into `Assets/2DMapGenerator`, then address the runtime limitations identified in the initial inspection. Keep the existing `Lvl1` scene usable while creating a clear export boundary for later Asset Store preparation.

## Problem and rationale
`LevelGenerator.cs` and its prefabs originally lived under `Assets/Scripts/DungeonGenerator` and were coupled to the old project layout. The runtime generator had a non-square dimension mismatch, hardcoded generation controls, no repeatable seed, no owned generated-object lifecycle, and no validation for missing configuration. A self-contained asset folder should make it easier to export and work on without unrelated gameplay content.

## Scope
- Move the existing `Assets/Scripts/DungeonGenerator` bundle intact to `Assets/2DMapGenerator` using Unity's AssetDatabase move operation; preserve all `.meta` GUIDs and references. Keep `Assets/Scenes/Lvl1.unity` and unrelated game assets outside the export folder.
- Keep the current `Lvl1` 50x50 setup and automatic runtime generation working.
- Correct rectangular map sizing; expose meaningful generation parameters; add deterministic seed control; parent generated objects and support clearing/regeneration; validate invalid dimensions and required prefab arrays with clear diagnostics.
- Keep Portal/enemy/boss and NavMesh assets out of the export bundle unless a real active dependency is discovered. Do not add custom Editor preview tooling in this work unit.

## Constraints
- Do not move assets through raw filesystem operations: use Unity's AssetDatabase so Unity preserves GUIDs and updates references.
- Do not edit unrelated gameplay, scenes, packages, or ProjectSettings.
- No commit, push, release, or publication without the user's explicit authorization.
- **TDD mode:** off for this change by explicit user selection (`Validación al final`). Implement first, then run the available Unity compilation/test and behavior checks; no RED-before-code requirement.
- **Runner:** Unity MCP `assets-refresh` for AssetDatabase refresh/script compilation and `tests-run` for Unity tests. No existing `Assets/**/*Test*.cs` tests were found; focused behavior was checked with temporary, non-persistent Unity Editor preview-scene harnesses.
- Do not bypass disabled Unity MCP tools through another tool or raw project edits.

## Tasks

### ODD-0 — Resolve editor and test prerequisites
- **Status:** done
- **Route:** inline orchestration/preflight.
- **Evidence:** Unity MCP connection is active; user selected final functional validation rather than strict TDD. `Lvl1` is loaded, valid, and not dirty.

### ODD-1 — Move the generator bundle
- **Status:** done
- **Route:** direct parent AssetDatabase operation after the delegated worker was blocked because its child session lacked Unity MCP access; no filesystem fallback.
- **Acceptance:** `Assets/2DMapGenerator` contains the old generator bundle; source folder is relocated; all `.meta` GUIDs are preserved; `Lvl1` still resolves the `LevelGenerator` component and its tile references.
- **Evidence:** Unity `assets-move` returned `MovedPaths: ["Assets/2DMapGenerator"]` with no errors. AssetDatabase search found the script and prefab hierarchy at the new paths, found no `DungeonGenerator` folder under `Assets/Scripts`, and showed GUIDs matching the pre-move values. The loaded `Lvl1` GameObject still has an enabled `LevelGenerator`; all 16 floor and 9 wall prefab references resolve, and the scene remains valid/not dirty.

### ODD-2 — Harden runtime generation
- **Status:** done
- **Route:** delegated direct writer, with bounded parent corrections during verification.
- **Changed file:** `Assets/2DMapGenerator/LevelGenerator.cs`.
- **Outcome:** corrected rectangular grid axes and centering; exposed generation parameters while preserving defaults; added opt-in fixed-seed generation with Unity random-state restoration; added public regeneration and an owned generated root; validates dimensions, limits, probabilities, fill, and prefab arrays; bounds walker loops; uses incremental floor counting and inclusive fill-threshold completion.
- **Checks:** `assets-refresh` returned `AssetDatabase refreshed successfully` after relocation. A temporary Unity Editor preview-scene harness using moved prefabs passed 20x10 dimensions, centered bounds, 200 tile instances, same-seed layout repeatability, regeneration cleanup, and global random-state restoration. A second validation within the harness passed rejection of NaN cell size and an empty floor array, and acceptance of valid configuration. The harnesses were temporary and closed without dirtying `Lvl1`. `tests-run` returned `No tests found`; no existing Unity test suite is present.

### ODD-3 — Verify the isolated asset boundary
- **Status:** done
- **Route:** parent Unity-MCP verification; delegated verifier could not access MCP in its child session.
- **Acceptance:** `Lvl1` component and prefab references resolve after the move; generation remains functional; the export folder does not require unrelated game assets for the active generator path.
- **Checks:** post-move `assets-refresh` succeeded. AssetDatabase search and live component inspection confirmed moved paths, enabled component, and populated prefab references. Final `scene-list-opened` reports `Assets/Scenes/Lvl1.unity` loaded, valid, and not dirty. A temporary Editor preview-scene harness after the move loaded the moved prefabs and passed 20x10 dimensions, centered bounds, 200 tiles, repeatability with fixed seed, cleanup on regeneration, global random-state restoration, rejection of NaN cell size/empty floor array, and valid configuration acceptance. No `Lvl1` or persistent preview assets were modified. The existing export bundle contains the referenced wall/floor prefabs and their sprite sheets/material; legacy extras remain bundled unchanged. `tests-run` reported `No tests found` (no existing project test suite). Native review `review-6120e18bcdfb8854` approved and was acknowledged; authority was burned. The review returned one non-blocking informational warning, `R3-RegenerateDeferredCleanup`, at `LevelGenerator.cs:346-356`: Play-mode `Destroy` is deferred, so replaced instances can remain until frame end; this remains a separate follow-up and did not open correction. A separate read-only static audit completed and identified two additional conditional follow-up risks documented below; no source was changed after the approved review.

## Progress and evidence
- Initial static inspection mapped `LevelGenerator.cs` to `LvlGenerator` in `Assets/Scenes/Lvl1.unity`; active terrain assignments include 16 floor and 9 wall prefabs, with `ParedBase` used for empty cells. Portal/enemy/boss and NavMesh references are not part of the active generation path.
- Unity version observed: `6000.5.9f1`.
- The full old `DungeonGenerator` bundle is now under `Assets/2DMapGenerator`, including the script, prefab subfolders, sprite sheets, physics material, and legacy extras.
- User enabled `assets-move`; Unity MCP now exposes 50 tools, including `assets-move`, `assets-refresh`, `tests-run`, and `script-execute`.
- Both generic child sessions (writer and verifier) lacked Unity MCP access. Parent executed the required Unity AssetDatabase move and final Editor checks directly after each child reported blocked. No raw file move was used.
- **TDD mode/source:** off by explicit user selection (`Validación al final`).
- **Test suite:** `tests-run` was attempted and returned `No tests found`; this is an unavailable project test suite, not a passing test run.
- **Review boundary/outcome:** native review `review-6120e18bcdfb8854` froze the current workspace after selecting only the new `Assets/2DMapGenerator` paths as intended untracked content; unrelated `.codex/config.toml` and this progress document were excluded. Review approved and was acknowledged; its receipt is not delivery authority. Risk tier was medium, one `review-reliability` lens ran, and it returned only the advisory recorded under ODD-3. The provider counted the move as 137 changed files / 11,145 lines because the moved asset tree is represented as additions and deletions.
- **Assessment:** read-only `assess` could not evaluate the ambient worktree because it requires an explicit untracked-file declaration. It failed closed to `unassessable` / high risk and requested writer self-verification plus a separate independent verifier. The parent ran the Unity smoke harness and a separate `gentle-ai-verify` source audit completed; runtime cases outside the harness remain unverified.
- **Independent source audit:** static verification noted that the non-serialized `generatedRoot` is not reacquired by name after a domain reload, so a later regeneration could leave an older generated root behind; it also noted that prefab destruction callbacks using `UnityEngine.Random` can affect global random state outside the generator's save/restore window (immediate callbacks before the snapshot, or deferred callbacks after it). These are conditional follow-ups, not changed in this approved candidate.
- **Git status:** shell Git status was unavailable because bash/Git Bash is absent. Native review independently froze and verified the scoped workspace candidate. No commit was made, in keeping with the user's no-commit-without-authorization constraint.
- **Forecast:** approximately 150–250 authored code/test diff lines, excluding asset moves/generated metadata; the native review's larger count includes the moved tree as additions/deletions. Delivery strategy: `ask-on-risk`.
- **Engram mirror:** synchronized to project observation 272 under `odd/isolate-2d-map-generator/tasks`.

## Next step
Implementation and scoped verification are complete. The domain-reload root and prefab-callback RNG edge cases remain explicit follow-ups; address them in a separate candidate if desired, not by reopening the approved review. Do not commit, push, or publish without explicit user authorization.