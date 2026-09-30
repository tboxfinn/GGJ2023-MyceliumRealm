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

	static readonly string[] RoleLabels = {
		"Unassigned",
		"Empty",
		"Floor",
		"Generic Wall",
		"Wall Up",
		"Wall Down",
		"Wall Left",
		"Wall Right"
	};

	LevelGeneratorTileSet profile;
	Texture2D scratchAtlas;
	Vector2Int scratchCellSize = new Vector2Int(128, 128);
	Vector2Int scratchOffset = Vector2Int.zero;
	Vector2Int scratchSpacing = Vector2Int.zero;
	float scratchPixelsPerUnit = 128f;
	LevelGenerator templateGenerator;
	LevelGenerator applyTarget;
	Vector2 slicesScrollPosition;
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
		DrawAtlasAndGridSettings();

		if (profile == null){
			EditorGUILayout.HelpBox("Create or select a Tile Set Profile to slice an atlas and persist sprite-role assignments.", MessageType.Info);
			return;
		}

		EditorGUILayout.Space();
		DrawTemplateAndTargetFields();
		DrawActions();
		if (!string.IsNullOrEmpty(operationMessage)){
			EditorGUILayout.HelpBox(operationMessage, operationMessageType);
		}
		DrawSliceGrid();
	}

	void DrawProfileSelector(){
		using (new EditorGUILayout.HorizontalScope()){
			EditorGUI.BeginChangeCheck();
			LevelGeneratorTileSet selectedProfile = EditorGUILayout.ObjectField(
				"Tile Set Profile", profile, typeof(LevelGeneratorTileSet), false) as LevelGeneratorTileSet;
			if (EditorGUI.EndChangeCheck()){
				profile = selectedProfile;
				if (profile != null){
					scratchAtlas = profile.sourceAtlas;
					scratchCellSize = profile.cellSize;
					scratchOffset = profile.offset;
					scratchSpacing = profile.spacing;
					scratchPixelsPerUnit = profile.pixelsPerUnit;
					operationMessage = null;
				}
			}

			if (GUILayout.Button("Create Profile", GUILayout.Width(105f))){
				CreateProfile();
			}
		}
	}

	void DrawAtlasAndGridSettings(){
		Texture2D atlas = profile == null ? scratchAtlas : profile.sourceAtlas;
		Vector2Int cellSize = profile == null ? scratchCellSize : profile.cellSize;
		Vector2Int offset = profile == null ? scratchOffset : profile.offset;
		Vector2Int spacing = profile == null ? scratchSpacing : profile.spacing;
		float pixelsPerUnit = profile == null ? scratchPixelsPerUnit : profile.pixelsPerUnit;

		EditorGUI.BeginChangeCheck();
		atlas = EditorGUILayout.ObjectField("Source Atlas", atlas, typeof(Texture2D), false) as Texture2D;
		cellSize = EditorGUILayout.Vector2IntField("Cell Size", cellSize);
		offset = EditorGUILayout.Vector2IntField("Offset (top-left)", offset);
		spacing = EditorGUILayout.Vector2IntField("Spacing", spacing);
		pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", pixelsPerUnit);
		if (EditorGUI.EndChangeCheck()){
			if (profile == null){
				scratchAtlas = atlas;
				scratchCellSize = cellSize;
				scratchOffset = offset;
				scratchSpacing = spacing;
				scratchPixelsPerUnit = pixelsPerUnit;
			}
			else{
				Undo.RecordObject(profile, "Change Tile Set Atlas Settings");
				profile.sourceAtlas = atlas;
				profile.cellSize = cellSize;
				profile.offset = offset;
				profile.spacing = spacing;
				profile.pixelsPerUnit = pixelsPerUnit;
				SaveProfile(profile);
			}
		}

		EditorGUILayout.HelpBox("Offset and spacing are measured from the atlas top-left. Pixels outside complete grid cells are ignored.", MessageType.None);
	}

	void DrawTemplateAndTargetFields(){
		EditorGUILayout.LabelField("Prefab Templates and Apply Target", EditorStyles.boldLabel);
		templateGenerator = EditorGUILayout.ObjectField(
			"Template Source", templateGenerator, typeof(LevelGenerator), true) as LevelGenerator;
		applyTarget = EditorGUILayout.ObjectField(
			"Apply To (optional)", applyTarget, typeof(LevelGenerator), true) as LevelGenerator;
		EditorGUILayout.HelpBox(
			"Generation uses the first valid prefab asset in each role array on Template Source. It updates only the SpriteRenderer on a child named 'Square' when present, otherwise the first child SpriteRenderer; other renderers, colliders, and prefab setup are preserved.",
			MessageType.Info);
		EditorGUILayout.HelpBox(
			"On Apply, roles with assigned slices use their generated variants. Roles without assigned slices keep their existing valid Apply To prefab assets; if none exist, the first valid Template Source prefab asset is used. Invalid target references block Apply.",
			MessageType.None);
	}

	void DrawActions(){
		using (new EditorGUILayout.HorizontalScope()){
			if (GUILayout.Button("Slice / Re-slice Atlas", GUILayout.Height(30f))){
				SliceAtlas();
			}
			if (GUILayout.Button("Generate / Update Prefab Variants", GUILayout.Height(30f))){
				GeneratePrefabVariants();
			}
			if (GUILayout.Button("Apply To LevelGenerator", GUILayout.Height(30f))){
				ApplyToTarget();
			}
		}

		string freshnessMessage;
		if (!IsSliceConfigurationCurrent(profile, out freshnessMessage)){
			EditorGUILayout.HelpBox(freshnessMessage, MessageType.Warning);
		}
	}

	void DrawSliceGrid(){
		if (profile.slices == null || profile.slices.Count == 0){
			EditorGUILayout.HelpBox("No slices are stored in this profile yet. Slice the atlas to begin.", MessageType.Info);
			return;
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Sliced Cells (" + profile.slices.Count.ToString() + ")", EditorStyles.boldLabel);
		float availableWidth = Mathf.Max(150f, position.width - 28f);
		int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / 150f));
		slicesScrollPosition = EditorGUILayout.BeginScrollView(slicesScrollPosition, GUILayout.MinHeight(150f));
		for (int first = 0; first < profile.slices.Count; first += columns){
			EditorGUILayout.BeginHorizontal();
			int end = Mathf.Min(first + columns, profile.slices.Count);
			for (int i = first; i < end; i++){
				DrawSliceCard(profile.slices[i]);
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndScrollView();
	}

	void DrawSliceCard(LevelGeneratorTileSetSlice slice){
		using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(142f))){
			Texture preview = null;
			if (slice.sprite != null){
				preview = AssetPreview.GetAssetPreview(slice.sprite);
				if (preview == null){
					preview = AssetPreview.GetMiniThumbnail(slice.sprite);
				}
			}
			GUILayout.Box(preview == null ? GUIContent.none : new GUIContent(preview), GUILayout.Width(112f), GUILayout.Height(92f));
			EditorGUILayout.LabelField(slice.spriteName, EditorStyles.miniBoldLabel, GUILayout.Width(130f));
			EditorGUILayout.LabelField(
				"(" + slice.rect.x.ToString("0") + ", " + slice.rect.y.ToString("0") + ") " +
				slice.rect.width.ToString("0") + " x " + slice.rect.height.ToString("0"),
				EditorStyles.miniLabel, GUILayout.Width(130f));

			int currentChoice = slice.hasRoleAssignment ? (int)slice.role + 1 : 0;
			EditorGUI.BeginChangeCheck();
			int selectedChoice = EditorGUILayout.Popup("Role", currentChoice, RoleLabels, GUILayout.Width(136f));
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
				}
				if (!slice.hasRoleAssignment){
					slice.generatedPrefab = null;
				}
				SaveProfile(profile);
			}

			string prefabStatus = slice.generatedPrefab == null ? "Prefab not generated" : "Prefab ready";
			EditorGUILayout.LabelField(prefabStatus, EditorStyles.miniLabel, GUILayout.Width(130f));
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
		AssetDatabase.CreateAsset(createdProfile, path);
		profile = createdProfile;
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
		string freshnessMessage;
		if (!IsSliceConfigurationCurrent(profile, out freshnessMessage)){
			SetMessage(freshnessMessage, MessageType.Warning);
			return;
		}
		if (profile.slices == null || profile.slices.Count == 0){
			SetMessage("This profile has no sliced cells to generate.", MessageType.Warning);
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
		for (int roleIndex = 0; roleIndex < Roles.Length; roleIndex++){
			LevelGeneratorTileRole role = Roles[roleIndex];
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
					messages.Add(slice.spriteName + " blocked: its imported Sprite reference is missing; re-slice the atlas.");
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
					renderer.sprite = slice.sprite;
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
		string freshnessMessage;
		if (!IsSliceConfigurationCurrent(profile, out freshnessMessage)){
			SetMessage(freshnessMessage, MessageType.Warning);
			return;
		}

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
							continue;
						}
						if (!IsUsablePrefabAsset(existingPrefab)){
							errors.Add(GetRoleLabel(role) + " target array " + GetGeneratorArrayName(role) + " entry " + (prefabIndex + 1).ToString() + " is not a persistent prefab asset.");
							continue;
						}
						prefabs.Add(existingPrefab);
					}
				}
				if (prefabs.Count == 0){
					GameObject template = GetTemplateForRole(templateGenerator, role);
					if (template == null){
						errors.Add(GetRoleLabel(role) + " has no assigned slices, no valid target prefab asset, and Template Source has no valid prefab asset in " + GetGeneratorArrayName(role) + ".");
					}
					else{
						prefabs.Add(template);
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
				else if (!PrefabUtility.IsPartOfPrefabAsset(slice.generatedPrefab)){
					errors.Add(slice.spriteName + " does not reference a prefab asset.");
				}
				else{
					prefabs.Add(slice.generatedPrefab);
				}
			}
			rolePrefabs.Add(role, prefabs);
		}

		if (errors.Count > 0){
			SetMessage("Apply blocked. Every role must resolve to at least one prefab, and every assigned slice must have a valid generated prefab:\n- " + string.Join("\n- ", errors.ToArray()), MessageType.Error);
			return;
		}

		SerializedObject serializedTarget = new SerializedObject(applyTarget);
		serializedTarget.Update();
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
		serializedTarget.ApplyModifiedProperties();
		EditorUtility.SetDirty(applyTarget);
		if (PrefabUtility.IsPartOfPrefabInstance(applyTarget)){
			PrefabUtility.RecordPrefabInstancePropertyModifications(applyTarget);
		}
		SetMessage("Applied all seven validated prefab arrays to " + applyTarget.name + ".", MessageType.Info);
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
			error = "The atlas importer is not configured as Sprite/Multiple. Re-slice the atlas before generating variants or applying this profile.";
			return false;
		}
		if (!Mathf.Approximately(importer.spritePixelsPerUnit, expectedPixelsPerUnit)){
			error = "Atlas Sprite Pixels Per Unit (" + importer.spritePixelsPerUnit.ToString("R", CultureInfo.InvariantCulture) + ") does not match the profile (" + expectedPixelsPerUnit.ToString("R", CultureInfo.InvariantCulture) + "). Re-slice the atlas before generating variants or applying this profile.";
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
			message = "Select a source atlas before using sliced mappings.";
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
			message = "The atlas, source dimensions, or grid settings differ from the last slice. Re-slice before generating variants or applying this profile.";
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

	static string GetGeneratorArrayName(LevelGeneratorTileRole role){
		switch (role){
			case LevelGeneratorTileRole.Empty: return "emptyObj";
			case LevelGeneratorTileRole.Floor: return "floorObj";
			case LevelGeneratorTileRole.GenericWall: return "wallObj";
			case LevelGeneratorTileRole.WallUp: return "wallUpObj";
			case LevelGeneratorTileRole.WallDown: return "wallDownObj";
			case LevelGeneratorTileRole.WallLeft: return "wallLeftObj";
			case LevelGeneratorTileRole.WallRight: return "wallRightObj";
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
	}

	void SetMessage(string message, MessageType messageType){
		operationMessage = message;
		operationMessageType = messageType;
	}
}
