using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(LevelGenerator))]
public sealed class LevelGeneratorEditor : Editor {
	static readonly string[] FillModeLabels = {"Random Walk", "Rooms + Corridors", "Cellular Automata"};
	bool refreshQueued;
	bool observedComponentEnabled;

	void OnEnable(){
		LevelGenerator generator = target as LevelGenerator;
		observedComponentEnabled = generator != null && generator.enabled;
		Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
		SchedulePreviewRefresh();
	}

	void OnDisable(){
		Editor.finishedDefaultHeaderGUI -= OnFinishedDefaultHeaderGUI;
	}

	void OnFinishedDefaultHeaderGUI(Editor editor){
		if (editor != this){
			return;
		}

		LevelGenerator generator = target as LevelGenerator;
		if (generator == null){
			return;
		}
		bool componentEnabled = generator.enabled;
		if (componentEnabled != observedComponentEnabled){
			observedComponentEnabled = componentEnabled;
			SchedulePreviewRefresh();
		}
	}

	public override void OnInspectorGUI(){
		serializedObject.Update();
		bool previewInputsChanged = false;
		bool previewToggleChanged = false;
		bool componentEnabledChanged = false;
		SerializedProperty fillModeProperty = serializedObject.FindProperty("fillMode");
		SerializedProperty tileSetProfileProperty = serializedObject.FindProperty("tileSetProfile");

		SerializedProperty property = serializedObject.GetIterator();
		bool enterChildren = true;
		while (property.NextVisible(enterChildren)){
			enterChildren = false;
			if (property.propertyPath == "wallMaskPrefabs"){
				continue;
			}
			if (tileSetProfileProperty != null && tileSetProfileProperty.objectReferenceValue != null &&
				IsDirectionalWallPrefabProperty(property.propertyPath)){
				continue;
			}
			if (property.propertyPath == "m_Script"){
				using (new EditorGUI.DisabledScope(true)){
					EditorGUILayout.PropertyField(property, true);
				}
				continue;
			}

			if (property.propertyPath == "fillMode"){
				int previousMode = Mathf.Clamp(property.enumValueIndex, 0, FillModeLabels.Length - 1);
				EditorGUI.BeginChangeCheck();
				int selectedMode = EditorGUILayout.Popup("Fill Mode", previousMode, FillModeLabels);
				if (EditorGUI.EndChangeCheck()){
					property.enumValueIndex = selectedMode;
					previewInputsChanged = true;
				}
				DrawFillModeHelp(fillModeProperty.enumValueIndex);
				continue;
			}

			int activeMode = fillModeProperty == null ? 0 : fillModeProperty.enumValueIndex;
			if (!IsVisibleForMode(property.propertyPath, activeMode)){
				continue;
			}

			bool isPreviewToggle = property.propertyPath == "livePreview";
			bool isPreviewInput = IsPreviewInput(property.propertyPath);
			bool isEnabledField = property.propertyPath == "m_Enabled";
			EditorGUI.BeginChangeCheck();
			if (property.propertyPath == "wallObj" && tileSetProfileProperty != null &&
				tileSetProfileProperty.objectReferenceValue != null){
				EditorGUILayout.PropertyField(property, new GUIContent("Wall Obj (Shared Template)"), true);
				EditorGUILayout.HelpBox(
					"Wall cells use this prefab as their shared template. Tile Set Profile Sprite assignments override its SpriteRenderer; Wall Topology sprites are tried by exact mask, canonical mask, then cardinal-only mask. If none is assigned, a single-cardinal wall uses its matching directional Sprite role, then Generic Wall. Missing all these assignments keeps the template's authored Sprite. The template hierarchy and collider are preserved; directional prefab arrays and legacy mask-prefab mappings are ignored while a profile is linked.",
					MessageType.Info);
			}
			else{
				EditorGUILayout.PropertyField(property, true);
			}
			if (EditorGUI.EndChangeCheck()){
				previewToggleChanged |= isPreviewToggle;
				previewInputsChanged |= isPreviewInput;
				componentEnabledChanged |= isEnabledField;
			}
		}

		bool applied = serializedObject.ApplyModifiedProperties();
		LevelGenerator generator = target as LevelGenerator;
		if (generator != null && !generator.IsLivePreviewRootMarker &&
			generator.LivePreviewEnabled && generator.LivePreviewCellLimitExceeded()){
			EditorGUILayout.HelpBox(
				"Live Preview is limited to " + LevelGenerator.LivePreviewCellLimit.ToString("N0") +
				" cells. Reduce the map dimensions or increase the cell size; the existing preview is retained until the settings are valid and within the limit.",
				MessageType.Warning);
		}

		if (applied && (previewToggleChanged || previewInputsChanged || componentEnabledChanged)){
			SchedulePreviewRefresh();
		}
	}

