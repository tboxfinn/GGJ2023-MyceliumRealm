# Feature: Create a 2DMapGenerator test scene

## Objective
Create an isolated Unity scene for trying the procedural generator without modifying `Assets/Scenes/Lvl1.unity`. Keep the test scene inside `Assets/2DMapGenerator` so it can serve as sample content when the package is exported later.

## Scope and decisions
- Scene path: `Assets/2DMapGenerator/Scenes/GeneratorTest.unity`.
- Use the existing `LevelGenerator` and current moved prefabs; do not change generator code or the existing game scene.
- Configure all required tile arrays from the working `Lvl1` component. A 20x12 rectangular map was initially selected; the currently saved scene baseline is 20x20 with fixed seed 10.
- Add an orthographic Main Camera framed for the map. No runtime UI or helper scripts: adjust settings in the Inspector and press Play; `Start()` triggers generation.
- Use Unity AssetDatabase/editor APIs for scene and folder creation/saving.

## Constraints
- Preserve `Assets/Scenes/Lvl1.unity` and all unrelated assets.
- Only add the dedicated scene and its Unity-generated metadata under `Assets/2DMapGenerator/Scenes`.
- Validation is functional-at-end; TDD is off per the prior explicit user selection.
- No commit, push, or publication without explicit user authorization.

## Tasks

### ODD-0 — Confirm test-scene scope
- **Status:** done
- **Evidence:** User selected a scene inside `Assets/2DMapGenerator` and Inspector + Play controls, with no UI.

### ODD-1 — Create the dedicated scene asset
- **Status:** done
- **Evidence:** Unity created `Assets/2DMapGenerator/Scenes` and saved an empty `GeneratorTest.unity`; the scene is valid and saved. `Lvl1` stayed loaded, valid, and not dirty.

### ODD-2 — Configure generator and camera
- **Status:** done
- **Evidence:** Added an enabled `LevelGenerator` with all seven required prefab arrays copied from `Lvl1`. The currently saved scene is 20x20, with fixed-seed mode enabled at seed 10; gameplay-only Exit/enemy/boss references remain empty. Added a tagged `Main Camera` with orthographic size 8 and a dark solid-color background. Earlier live reads of 20x12 with fixed seed disabled were transient and did not survive reload.

### ODD-3 — Run and verify the test scene
- **Status:** done
- **Acceptance:** entering Play generates a visible map in the new scene; stop Play cleanly; confirm `Lvl1` remains valid and unchanged and the saved test scene is not dirty.
- **Evidence:** The user ran Play mode in `GeneratorTest` and reports the generator works correctly. The earlier edit-mode smoke produced a 20x12 grid with 240 renderable tiles from temporary settings; those settings were not persisted. Fresh Unity/YAML reads confirm the saved scene baseline is 20x20, fixed seed 10. Before isolating the test scene, `Lvl1` was valid and clean; no later action targeted it.

## Progress and evidence
- Existing `LevelGenerator.Start()` calls `RegenerateLevel()`; all seven tile arrays (`emptyObj`, `floorObj`, `wallObj`, and four directional wall arrays) must be non-empty and assigned.
- User chose Inspector + Play rather than in-scene UI. Map dimensions and the optional fixed-seed controls remain editable in the component Inspector.
- Before switching to the test scene alone, the active Unity Editor reported `Lvl1` clean/valid. The current editor has only `GeneratorTest` open; it is valid, saved, clean, and has two roots. Fresh reads of the live component and saved YAML agree on 20x20, fixed seed enabled at 10, all required arrays populated, and no gameplay-only references. The camera reports orthographic mode, size 8, and configured background.
- An edit-mode functional smoke generated 240 renderable tiles from temporary 20x12 in-memory settings; its generated root was removed. The user subsequently ran Play mode and confirmed the saved 20x20/fixed-seed-10 scene works.

## Next step
The test scene and live preview are verified by the user. Next, choose the intended distribution target (UPM package or Unity Asset Store export) before restructuring the isolated generator bundle.