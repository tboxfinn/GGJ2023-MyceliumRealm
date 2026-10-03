using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum LevelGeneratorTileRole {
	Empty,
	Floor,
	GenericWall,
	WallUp,
	WallDown,
	WallLeft,
	WallRight,
	WallTopology
}

[Serializable]
public sealed class LevelGeneratorTileSetSlice {
	public string spriteName;
	public Rect rect;
	public bool hasRoleAssignment;
	public LevelGeneratorTileRole role;
	[Range(1, 255)] public int wallMask = 1;
	public Sprite sprite;
	public GameObject generatedPrefab;
}

[CreateAssetMenu(menuName = "2D Map Generator/Tile Set Profile", fileName = "LevelGeneratorTileSet")]
public sealed class LevelGeneratorTileSet : ScriptableObject {
	public Texture2D sourceAtlas;
	public Vector2Int cellSize = new Vector2Int(128, 128);
	public Vector2Int offset = Vector2Int.zero;
	public Vector2Int spacing = Vector2Int.zero;
	[Min(0.01f)] public float pixelsPerUnit = 128f;
	[Tooltip("Exact positive X/Y SpriteRenderer dimensions in world units for runtime profile overrides. This is independent of importer PPU and does not scale prefab transforms or colliders. Missing or invalid legacy values retain the prefab renderer footprint.")]
	public Vector2 spriteRenderSizeWorldUnits = Vector2.one;
	public List<LevelGeneratorTileSetSlice> slices = new List<LevelGeneratorTileSetSlice>();

	[HideInInspector] public string lastSliceSignature;
	[HideInInspector] public string lastSlicedAtlasGuid;

#if UNITY_EDITOR
	void OnValidate(){
		ScheduleLinkedGeneratorRefresh();
	}

	public void ScheduleLinkedGeneratorRefresh(){
		EditorApplication.delayCall -= RefreshLinkedGenerators;
		EditorApplication.delayCall += RefreshLinkedGenerators;
	}

	void RefreshLinkedGenerators(){
		if (this == null || EditorApplication.isPlayingOrWillChangePlaymode){
			return;
		}

		LevelGenerator[] generators = Resources.FindObjectsOfTypeAll<LevelGenerator>();
		bool refreshedAny = false;
		for (int i = 0; i < generators.Length; i++){
			LevelGenerator generator = generators[i];
			if (!IsSceneGenerator(generator) || generator.TileSetProfile != this){
				continue;
			}
			if (generator.isActiveAndEnabled && generator.LivePreviewEnabled){
				generator.RefreshLivePreview();
			}
			else{
				generator.ClearLivePreview();
			}
			refreshedAny = true;
		}
		if (refreshedAny){
			SceneView.RepaintAll();
		}
	}

	static bool IsSceneGenerator(LevelGenerator generator){
		return generator != null && !EditorUtility.IsPersistent(generator) &&
			generator.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(generator.gameObject.scene) &&
			!PrefabUtility.IsPartOfPrefabAsset(generator.gameObject) && !generator.IsLivePreviewRootMarker &&
			!generator.IsInLivePreviewHierarchy();
	}
#endif
}