	static void DrawFillModeHelp(int mode){
		switch (mode){
			case 1:
				EditorGUILayout.HelpBox("Places bounded rectangular rooms inside the outer border and joins them with one-cell L-shaped corridors.", MessageType.Info);
				break;
			case 2:
				EditorGUILayout.HelpBox("Randomly fills the interior, smooths it using all eight neighboring cells, then connects separated floor regions.", MessageType.Info);
				break;
			default:
				EditorGUILayout.HelpBox("The existing random walker carves floors until the Percent To Fill target is reached.", MessageType.Info);
				break;
		}
	}

	static bool IsVisibleForMode(string propertyPath, int mode){
		switch (propertyPath){
			case "walkers":
				return mode == 0;
			case "chanceWalkerChangeDir":
			case "chanceWalkerSpawn":
			case "chanceWalkerDestoy":
			case "maxWalkers":
			case "percentToFill":
				return mode == 0;
			case "roomCount":
			case "minRoomDimension":
			case "maxRoomDimension":
				return mode == 1;
			case "cellularAutomataFillChance":
			case "cellularAutomataIterations":
			case "cellularAutomataNeighborThreshold":
				return mode == 2;
			default:
				return true;
		}
	}

	static bool IsDirectionalWallPrefabProperty(string propertyPath){
		switch (propertyPath){
			case "wallUpObj":
			case "wallDownObj":
			case "wallLeftObj":
			case "wallRightObj":
				return true;
			default:
				return false;
		}
	}

	static bool IsPreviewInput(string propertyPath){
		switch (propertyPath){
			case "roomSizeWorldUnits":
			case "worldUnitsInOneGridCell":
			case "chanceWalkerChangeDir":
			case "chanceWalkerSpawn":
			case "chanceWalkerDestoy":
			case "maxWalkers":
			case "percentToFill":
			case "fillMode":
			case "roomCount":
			case "minRoomDimension":
			case "maxRoomDimension":
			case "cellularAutomataFillChance":
			case "cellularAutomataIterations":
			case "cellularAutomataNeighborThreshold":
			case "useFixedSeed":
			case "fixedSeed":
			case "tileSetProfile":
			case "emptyObj":
			case "floorObj":
			case "wallObj":
			case "wallUpObj":
			case "wallDownObj":
			case "wallRightObj":
			case "wallLeftObj":
				return true;
			default:
				return false;
		}
	}

	void SchedulePreviewRefresh(){
		if (refreshQueued){
			return;
		}
		refreshQueued = true;
		EditorApplication.delayCall += RefreshPreview;
	}

	void RefreshPreview(){
		refreshQueued = false;
		if (this == null || target == null){
			return;
		}

		LevelGenerator generator = (LevelGenerator)target;
		if (!LevelGeneratorPreviewLifecycle.IsSceneInstance(generator)){
			return;
		}
		if (EditorApplication.isPlayingOrWillChangePlaymode || !generator.isActiveAndEnabled){
			generator.ClearLivePreview();
		}
		else{
			generator.RefreshLivePreview();
		}
		SceneView.RepaintAll();
	}
}

[InitializeOnLoad]
static class LevelGeneratorPreviewLifecycle {
	static readonly Dictionary<EntityId, bool> activeInHierarchyByInstanceId = new Dictionary<EntityId, bool>();
	static bool refreshQueued;
	static bool activationCheckQueued;

	static LevelGeneratorPreviewLifecycle(){
		EditorApplication.delayCall += ScheduleRefreshAll;
		EditorSceneManager.sceneOpened += OnSceneOpened;
		EditorApplication.hierarchyChanged += OnHierarchyChanged;
		Undo.undoRedoPerformed += ScheduleRefreshAll;
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
	}

	static void OnSceneOpened(Scene scene, OpenSceneMode mode){
		ScheduleRefreshAll();
	}

	static void OnHierarchyChanged(){
		if (activationCheckQueued){
			return;
		}
		activationCheckQueued = true;
		EditorApplication.delayCall += CheckHierarchyActivationChanges;
	}

