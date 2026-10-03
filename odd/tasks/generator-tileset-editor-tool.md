# Feature: Sprite atlas authoring and topology-aware walls

## Objective
Let the user select/drag an already sliced Unity sprite atlas into the generator EditorWindow, visually assign sprites to wall/floor cases, connect the profile directly to `LevelGenerator`, and use its Sprite assignments at runtime for topology-aware wall cells while preserving prefab structure and colliders.

## Confirmed decisions
- The user's atlas is already configured as Sprite/Multiple. The Builder must enumerate its existing Sprite subassets instead of forcing the user to slice the same grid again. The existing grid-slice workflow may remain as an optional path, but importing existing slices is primary.
- The wall atlas sprites replace a complete cell; they are not overlays.
- Preserve the atlas importer and its PPU. Generated visuals should match the existing template's footprint without resizing/modifying colliders.
- The wall-role mapping is based on which neighboring cells around a wall cell contain floor. Provide named assignments for cardinal sides, corner turns, straight joins, three-side/T junctions, and four-side cells. A mapping can have multiple visual variants. The user's 27-sprite wall atlas is the initial manual validation target.
- Keep the seven existing `LevelGenerator` arrays, including `wallObj` (Generic Wall), as prefab templates that preserve hierarchy/components/colliders. Existing scenes without a linked profile retain their legacy behavior.
- Primary workflow decision: link the Tile Set Profile directly to `LevelGenerator`; use assigned Sprite references during generation and do not require permanent per-sprite prefab variants. The existing bake-to-prefab path may remain optional/advanced for compatibility.
- No Tilemap migration. The user will test manually; assistant does not enter Play Mode. No commits/pushes/publication without explicit authorization.
- The Builder should stay usable at small, medium, and wide EditorWindow sizes: no clipped controls, meaningful wrapping/reflow, readable card widths, preview placement that adapts to available room, and good use of extra wide/tall space.
- The Builder should receive a visual polish pass using a restrained Unity-native style: consistent hierarchy/spacing, built-in theme-aware controls, minimal color accents, and emphasis reserved for key actions.
- User supplied a dark, minimal mockup as a visual reference (subtle surfaces/borders and a blue focus accent). User chose to keep the Builder adaptive to Unity's light/dark theme: approximate the reference in the dark Editor theme and preserve light-theme legibility; do not alter or imitate Unity's outer window chrome.
- User wants one wall prefab for profile-linked generation: use the single `wallObj` entry as the shared hierarchy/collider template and obtain wall art from profile topology/directional/generic Sprite assignments. Keep legacy directional prefab arrays and hidden mask-prefab mappings serialized and functional when no profile is linked; do not delete old prefab assets or scene references.

## Current asset/code evidence
- Unity is 6000.5.9f1; `com.unity.2d.sprite` is builtin 1.0.0. Use public Sprite Editor data-provider APIs when needed, not `InternalSpriteUtility`.
- MCP `assets-find` confirmed `Assets/2DMapGenerator/DungeonGenPrefabs/WallTiles/WallTiles.png` (GUID `b2ecba99dafe40246a9aa2054d6d3634`). The current `.meta` still has Sprite/Multiple mode, PPU 100, and 27 16x16 Sprite subassets. The primary import path does not write importer settings.
- Existing wall prefab templates use 128x128 sprites at 128 PPU and occupy one grid cell. Their root/child colliders and SpriteRenderer setup must remain intact. Scale the generated SpriteRenderer visual footprint without moving/resizing colliders or changing atlas PPU.
- `LevelGenerator.CreateWalls()` accumulates all adjacent floor directions into a four-bit mask; `SpawnWall()` checks an exact mask first, then falls back to the legacy directional array for one-sided masks or `wallObj` for compound masks. With no topology mappings, old scenes use their legacy directional arrays.
- The Builder imports Sprite subassets without importer mutation, exposes all 15 non-empty masks with labels, persists assignments, creates prefab variants by changing only the selected SpriteRenderer's sprite/draw mode/size, and applies all seven base roles plus mask mappings explicitly with Undo.
- The import action replaces the current profile palette with the selected Texture's subassets or selected Sprite set; assignments are retained for Sprite references that remain in the imported set. Optional grid slicing remains a separate explicit action.

## Existing implementation tasks

### ODD-0 — Confirm the sprite workflow
- **Status:** done
- **Evidence:** User confirmed already-sliced sprites, whole-cell replacement for corner/junction pieces, and matching the template visual size while preserving source PPU/colliders.

### ODD-1 — Preserve optional regular-grid slicing
- **Status:** done
- **Evidence:** The first version already slices regular grids via public provider APIs, persists the profile, and compiles. Keep it optional; do not make it the primary import path.

