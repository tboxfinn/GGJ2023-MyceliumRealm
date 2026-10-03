using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public sealed class LevelGeneratorTileSetEditorWindow : EditorWindow {
	static readonly LevelGeneratorTileRole[] Roles = {
		LevelGeneratorTileRole.Empty,
		LevelGeneratorTileRole.Floor,
		LevelGeneratorTileRole.GenericWall,
		LevelGeneratorTileRole.WallUp,
		LevelGeneratorTileRole.WallDown,
		LevelGeneratorTileRole.WallLeft,
		LevelGeneratorTileRole.WallRight
	};

	static readonly LevelGeneratorTileRole[] AssignmentRoles = {
		LevelGeneratorTileRole.Empty,
		LevelGeneratorTileRole.Floor,
		LevelGeneratorTileRole.GenericWall,
		LevelGeneratorTileRole.WallUp,
		LevelGeneratorTileRole.WallDown,
		LevelGeneratorTileRole.WallLeft,
		LevelGeneratorTileRole.WallRight,
		LevelGeneratorTileRole.WallTopology
	};

	static readonly string[] RoleLabels = {
		"Unassigned",
		"Base Wall / Background",
		"Floor",
		"Generic Wall",
		"Wall Up",
		"Wall Down",
		"Wall Left",
		"Wall Right",
		"Wall Topology"
	};

	static readonly string[] CompactRoleLabels = {
		"Unassigned",
		"Base Wall",
		"Floor",
		"Generic Wall",
		"Wall Up",
		"Wall Down",
		"Wall Left",
		"Wall Right",
		"Wall Topology"
	};

	static readonly int[] TopologyPreviewMasks = {
		LevelGenerator.WallFloorNorthWest, LevelGenerator.WallFloorNorth, LevelGenerator.WallFloorNorthEast,
		LevelGenerator.WallFloorWest, 0, LevelGenerator.WallFloorEast,
		LevelGenerator.WallFloorSouthWest, LevelGenerator.WallFloorSouth, LevelGenerator.WallFloorSouthEast
	};

	static readonly string[] TopologyPreviewDirections = {
		"NW", "N", "NE", "W", "WALL", "E", "SW", "S", "SE"
	};

	static readonly int[] TopologyDirectionMasks = {
		LevelGenerator.WallFloorNorth, LevelGenerator.WallFloorEast,
		LevelGenerator.WallFloorSouth, LevelGenerator.WallFloorWest,
		LevelGenerator.WallFloorNorthEast, LevelGenerator.WallFloorSouthEast,
		LevelGenerator.WallFloorSouthWest, LevelGenerator.WallFloorNorthWest
	};

	static readonly string[] TopologyDirectionNames = {"N", "E", "S", "W", "NE", "SE", "SW", "NW"};
	static readonly Color TopologyWallColor = new Color(1f, 0.55f, 0.12f);
	static readonly Color TopologyFloorColor = new Color(0.25f, 0.62f, 0.95f);
	static readonly Color TopologyNeutralColor = new Color(0.72f, 0.74f, 0.77f);

	LevelGeneratorTileSet profile;
	UnityEngine.Object existingSpriteSource;
	bool gridSlicingOptionsExpanded;
	Texture2D scratchAtlas;
	Vector2Int scratchCellSize = new Vector2Int(128, 128);
	Vector2Int scratchOffset = Vector2Int.zero;
	Vector2Int scratchSpacing = Vector2Int.zero;
	float scratchPixelsPerUnit = 128f;
	LevelGenerator templateGenerator;
	LevelGenerator applyTarget;
	LevelGenerator connectionTarget;
	bool advancedWorkflowExpanded;
	Vector2 contentScrollPosition;
	Vector2 slicesScrollPosition;
	Vector2 topologyScrollPosition;
	float measuredMainContentViewportHeight;
	float measuredPaletteHeaderContentY;
	[SerializeField] bool directProfileWorkflowExpanded;
	[SerializeField] bool existingSpriteImportExpanded;
	[SerializeField] bool baseWallAssignmentExpanded = true;
	[SerializeField] int selectedTopologyGroup = 1;
	string topologyFilter = string.Empty;
	Sprite selectedTopologyPreview;
	int selectedTopologyPreviewMask;
	string operationMessage;
	MessageType operationMessageType = MessageType.Info;

	[MenuItem("Tools/2D Map Generator/Tile Set Builder")]
	static void OpenWindow(){
		LevelGeneratorTileSetEditorWindow window = GetWindow<LevelGeneratorTileSetEditorWindow>();
		window.titleContent = new GUIContent("Tile Set Builder");
		window.InitializeTargetsFromSelection();
		window.Show();
	}

	void InitializeTargetsFromSelection(){
		LevelGenerator selectedGenerator = GetSelectedGenerator();
		if (selectedGenerator != null){
			templateGenerator = selectedGenerator;
			applyTarget = selectedGenerator;
			connectionTarget = selectedGenerator;
		}
	}

	static LevelGenerator GetSelectedGenerator(){
		LevelGenerator generator = Selection.activeObject as LevelGenerator;
		if (generator != null){
			return generator;
		}
		GameObject selectedObject = Selection.activeGameObject;
		return selectedObject == null ? null : selectedObject.GetComponent<LevelGenerator>();
	}

	void OnGUI(){
		DrawProfileSelector();
		EditorGUILayout.Space();
		DrawDirectProfileConnection();
		EditorGUILayout.Space();
		DrawExistingSpriteImport();
		if (!string.IsNullOrEmpty(operationMessage)){
			EditorGUILayout.HelpBox(operationMessage, operationMessageType);
		}

		if (profile == null){
			EditorGUILayout.HelpBox("Create or select a Tile Set Profile before importing sprites or assigning roles.", MessageType.Info);
			return;
		}

		EditorGUILayout.Space();
		DrawSpriteRenderSizeField();
		EditorGUILayout.Space();
		float contentViewportHeight = measuredMainContentViewportHeight > 0f
			? measuredMainContentViewportHeight
			: Mathf.Max(1f, position.height * 0.32f);
		if (Event.current.type == EventType.Repaint){
			measuredMainContentViewportHeight = Mathf.Max(
				1f, position.height - GUILayoutUtility.GetLastRect().yMax - 8f);
		}
		contentScrollPosition = EditorGUILayout.BeginScrollView(contentScrollPosition);
		DrawBaseWallAssignment();
		DrawTopologyBoard();
		DrawSliceGrid(contentViewportHeight);
		DrawAdvancedWorkflow();
		EditorGUILayout.EndScrollView();
	}

	static float GetAvailableContentWidth(float windowWidth){
		return Mathf.Max(1f, windowWidth - 48f);
	}

	static void DrawSectionHeading(string title, string metadata){
		EditorGUILayout.Space();
		EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
		if (!string.IsNullOrEmpty(metadata)){
			EditorGUILayout.LabelField(metadata, EditorStyles.miniLabel);
		}
	}

	UnityEngine.Object DrawResponsiveObjectField(string label, UnityEngine.Object selectedObject,
		Type objectType, bool allowSceneObjects){
		if (GetAvailableContentWidth(position.width) >= 400f){
			return EditorGUILayout.ObjectField(label, selectedObject, objectType, allowSceneObjects);
		}
		EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
		return EditorGUILayout.ObjectField(selectedObject, objectType, allowSceneObjects, GUILayout.ExpandWidth(true));
	}

	void DrawProfileSelector(){
		float availableWidth = GetAvailableContentWidth(position.width);
		LevelGeneratorTileSet selectedProfile;
		bool profileChanged;
		bool createProfile;
		if (availableWidth >= 400f){
			using (new EditorGUILayout.HorizontalScope()){
				EditorGUI.BeginChangeCheck();
				selectedProfile = EditorGUILayout.ObjectField(
					"Tile Set Profile", profile, typeof(LevelGeneratorTileSet), false) as LevelGeneratorTileSet;
				profileChanged = EditorGUI.EndChangeCheck();
				createProfile = GUILayout.Button("Create Profile", GUILayout.Width(105f));
			}
		}
		else{
			EditorGUILayout.LabelField("Tile Set Profile", EditorStyles.miniBoldLabel);
			EditorGUI.BeginChangeCheck();
			selectedProfile = EditorGUILayout.ObjectField(
				profile, typeof(LevelGeneratorTileSet), false, GUILayout.ExpandWidth(true)) as LevelGeneratorTileSet;
			profileChanged = EditorGUI.EndChangeCheck();
			createProfile = GUILayout.Button("Create Profile", GUILayout.ExpandWidth(true));
		}

		if (profileChanged){
			profile = selectedProfile;
			if (profile != null){
				scratchAtlas = profile.sourceAtlas;
				existingSpriteSource = profile.sourceAtlas;
				scratchCellSize = profile.cellSize;
				scratchOffset = profile.offset;
				scratchSpacing = profile.spacing;
				scratchPixelsPerUnit = profile.pixelsPerUnit;
				operationMessage = null;
			}
			else{
				existingSpriteSource = null;
			}
		}
		if (createProfile){
			CreateProfile();
		}
	}

	void DrawSpriteRenderSizeField(){
		Vector2 currentSize = profile.spriteRenderSizeWorldUnits;
		EditorGUI.BeginChangeCheck();
		Vector2 editedSize;
		if (GetAvailableContentWidth(position.width) >= 400f){
			editedSize = EditorGUILayout.Vector2Field("Sprite Render Size (World Units)", currentSize);
		}
		else{
			EditorGUILayout.LabelField("Sprite Render Size (World Units)", EditorStyles.miniBoldLabel);
			float previousLabelWidth = EditorGUIUtility.labelWidth;
			try{
				EditorGUIUtility.labelWidth = 0f;
				editedSize = EditorGUILayout.Vector2Field(GUIContent.none, currentSize, GUILayout.ExpandWidth(true));
			}
			finally{
				EditorGUIUtility.labelWidth = previousLabelWidth;
			}
		}
		if (EditorGUI.EndChangeCheck()){
			if (!IsValidSpriteRenderSize(editedSize)){
				SetMessage("Sprite Render Size must contain finite, positive X and Y dimensions.", MessageType.Warning);
			}
			else{
				Undo.RecordObject(profile, "Change Tile Set Sprite Render Size");
				profile.spriteRenderSizeWorldUnits = editedSize;
				SaveProfile(profile);
				SetMessage("Updated runtime Sprite render size in world units. Prefab transforms and colliders were not changed.", MessageType.Info);
			}
		}
		EditorGUILayout.LabelField(
			"This exact X/Y size is independent of importer PPU and is applied only to SpriteRenderers receiving a runtime profile override. Renderer transform scale is accounted for; prefab transforms and colliders are unchanged.",
			EditorStyles.wordWrappedMiniLabel);
		if (!IsValidSpriteRenderSize(currentSize)){
			EditorGUILayout.HelpBox("This profile has no valid Sprite render size; runtime profile overrides safely retain each prefab renderer's existing footprint until a positive size is set.", MessageType.Warning);
		}
	}

	static bool IsValidSpriteRenderSize(Vector2 size){
		return IsFinitePositive(size.x) && IsFinitePositive(size.y);
	}

	static bool IsFinitePositive(float value){
		return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
	}

	void DrawExistingSpriteImport(){
		existingSpriteImportExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
			existingSpriteImportExpanded, "Import Existing Sliced Sprites");
		if (existingSpriteImportExpanded){
			EditorGUILayout.HelpBox(
				"Choose an already-sliced Texture2D to load its Sprite subassets, or choose/drop Sprite objects. This path only reads imported assets; it does not re-slice, reimport, or change PPU.",
				MessageType.Info);
			EditorGUI.BeginChangeCheck();
			UnityEngine.Object selectedSource = DrawResponsiveObjectField(
				"Texture or Sprite", existingSpriteSource, typeof(UnityEngine.Object), false);
			if (EditorGUI.EndChangeCheck()){
				existingSpriteSource = selectedSource;
				LoadSelectedSpriteSource(selectedSource);
			}

			Rect dropRect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
			GUI.Box(dropRect, "Drop a Texture2D or one or more Sprite assets here", EditorStyles.helpBox);
			Event currentEvent = Event.current;
			if (dropRect.Contains(currentEvent.mousePosition) &&
				(currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform) &&
				HasImportableSpriteSource(DragAndDrop.objectReferences)){
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				if (currentEvent.type == EventType.DragPerform){
					UnityEngine.Object[] droppedObjects = DragAndDrop.objectReferences;
					DragAndDrop.AcceptDrag();
					ImportDroppedSpriteSources(droppedObjects);
				}
				currentEvent.Use();
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();
	}

	void LoadSelectedSpriteSource(UnityEngine.Object selectedSource){
		if (selectedSource == null){
			return;
		}
		Texture2D atlas = selectedSource as Texture2D;
		if (atlas != null){
			LoadSpritesFromTexture(atlas);
			return;
		}
		Sprite sprite = selectedSource as Sprite;
		if (sprite != null){
			string assetPath = AssetDatabase.GetAssetPath(sprite);
			Texture2D sourceAtlas = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
			List<Sprite> sprites = new List<Sprite>();
			sprites.Add(sprite);
			LoadSpriteReferences(sprites, sourceAtlas);
			return;
		}
		SetMessage("Select a Texture2D or Sprite asset.", MessageType.Warning);
	}

	void LoadSpritesFromTexture(Texture2D atlas){
		string assetPath = AssetDatabase.GetAssetPath(atlas);
		if (string.IsNullOrEmpty(assetPath)){
			SetMessage("The selected Texture2D is not a persistent project asset.", MessageType.Error);
			return;
		}
		UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
		List<Sprite> sprites = new List<Sprite>();
		if (importedAssets != null){
			for (int i = 0; i < importedAssets.Length; i++){
				Sprite sprite = importedAssets[i] as Sprite;
				if (sprite != null){
					sprites.Add(sprite);
				}
			}
		}
		if (sprites.Count == 0){
			SetMessage("No Sprite subassets were found in " + assetPath + ". The atlas was not re-sliced; use the separate grid-slicing action only if you intend to change its import data.", MessageType.Error);
			return;
		}
		LoadSpriteReferences(sprites, atlas);
	}

	static bool HasImportableSpriteSource(UnityEngine.Object[] objects){
		if (objects == null){
			return false;
		}
		for (int i = 0; i < objects.Length; i++){
			if (objects[i] is Texture2D || objects[i] is Sprite){
				return true;
			}
		}
		return false;
	}

	void ImportDroppedSpriteSources(UnityEngine.Object[] objects){
		if (profile == null){
			SetMessage("Create or select a Tile Set Profile before importing Sprite assets.", MessageType.Warning);
			return;
		}

		List<Sprite> sprites = new List<Sprite>();
		HashSet<Sprite> uniqueSprites = new HashSet<Sprite>();
		List<string> texturesWithoutSprites = new List<string>();
		string commonTexturePath = null;
		bool hasMultipleTexturePaths = false;
		for (int i = 0; objects != null && i < objects.Length; i++){
			Texture2D texture = objects[i] as Texture2D;
			if (texture != null){
				string texturePath = AssetDatabase.GetAssetPath(texture);
				UnityEngine.Object[] importedAssets = string.IsNullOrEmpty(texturePath)
					? null
					: AssetDatabase.LoadAllAssetsAtPath(texturePath);
				bool foundSpriteSubasset = false;
				for (int assetIndex = 0; importedAssets != null && assetIndex < importedAssets.Length; assetIndex++){
					Sprite importedSprite = importedAssets[assetIndex] as Sprite;
					if (importedSprite != null){
						foundSpriteSubasset = true;
						if (uniqueSprites.Add(importedSprite)){
							sprites.Add(importedSprite);
						}
					}
				}
				if (!foundSpriteSubasset){
					texturesWithoutSprites.Add(string.IsNullOrEmpty(texturePath) ? texture.name : texturePath);
				}
				RecordCommonTexturePath(texturePath, ref commonTexturePath, ref hasMultipleTexturePaths);
				continue;
			}

			Sprite sprite = objects[i] as Sprite;
			if (sprite != null && uniqueSprites.Add(sprite)){
				sprites.Add(sprite);
				RecordCommonTexturePath(AssetDatabase.GetAssetPath(sprite), ref commonTexturePath, ref hasMultipleTexturePaths);
			}
		}

		if (sprites.Count == 0){
			SetMessage("The drop contained no imported Sprite subassets. The profile was left unchanged; the Builder will not silently slice the texture.", MessageType.Error);
			return;
		}

		Texture2D commonAtlas = hasMultipleTexturePaths || string.IsNullOrEmpty(commonTexturePath)
			? null
			: AssetDatabase.LoadAssetAtPath<Texture2D>(commonTexturePath);
		existingSpriteSource = commonAtlas != null ? (UnityEngine.Object)commonAtlas : sprites[0];
		LoadSpriteReferences(sprites, commonAtlas);
		if (texturesWithoutSprites.Count > 0){
			SetMessage("Loaded Sprite assets, but these dropped textures had no Sprite subassets and were not sliced: " + string.Join(", ", texturesWithoutSprites.ToArray()), MessageType.Warning);
		}
	}

	static void RecordCommonTexturePath(string texturePath, ref string commonTexturePath, ref bool hasMultipleTexturePaths){
		if (string.IsNullOrEmpty(texturePath)){
			hasMultipleTexturePaths = true;
			return;
		}
		if (commonTexturePath == null){
			commonTexturePath = texturePath;
		}
		else if (!string.Equals(commonTexturePath, texturePath, StringComparison.OrdinalIgnoreCase)){
			hasMultipleTexturePaths = true;
		}
	}

	void LoadSpriteReferences(List<Sprite> importedSprites, Texture2D atlas){
		if (profile == null){
			SetMessage("Create or select a Tile Set Profile before importing Sprite assets.", MessageType.Warning);
			return;
		}

		Dictionary<Sprite, LevelGeneratorTileSetSlice> previousSlices = new Dictionary<Sprite, LevelGeneratorTileSetSlice>();
		if (profile.slices != null){
			for (int i = 0; i < profile.slices.Count; i++){
				LevelGeneratorTileSetSlice previous = profile.slices[i];
				if (previous != null && previous.sprite != null && !previousSlices.ContainsKey(previous.sprite)){
					previousSlices.Add(previous.sprite, previous);
				}
			}
		}

		List<LevelGeneratorTileSetSlice> updatedSlices = new List<LevelGeneratorTileSetSlice>();
		HashSet<Sprite> uniqueSprites = new HashSet<Sprite>();
		for (int i = 0; importedSprites != null && i < importedSprites.Count; i++){
			Sprite sprite = importedSprites[i];
			if (sprite == null || !uniqueSprites.Add(sprite) || !EditorUtility.IsPersistent(sprite) ||
				string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite))){
				continue;
			}
			LevelGeneratorTileSetSlice slice = new LevelGeneratorTileSetSlice();
			slice.sprite = sprite;
			slice.spriteName = sprite.name;
			slice.rect = sprite.rect;
			LevelGeneratorTileSetSlice previous;
			if (previousSlices.TryGetValue(sprite, out previous)){
				slice.hasRoleAssignment = previous.hasRoleAssignment;
				slice.role = previous.role;
				slice.wallMask = previous.wallMask;
				slice.generatedPrefab = previous.generatedPrefab;
			}
			updatedSlices.Add(slice);
		}
		if (updatedSlices.Count == 0){
			SetMessage("No persistent Sprite assets could be loaded. Existing profile assignments were retained.", MessageType.Error);
			return;
		}

		Undo.RecordObject(profile, "Load Existing Sliced Sprites");
		if (atlas != null){
			profile.sourceAtlas = atlas;
		}
		profile.slices = updatedSlices;
		SaveProfile(profile);
		SetMessage("Loaded " + updatedSlices.Count.ToString() + " existing Sprite subasset(s). Importer settings and source PPU were not changed.", MessageType.Info);
	}

	void DrawDirectProfileConnection(){
		directProfileWorkflowExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
			directProfileWorkflowExpanded, "Direct Profile Workflow");
		if (directProfileWorkflowExpanded){
			connectionTarget = DrawResponsiveObjectField(
				"Scene LevelGenerator", connectionTarget, typeof(LevelGenerator), true) as LevelGenerator;
			EditorGUILayout.HelpBox(
				"The target's existing prefab arrays remain the hierarchy, renderer, sorting/material, and collider templates. Connecting a profile assigns only the profile reference; runtime generation overrides SpriteRenderer visuals on spawned instances.",
				MessageType.Info);

			bool isSceneTarget = LevelGeneratorPreviewLifecycle.IsSceneInstance(connectionTarget);
			if (connectionTarget != null && !isSceneTarget){
				EditorGUILayout.HelpBox("Choose an actual LevelGenerator component in an open scene. Prefab assets and prefab-stage objects cannot be connected here.", MessageType.Warning);
			}
			using (new EditorGUI.DisabledScope(profile == null || !isSceneTarget)){
				string connectLabel = GetAvailableContentWidth(position.width) < 280f
					? "Connect Profile"
					: "Connect Profile to LevelGenerator";
				if (GUILayout.Button(connectLabel, GUILayout.Height(30f), GUILayout.ExpandWidth(true))){
					ConnectProfileToTarget();
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();
	}

	void ConnectProfileToTarget(){
		if (profile == null || !LevelGeneratorPreviewLifecycle.IsSceneInstance(connectionTarget)){
			SetMessage("Choose a Tile Set Profile and an actual scene LevelGenerator before connecting.", MessageType.Warning);
			return;
		}

		SerializedObject serializedTarget = new SerializedObject(connectionTarget);
		serializedTarget.Update();
		SerializedProperty profileProperty = serializedTarget.FindProperty("tileSetProfile");
		if (profileProperty == null){
			SetMessage("The selected LevelGenerator does not expose its serialized Tile Set Profile reference.", MessageType.Error);
			return;
		}

		Undo.RecordObject(connectionTarget, "Connect Tile Set Profile to Level Generator");
		profileProperty.objectReferenceValue = profile;
		serializedTarget.ApplyModifiedProperties();
		EditorUtility.SetDirty(connectionTarget);
		if (PrefabUtility.IsPartOfPrefabInstance(connectionTarget)){
			PrefabUtility.RecordPrefabInstancePropertyModifications(connectionTarget);
		}
		if (connectionTarget.LivePreviewEnabled){
			if (connectionTarget.isActiveAndEnabled){
				connectionTarget.RefreshLivePreview();
			}
			else{
				connectionTarget.ClearLivePreview();
			}
		}
		SceneView.RepaintAll();
		SetMessage("Connected " + profile.name + " to " + connectionTarget.name + ". Prefab arrays were not changed.", MessageType.Info);
	}

	void DrawBaseWallAssignment(){
		List<Sprite> selectedSprites = GetSpritesFromObjects(Selection.objects);
		List<LevelGeneratorTileSetSlice> assignedSlices = GetAssignedSlices(profile, LevelGeneratorTileRole.Empty);
		baseWallAssignmentExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
			baseWallAssignmentExpanded,
			"Base Wall / Background (" + assignedSlices.Count.ToString() + " variants)");
		bool baseWallSliceRemoved = false;
		if (baseWallAssignmentExpanded){
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
				EditorGUILayout.LabelField("Assignment target: existing Empty role", EditorStyles.miniBoldLabel);
				EditorGUILayout.LabelField(
					"This is the brown map area. Sprites assigned here render on generated empty grid cells through the existing Empty role. It is separate from wall topology and is never mask 0.",
					EditorStyles.wordWrappedMiniLabel);
				if (assignedSlices.Count == 0){
					EditorGUILayout.LabelField("No Base Wall Sprite assigned. Select Sprite assets below or drop them here.", EditorStyles.miniLabel);
				}
				else{
					for (int i = 0; i < assignedSlices.Count && !baseWallSliceRemoved; i++){
						LevelGeneratorTileSetSlice slice = assignedSlices[i];
						if (GetAvailableContentWidth(position.width) >= 170f){
							using (new EditorGUILayout.HorizontalScope()){
								Texture preview = slice.sprite == null ? null : AssetPreview.GetMiniThumbnail(slice.sprite);
								GUILayout.Box(preview == null ? GUIContent.none : new GUIContent(preview),
									GUILayout.Width(28f), GUILayout.Height(24f));
								EditorGUILayout.LabelField(
									slice.sprite == null ? "Missing Sprite: " + slice.spriteName : slice.sprite.name,
									EditorStyles.miniLabel, GUILayout.MinWidth(1f));
								if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(62f))){
									UnassignBaseWallSlice(slice);
									baseWallSliceRemoved = true;
								}
							}
						}
						else{
							EditorGUILayout.LabelField(
								slice.sprite == null ? "Missing Sprite: " + slice.spriteName : slice.sprite.name,
								EditorStyles.wordWrappedMiniLabel);
							if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.ExpandWidth(true))){
								UnassignBaseWallSlice(slice);
								baseWallSliceRemoved = true;
							}
						}
					}
				}

				if (!baseWallSliceRemoved){
					string assignLabel = GetAvailableContentWidth(position.width) < 180f
						? "Assign Selected"
						: "Assign Selected Sprite(s)" + (selectedSprites.Count == 0
							? string.Empty
							: " (" + selectedSprites.Count.ToString() + ")");
					using (new EditorGUI.DisabledScope(selectedSprites.Count == 0)){
						if (GUILayout.Button(assignLabel, GUILayout.Height(26f), GUILayout.ExpandWidth(true))){
							AssignBaseWallSprites(selectedSprites);
						}
					}

					Rect dropRect = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
					GUI.Box(dropRect, "Drop Sprite asset(s) here for Base Wall / Background", EditorStyles.helpBox);
					Event currentEvent = Event.current;
					if (dropRect.Contains(currentEvent.mousePosition) &&
						(currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)){
						List<Sprite> droppedSprites = GetSpritesFromObjects(DragAndDrop.objectReferences);
						if (droppedSprites.Count > 0){
							DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
							if (currentEvent.type == EventType.DragPerform){
								DragAndDrop.AcceptDrag();
								AssignBaseWallSprites(droppedSprites);
							}
							currentEvent.Use();
						}
					}
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();
	}

	void AssignBaseWallSprites(List<Sprite> sprites){
		if (profile == null || sprites == null || sprites.Count == 0){
			SetMessage("Choose at least one persistent Sprite asset for Base Wall / Background.", MessageType.Warning);
			return;
		}
		Undo.RecordObject(profile, "Assign Base Wall Sprite Variants");
		if (profile.slices == null){
			profile.slices = new List<LevelGeneratorTileSetSlice>();
		}

		HashSet<Sprite> assignedSprites = new HashSet<Sprite>();
		for (int spriteIndex = 0; spriteIndex < sprites.Count; spriteIndex++){
			Sprite sprite = sprites[spriteIndex];
			if (sprite == null || !assignedSprites.Add(sprite) || !EditorUtility.IsPersistent(sprite) ||
				string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite))){
				continue;
			}

			bool foundExistingSlice = false;
			for (int sliceIndex = 0; sliceIndex < profile.slices.Count; sliceIndex++){
				LevelGeneratorTileSetSlice existingSlice = profile.slices[sliceIndex];
				if (existingSlice == null || existingSlice.sprite != sprite){
					continue;
				}
				if (!existingSlice.hasRoleAssignment || existingSlice.role != LevelGeneratorTileRole.Empty){
					existingSlice.generatedPrefab = null;
				}
				existingSlice.hasRoleAssignment = true;
				existingSlice.role = LevelGeneratorTileRole.Empty;
				foundExistingSlice = true;
			}

			if (!foundExistingSlice){
				profile.slices.Add(new LevelGeneratorTileSetSlice {
					spriteName = sprite.name,
					rect = sprite.rect,
					hasRoleAssignment = true,
					role = LevelGeneratorTileRole.Empty,
					sprite = sprite
				});
			}
		}
		SaveProfile(profile);
		SetMessage("Assigned Sprite variant(s) to Base Wall / Background via the existing Empty role. No topology mask was assigned.", MessageType.Info);
	}

	void UnassignBaseWallSlice(LevelGeneratorTileSetSlice slice){
		if (profile == null || slice == null){
			return;
		}
		Undo.RecordObject(profile, "Remove Base Wall Sprite Variant");
		slice.hasRoleAssignment = false;
		slice.generatedPrefab = null;
		SaveProfile(profile);
	}

	void DrawTopologyBoard(){
		int canonicalMixedPatternCount = CountCanonicalMixedMasks();
		float availableWidth = GetAvailableContentWidth(position.width);
		DrawSectionHeading("Wall Topology Assignments",
			(30 + canonicalMixedPatternCount).ToString() + " unique patterns");
		EditorGUILayout.LabelField(
			"Choose a pattern, then drop Sprite assets or select multiple Sprite subassets and assign randomized variants. Connected diagonals reuse their canonical representative Sprite; only distinct silhouettes need separate overrides. Exact raw-mask assignments still win, followed by the canonical representative, cardinal-only fallback, and existing legacy behavior. Diagonal-only walls are generated when an empty cell touches Floor diagonally and has no cardinal Floor neighbors; mask 0 is not used.",
			EditorStyles.wordWrappedMiniLabel);
		DrawTopologyLegend(availableWidth);
		if (availableWidth >= 390f){
			using (new EditorGUILayout.HorizontalScope()){
				topologyFilter = EditorGUILayout.TextField("Filter patterns", topologyFilter);
				if (GUILayout.Button("Clear", GUILayout.Width(56f))){
					topologyFilter = string.Empty;
				}
			}
		}
		else{
			EditorGUILayout.LabelField("Filter patterns", EditorStyles.miniBoldLabel);
			if (availableWidth >= 210f){
				using (new EditorGUILayout.HorizontalScope()){
					topologyFilter = EditorGUILayout.TextField(topologyFilter, GUILayout.ExpandWidth(true));
					if (GUILayout.Button("Clear", GUILayout.Width(56f))){
						topologyFilter = string.Empty;
					}
				}
			}
			else{
				topologyFilter = EditorGUILayout.TextField(topologyFilter, GUILayout.ExpandWidth(true));
				if (GUILayout.Button("Clear", GUILayout.ExpandWidth(true))){
					topologyFilter = string.Empty;
				}
			}
		}

		List<Sprite> selectedSprites = GetSpritesFromObjects(Selection.objects);
		EditorGUILayout.LabelField(
			"Selected Sprite assets: " + selectedSprites.Count.ToString() + " — drop onto a pattern or use its Assign Selected button.",
			EditorStyles.miniLabel);

		string[] topologyTabs = {
			"Cardinal Walls (15)",
			"Diagonals (15)",
			"Mixed (" + canonicalMixedPatternCount.ToString() + " unique)"
		};
		int requestedTopologyGroup;
		selectedTopologyGroup = Mathf.Clamp(selectedTopologyGroup, 0, topologyTabs.Length - 1);
		EditorGUI.BeginChangeCheck();
		if (availableWidth >= 520f){
			requestedTopologyGroup = GUILayout.Toolbar(
				selectedTopologyGroup, topologyTabs, EditorStyles.toolbarButton, GUILayout.Height(25f));
		}
		else if (availableWidth >= 330f){
			string[] compactTopologyTabs = {
				"Cardinal (15)",
				"Diagonals (15)",
				"Mixed (" + canonicalMixedPatternCount.ToString() + ")"
			};
			requestedTopologyGroup = GUILayout.Toolbar(
				selectedTopologyGroup, compactTopologyTabs, EditorStyles.toolbarButton, GUILayout.Height(25f));
		}
		else{
			EditorGUILayout.LabelField("Topology group", EditorStyles.miniBoldLabel);
			requestedTopologyGroup = EditorGUILayout.Popup(selectedTopologyGroup, topologyTabs, GUILayout.ExpandWidth(true));
		}
		if (EditorGUI.EndChangeCheck()){
			selectedTopologyGroup = Mathf.Clamp(requestedTopologyGroup, 0, topologyTabs.Length - 1);
			topologyScrollPosition = Vector2.zero;
		}
		if (selectedTopologyGroup == 2){
			int exactOverrideCount = CountAssignedNoncanonicalMixedMasks();
			EditorGUILayout.LabelField(
				"Showing " + canonicalMixedPatternCount.ToString() + " canonical mixed patterns plus " +
				exactOverrideCount.ToString() + " directly assigned noncanonical exact-override card(s). Other raw masks appear as aliases on the representative or cardinal card.",
				EditorStyles.wordWrappedMiniLabel);
		}

		const float minimumCardWidth = 220f;
		const float maximumCardWidth = 320f;
		const float cardGap = 8f;
		const float previewPanelMinimumWidth = 200f;
		const float previewPanelMaximumWidth = 280f;
		const float previewGap = 8f;
		const float boardScrollPadding = 28f;
		bool showPreviewBesideBoard = availableWidth >=
			maximumCardWidth + boardScrollPadding + previewPanelMinimumWidth + previewGap;
		float previewPanelWidth = showPreviewBesideBoard
			? Mathf.Clamp(availableWidth * 0.3f, previewPanelMinimumWidth, previewPanelMaximumWidth)
			: availableWidth;
		float boardWidth = showPreviewBesideBoard
			? availableWidth - previewPanelWidth - previewGap
			: availableWidth;
		float boardContentWidth = Mathf.Max(1f, boardWidth - boardScrollPadding);
		int columns = Mathf.Max(1, Mathf.FloorToInt((boardContentWidth + cardGap) / (minimumCardWidth + cardGap)));
		float cardWidth = Mathf.Max(1f, Mathf.Min(maximumCardWidth,
			(boardContentWidth - cardGap * (columns - 1)) / columns));
		float boardHeight = Mathf.Clamp(position.height * 0.48f, 220f, 500f);

		using (new EditorGUILayout.HorizontalScope()){
			topologyScrollPosition = EditorGUILayout.BeginScrollView(
				topologyScrollPosition,
				GUILayout.Width(boardWidth), GUILayout.Height(boardHeight));

			switch (selectedTopologyGroup){
				case 0:
					DrawTopologyGroup(0, "Cardinal-only walls", cardWidth, cardGap, columns, selectedSprites);
					break;
				case 1:
					DrawTopologyGroup(1, "Diagonal-only walls", cardWidth, cardGap, columns, selectedSprites);
					break;
				default:
					DrawTopologyGroup(2, "Canonical mixed walls", cardWidth, cardGap, columns, selectedSprites);
					break;
			}
			EditorGUILayout.EndScrollView();

			if (showPreviewBesideBoard){
				DrawTopologyPreviewPanel(previewPanelWidth, boardHeight);
			}
		}
		if (!showPreviewBesideBoard){
			DrawTopologyPreviewPanel(previewPanelWidth, Mathf.Clamp(boardHeight * 0.48f, 180f, 260f));
		}
	}

	void DrawTopologyGroup(int group, string title, float cardWidth, float cardGap, int columns, List<Sprite> selectedSprites){
		List<int> displayMasks = GetTopologyDisplayMasks(group);
		List<int> matchingMasks = new List<int>();
		int matchingCanonicalPatterns = 0;
		int matchingExactOverrides = 0;
		for (int i = 0; i < displayMasks.Count; i++){
			int mask = displayMasks[i];
			if (!MatchesTopologyFilter(mask, topologyFilter)){
				continue;
			}
			matchingMasks.Add(mask);
			if (group == 2 && LevelGenerator.GetCanonicalWallMask(mask) != mask){
				matchingExactOverrides++;
			}
			else{
				matchingCanonicalPatterns++;
			}
		}

		EditorGUILayout.Space();
		string groupHeader = group == 2
			? title + " (" + matchingCanonicalPatterns.ToString() + " canonical patterns, " +
				matchingExactOverrides.ToString() + " exact overrides shown)"
			: title + " (" + matchingMasks.Count.ToString() + " patterns)";
		EditorGUILayout.LabelField(groupHeader, EditorStyles.boldLabel);
		if (matchingMasks.Count == 0){
			EditorGUILayout.HelpBox("No " + title.ToLowerInvariant() + " match this filter.", MessageType.Info);
			return;
		}

		bool rowOpen = false;
		for (int i = 0; i < matchingMasks.Count; i++){
			int mask = matchingMasks[i];
			if (!rowOpen){
				EditorGUILayout.BeginHorizontal();
				rowOpen = true;
			}
			else{
				GUILayout.Space(cardGap);
			}
			bool exactOverride = group == 2 && LevelGenerator.GetCanonicalWallMask(mask) != mask;
			DrawTopologySlot(mask, exactOverride, group, cardWidth, selectedSprites);
			if ((i + 1) % columns == 0){
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				rowOpen = false;
			}
		}
		if (rowOpen){
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}

	static bool IsTopologyMaskInGroup(int mask, int group){
		int cardinalMask = mask & 0x0F;
		int diagonalMask = mask & 0xF0;
		switch (group){
			case 0: return cardinalMask != 0 && diagonalMask == 0;
			case 1: return cardinalMask == 0 && diagonalMask != 0;
			case 2: return cardinalMask != 0 && diagonalMask != 0;
			default: return false;
		}
	}

	List<int> GetTopologyDisplayMasks(int group){
		List<int> masks = new List<int>();
		for (int mask = 1; mask <= 255; mask++){
			if (!IsTopologyMaskInGroup(mask, group)){
				continue;
			}
			if (group != 2 || LevelGenerator.GetCanonicalWallMask(mask) == mask || GetTopologySlices(mask).Count > 0){
				masks.Add(mask);
			}
		}
		return masks;
	}

	static int CountCanonicalMixedMasks(){
		int count = 0;
		for (int mask = 1; mask <= 255; mask++){
			if (IsTopologyMaskInGroup(mask, 2) && LevelGenerator.GetCanonicalWallMask(mask) == mask){
				count++;
			}
		}
		return count;
	}

	int CountAssignedNoncanonicalMixedMasks(){
		HashSet<int> masks = new HashSet<int>();
		if (profile == null || profile.slices == null){
			return 0;
		}
		for (int i = 0; i < profile.slices.Count; i++){
			LevelGeneratorTileSetSlice slice = profile.slices[i];
			if (slice != null && slice.hasRoleAssignment && slice.role == LevelGeneratorTileRole.WallTopology &&
				IsTopologyMaskInGroup(slice.wallMask, 2) &&
				LevelGenerator.GetCanonicalWallMask(slice.wallMask) != slice.wallMask){
				masks.Add(slice.wallMask);
			}
		}
		return masks.Count;
	}

	static void DrawTopologyLegend(float availableWidth){
		if (availableWidth >= 650f){
			using (new EditorGUILayout.HorizontalScope()){
				EditorGUILayout.LabelField("Legend", EditorStyles.miniBoldLabel, GUILayout.Width(42f));
				DrawTopologyLegendItem("WALL receives the sprite", TopologyWallColor, false);
				DrawTopologyLegendItem("FLOOR neighbor", TopologyFloorColor, false);
				DrawTopologyLegendItem("non-floor neighbor", TopologyNeutralColor, false);
			}
		}
		else{
			using (new EditorGUILayout.VerticalScope()){
				EditorGUILayout.LabelField("Legend", EditorStyles.miniBoldLabel);
				DrawTopologyLegendItem("WALL receives the sprite", TopologyWallColor, true);
				DrawTopologyLegendItem("FLOOR neighbor", TopologyFloorColor, true);
				DrawTopologyLegendItem("non-floor neighbor", TopologyNeutralColor, true);
			}
		}
	}

	static void DrawTopologyLegendItem(string label, Color color, bool wrapLabel){
		if (wrapLabel){
			using (new EditorGUILayout.HorizontalScope()){
				Rect swatch = GUILayoutUtility.GetRect(13f, 13f, GUILayout.Width(13f), GUILayout.Height(13f));
				EditorGUI.DrawRect(swatch, color);
				EditorGUILayout.LabelField(label, EditorStyles.wordWrappedMiniLabel, GUILayout.ExpandWidth(true));
			}
			return;
		}

		Rect wideSwatch = GUILayoutUtility.GetRect(13f, 13f, GUILayout.Width(13f), GUILayout.Height(13f));
		EditorGUI.DrawRect(wideSwatch, color);
		EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
		GUILayout.Space(8f);
	}

	void DrawTopologySlot(int mask, bool exactOverrideCard, int group, float cardWidth, List<Sprite> selectedSprites){
		float contentWidth = Mathf.Max(1f, cardWidth - 16f);
		using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(cardWidth))){
			EditorGUILayout.LabelField(
				"Mask " + mask.ToString() + "  ·  0x" + mask.ToString("X2"),
				EditorStyles.miniBoldLabel, GUILayout.Width(contentWidth));
			if (exactOverrideCard){
				EditorGUILayout.LabelField(
					"Exact raw-mask override · canonical Mask " + LevelGenerator.GetCanonicalWallMask(mask).ToString(),
					EditorStyles.miniBoldLabel, GUILayout.Width(contentWidth));
			}
			else if (group == 2){
				EditorGUILayout.LabelField("Canonical mixed representative", EditorStyles.miniBoldLabel, GUILayout.Width(contentWidth));
			}
			EditorGUILayout.LabelField(GetTopologyCardinalClassification(mask), EditorStyles.miniLabel, GUILayout.Width(contentWidth));
			EditorGUILayout.LabelField(GetTopologyDiagonalQualifier(mask), EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
			if (group == 0){
				DrawTopologyAliases(mask, "Mixed aliases", contentWidth);
			}
			else if (group == 2 && !exactOverrideCard){
				DrawTopologyAliases(mask, "Raw aliases", contentWidth);
			}
			DrawTopologyPreview(mask, Mathf.Min(28f, Mathf.Max(1f, contentWidth / 3f)));

			List<LevelGeneratorTileSetSlice> assignedSlices = GetTopologySlices(mask);
			if (assignedSlices.Count == 0 && (mask & 0x0F) != 0 && (mask & 0xF0) != 0){
				DrawMixedFallbackInfo(mask, contentWidth);
			}
			else{
				EditorGUILayout.LabelField(
					assignedSlices.Count.ToString() + " assigned variant(s)", EditorStyles.miniLabel, GUILayout.Width(contentWidth));
				if (assignedSlices.Count == 0){
					EditorGUILayout.LabelField("No Sprite assigned", EditorStyles.miniLabel, GUILayout.Width(contentWidth));
				}
				for (int i = 0; i < assignedSlices.Count; i++){
					LevelGeneratorTileSetSlice slice = assignedSlices[i];
					string spriteLabel = slice.sprite == null
						? "Missing Sprite: " + slice.spriteName
						: slice.sprite.name;
					if (contentWidth >= 220f){
						using (new EditorGUILayout.HorizontalScope()){
							Texture preview = slice.sprite == null ? null : AssetPreview.GetMiniThumbnail(slice.sprite);
							GUILayout.Box(preview == null ? GUIContent.none : new GUIContent(preview),
								GUILayout.Width(22f), GUILayout.Height(20f));
							EditorGUILayout.LabelField(spriteLabel, EditorStyles.miniLabel,
								GUILayout.Width(contentWidth - 124f));
							using (new EditorGUI.DisabledScope(slice.sprite == null)){
								if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(42f))){
									selectedTopologyPreview = slice.sprite;
									selectedTopologyPreviewMask = mask;
									Repaint();
								}
							}
							if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(52f))){
								UnassignTopologySlice(slice);
								return;
							}
						}
					}
					else{
						Texture preview = slice.sprite == null ? null : AssetPreview.GetMiniThumbnail(slice.sprite);
						if (contentWidth >= 54f){
							using (new EditorGUILayout.HorizontalScope()){
								GUILayout.Box(preview == null ? GUIContent.none : new GUIContent(preview),
									GUILayout.Width(22f), GUILayout.Height(20f));
								EditorGUILayout.LabelField(spriteLabel, EditorStyles.wordWrappedMiniLabel,
									GUILayout.Width(contentWidth - 28f));
							}
						}
						else{
							EditorGUILayout.LabelField(spriteLabel, EditorStyles.wordWrappedMiniLabel,
								GUILayout.Width(contentWidth));
						}

						if (contentWidth >= 110f){
							using (new EditorGUILayout.HorizontalScope()){
								using (new EditorGUI.DisabledScope(slice.sprite == null)){
									if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(42f))){
										selectedTopologyPreview = slice.sprite;
										selectedTopologyPreviewMask = mask;
										Repaint();
									}
								}
								if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(52f))){
									UnassignTopologySlice(slice);
									return;
								}
							}
						}
						else{
							using (new EditorGUI.DisabledScope(slice.sprite == null)){
								if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(contentWidth))){
									selectedTopologyPreview = slice.sprite;
									selectedTopologyPreviewMask = mask;
									Repaint();
								}
							}
							if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(contentWidth))){
								UnassignTopologySlice(slice);
								return;
							}
						}
					}
				}
			}

			string assignLabel = contentWidth < 150f
				? "Assign"
				: (contentWidth < 220f ? "Assign Selected" :
					"Assign Selected (" + selectedSprites.Count.ToString() + ")");
			using (new EditorGUI.DisabledScope(selectedSprites == null || selectedSprites.Count == 0)){
				if (GUILayout.Button(assignLabel, GUILayout.Width(contentWidth))){
					AssignTopologySprites(mask, selectedSprites);
				}
			}

			string dropLabel = contentWidth < 150f ? "Drop" :
				(contentWidth < 220f ? "Drop Sprite(s)" : "Drop Sprite asset(s) here");
			Rect dropRect = GUILayoutUtility.GetRect(contentWidth, 28f, GUILayout.Width(contentWidth), GUILayout.Height(28f));
			GUI.Box(dropRect, dropLabel, EditorStyles.helpBox);
			Event currentEvent = Event.current;
			if (dropRect.Contains(currentEvent.mousePosition) &&
				(currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)){
				List<Sprite> droppedSprites = GetSpritesFromObjects(DragAndDrop.objectReferences);
				if (droppedSprites.Count > 0){
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
					if (currentEvent.type == EventType.DragPerform){
						DragAndDrop.AcceptDrag();
						AssignTopologySprites(mask, droppedSprites);
					}
					currentEvent.Use();
				}
			}
		}
	}

	static void DrawTopologyAliases(int representativeMask, string label, float contentWidth){
		List<int> aliases = GetMixedMaskAliases(representativeMask);
		if (aliases.Count == 0){
			return;
		}

		List<string> aliasLabels = new List<string>(aliases.Count);
		for (int i = 0; i < aliases.Count; i++){
			int aliasMask = aliases[i];
			aliasLabels.Add(GetTopologyDirectionLabel(aliasMask) + " (" + aliasMask.ToString() + ")");
		}
		EditorGUILayout.LabelField(label + ": " + string.Join(", ", aliasLabels.ToArray()),
			EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
	}

	static List<int> GetMixedMaskAliases(int representativeMask){
		List<int> aliases = new List<int>();
		for (int mask = 1; mask <= 255; mask++){
			if (mask != representativeMask && IsTopologyMaskInGroup(mask, 2) &&
				LevelGenerator.GetCanonicalWallMask(mask) == representativeMask){
				aliases.Add(mask);
			}
		}
		return aliases;
	}

	void DrawMixedFallbackInfo(int mixedMask, float contentWidth){
		int cardinalMask = mixedMask & 0x0F;
		List<LevelGeneratorTileSetSlice> cardinalSlices = GetTopologySlices(cardinalMask);
		List<LevelGeneratorTileSetSlice> usableFallbacks = new List<LevelGeneratorTileSetSlice>();
		for (int i = 0; i < cardinalSlices.Count; i++){
			if (cardinalSlices[i] != null && cardinalSlices[i].sprite != null){
				usableFallbacks.Add(cardinalSlices[i]);
			}
		}

		EditorGUILayout.LabelField("Optional mixed override", EditorStyles.miniBoldLabel, GUILayout.Width(contentWidth));
		if (usableFallbacks.Count == 0){
			EditorGUILayout.LabelField(
				"No cardinal Sprite assigned for Mask " + cardinalMask.ToString() + ". The existing generic/legacy wall fallback is used.",
				EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
			return;
		}

		EditorGUILayout.LabelField(
			"Inherits cardinal Mask " + cardinalMask.ToString() + " · " + GetTopologyCardinalClassification(cardinalMask),
			EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
		string fallbackLabel = usableFallbacks[0].sprite.name;
		if (usableFallbacks.Count > 1){
			fallbackLabel += " (" + usableFallbacks.Count.ToString() + " random variants)";
		}
		if (contentWidth >= 100f){
			using (new EditorGUILayout.HorizontalScope()){
				Texture fallbackPreview = AssetPreview.GetMiniThumbnail(usableFallbacks[0].sprite);
				GUILayout.Box(fallbackPreview == null ? GUIContent.none : new GUIContent(fallbackPreview),
					GUILayout.Width(22f), GUILayout.Height(20f));
				EditorGUILayout.LabelField(fallbackLabel, EditorStyles.wordWrappedMiniLabel, GUILayout.MinWidth(1f));
				if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(42f))){
					selectedTopologyPreview = usableFallbacks[0].sprite;
					selectedTopologyPreviewMask = cardinalMask;
					Repaint();
				}
			}
		}
		else{
			EditorGUILayout.LabelField(fallbackLabel, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
			if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(contentWidth))){
				selectedTopologyPreview = usableFallbacks[0].sprite;
				selectedTopologyPreviewMask = cardinalMask;
				Repaint();
			}
		}
	}

	void DrawTopologyPreviewPanel(float panelWidth, float panelHeight){
		using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox,
			GUILayout.Width(panelWidth), GUILayout.Height(panelHeight))){
			EditorGUILayout.LabelField(panelWidth < 180f ? "Sprite Preview" : "Selected Sprite Preview", EditorStyles.boldLabel);
			if (selectedTopologyPreview == null){
				EditorGUILayout.HelpBox("Click View beside an assigned variant to see its artwork here.", MessageType.Info);
				return;
			}

			EditorGUILayout.LabelField(selectedTopologyPreview.name,
				panelWidth < 220f ? EditorStyles.wordWrappedMiniLabel : EditorStyles.miniBoldLabel);
			EditorGUILayout.LabelField(
				"Mask " + selectedTopologyPreviewMask.ToString() + " · 0x" + selectedTopologyPreviewMask.ToString("X2"),
				panelWidth < 220f ? EditorStyles.wordWrappedMiniLabel : EditorStyles.miniLabel);
			EditorGUILayout.LabelField(GetTopologyCardinalClassification(selectedTopologyPreviewMask),
				panelWidth < 220f ? EditorStyles.wordWrappedMiniLabel : EditorStyles.miniLabel);
			EditorGUILayout.LabelField(GetTopologyDiagonalQualifier(selectedTopologyPreviewMask), EditorStyles.wordWrappedMiniLabel);
			Texture preview = AssetPreview.GetAssetPreview(selectedTopologyPreview);
			if (preview == null){
				preview = AssetPreview.GetMiniThumbnail(selectedTopologyPreview);
			}
			float canvasSize = Mathf.Max(1f, Mathf.Min(panelWidth - 24f, panelHeight - 150f));
			GUILayout.FlexibleSpace();
			using (new EditorGUILayout.HorizontalScope()){
				GUILayout.FlexibleSpace();
				Rect canvasRect = GUILayoutUtility.GetRect(
					canvasSize, canvasSize, GUILayout.Width(canvasSize), GUILayout.Height(canvasSize));
				GUI.Box(canvasRect, GUIContent.none, EditorStyles.helpBox);
				if (preview != null){
					GUI.DrawTexture(canvasRect, preview, ScaleMode.ScaleToFit, true);
				}
				GUILayout.FlexibleSpace();
			}
			if (preview == null){
				EditorGUILayout.HelpBox("Unity is still preparing the Sprite preview.", MessageType.Info);
			}
			GUILayout.FlexibleSpace();
			using (new EditorGUILayout.HorizontalScope()){
				GUILayout.FlexibleSpace();
				string pingLabel = panelWidth < 130f ? "Ping" : "Ping Sprite";
				if (GUILayout.Button(pingLabel, GUILayout.Width(Mathf.Max(1f, Mathf.Min(100f, panelWidth - 24f))))){
					EditorGUIUtility.PingObject(selectedTopologyPreview);
				}
				GUILayout.FlexibleSpace();
			}
		}
	}

	static bool MatchesTopologyFilter(int mask, string filter){
		if (string.IsNullOrEmpty(filter)){
			return true;
		}

		int representativeMask = LevelGenerator.GetCanonicalWallMask(mask);
		HashSet<int> searchableMasks = new HashSet<int>();
		searchableMasks.Add(mask);
		searchableMasks.Add(representativeMask);
		List<int> aliases = GetMixedMaskAliases(representativeMask);
		for (int i = 0; i < aliases.Count; i++){
			searchableMasks.Add(aliases[i]);
		}

		List<string> searchParts = new List<string>();
		foreach (int searchableMask in searchableMasks){
			int cardinalMask = searchableMask & 0x0F;
			int diagonalMask = searchableMask & 0xF0;
			string groupLabel = cardinalMask == 0
				? "Diagonal-only"
				: (diagonalMask == 0 ? "Cardinal-only" : "Cardinal + diagonals");
			searchParts.Add(searchableMask.ToString());
			searchParts.Add("Mask" + searchableMask.ToString());
			searchParts.Add("Mask " + searchableMask.ToString());
			searchParts.Add("0x" + searchableMask.ToString("X2"));
			searchParts.Add(System.Convert.ToString(searchableMask, 2).PadLeft(8, '0'));
			searchParts.Add(GetTopologyDirectionLabel(searchableMask));
			searchParts.Add(groupLabel);
			searchParts.Add(GetTopologyCardinalClassification(searchableMask));
			searchParts.Add(GetTopologyDiagonalQualifier(searchableMask));
		}
		return string.Join(" ", searchParts.ToArray()).IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
	}

	static string GetTopologyCardinalClassification(int mask){
		int cardinalMask = mask & 0x0F;
		List<string> cardinals = new List<string>();
		for (int i = 0; i < 4; i++){
			if ((cardinalMask & TopologyDirectionMasks[i]) != 0){
				cardinals.Add(TopologyDirectionNames[i]);
			}
		}

		string classification;
		switch (cardinals.Count){
			case 1:
				classification = "Single edge";
				break;
			case 2:
				classification = cardinalMask == (LevelGenerator.WallFloorNorth | LevelGenerator.WallFloorSouth) ||
					cardinalMask == (LevelGenerator.WallFloorEast | LevelGenerator.WallFloorWest)
					? "Opposite straight"
					: "Adjacent corner";
				break;
			case 3:
				classification = "T-junction";
				break;
			case 4:
				classification = "Cross";
				break;
			default:
				classification = (mask & 0xF0) != 0 ? "Diagonal-only" : "No cardinal edge";
				break;
		}
		return cardinals.Count == 0 ? classification : classification + " · " + string.Join("+", cardinals.ToArray());
	}

	static string GetTopologyDiagonalQualifier(int mask){
		List<string> diagonals = new List<string>();
		for (int i = 4; i < TopologyDirectionMasks.Length; i++){
			if ((mask & TopologyDirectionMasks[i]) != 0){
				diagonals.Add(TopologyDirectionNames[i]);
			}
		}
		return "Diagonals: " + (diagonals.Count == 0 ? "none" : string.Join("+", diagonals.ToArray()));
	}

	static string GetTopologyDirectionLabel(int mask){
		List<string> directions = new List<string>();
		for (int i = 0; i < TopologyDirectionMasks.Length; i++){
			if ((mask & TopologyDirectionMasks[i]) != 0){
				directions.Add(TopologyDirectionNames[i]);
			}
		}
		return string.Join("+", directions.ToArray());
	}

	static void DrawTopologyPreview(int mask, float cellSize){
		Rect previewRect = GUILayoutUtility.GetRect(cellSize * 3f, cellSize * 3f,
			GUILayout.Width(cellSize * 3f), GUILayout.Height(cellSize * 3f));
		for (int i = 0; i < TopologyPreviewMasks.Length; i++){
			int column = i % 3;
			int row = i / 3;
			Rect cellRect = new Rect(
				previewRect.x + column * cellSize,
				previewRect.y + row * cellSize,
				cellSize,
				cellSize);
			bool isWall = i == 4;
			bool isFloor = !isWall && (mask & TopologyPreviewMasks[i]) != 0;
			Color background = isWall ? TopologyWallColor : (isFloor ? TopologyFloorColor : TopologyNeutralColor);
			EditorGUI.DrawRect(cellRect, background);
			Color border = new Color(0.18f, 0.2f, 0.23f);
			EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1f), border);
			EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 1f, cellRect.width, 1f), border);
			EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1f, cellRect.height), border);
			EditorGUI.DrawRect(new Rect(cellRect.xMax - 1f, cellRect.y, 1f, cellRect.height), border);
			string label = isWall
				? "WALL"
				: TopologyPreviewDirections[i] + "\n" + (isFloor ? "F" : "·");
			GUI.Label(cellRect, label, GetTopologyCellLabelStyle());
		}
	}

	static GUIStyle topologyCellLabelStyle;
	static GUIStyle GetTopologyCellLabelStyle(){
		if (topologyCellLabelStyle == null){
			topologyCellLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
			topologyCellLabelStyle.alignment = TextAnchor.MiddleCenter;
			topologyCellLabelStyle.wordWrap = false;
			// The diagram's semantic fills stay fixed and bright in both editor skins,
			// so dark bold labels retain contrast without theme-specific panel colors.
			topologyCellLabelStyle.normal.textColor = new Color(0.12f, 0.13f, 0.15f);
		}
		return topologyCellLabelStyle;
	}

	List<LevelGeneratorTileSetSlice> GetTopologySlices(int mask){
		List<LevelGeneratorTileSetSlice> assignedSlices = new List<LevelGeneratorTileSetSlice>();
		if (profile == null || profile.slices == null){
			return assignedSlices;
		}
		for (int i = 0; i < profile.slices.Count; i++){
			LevelGeneratorTileSetSlice slice = profile.slices[i];
			if (slice != null && slice.hasRoleAssignment && slice.role == LevelGeneratorTileRole.WallTopology && slice.wallMask == mask){
				assignedSlices.Add(slice);
			}
		}
		return assignedSlices;
	}

	static List<Sprite> GetSpritesFromObjects(UnityEngine.Object[] objects){
		List<Sprite> sprites = new List<Sprite>();
		HashSet<Sprite> uniqueSprites = new HashSet<Sprite>();
		for (int i = 0; objects != null && i < objects.Length; i++){
			Sprite selectedSprite = objects[i] as Sprite;
			if (selectedSprite != null){
				AddPersistentSprite(selectedSprite, uniqueSprites, sprites);
				continue;
			}

			Texture2D texture = objects[i] as Texture2D;
			if (texture == null){
				continue;
			}
			string assetPath = AssetDatabase.GetAssetPath(texture);
			UnityEngine.Object[] assets = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAllAssetsAtPath(assetPath);
			for (int assetIndex = 0; assets != null && assetIndex < assets.Length; assetIndex++){
				AddPersistentSprite(assets[assetIndex] as Sprite, uniqueSprites, sprites);
			}
		}
		return sprites;
	}

	static void AddPersistentSprite(Sprite sprite, HashSet<Sprite> uniqueSprites, List<Sprite> sprites){
		if (sprite != null && EditorUtility.IsPersistent(sprite) &&
			!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)) && uniqueSprites.Add(sprite)){
			sprites.Add(sprite);
		}
	}

	void AssignTopologySprites(int mask, List<Sprite> sprites){
		if (profile == null || mask < 1 || mask > 255 || sprites == null || sprites.Count == 0){
			SetMessage("Choose a topology pattern and at least one persistent Sprite asset.", MessageType.Warning);
			return;
		}
		Undo.RecordObject(profile, "Assign Wall Topology Sprite Variants");
		if (profile.slices == null){
			profile.slices = new List<LevelGeneratorTileSetSlice>();
		}

		HashSet<Sprite> assignedSprites = new HashSet<Sprite>();
		for (int spriteIndex = 0; spriteIndex < sprites.Count; spriteIndex++){
			Sprite sprite = sprites[spriteIndex];
			if (sprite == null || !assignedSprites.Add(sprite) || !EditorUtility.IsPersistent(sprite) ||
				string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite))){
				continue;
			}

			bool foundExistingSlice = false;
			for (int sliceIndex = 0; sliceIndex < profile.slices.Count; sliceIndex++){
				LevelGeneratorTileSetSlice existingSlice = profile.slices[sliceIndex];
				if (existingSlice == null || existingSlice.sprite != sprite){
					continue;
				}
				if (!existingSlice.hasRoleAssignment || existingSlice.role != LevelGeneratorTileRole.WallTopology){
					existingSlice.generatedPrefab = null;
				}
				existingSlice.hasRoleAssignment = true;
				existingSlice.role = LevelGeneratorTileRole.WallTopology;
				existingSlice.wallMask = mask;
				foundExistingSlice = true;
			}

			if (!foundExistingSlice){
				profile.slices.Add(new LevelGeneratorTileSetSlice {
					spriteName = sprite.name,
					rect = sprite.rect,
					hasRoleAssignment = true,
					role = LevelGeneratorTileRole.WallTopology,
					wallMask = mask,
					sprite = sprite
				});
			}
		}
		SaveProfile(profile);
		SetMessage("Assigned Sprite variant(s) to wall topology mask " + mask.ToString() + ". Existing atlas slicing and importer data were not changed.", MessageType.Info);
	}

	void UnassignTopologySlice(LevelGeneratorTileSetSlice slice){
		if (profile == null || slice == null){
			return;
		}
		Undo.RecordObject(profile, "Remove Wall Topology Sprite Variant");
		slice.hasRoleAssignment = false;
		slice.generatedPrefab = null;
		SaveProfile(profile);
	}

	void DrawAdvancedWorkflow(){
		advancedWorkflowExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
			advancedWorkflowExpanded, "Advanced: Bake Sprite Variants to Prefabs");
		if (advancedWorkflowExpanded){
			EditorGUI.indentLevel++;
			DrawGridSlicingOptions();
			DrawTemplateAndTargetFields();
			DrawActions();
			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndFoldoutHeaderGroup();
	}

	void DrawGridSlicingOptions(){
		gridSlicingOptionsExpanded = EditorGUILayout.Foldout(gridSlicingOptionsExpanded, "Optional Grid Slicing", true);
		if (!gridSlicingOptionsExpanded){
			return;
		}

		Texture2D atlas = profile.sourceAtlas;
		Vector2Int cellSize = profile.cellSize;
		Vector2Int offset = profile.offset;
		Vector2Int spacing = profile.spacing;
		float pixelsPerUnit = profile.pixelsPerUnit;
		EditorGUI.BeginChangeCheck();
		atlas = DrawResponsiveObjectField("Grid Source Atlas", atlas, typeof(Texture2D), false) as Texture2D;
		cellSize = EditorGUILayout.Vector2IntField("Cell Size", cellSize);
		offset = EditorGUILayout.Vector2IntField("Offset (top-left)", offset);
		spacing = EditorGUILayout.Vector2IntField("Spacing", spacing);
		pixelsPerUnit = EditorGUILayout.FloatField(
			new GUIContent("Grid Slice Pixels Per Unit", "Used only by the explicit grid Slice / Re-slice action, which writes this value to the source TextureImporter. It does not set SpriteRenderer visual size."),
			pixelsPerUnit);
		if (EditorGUI.EndChangeCheck()){
			Undo.RecordObject(profile, "Change Tile Set Grid Settings");
			profile.sourceAtlas = atlas;
			profile.cellSize = cellSize;
			profile.offset = offset;
			profile.spacing = spacing;
			profile.pixelsPerUnit = pixelsPerUnit;
			SaveProfile(profile);
		}
		EditorGUILayout.LabelField(
			"These profile values describe optional grid slicing only. Grid Slice Pixels Per Unit is written to the source TextureImporter only when you explicitly run Slice / Re-slice; it is separate from the current importer setting and from Sprite Render Size (World Units). Existing imported Sprites do not depend on these grid settings.",
			EditorStyles.wordWrappedMiniLabel);
		if (GUILayout.Button("Slice / Re-slice with Grid", GUILayout.Height(24f))){
			SliceAtlas();
		}
		string freshnessMessage;
		if (!IsSliceConfigurationCurrent(profile, out freshnessMessage)){
			EditorGUILayout.HelpBox("Grid slice status: " + freshnessMessage, MessageType.Warning);
		}
	}

	void DrawTemplateAndTargetFields(){
		EditorGUILayout.LabelField("Optional Baked Prefab Setup", EditorStyles.boldLabel);
		templateGenerator = DrawResponsiveObjectField(
			"Template Source", templateGenerator, typeof(LevelGenerator), true) as LevelGenerator;
		applyTarget = DrawResponsiveObjectField(
			"Apply To (optional)", applyTarget, typeof(LevelGenerator), true) as LevelGenerator;
		EditorGUILayout.LabelField(
			"This is an optional legacy/advanced workflow. Template Source supplies prefab assets used to bake variants; Wall Topology uses its Generic Wall (wallObj) prefab. Apply To receives prefab-array assignments when you click Apply.",
			EditorStyles.wordWrappedMiniLabel);
		EditorGUILayout.LabelField(
			"For direct profiles, use Connect Profile to LevelGenerator instead: it keeps the target's existing arrays as hierarchy/collider templates and does not require baking variants. To opt into baking, assign roles and masks, Generate / Update Prefab Variants, then Apply To LevelGenerator. Unassigned base roles keep target arrays or fall back to Template Source; assigned topology masks replace only those masks. Apply remains explicit and Undo-backed.",
			EditorStyles.wordWrappedMiniLabel);
	}

	void DrawActions(){
		float availableWidth = GetAvailableContentWidth(position.width);
		string generateLabel = availableWidth < 150f ? "Generate" :
			(availableWidth < 300f ? "Generate Variants" : "Generate / Update Prefab Variants");
		string applyLabel = availableWidth < 150f ? "Apply" :
			(availableWidth < 300f ? "Apply Profile" : "Apply To LevelGenerator");
		if (availableWidth >= 480f){
			using (new EditorGUILayout.HorizontalScope()){
				if (GUILayout.Button(generateLabel, GUILayout.Height(30f))){
					GeneratePrefabVariants();
				}
				if (GUILayout.Button(applyLabel, GUILayout.Height(30f))){
					ApplyToTarget();
				}
			}
		}
		else{
			if (GUILayout.Button(generateLabel, GUILayout.Height(30f), GUILayout.ExpandWidth(true))){
				GeneratePrefabVariants();
			}
			if (GUILayout.Button(applyLabel, GUILayout.Height(30f), GUILayout.ExpandWidth(true))){
				ApplyToTarget();
			}
		}
	}

	void DrawSliceGrid(float contentViewportHeight){
		if (profile.slices == null || profile.slices.Count == 0){
			EditorGUILayout.HelpBox("No sprites are stored in this profile yet. Import existing Sprite subassets above, or use Optional Grid Slicing.", MessageType.Info);
			return;
		}

		DrawSectionHeading("Sprite Palette", profile.slices.Count.ToString() + " sprites");
		EditorGUILayout.LabelField(
			"Base Wall / Background has its own first assignment target above, and topology assignments are grouped in the 8-neighbor board. The sprite palette below remains available for Base Wall (the existing Empty role), Floor, Generic Wall, and directional role assignments.",
			EditorStyles.wordWrappedMiniLabel);
		Rect paletteHeaderRect = GUILayoutUtility.GetLastRect();
		float paletteHeaderContentY = measuredPaletteHeaderContentY;
		float paletteTopInViewport = paletteHeaderContentY - contentScrollPosition.y;
		float remainingPaletteHeight = contentViewportHeight - paletteTopInViewport - 8f;
		if (Event.current.type == EventType.Repaint){
			measuredPaletteHeaderContentY = paletteHeaderRect.yMax;
		}
		float maximumPaletteHeight = Mathf.Max(1f, Mathf.Min(480f, contentViewportHeight));
		float paletteScrollHeight = Mathf.Min(maximumPaletteHeight, Mathf.Max(120f, remainingPaletteHeight));
		const float minimumCardWidth = 250f;
		const float maximumCardWidth = 340f;
		const float cardGap = 8f;
		float availableWidth = GetAvailableContentWidth(position.width);
		int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + cardGap) / (minimumCardWidth + cardGap)));
		float cardWidth = Mathf.Max(1f, Mathf.Min(maximumCardWidth,
			(availableWidth - cardGap * (columns - 1)) / columns));
		slicesScrollPosition = EditorGUILayout.BeginScrollView(
			slicesScrollPosition,
			GUILayout.Height(paletteScrollHeight));
		for (int first = 0; first < profile.slices.Count; first += columns){
			EditorGUILayout.BeginHorizontal();
			int end = Mathf.Min(first + columns, profile.slices.Count);
			for (int i = first; i < end; i++){
				if (i > first){
					GUILayout.Space(cardGap);
				}
				DrawSliceCard(profile.slices[i], cardWidth);
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndScrollView();
	}

	void DrawSliceCard(LevelGeneratorTileSetSlice slice, float cardWidth){
		float contentWidth = Mathf.Max(1f, cardWidth - 16f);
		using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(cardWidth))){
			Texture preview = null;
			if (slice.sprite != null){
				preview = AssetPreview.GetAssetPreview(slice.sprite);
				if (preview == null){
					preview = AssetPreview.GetMiniThumbnail(slice.sprite);
				}
			}
			GUILayout.Box(
				preview == null ? GUIContent.none : new GUIContent(preview),
				GUILayout.Width(Mathf.Min(112f, contentWidth)),
				GUILayout.Height(92f));
			EditorGUILayout.LabelField(slice.spriteName, EditorStyles.miniBoldLabel, GUILayout.Width(contentWidth));
			EditorGUILayout.LabelField(
				"(" + slice.rect.x.ToString("0") + ", " + slice.rect.y.ToString("0") + ") " +
				slice.rect.width.ToString("0") + " x " + slice.rect.height.ToString("0"),
				EditorStyles.miniLabel, GUILayout.Width(contentWidth));

			int currentChoice = slice.hasRoleAssignment ? Mathf.Clamp((int)slice.role + 1, 0, RoleLabels.Length - 1) : 0;
			EditorGUILayout.LabelField("Role", EditorStyles.miniBoldLabel);
			EditorGUI.BeginChangeCheck();
			string[] roleLabels = contentWidth < 220f ? CompactRoleLabels : RoleLabels;
			int selectedChoice = EditorGUILayout.Popup(currentChoice, roleLabels, GUILayout.Width(contentWidth));
			if (EditorGUI.EndChangeCheck()){
				Undo.RecordObject(profile, "Assign Tile Set Role");
				if (selectedChoice == 0){
					slice.hasRoleAssignment = false;
				}
				else{
					LevelGeneratorTileRole selectedRole = (LevelGeneratorTileRole)(selectedChoice - 1);
					if (!slice.hasRoleAssignment || slice.role != selectedRole){
						slice.generatedPrefab = null;
					}
					slice.hasRoleAssignment = true;
					slice.role = selectedRole;
					if (selectedRole == LevelGeneratorTileRole.WallTopology && (slice.wallMask < 1 || slice.wallMask > 255)){
						slice.wallMask = 1;
					}
				}
				if (!slice.hasRoleAssignment){
					slice.generatedPrefab = null;
				}
				SaveProfile(profile);
			}


			DrawPlacementGuidance(slice, contentWidth);
			string prefabStatus = slice.generatedPrefab == null ? "Prefab not generated" : "Prefab ready";
			EditorGUILayout.LabelField(prefabStatus, EditorStyles.miniLabel, GUILayout.Width(contentWidth));
		}
	}

	static void DrawPlacementGuidance(LevelGeneratorTileSetSlice slice, float contentWidth){
		if (slice == null){
			return;
		}
		if (!slice.hasRoleAssignment){
			EditorGUILayout.LabelField("Unassigned: this sprite will not appear in generated levels.",
				EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
			return;
		}

		switch (slice.role){
			case LevelGeneratorTileRole.Empty:
				EditorGUILayout.LabelField("Base Wall / Background: used on generated empty grid cells via the existing Empty role, not on topology mask 0.", EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				break;
			case LevelGeneratorTileRole.Floor:
				EditorGUILayout.LabelField("Used on a generated floor cell.", EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				break;
			case LevelGeneratorTileRole.GenericWall:
				EditorGUILayout.LabelField("Fallback for a wall whose exact neighbor pattern has no assigned sprite.",
					EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				break;
			case LevelGeneratorTileRole.WallUp:
				EditorGUILayout.LabelField("Single-wall fallback: wall above its floor neighbor (floor is S).",
					EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				break;
			case LevelGeneratorTileRole.WallDown:
				EditorGUILayout.LabelField("Single-wall fallback: wall below its floor neighbor (floor is N).",
					EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				break;
			case LevelGeneratorTileRole.WallLeft:
				EditorGUILayout.LabelField("Single-wall fallback: wall left of its floor neighbor (floor is E).",
					EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				break;
			case LevelGeneratorTileRole.WallRight:
				EditorGUILayout.LabelField("Single-wall fallback: wall right of its floor neighbor (floor is W).",
					EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				break;
			case LevelGeneratorTileRole.WallTopology:
				EditorGUILayout.LabelField(
					"Wall topology mask " + slice.wallMask.ToString() + " is edited in the position-first board.",
					EditorStyles.wordWrappedMiniLabel, GUILayout.Width(contentWidth));
				DrawTopologyPreview(slice.wallMask, 26f);
				break;
		}
	}


	void CreateProfile(){
		string path = EditorUtility.SaveFilePanelInProject(
			"Create Tile Set Profile", "LevelGeneratorTileSet", "asset", "Choose where to save the reusable tile set profile.");
		if (string.IsNullOrEmpty(path)){
			return;
		}

		LevelGeneratorTileSet createdProfile = CreateInstance<LevelGeneratorTileSet>();
		createdProfile.sourceAtlas = scratchAtlas;
		createdProfile.cellSize = scratchCellSize;
		createdProfile.offset = scratchOffset;
		createdProfile.spacing = scratchSpacing;
		createdProfile.pixelsPerUnit = scratchPixelsPerUnit;
		createdProfile.spriteRenderSizeWorldUnits = Vector2.one;
		AssetDatabase.CreateAsset(createdProfile, path);
		profile = createdProfile;
		existingSpriteSource = createdProfile.sourceAtlas;
		SaveProfile(profile);
		EditorGUIUtility.PingObject(profile);
		operationMessage = "Created profile: " + path;
		operationMessageType = MessageType.Info;
	}

	void SliceAtlas(){
		if (profile == null){
			SetMessage("Create or select a Tile Set Profile before slicing.", MessageType.Warning);
			return;
		}
		if (profile.sourceAtlas == null){
			SetMessage("Select a PNG or Texture2D asset in Source Atlas.", MessageType.Warning);
			return;
		}
		if (!IsValidPixelsPerUnit(profile.pixelsPerUnit)){
			SetMessage("Pixels Per Unit must be a finite value greater than zero.", MessageType.Error);
			return;
		}

		string atlasPath = AssetDatabase.GetAssetPath(profile.sourceAtlas);
		string profilePath = AssetDatabase.GetAssetPath(profile);
		if (string.IsNullOrEmpty(atlasPath) || !atlasPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)){
			SetMessage("Source Atlas must be an imported asset inside this project's Assets folder.", MessageType.Error);
			return;
		}
		if (string.IsNullOrEmpty(profilePath) || !profilePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)){
			SetMessage("Save the Tile Set Profile inside the project's Assets folder before slicing.", MessageType.Error);
			return;
		}

		int sourceWidth;
		int sourceHeight;
		int columns;
		int rows;
		try{
			TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
			if (importer == null){
				SetMessage("Unity could not find a TextureImporter for the selected atlas.", MessageType.Error);
				return;
			}

			bool importerChanged = false;
			if (importer.textureType != TextureImporterType.Sprite){
				importer.textureType = TextureImporterType.Sprite;
				importerChanged = true;
			}
			if (importer.spriteImportMode != SpriteImportMode.Multiple){
				importer.spriteImportMode = SpriteImportMode.Multiple;
				importerChanged = true;
			}
			if (importer.spritePixelsPerUnit != profile.pixelsPerUnit){
				importer.spritePixelsPerUnit = profile.pixelsPerUnit;
				importerChanged = true;
			}
			if (importerChanged){
				importer.SaveAndReimport();
			}

			importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
			if (importer == null){
				SetMessage("TextureImporter was unavailable after reimporting the atlas.", MessageType.Error);
				return;
			}

			SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
			factories.Init();
			ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
			if (dataProvider == null){
				SetMessage("The selected atlas does not provide Unity Sprite Editor slicing data.", MessageType.Error);
				return;
			}
			dataProvider.InitSpriteEditorDataProvider();
			string dimensionsError;
			if (!TryGetSourceDimensions(dataProvider, out sourceWidth, out sourceHeight, out dimensionsError)){
				SetMessage("Could not obtain original source image dimensions: " + dimensionsError, MessageType.Error);
				return;
			}
			string gridError;
			if (!TryGetGridDimensions(sourceWidth, sourceHeight, profile.cellSize, profile.offset, profile.spacing, out columns, out rows, out gridError)){
				SetMessage(gridError, MessageType.Error);
				return;
			}

			ISpriteNameFileIdDataProvider nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
			if (nameFileIdProvider == null){
				SetMessage("The installed Sprite Editor data provider does not support sprite name/file-ID mappings.", MessageType.Error);
				return;
			}

			SpriteRect[] existingRects = dataProvider.GetSpriteRects();
			IEnumerable<SpriteNameFileIdPair> existingPairsEnumerable = nameFileIdProvider.GetNameFileIdPairs();
			List<SpriteNameFileIdPair> existingPairs = existingPairsEnumerable == null
				? new List<SpriteNameFileIdPair>()
				: new List<SpriteNameFileIdPair>(existingPairsEnumerable);
			if (existingRects == null){
				existingRects = new SpriteRect[0];
			}

			string atlasGuid = AssetDatabase.AssetPathToGUID(atlasPath);
			string profileGuid = AssetDatabase.AssetPathToGUID(profilePath).Replace("-", string.Empty);
			if (string.IsNullOrEmpty(profileGuid)){
				SetMessage("Unity could not determine the Tile Set Profile asset GUID.", MessageType.Error);
				return;
			}
			string ownedPrefix = "TileSet_" + profileGuid + "_";
			HashSet<string> previousProfileNames = new HashSet<string>(StringComparer.Ordinal);
			bool samePreviouslySlicedAtlas = profile.lastSlicedAtlasGuid == atlasGuid;
			if (samePreviouslySlicedAtlas && profile.slices != null){
				for (int i = 0; i < profile.slices.Count; i++){
					if (profile.slices[i] != null && !string.IsNullOrEmpty(profile.slices[i].spriteName)){
						previousProfileNames.Add(profile.slices[i].spriteName);
					}
				}
			}

			Dictionary<string, SpriteRect> existingByIdentity = new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
			List<SpriteRect> retainedRects = new List<SpriteRect>();
			for (int i = 0; i < existingRects.Length; i++){
				SpriteRect existingRect = existingRects[i];
				if (existingRect == null){
					continue;
				}
				string identity = GetRectIdentity(existingRect.name, existingRect.rect);
				if (!existingByIdentity.ContainsKey(identity)){
					existingByIdentity.Add(identity, existingRect);
				}
				bool isOwned = existingRect.name != null &&
					(existingRect.name.StartsWith(ownedPrefix, StringComparison.Ordinal) || previousProfileNames.Contains(existingRect.name));
				if (!isOwned){
					retainedRects.Add(existingRect);
				}
			}

			List<SpriteRect> generatedRects = new List<SpriteRect>(columns * rows);
			List<SpriteNameFileIdPair> generatedPairs = new List<SpriteNameFileIdPair>(columns * rows);
			long stepX = (long)profile.cellSize.x + profile.spacing.x;
			long stepY = (long)profile.cellSize.y + profile.spacing.y;
			for (int row = 0; row < rows; row++){
				for (int column = 0; column < columns; column++){
					string spriteName = ownedPrefix + "r" + (row + 1).ToString("D3") + "_c" + (column + 1).ToString("D3");
					float x = profile.offset.x + column * stepX;
					float y = sourceHeight - profile.offset.y - row * stepY - profile.cellSize.y;
					Rect rect = new Rect(x, y, profile.cellSize.x, profile.cellSize.y);
					GUID spriteId = GUID.Generate();
					SpriteRect previousRect;
					if (existingByIdentity.TryGetValue(GetRectIdentity(spriteName, rect), out previousRect)){
						spriteId = previousRect.spriteID;
					}

					SpriteRect generatedRect = new SpriteRect();
					generatedRect.name = spriteName;
					generatedRect.rect = rect;
					generatedRect.spriteID = spriteId;
					generatedRect.alignment = SpriteAlignment.Center;
					generatedRect.pivot = new Vector2(0.5f, 0.5f);
					generatedRects.Add(generatedRect);
					generatedPairs.Add(new SpriteNameFileIdPair(spriteName, spriteId));
				}
			}

			List<SpriteRect> updatedRects = new List<SpriteRect>(retainedRects.Count + generatedRects.Count);
			updatedRects.AddRange(retainedRects);
			updatedRects.AddRange(generatedRects);
			List<SpriteNameFileIdPair> updatedPairs = new List<SpriteNameFileIdPair>();
			for (int i = 0; i < existingPairs.Count; i++){
				SpriteNameFileIdPair pair = existingPairs[i];
				if (pair == null || string.IsNullOrEmpty(pair.name)){
					continue;
				}
				if (pair.name.StartsWith(ownedPrefix, StringComparison.Ordinal) || previousProfileNames.Contains(pair.name)){
					continue;
				}
				updatedPairs.Add(pair);
			}
			updatedPairs.AddRange(generatedPairs);

			dataProvider.SetSpriteRects(updatedRects.ToArray());
			nameFileIdProvider.SetNameFileIdPairs(updatedPairs);
			dataProvider.Apply();
			importer.SaveAndReimport();

			Texture2D reimportedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
			string currentAtlasGuid = AssetDatabase.AssetPathToGUID(atlasPath);
			Dictionary<string, LevelGeneratorTileSetSlice> oldSlicesByIdentity = new Dictionary<string, LevelGeneratorTileSetSlice>(StringComparer.Ordinal);
			if (samePreviouslySlicedAtlas && profile.slices != null){
				for (int i = 0; i < profile.slices.Count; i++){
					LevelGeneratorTileSetSlice oldSlice = profile.slices[i];
					if (oldSlice != null && !string.IsNullOrEmpty(oldSlice.spriteName)){
						string identity = GetRectIdentity(oldSlice.spriteName, oldSlice.rect);
						if (!oldSlicesByIdentity.ContainsKey(identity)){
							oldSlicesByIdentity.Add(identity, oldSlice);
						}
					}
				}
			}

			Dictionary<string, Sprite> importedSpritesByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
			UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(atlasPath);
			for (int i = 0; i < importedAssets.Length; i++){
				Sprite importedSprite = importedAssets[i] as Sprite;
				if (importedSprite != null && !importedSpritesByName.ContainsKey(importedSprite.name)){
					importedSpritesByName.Add(importedSprite.name, importedSprite);
				}
			}

			List<LevelGeneratorTileSetSlice> updatedSlices = new List<LevelGeneratorTileSetSlice>(generatedRects.Count);
			for (int i = 0; i < generatedRects.Count; i++){
				SpriteRect generatedRect = generatedRects[i];
				Sprite importedSprite;
				importedSpritesByName.TryGetValue(generatedRect.name, out importedSprite);
				LevelGeneratorTileSetSlice newSlice = new LevelGeneratorTileSetSlice();
				newSlice.spriteName = generatedRect.name;
				newSlice.rect = generatedRect.rect;
				newSlice.sprite = importedSprite;
				LevelGeneratorTileSetSlice previousSlice;
				if (samePreviouslySlicedAtlas && oldSlicesByIdentity.TryGetValue(GetRectIdentity(newSlice.spriteName, newSlice.rect), out previousSlice)){
					newSlice.hasRoleAssignment = previousSlice.hasRoleAssignment;
					newSlice.role = previousSlice.role;
					newSlice.wallMask = previousSlice.wallMask;
					newSlice.generatedPrefab = previousSlice.generatedPrefab;
				}
				updatedSlices.Add(newSlice);
			}

			Undo.RecordObject(profile, "Slice Tile Set Atlas");
			profile.sourceAtlas = reimportedAtlas == null ? profile.sourceAtlas : reimportedAtlas;
			profile.slices = updatedSlices;
			profile.lastSlicedAtlasGuid = currentAtlasGuid;
			profile.lastSliceSignature = BuildSliceSignature(profile, sourceWidth, sourceHeight);
			SaveProfile(profile);
			SetMessage("Sliced " + generatedRects.Count.ToString() + " cells from " + atlasPath + ". Unrelated Sprite Editor slices were preserved.", MessageType.Info);
		}
		catch (Exception exception){
			Debug.LogException(exception);
			SetMessage("Atlas slicing failed: " + exception.Message, MessageType.Error);
		}
	}

	void GeneratePrefabVariants(){
		if (profile == null){
			SetMessage("Select a Tile Set Profile before generating prefab variants.", MessageType.Warning);
			return;
		}
		if (profile.slices == null || profile.slices.Count == 0){
			SetMessage("This profile has no Sprite entries to generate.", MessageType.Warning);
			return;
		}

		List<string> messages = new List<string>();
		int generatedCount = 0;
		string outputFolder;
		try{
			outputFolder = GetOrCreateOutputFolder(profile);
		}
		catch (Exception exception){
			Debug.LogException(exception);
			SetMessage("Could not prepare this profile's dedicated prefab folder: " + exception.Message, MessageType.Error);
			return;
		}

		Undo.RecordObject(profile, "Generate Tile Set Prefab Variants");
		for (int roleIndex = 0; roleIndex < AssignmentRoles.Length; roleIndex++){
			LevelGeneratorTileRole role = AssignmentRoles[roleIndex];
			List<LevelGeneratorTileSetSlice> roleSlices = GetAssignedSlices(profile, role);
			if (roleSlices.Count == 0){
				continue;
			}

			string arrayName = GetGeneratorArrayName(role);
			GameObject template = GetTemplateForRole(templateGenerator, role);
			if (template == null){
				messages.Add(GetRoleLabel(role) + " blocked: select a Template Source with a valid prefab asset in " + arrayName + ".");
				continue;
			}

			SpriteRenderer templateRenderer = FindSpriteRenderer(template);
			if (templateRenderer == null){
				messages.Add(GetRoleLabel(role) + " blocked: template " + template.name + " has no child SpriteRenderer.");
				continue;
			}

			for (int sliceIndex = 0; sliceIndex < roleSlices.Count; sliceIndex++){
				LevelGeneratorTileSetSlice slice = roleSlices[sliceIndex];
				if (slice.sprite == null){
					messages.Add(slice.spriteName + " blocked: its imported Sprite reference is missing; reload the source Sprite assets.");
					continue;
				}

				GameObject instance = null;
				try{
					instance = PrefabUtility.InstantiatePrefab(template) as GameObject;
					if (instance == null){
						messages.Add(slice.spriteName + " blocked: Unity could not instantiate its " + GetRoleLabel(role) + " template.");
						continue;
					}

					SpriteRenderer renderer = FindSpriteRenderer(instance);
					if (renderer == null){
						messages.Add(slice.spriteName + " blocked: the instantiated template has no child SpriteRenderer.");
						continue;
					}
					Vector2 templateFootprint = GetTemplateRendererFootprint(templateRenderer);
					renderer.sprite = slice.sprite;
					renderer.drawMode = SpriteDrawMode.Sliced;
					renderer.size = templateFootprint;
					string prefabPath = GetPrefabOutputPath(slice, outputFolder);
					bool savedSuccessfully;
					GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out savedSuccessfully);
					if (!savedSuccessfully || savedPrefab == null){
						messages.Add(slice.spriteName + " failed to save at " + prefabPath + ".");
						continue;
					}

					slice.generatedPrefab = savedPrefab;
					generatedCount++;
				}
				catch (Exception exception){
					Debug.LogException(exception);
					messages.Add(slice.spriteName + " failed: " + exception.Message);
				}
				finally{
					if (instance != null){
						DestroyImmediate(instance);
					}
				}
			}
		}

		if (generatedCount > 0){
			SaveProfile(profile);
		}
		if (messages.Count == 0){
			SetMessage("Generated or updated " + generatedCount.ToString() + " prefab variant(s) in " + outputFolder + ". No generator arrays were changed.", MessageType.Info);
		}
		else{
			messages.Insert(0, "Generated or updated " + generatedCount.ToString() + " prefab variant(s). Roles without a valid template or slice were skipped; no generator arrays were changed.");
			SetMessage(string.Join("\n", messages.ToArray()), MessageType.Warning);
		}
	}

	void ApplyToTarget(){
		if (profile == null){
			SetMessage("Select a Tile Set Profile before applying it.", MessageType.Warning);
			return;
		}
		if (applyTarget == null){
			SetMessage("Select an Apply To LevelGenerator. Applying is explicit and never happens when role assignments change.", MessageType.Warning);
			return;
		}

		SerializedObject serializedTarget = new SerializedObject(applyTarget);
		serializedTarget.Update();
		Dictionary<LevelGeneratorTileRole, List<GameObject>> rolePrefabs = new Dictionary<LevelGeneratorTileRole, List<GameObject>>();
		List<string> errors = new List<string>();
		for (int roleIndex = 0; roleIndex < Roles.Length; roleIndex++){
			LevelGeneratorTileRole role = Roles[roleIndex];
			List<LevelGeneratorTileSetSlice> roleSlices = GetAssignedSlices(profile, role);
			List<GameObject> prefabs = new List<GameObject>();
			if (roleSlices.Count == 0){
				GameObject[] existingPrefabs = GetPrefabArrayForRole(applyTarget, role);
				if (existingPrefabs != null){
					for (int prefabIndex = 0; prefabIndex < existingPrefabs.Length; prefabIndex++){
						GameObject existingPrefab = existingPrefabs[prefabIndex];
						if (existingPrefab == null){
							errors.Add(GetRoleLabel(role) + " target array " + GetGeneratorArrayName(role) + " contains an unassigned prefab at index " + (prefabIndex + 1).ToString() + ".");
							prefabs.Add(null);
							continue;
						}
						if (!IsUsablePrefabAsset(existingPrefab)){
							errors.Add(GetRoleLabel(role) + " target array " + GetGeneratorArrayName(role) + " entry " + (prefabIndex + 1).ToString() + " is not a persistent prefab asset.");
						}
						prefabs.Add(existingPrefab);
					}
				}
				if (prefabs.Count == 0){
					GameObject[] templatePrefabs = GetPrefabArrayForRole(templateGenerator, role);
					if (templatePrefabs == null || templatePrefabs.Length == 0){
						errors.Add(GetRoleLabel(role) + " has no assigned slices or current target array, and Template Source has no prefab array in " + GetGeneratorArrayName(role) + ".");
					}
					else{
						for (int templateIndex = 0; templateIndex < templatePrefabs.Length; templateIndex++){
							GameObject templatePrefab = templatePrefabs[templateIndex];
							if (!IsUsablePrefabAsset(templatePrefab)){
								errors.Add(GetRoleLabel(role) + " Template Source array " + GetGeneratorArrayName(role) + " contains a nonpersistent or unassigned prefab at index " + (templateIndex + 1).ToString() + ".");
							}
							prefabs.Add(templatePrefab);
						}
					}
				}
				rolePrefabs.Add(role, prefabs);
				continue;
			}

			for (int sliceIndex = 0; sliceIndex < roleSlices.Count; sliceIndex++){
				LevelGeneratorTileSetSlice slice = roleSlices[sliceIndex];
				if (slice.sprite == null){
					errors.Add(slice.spriteName + " has no imported Sprite reference.");
				}
				if (slice.generatedPrefab == null){
					errors.Add(slice.spriteName + " has no generated prefab. Generate or update variants first.");
				}
				else if (!IsUsablePrefabAsset(slice.generatedPrefab)){
					errors.Add(slice.spriteName + " does not reference a persistent prefab asset.");
				}
				else{
					prefabs.Add(slice.generatedPrefab);
				}
			}
			rolePrefabs.Add(role, prefabs);
		}

		Dictionary<int, List<GameObject>> topologyPrefabs = new Dictionary<int, List<GameObject>>();
		List<LevelGeneratorTileSetSlice> topologySlices = GetAssignedSlices(profile, LevelGeneratorTileRole.WallTopology);
		for (int sliceIndex = 0; sliceIndex < topologySlices.Count; sliceIndex++){
			LevelGeneratorTileSetSlice slice = topologySlices[sliceIndex];
			if (slice.wallMask < 1 || slice.wallMask > 255){
				errors.Add(slice.spriteName + " has an invalid wall topology mask. Choose one of the 255 non-empty 8-neighbor patterns.");
				continue;
			}
			if (slice.sprite == null){
				errors.Add(slice.spriteName + " has no imported Sprite reference.");
			}
			if (slice.generatedPrefab == null){
				errors.Add(slice.spriteName + " has no generated prefab. Generate or update variants first.");
				continue;
			}
			if (!IsUsablePrefabAsset(slice.generatedPrefab)){
				errors.Add(slice.spriteName + " does not reference a persistent prefab asset.");
				continue;
			}
			List<GameObject> variants;
			if (!topologyPrefabs.TryGetValue(slice.wallMask, out variants)){
				variants = new List<GameObject>();
				topologyPrefabs.Add(slice.wallMask, variants);
			}
			variants.Add(slice.generatedPrefab);
		}

		SerializedProperty wallMaskMappingsProperty = serializedTarget.FindProperty("wallMaskPrefabs");
		List<LevelGeneratorWallMaskPrefabMapping> wallMaskMappings;
		TryMergeWallMaskMappings(wallMaskMappingsProperty, topologyPrefabs, out wallMaskMappings, errors);

		if (errors.Count > 0){
			SetMessage("Apply blocked. Every base role and topology mapping must resolve to persistent prefab assets:\n- " + string.Join("\n- ", errors.ToArray()), MessageType.Error);
			return;
		}

		Dictionary<LevelGeneratorTileRole, SerializedProperty> arrayProperties = new Dictionary<LevelGeneratorTileRole, SerializedProperty>();
		for (int roleIndex = 0; roleIndex < Roles.Length; roleIndex++){
			LevelGeneratorTileRole role = Roles[roleIndex];
			SerializedProperty property = serializedTarget.FindProperty(GetGeneratorArrayName(role));
			if (property == null || !property.isArray){
				errors.Add("LevelGenerator field " + GetGeneratorArrayName(role) + " is unavailable or is not an array.");
			}
			else{
				arrayProperties.Add(role, property);
			}
		}
		if (errors.Count > 0){
			SetMessage("Apply blocked because the target does not expose every expected prefab array:\n- " + string.Join("\n- ", errors.ToArray()), MessageType.Error);
			return;
		}

		Undo.RecordObject(applyTarget, "Apply Tile Set to Level Generator");
		for (int roleIndex = 0; roleIndex < Roles.Length; roleIndex++){
			LevelGeneratorTileRole role = Roles[roleIndex];
			List<GameObject> prefabs = rolePrefabs[role];
			SerializedProperty arrayProperty = arrayProperties[role];
			arrayProperty.arraySize = prefabs.Count;
			for (int prefabIndex = 0; prefabIndex < prefabs.Count; prefabIndex++){
				arrayProperty.GetArrayElementAtIndex(prefabIndex).objectReferenceValue = prefabs[prefabIndex];
			}
		}
		WriteWallMaskMappings(wallMaskMappingsProperty, wallMaskMappings);
		serializedTarget.ApplyModifiedProperties();
		EditorUtility.SetDirty(applyTarget);
		if (PrefabUtility.IsPartOfPrefabInstance(applyTarget)){
			PrefabUtility.RecordPrefabInstancePropertyModifications(applyTarget);
		}
		if (applyTarget.LivePreviewEnabled && LevelGeneratorPreviewLifecycle.IsSceneInstance(applyTarget)){
			applyTarget.RefreshLivePreview();
			SceneView.RepaintAll();
		}
		SetMessage("Applied all seven base prefab arrays and " + wallMaskMappings.Count.ToString() + " wall topology mapping(s) to " + applyTarget.name + ".", MessageType.Info);
	}

	static bool TryMergeWallMaskMappings(SerializedProperty targetMappingsProperty,
		Dictionary<int, List<GameObject>> assignedMappings,
		out List<LevelGeneratorWallMaskPrefabMapping> mergedMappings,
		List<string> errors){
		mergedMappings = new List<LevelGeneratorWallMaskPrefabMapping>();
		if (targetMappingsProperty == null || !targetMappingsProperty.isArray){
			errors.Add("LevelGenerator field wallMaskPrefabs is unavailable or is not an array.");
			return false;
		}

		HashSet<int> assignedMasks = assignedMappings == null
			? new HashSet<int>()
			: new HashSet<int>(assignedMappings.Keys);
		HashSet<int> configuredMasks = new HashSet<int>();
		for (int i = 0; i < targetMappingsProperty.arraySize; i++){
			SerializedProperty mappingProperty = targetMappingsProperty.GetArrayElementAtIndex(i);
			SerializedProperty maskProperty = mappingProperty == null ? null : mappingProperty.FindPropertyRelative("floorNeighborMask");
			SerializedProperty prefabsProperty = mappingProperty == null ? null : mappingProperty.FindPropertyRelative("prefabs");
			if (maskProperty == null || prefabsProperty == null || !prefabsProperty.isArray){
				errors.Add("LevelGenerator wallMaskPrefabs entry " + (i + 1).ToString() + " has an invalid serialized structure.");
				continue;
			}

			int mask = maskProperty.intValue;
			if (assignedMasks.Contains(mask)){
				// An explicit profile assignment replaces every old entry for this mask,
				// even if the target mapping is duplicated or otherwise invalid.
				continue;
			}
			if (mask < 1 || mask > 255){
				errors.Add("LevelGenerator wallMaskPrefabs entry " + (i + 1).ToString() + " uses a mask outside 1..255.");
			}
			if (!configuredMasks.Add(mask)){
				errors.Add("LevelGenerator wallMaskPrefabs contains duplicate mask " + mask.ToString() + ".");
			}
			if (prefabsProperty.arraySize == 0){
				errors.Add("LevelGenerator wallMaskPrefabs mask " + mask.ToString() + " has no prefab variants.");
			}

			GameObject[] prefabs = new GameObject[prefabsProperty.arraySize];
			for (int prefabIndex = 0; prefabIndex < prefabs.Length; prefabIndex++){
				GameObject prefab = prefabsProperty.GetArrayElementAtIndex(prefabIndex).objectReferenceValue as GameObject;
				if (prefab == null){
					errors.Add("LevelGenerator wallMaskPrefabs mask " + mask.ToString() + " has an unassigned prefab at index " + (prefabIndex + 1).ToString() + ".");
				}
				else if (!IsUsablePrefabAsset(prefab)){
					errors.Add("LevelGenerator wallMaskPrefabs mask " + mask.ToString() + " references a nonpersistent prefab.");
				}
				prefabs[prefabIndex] = prefab;
			}
			mergedMappings.Add(new LevelGeneratorWallMaskPrefabMapping {
				floorNeighborMask = mask,
				prefabs = prefabs
			});
		}

		List<int> sortedAssignedMasks = assignedMappings == null
			? new List<int>()
			: new List<int>(assignedMappings.Keys);
		sortedAssignedMasks.Sort();
		for (int i = 0; i < sortedAssignedMasks.Count; i++){
			int mask = sortedAssignedMasks[i];
			if (mask < 1 || mask > 255){
				errors.Add("Tile Set topology assignment uses a mask outside 1..255.");
				continue;
			}
			List<GameObject> variants = assignedMappings[mask];
			if (variants == null || variants.Count == 0){
				errors.Add("Tile Set topology mask " + mask.ToString() + " has no prefab variants.");
				continue;
			}
			bool variantsValid = true;
			for (int variantIndex = 0; variantIndex < variants.Count; variantIndex++){
				if (!IsUsablePrefabAsset(variants[variantIndex])){
					errors.Add("Tile Set topology mask " + mask.ToString() + " contains a nonpersistent prefab variant.");
					variantsValid = false;
				}
			}
			if (!variantsValid){
				continue;
			}

			mergedMappings.Add(new LevelGeneratorWallMaskPrefabMapping {
				floorNeighborMask = mask,
				prefabs = variants.ToArray()
			});
		}
		return errors.Count == 0;
	}

	static void WriteWallMaskMappings(SerializedProperty targetMappingsProperty, List<LevelGeneratorWallMaskPrefabMapping> mappings){
		if (targetMappingsProperty == null || !targetMappingsProperty.isArray){
			return;
		}
		targetMappingsProperty.arraySize = mappings == null ? 0 : mappings.Count;
		for (int i = 0; mappings != null && i < mappings.Count; i++){
			SerializedProperty mappingProperty = targetMappingsProperty.GetArrayElementAtIndex(i);
			mappingProperty.FindPropertyRelative("floorNeighborMask").intValue = mappings[i].floorNeighborMask;
			SerializedProperty prefabsProperty = mappingProperty.FindPropertyRelative("prefabs");
			prefabsProperty.arraySize = mappings[i].prefabs == null ? 0 : mappings[i].prefabs.Length;
			for (int prefabIndex = 0; mappings[i].prefabs != null && prefabIndex < mappings[i].prefabs.Length; prefabIndex++){
				prefabsProperty.GetArrayElementAtIndex(prefabIndex).objectReferenceValue = mappings[i].prefabs[prefabIndex];
			}
		}
	}

	static bool TryGetSourceDimensions(Texture2D atlas, float expectedPixelsPerUnit, out int sourceWidth, out int sourceHeight, out string error){
		sourceWidth = 0;
		sourceHeight = 0;
		error = null;
		if (atlas == null){
			error = "Select a source Texture2D.";
			return false;
		}

		string atlasPath = AssetDatabase.GetAssetPath(atlas);
		if (string.IsNullOrEmpty(atlasPath)){
			error = "The selected texture is not an imported project asset.";
			return false;
		}
		TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
		if (importer == null){
			error = "Unity could not find a TextureImporter for " + atlasPath + ".";
			return false;
		}
		if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple){
			error = "The atlas importer is not configured as Sprite/Multiple, which is required only for the optional grid-slicing workflow.";
			return false;
		}
		if (!Mathf.Approximately(importer.spritePixelsPerUnit, expectedPixelsPerUnit)){
			error = "Atlas Sprite Pixels Per Unit (" + importer.spritePixelsPerUnit.ToString("R", CultureInfo.InvariantCulture) + ") does not match the grid-slicing profile value (" + expectedPixelsPerUnit.ToString("R", CultureInfo.InvariantCulture) + "). This does not affect imported Sprite workflows.";
			return false;
		}

		try{
			SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
			factories.Init();
			ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
			if (dataProvider == null){
				error = "Unity could not create a Sprite Editor data provider for " + atlasPath + ".";
				return false;
			}
			dataProvider.InitSpriteEditorDataProvider();
			return TryGetSourceDimensions(dataProvider, out sourceWidth, out sourceHeight, out error);
		}
		catch (Exception exception){
			error = "Sprite Editor data provider initialization failed for " + atlasPath + ": " + exception.Message;
			return false;
		}
	}

	static bool TryGetSourceDimensions(ISpriteEditorDataProvider dataProvider, out int sourceWidth, out int sourceHeight, out string error){
		sourceWidth = 0;
		sourceHeight = 0;
		error = null;
		if (dataProvider == null){
			error = "The Sprite Editor data provider is unavailable.";
			return false;
		}

		try{
			ITextureDataProvider textureDataProvider = dataProvider.GetDataProvider<ITextureDataProvider>();
			if (textureDataProvider == null){
				error = "The Sprite Editor data provider does not expose ITextureDataProvider.";
				return false;
			}
			textureDataProvider.GetTextureActualWidthAndHeight(out sourceWidth, out sourceHeight);
		}
		catch (Exception exception){
			error = "ITextureDataProvider.GetTextureActualWidthAndHeight failed: " + exception.Message;
			return false;
		}
		if (sourceWidth <= 0 || sourceHeight <= 0){
			error = "ITextureDataProvider returned invalid source dimensions (" + sourceWidth.ToString() + " x " + sourceHeight.ToString() + ").";
			return false;
		}
		return true;
	}

	static bool IsValidPixelsPerUnit(float pixelsPerUnit){
		return !float.IsNaN(pixelsPerUnit) && !float.IsInfinity(pixelsPerUnit) && pixelsPerUnit > 0f;
	}

	static bool TryGetGridDimensions(int sourceWidth, int sourceHeight, Vector2Int cellSize, Vector2Int offset, Vector2Int spacing, out int columns, out int rows, out string error){
		columns = 0;
		rows = 0;
		error = null;
		if (cellSize.x <= 0 || cellSize.y <= 0){
			error = "Cell Size must be greater than zero in both dimensions.";
			return false;
		}
		if (offset.x < 0 || offset.y < 0 || spacing.x < 0 || spacing.y < 0){
			error = "Offset and Spacing values cannot be negative.";
			return false;
		}
		if (sourceWidth <= 0 || sourceHeight <= 0){
			error = "The original source image has invalid dimensions.";
			return false;
		}

		long remainingWidth = (long)sourceWidth - offset.x;
		long remainingHeight = (long)sourceHeight - offset.y;
		if (remainingWidth < cellSize.x || remainingHeight < cellSize.y){
			error = "Cell Size and Offset do not leave room for a complete cell inside the original source image.";
			return false;
		}
		long stepX = (long)cellSize.x + spacing.x;
		long stepY = (long)cellSize.y + spacing.y;
		columns = (int)(1L + (remainingWidth - cellSize.x) / stepX);
		rows = (int)(1L + (remainingHeight - cellSize.y) / stepY);
		if ((long)columns * rows > 10000L){
			columns = 0;
			rows = 0;
			error = "This grid produces more than 10,000 cells. Increase cell size or spacing before slicing.";
			return false;
		}
		return true;
	}

	static string BuildSliceSignature(LevelGeneratorTileSet tileSet, int sourceWidth, int sourceHeight){
		if (tileSet == null || tileSet.sourceAtlas == null){
			return string.Empty;
		}
		string atlasPath = AssetDatabase.GetAssetPath(tileSet.sourceAtlas);
		string atlasGuid = string.IsNullOrEmpty(atlasPath) ? string.Empty : AssetDatabase.AssetPathToGUID(atlasPath);
		return atlasGuid + "|" + sourceWidth.ToString(CultureInfo.InvariantCulture) + "|" +
			sourceHeight.ToString(CultureInfo.InvariantCulture) + "|" +
			tileSet.cellSize.x.ToString(CultureInfo.InvariantCulture) + "|" +
			tileSet.cellSize.y.ToString(CultureInfo.InvariantCulture) + "|" +
			tileSet.offset.x.ToString(CultureInfo.InvariantCulture) + "|" +
			tileSet.offset.y.ToString(CultureInfo.InvariantCulture) + "|" +
			tileSet.spacing.x.ToString(CultureInfo.InvariantCulture) + "|" +
			tileSet.spacing.y.ToString(CultureInfo.InvariantCulture) + "|" +
			tileSet.pixelsPerUnit.ToString("R", CultureInfo.InvariantCulture);
	}

	static bool IsSliceConfigurationCurrent(LevelGeneratorTileSet tileSet, out string message){
		message = null;
		if (tileSet == null || tileSet.sourceAtlas == null){
			message = "Select a source atlas before using optional grid slicing.";
			return false;
		}

		if (!IsValidPixelsPerUnit(tileSet.pixelsPerUnit)){
			message = "Profile Pixels Per Unit must be finite and greater than zero. Correct the profile value, then re-slice before generating variants or applying it.";
			return false;
		}

		int sourceWidth;
		int sourceHeight;
		string dimensionsError;
		if (!TryGetSourceDimensions(tileSet.sourceAtlas, tileSet.pixelsPerUnit, out sourceWidth, out sourceHeight, out dimensionsError)){
			message = "Atlas freshness check failed: " + dimensionsError;
			return false;
		}
		string currentSignature = BuildSliceSignature(tileSet, sourceWidth, sourceHeight);
		if (string.IsNullOrEmpty(tileSet.lastSliceSignature) || tileSet.lastSliceSignature != currentSignature){
			message = "The atlas, source dimensions, or grid settings differ from the last grid slice. This status is advisory; existing Sprite imports remain usable.";
			return false;
		}
		return true;
	}

	static string GetRectIdentity(string spriteName, Rect rect){
		return (spriteName ?? string.Empty) + "|" + rect.x.ToString("R", CultureInfo.InvariantCulture) + "|" +
			rect.y.ToString("R", CultureInfo.InvariantCulture) + "|" +
			rect.width.ToString("R", CultureInfo.InvariantCulture) + "|" +
			rect.height.ToString("R", CultureInfo.InvariantCulture);
	}

	static List<LevelGeneratorTileSetSlice> GetAssignedSlices(LevelGeneratorTileSet tileSet, LevelGeneratorTileRole role){
		List<LevelGeneratorTileSetSlice> assignedSlices = new List<LevelGeneratorTileSetSlice>();
		if (tileSet == null || tileSet.slices == null){
			return assignedSlices;
		}
		for (int i = 0; i < tileSet.slices.Count; i++){
			LevelGeneratorTileSetSlice slice = tileSet.slices[i];
			if (slice != null && slice.hasRoleAssignment && slice.role == role){
				assignedSlices.Add(slice);
			}
		}
		return assignedSlices;
	}

	static GameObject GetTemplateForRole(LevelGenerator generator, LevelGeneratorTileRole role){
		GameObject[] prefabs = GetPrefabArrayForRole(generator, role);
		if (prefabs == null){
			return null;
		}
		for (int i = 0; i < prefabs.Length; i++){
			if (IsUsablePrefabAsset(prefabs[i])){
				return prefabs[i];
			}
		}
		return null;
	}

	static bool IsUsablePrefabAsset(GameObject prefab){
		return prefab != null && PrefabUtility.IsPartOfPrefabAsset(prefab) && EditorUtility.IsPersistent(prefab);
	}

	static GameObject[] GetPrefabArrayForRole(LevelGenerator generator, LevelGeneratorTileRole role){
		if (generator == null){
			return null;
		}
		switch (role){
			case LevelGeneratorTileRole.Empty: return generator.emptyObj;
			case LevelGeneratorTileRole.Floor: return generator.floorObj;
			case LevelGeneratorTileRole.GenericWall: return generator.wallObj;
			case LevelGeneratorTileRole.WallUp: return generator.wallUpObj;
			case LevelGeneratorTileRole.WallDown: return generator.wallDownObj;
			case LevelGeneratorTileRole.WallLeft: return generator.wallLeftObj;
			case LevelGeneratorTileRole.WallRight: return generator.wallRightObj;
			case LevelGeneratorTileRole.WallTopology: return generator.wallObj;
			default: return null;
		}
	}

	static SpriteRenderer FindSpriteRenderer(GameObject root){
		if (root == null){
			return null;
		}
		SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
		if (renderers == null || renderers.Length == 0){
			return null;
		}
		for (int i = 0; i < renderers.Length; i++){
			if (renderers[i] != null && string.Equals(renderers[i].gameObject.name, "Square", StringComparison.OrdinalIgnoreCase)){
				return renderers[i];
			}
		}
		return renderers[0];
	}

	static Vector2 GetTemplateRendererFootprint(SpriteRenderer renderer){
		if (renderer == null){
			return Vector2.one;
		}
		if (renderer.drawMode != SpriteDrawMode.Simple && renderer.size.x > 0f && renderer.size.y > 0f){
			return renderer.size;
		}
		if (renderer.sprite != null){
			Vector2 spriteSize = renderer.sprite.bounds.size;
			if (spriteSize.x > 0f && spriteSize.y > 0f){
				return spriteSize;
			}
		}
		return Vector2.one;
	}

	static string GetGeneratorArrayName(LevelGeneratorTileRole role){
		switch (role){
			case LevelGeneratorTileRole.Empty: return "emptyObj";
			case LevelGeneratorTileRole.Floor: return "floorObj";
			case LevelGeneratorTileRole.GenericWall: return "wallObj";
			case LevelGeneratorTileRole.WallUp: return "wallUpObj";
			case LevelGeneratorTileRole.WallDown: return "wallDownObj";
			case LevelGeneratorTileRole.WallLeft: return "wallLeftObj";
			case LevelGeneratorTileRole.WallRight: return "wallRightObj";
			case LevelGeneratorTileRole.WallTopology: return "wallObj";
			default: return string.Empty;
		}
	}

	static string GetRoleLabel(LevelGeneratorTileRole role){
		int index = (int)role + 1;
		return index >= 0 && index < RoleLabels.Length ? RoleLabels[index] : role.ToString();
	}

	static string GetOrCreateOutputFolder(LevelGeneratorTileSet tileSet){
		string profilePath = AssetDatabase.GetAssetPath(tileSet);
		if (string.IsNullOrEmpty(profilePath) || !profilePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)){
			throw new InvalidOperationException("The Tile Set Profile must be saved under Assets/.");
		}
		string profileGuid = AssetDatabase.AssetPathToGUID(profilePath).Replace("-", string.Empty);
		if (string.IsNullOrEmpty(profileGuid)){
			throw new InvalidOperationException("Unity could not determine the Tile Set Profile asset GUID.");
		}
		int slash = profilePath.LastIndexOf('/');
		string parentFolder = slash < 0 ? "Assets" : profilePath.Substring(0, slash);
		string profileFolderName = SanitizeFileName(tileSet.name);
		string folderName = profileFolderName + "_TileSet_" + profileGuid;
		string outputFolder = parentFolder + "/" + folderName;
		if (!AssetDatabase.IsValidFolder(outputFolder)){
			if (!AssetDatabase.IsValidFolder(parentFolder)){
				throw new InvalidOperationException("The profile's parent folder is not a valid Unity asset folder.");
			}
			AssetDatabase.CreateFolder(parentFolder, folderName);
		}
		if (!AssetDatabase.IsValidFolder(outputFolder)){
			throw new InvalidOperationException("Unity could not create the profile's dedicated output folder.");
		}
		return outputFolder;
	}

	static string GetPrefabOutputPath(LevelGeneratorTileSetSlice slice, string outputFolder){
		if (slice.generatedPrefab != null){
			string trackedPath = AssetDatabase.GetAssetPath(slice.generatedPrefab);
			if (!string.IsNullOrEmpty(trackedPath) && trackedPath.StartsWith(outputFolder + "/", StringComparison.OrdinalIgnoreCase) &&
				AssetDatabase.LoadAssetAtPath<GameObject>(trackedPath) == slice.generatedPrefab){
				return trackedPath;
			}
		}

		string fileName = SanitizeFileName(slice.spriteName);
		string candidate = outputFolder + "/" + fileName + ".prefab";
		int suffix = 2;
		while (File.Exists(candidate) || AssetDatabase.LoadMainAssetAtPath(candidate) != null){
			candidate = outputFolder + "/" + fileName + "_" + suffix.ToString(CultureInfo.InvariantCulture) + ".prefab";
			suffix++;
		}
		return candidate;
	}

	static string SanitizeFileName(string value){
		if (string.IsNullOrEmpty(value)){
			return "TileSet";
		}
		char[] characters = value.ToCharArray();
		for (int i = 0; i < characters.Length; i++){
			if (!char.IsLetterOrDigit(characters[i]) && characters[i] != '_' && characters[i] != '-'){
				characters[i] = '_';
			}
		}
		string sanitized = new string(characters).Trim('_');
		return string.IsNullOrEmpty(sanitized) ? "TileSet" : sanitized;
	}

	static void SaveProfile(LevelGeneratorTileSet tileSet){
		if (tileSet == null){
			return;
		}
		EditorUtility.SetDirty(tileSet);
		AssetDatabase.SaveAssetIfDirty(tileSet);
		tileSet.ScheduleLinkedGeneratorRefresh();
	}

	void SetMessage(string message, MessageType messageType){
		operationMessage = message;
		operationMessageType = messageType;
	}
}