	static void CheckHierarchyActivationChanges(){
		activationCheckQueued = false;
		LevelGenerator[] generators = Resources.FindObjectsOfTypeAll<LevelGenerator>();
		HashSet<EntityId> currentInstanceIds = new HashSet<EntityId>();
		List<LevelGenerator> activationChanges = new List<LevelGenerator>();

		for (int i = 0; i < generators.Length; i++){
			LevelGenerator generator = generators[i];
			if (!IsSceneInstance(generator)){
				continue;
			}

			EntityId instanceId = generator.GetEntityId();
			bool activeInHierarchy = generator.gameObject.activeInHierarchy;
			currentInstanceIds.Add(instanceId);
			bool previousActiveInHierarchy;
			if (activeInHierarchyByInstanceId.TryGetValue(instanceId, out previousActiveInHierarchy) &&
				previousActiveInHierarchy != activeInHierarchy){
				activationChanges.Add(generator);
			}
			activeInHierarchyByInstanceId[instanceId] = activeInHierarchy;
		}

		RemoveMissingInstances(currentInstanceIds);
		for (int i = 0; i < activationChanges.Count; i++){
			RefreshOrClear(activationChanges[i]);
		}
		if (activationChanges.Count > 0){
			SceneView.RepaintAll();
		}
	}

	static void OnPlayModeStateChanged(PlayModeStateChange state){
		if (state == PlayModeStateChange.ExitingEditMode){
			ClearAllPreviews();
		}
		else if (state == PlayModeStateChange.EnteredEditMode){
			ScheduleRefreshAll();
		}
	}

	static void ScheduleRefreshAll(){
		if (refreshQueued){
			return;
		}
		refreshQueued = true;
		EditorApplication.delayCall += RefreshAllPreviews;
	}

	static void RefreshAllPreviews(){
		refreshQueued = false;
		if (EditorApplication.isPlayingOrWillChangePlaymode){
			return;
		}

		LevelGenerator[] generators = Resources.FindObjectsOfTypeAll<LevelGenerator>();
		HashSet<EntityId> currentInstanceIds = new HashSet<EntityId>();
		for (int i = 0; i < generators.Length; i++){
			LevelGenerator generator = generators[i];
			if (!IsSceneInstance(generator)){
				continue;
			}
			EntityId instanceId = generator.GetEntityId();
			currentInstanceIds.Add(instanceId);
			activeInHierarchyByInstanceId[instanceId] = generator.gameObject.activeInHierarchy;
			RefreshOrClear(generator);
		}
		RemoveMissingInstances(currentInstanceIds);
		SceneView.RepaintAll();
	}

	static void ClearAllPreviews(){
		LevelGenerator[] generators = Resources.FindObjectsOfTypeAll<LevelGenerator>();
		HashSet<EntityId> currentInstanceIds = new HashSet<EntityId>();
		for (int i = 0; i < generators.Length; i++){
			LevelGenerator generator = generators[i];
			if (!IsSceneInstance(generator)){
				continue;
			}
			EntityId instanceId = generator.GetEntityId();
			currentInstanceIds.Add(instanceId);
			activeInHierarchyByInstanceId[instanceId] = generator.gameObject.activeInHierarchy;
			generator.ClearLivePreview();
		}
		RemoveMissingInstances(currentInstanceIds);
		SceneView.RepaintAll();
	}

	static void RefreshOrClear(LevelGenerator generator){
		if (generator.isActiveAndEnabled && generator.LivePreviewEnabled){
			generator.RefreshLivePreview();
		}
		else{
			generator.ClearLivePreview();
		}
	}

	static void RemoveMissingInstances(HashSet<EntityId> currentInstanceIds){
		List<EntityId> staleInstanceIds = new List<EntityId>();
		foreach (KeyValuePair<EntityId, bool> entry in activeInHierarchyByInstanceId){
			if (!currentInstanceIds.Contains(entry.Key)){
				staleInstanceIds.Add(entry.Key);
			}
		}
		for (int i = 0; i < staleInstanceIds.Count; i++){
			activeInHierarchyByInstanceId.Remove(staleInstanceIds[i]);
		}
	}

	public static bool IsSceneInstance(LevelGenerator generator){
		if (generator == null || EditorUtility.IsPersistent(generator) ||
			generator.IsLivePreviewRootMarker || generator.IsInLivePreviewHierarchy()){
			return false;
		}

		Scene scene = generator.gameObject.scene;
		return scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene);
	}
}