## Active revision tasks

### ODD-2 — Import existing Sprite subassets and expose a drag/drop palette
- **Status:** done
- **Evidence:** `LoadSpritesFromTexture()` and `ImportDroppedSpriteSources()` use `AssetDatabase.LoadAllAssetsAtPath()` and filter Sprite objects; `LoadSpriteReferences()` retains role/mask/generated-prefab assignments for matching Sprite references. No importer writes occur in this path. Static audit completed.

### ODD-3 — Add persistent topology mapping and compatible prefab variants
- **Status:** done
- **Evidence:** The profile stores role and mask per Sprite; the UI exposes all 15 non-empty masks and multiple variants per mask. Prefab generation sets the child `SpriteRenderer` sprite, draw mode, and visual size while retaining hierarchy/transforms/colliders. Apply is explicit and Undo-backed across seven base arrays and topology masks. Static audit completed.

### ODD-4 — Classify runtime wall cells by neighbor mask
- **Status:** done
- **Evidence:** Runtime accumulates N/E/S/W bits, selects exact-mask variants, uses the seeded `Random` sequence for variants, falls back for missing masks, preserves legacy behavior without mappings, handles wall cleanup symmetrically, and snapshots/restores transient mask state for preview. Static audit completed.

### ODD-5 — Compile and statically verify the revised workflow
- **Status:** done
- **Evidence:** Unity `assets-refresh` completed with `ForceSynchronousImport`; `console-get-logs` returned no Error entries for the last 10 minutes. Re-read `TryMergeWallMaskMappings()` after its repair change. It now discards all existing target entries for explicitly assigned masks before validating the preserved mappings, so an explicit profile assignment can repair duplicate/invalid entries for that mask. No Play Mode or automated tests were run.
- **Manual validation pending:** The user should load the atlas, confirm all 27 sprites appear, map roles/topologies, generate/apply prefabs, inspect corners/T-junctions and visual size, confirm collider bounds and atlas metadata remain unchanged, and verify Undo. User also still needs to manually verify the three fill modes.

### ODD-6 — Fix palette clipping and clarify setup fields
- **Status:** done
- **Evidence:** `DrawSliceGrid()` chooses responsive card widths and computes an explicit scroll viewport height from the measured palette header to the bottom of the EditorWindow; the header measurement is cached during Repaint, and the scroll region keeps a 150 px minimum. `DrawSliceCard()` sizes controls from the card width. Help text defines Template Source, Apply To, and the Generate-then-Apply workflow. Unity `ForceSynchronousImport` refresh completed; Console returned no Error entries in the last 5 minutes. Visual confirmation in the user's window remains pending.

### ODD-7 — Connect the Tile Set Profile directly to LevelGenerator
- **Status:** done (implementation; user visual/runtime validation pending)
- **Decision:** User selected direct profile consumption at runtime. Keep the existing prefab arrays as templates for hierarchy, SpriteRenderer setup, and collider geometry; the linked profile supplies role/mask Sprite variants at spawn. Do not require generated prefab variants or array-baking Apply for the primary path. Preserve the old prefab-only behavior for scenes without a profile, and retain the prior bake flow as optional/advanced.
- **Evidence:** `LevelGenerator` stores a profile reference; runtime uses profile Sprite variants for Empty/Floor/base wall roles and exact wall-neighbor masks through the seeded random stream. It overrides only spawned SpriteRenderer components and sizes visuals to the template footprint. Exact direct masks use `wallObj`; compound GenericWall profile fallbacks use the generic template; explicit existing mask-prefab mappings remain honored. The Builder has an Undo-backed direct-link action for regular scene instances, refreshes linked previews when profiles change, and presents baking as an optional advanced workflow. Static verification covered all four code files.
- **Compatibility:** With no linked profile or no applicable assigned Sprite, existing arrays/mask-prefab behavior remains. Prefab arrays remain required as structure/collider templates; profile connection does not rewrite them.
- **Object-picker note:** With Live Preview enabled, Unity's `Scene LevelGenerator` object picker can list both the actual `LevelGenerator` and the temporary `__LevelGenerator Live Preview` marker. Select the actual `LevelGenerator`; the scene-instance guard rejects the preview object, which is transient and not saved.

### ODD-8 — Compile and verify direct profile integration
- **Status:** done
- **Evidence:** Unity `assets-refresh` with `ForceSynchronousImport` completed successfully; `console-get-logs` returned no Error entries in the last 10 minutes. Static review found no remaining blocker. No automated tests or Play Mode were run.
- **Manual validation pending:** The user must confirm the palette viewport no longer leaves a blank area and test direct profile linking, Sprite-role/mask assignments, live preview, generated corners/T-junctions, visual size, collider bounds, atlas metadata, Undo, and all fill modes in `GeneratorTest`. A mapped legacy `wallMaskPrefabs` entry intentionally takes precedence over a GenericWall fallback when that exact profile mask is absent.

