using System;
using System.Collections.Generic;
using UnityEngine;

public enum LevelGeneratorTileRole {
	Empty,
	Floor,
	GenericWall,
	WallUp,
	WallDown,
	WallLeft,
	WallRight
}

[Serializable]
public sealed class LevelGeneratorTileSetSlice {
	public string spriteName;
	public Rect rect;
	public bool hasRoleAssignment;
	public LevelGeneratorTileRole role;
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
	public List<LevelGeneratorTileSetSlice> slices = new List<LevelGeneratorTileSetSlice>();

	[HideInInspector] public string lastSliceSignature;
	[HideInInspector] public string lastSlicedAtlasGuid;
}
