# Feature: Extensible 8-neighbor, position-first tileset board

## Objective
Replace sprite-by-sprite topology dropdowns with a target-first board for the eight cells around a wall. Make all 240 wall patterns reachable under existing cardinal wall placement available in a scrollable/filterable board, distinguish the receiving WALL cell from adjacent FLOOR cells by color, and let the user assign one or more Sprite variants to each pattern by drag/drop or selection. Keep randomized selection at runtime.

## Constraints
- Preserve `Assets/Scenes/Lvl1.unity`, the wall atlas and its `.meta`, importer settings, Sprite identities, PPU, and colliders.
- Do not enter Play Mode or run automated tests. The user owns visual/manual confirmation.
- The user explicitly selected diagonal floor neighbors to distinguish additional corner cases.
- Preserve existing four-cardinal profile assignments as fallback for more-specific diagonal masks that have no exact sprite.
- Use Unity AssetDatabase tooling for profile edits; do not hand-edit Unity asset YAML.
- No commit/push without explicit authorization.

## Design and evidence
- Preserve cardinal bit constants N=1, E=2, S=4, W=8; add NE=16, SE=32, SW=64, NW=128. Existing serialized 1..15 masks retain meaning.
- Eight neighbors give 255 non-empty masks, but 15 diagonal-only masks are unreachable because wall creation remains cardinal-adjacent to floors. The Builder exposes the 240 reachable patterns and explains the omitted states.
- Runtime computes 8-neighbor masks from the final grid after `RemoveSingleWalls`; it does not create diagonal-only perimeter walls.
- Exact 8-bit Sprite/prefab mappings are preferred; on miss, the runtime uses the cardinal projection (`mask & 0x0F`) to retain existing mappings.
- Runtime randomizes among matching profile slices. The target-first board supports multiple Sprite assignments per pattern; base roles and advanced prefab workflow remain available.
- Unity AssetDatabase verified and updated the live profile to `_0` -> W+N (mask 9) and `_9` -> S+W (mask 12), matching user screenshots.

## Tasks

### ODD-1 — Map 8-neighbor topology and compatibility
- **Status:** done
- **Evidence:** Read-only audit confirmed mask lifecycle, old bit values, profile storage, random variants, unreachable diagonal-only masks, and safe final-grid recomputation.

### ODD-2 — Extend runtime topology to diagonal neighbors
- **Status:** done
- **Evidence:** 8-bit masks are recomputed from the final grid after cleanup. Exact mappings precede cardinal fallback; diagonal-only masks do not select legacy cardinal direction prefabs. Static verification passed.

### ODD-3 — Build the position-first 8-neighbor assignment board
- **Status:** done
- **Evidence:** Added searchable/scrollable target slots for 240 reachable patterns, colored WALL/FLOOR diagrams, and multi-Sprite assignment by selection/drop. Preserved non-topology roles and advanced prefab workflow. Source review passed.

### ODD-4 — Reconcile corner assignment data
- **Status:** done
- **Evidence:** Unity `assets-get-data` confirmed the live profile originally differed from screenshots; `assets-modify` changed only `_0` to mask 9 and `_9` to mask 12; readback confirmed both values. Importer and atlas were not changed.

### ODD-5 — Compile-check and hand off visual validation
- **Status:** done
- **Evidence:** Final Unity `ForceSynchronousImport` succeeded and reported no source compile error. Console query returned 2 HubConnection reconnect Errors and 6 Unity Connect token-exchange/timeout Exceptions in the last 10 minutes; verifier found no code-specific failures. No Play Mode or automated tests; user must confirm final appearance.

### ODD-6 — Preview the selected variant beside the pattern board
- **Status:** done
- **Evidence:** Added a `View` button beside each assigned variant's `Remove` button. Clicking it shows the selected Sprite preview, name, and mask in a panel beside the board on wide windows and below it when narrow. ForceSynchronousImport succeeded; Error/Exception logs were empty in the last 10 minutes. The UI was not visually exercised; user confirmation remains pending.

## Delivery note
Implementation is verified but uncommitted; no commit was made because the user has not authorized one. Manual scene/preview verification remains user-owned.