### ODD-9 — Make the Builder responsive across window sizes
- **Status:** completed; user visual confirmation remains pending
- Adapted the profile/object rows, filter/Clear, tabs, legend, card actions, and preview placement to available width. Preview stacks until board and panel both fit; card widths are bounded with more columns at larger sizes; palette height uses measured header and remaining viewport. Existing scroll, foldout, alias, import, assignment, link and preview behavior remains. Unity recompile completed with `failed:false`, `errors:[]`, `compilationFailed:false`; current Console error count is zero. No tests, Play Mode, or asset/profile/scene changes.

### ODD-10 — Verify responsive Builder layout
- **Status:** pending
- User manually checks narrow, medium, and wide window sizes, scroll behavior, readability, and absence of clipping after visual polish. No automated tests or Play Mode.

### ODD-11 — Choose visual design direction
- **Status:** completed
- User selected a polished Unity-native style: consistent section hierarchy and spacing, built-in theme-aware controls, restrained accents, and emphasis on key actions.

### ODD-12 — Apply Unity-native visual polish
- **Status:** done
- Established a clearer section/card hierarchy, quieter wrapped guidance, and consistent secondary actions. Both adaptive topology tab bars use `EditorStyles.toolbarButton` so Unity provides theme-aware selected/focus treatment. Existing help-box cards and spacing approximate the reference's subtle surfaces. Responsive layout, foldouts, topology aliases, and workflows are unchanged; no custom assets/fonts or window chrome were added.

### ODD-13 — Verify the polished responsive Builder
- **Status:** done (manual visual checks pending)
- Independent source audit confirmed responsive thresholds, preview stacking, card bounds, measured palette viewport, scrolls, foldout defaults, canonical aliases/exact overrides, and import/link/assignment actions remain. `git diff --check` passed. Unity recompile completed; `compilationFailed:false`, `compiling:false`, `consoleErrors:0`, `consoleWarnings:4`. The same console status included aggregate buffered counts of 1 error and 1999 warnings, without returned entries. No tests or Play Mode.
- Native review preflight `gentle_review inspect` was blocked (`package-local-binary-missing`, `lineage_created:false`, `mutation_performed:false`); no review lineage was started and no package install/recovery command was run.

### ODD-14 — Use one wall prefab with profile-driven Sprites
- **Status:** done
- Linked-profile `SpawnWall` now always uses the `wallObj` template; selects Sprite by raw → canonical → cardinal topology, then directional-role/GenericWall fallback, otherwise retains the template Sprite. Directional prefab arrays and hidden `wallMaskPrefabs` are not used in this mode. Validation requires `wallObj` but permits empty dormant arrays/maps; unrelated base arrays remain validated. Custom Inspector explains the shared template and hides directional fields without clearing references. Profile-free legacy path remains unchanged. The writer changed only the two authorized source files; no scene/asset edits were intentionally made for this task.

### ODD-15 — Verify single-template wall mode
- **Status:** done (manual validation pending)
- Independent source audit confirmed profile-linked spawning uses only `wallObj`, Sprite fallback order is exact → canonical → cardinal → directional role → GenericWall → template Sprite, and legacy profile-free selection/validation is preserved. Inspector retains `wallObj`, explains shared-template use, hides directional arrays only while linked, and restores them when unlinked. `git diff --check` passed. Unity recompile completed; `compilationFailed:false`, `compiling:false`, `consoleErrors:0`, `consoleWarnings:5`; aggregate console counts included 1 error/1999 warnings with no entries returned. No tests or Play Mode.
- Native review preflight `gentle_review inspect` is blocked (`package-local-binary-missing`, no lineage/mutation); no install/recovery command was run. The pre/post-compile worktree includes modified `GeneratorTest.unity`, `LevelGeneratorTileSet.cs`, and untracked profile/WallTiles assets. The `GeneratorTest` diff links a profile and changes `percentToFill`; without an initial baseline, attribution cannot be certified. These paths were preserved and must not be reported as clean or unrelated.
- User manually verifies profile mask appearance/fallback, renderer size/collider preservation, and legacy `Lvl1` behavior.

## Next step
User manually validates that linked-profile wall patterns all use the single `wallObj` template with correct profile Sprites, that visual size/colliders are unchanged, and that `Lvl1` retains legacy directional prefab behavior. Old directional references remain serialized but are hidden and unused while the profile is linked. Preserve scenes/assets/importer settings; no tests, Play Mode, commits, or pushes.
