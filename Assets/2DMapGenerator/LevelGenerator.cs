using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LevelGenerator : MonoBehaviour {
	const int MaxGridDimension = 1024;
	const int MaxGridCells = 1000000;
	const int MaxWalkerCount = 100;
	const int MaxRoomCount = 1000;
	const int MaxRoomPlacementAttempts = 10000;
	const int MaxCellularAutomataIterations = 100;
	const long MaxCellularAutomataWork = 10000000L;
	const int MaxGenerationIterations = 100000;
	const string GeneratedRootName = "Generated Level";
#if UNITY_EDITOR
	const string PreviewRootName = "__LevelGenerator Live Preview";
	public const int LivePreviewCellLimit = 10000;
#endif

	enum gridSpace {empty, floor, wall, wallUp, wallDown, wallRight, wallLeft};
	enum FillMode {RandomWalk, RoomsAndCorridors, CellularAutomata};
	gridSpace[,] grid;
	int roomHeight, roomWidth;
	public Vector2 roomSizeWorldUnits = new Vector2(50,50); // cambia el tamaño del mapa
	[SerializeField, Min(0.0001f)] float worldUnitsInOneGridCell = 1f;
	Transform generatedRoot;
	struct walker{
		public Vector2 dir;
		public Vector2 pos;
	}
	[SerializeField] List<walker> walkers;
	[SerializeField] FillMode fillMode = FillMode.RandomWalk;
    // cosas que puedo cambiar para ver como cambia el mapa
	[SerializeField, Range(0f, 1f)] float chanceWalkerChangeDir = 0.5f;
	[SerializeField, Range(0f, 1f)] float chanceWalkerSpawn = 0.05f;
	[SerializeField, Range(0f, 1f)] float chanceWalkerDestoy = 0.05f;
	[SerializeField, Min(1)] int maxWalkers = 10;
	[SerializeField, Range(0.01f, 0.99f)] float percentToFill = 0.3f; //
	[SerializeField, Min(1), Tooltip("Number of non-overlapping rooms to place when possible.")] int roomCount = 5;
	[SerializeField, Min(2), Tooltip("Minimum room width and height in grid cells.")] int minRoomDimension = 3;
	[SerializeField, Min(2), Tooltip("Maximum room width and height in grid cells.")] int maxRoomDimension = 7;
	[SerializeField, Range(0f, 1f), Tooltip("Initial probability that an interior cell starts as floor.")] float cellularAutomataFillChance = 0.55f;
	[SerializeField, Range(0, MaxCellularAutomataIterations), Tooltip("Number of smoothing passes, subject to the grid work limit.")] int cellularAutomataIterations = 5;
	[SerializeField, Range(0, 8), Tooltip("Minimum floor neighbors out of the eight cardinal and diagonal cells.")] int cellularAutomataNeighborThreshold = 5;
	[SerializeField] bool useFixedSeed = false;
	[SerializeField] int fixedSeed = 0;
#if UNITY_EDITOR
	[SerializeField] bool livePreview;
	[SerializeField, HideInInspector] bool isLivePreviewRootMarker;
	[SerializeField, HideInInspector] LevelGenerator livePreviewOwner;
	[SerializeField, HideInInspector] string livePreviewOwnerGlobalId;
	[System.NonSerialized] Transform previewRoot;

	public bool LivePreviewEnabled { get { return livePreview; } }
	public bool IsLivePreviewRootMarker { get { return isLivePreviewRootMarker; } }
	public bool IsInLivePreviewHierarchy(){
		for (Transform ancestor = transform; ancestor != null; ancestor = ancestor.parent){
			LevelGenerator marker = ancestor.GetComponent<LevelGenerator>();
			LevelGenerator owner = ancestor.parent == null ? null : ancestor.parent.GetComponent<LevelGenerator>();
			if (marker != null && marker.isLivePreviewRootMarker && owner != null && owner.IsOwnedPreviewRoot(ancestor)){
				return true;
			}
		}
		return false;
	}
#endif

	public GameObject Exit;
	public GameObject[] wallObj, wallUpObj, wallDownObj, wallRightObj, wallLeftObj, floorObj, enemyObj, bossObj, emptyObj;
	public bool BossCreado = false;
	
	void Start () {
#if UNITY_EDITOR
		if (isLivePreviewRootMarker){
			return;
		}
#endif
		RegenerateLevel();
	}

	public void RegenerateLevel(){
		if (!ValidateConfiguration()){
			return;
		}

		EnsureGeneratedRoot();
		ClearGeneratedObjects();
		GenerateValidatedLevel(generatedRoot, false);
	}

	void GenerateValidatedLevel(Transform spawnRoot, bool preserveRandomState){
		bool restoreRandomState = preserveRandomState || useFixedSeed;
		Random.State previousRandomState = Random.state;
		if (useFixedSeed){
			Random.InitState(fixedSeed);
		}

		try{
			Setup();
			CreateFloors();
			CreateWalls();
			RemoveSingleWalls();
			SpawnLevel(spawnRoot);
			BossCreado = false;
		}
		finally{
			if (restoreRandomState){
				Random.state = previousRandomState;
			}
		}
	}

#if UNITY_EDITOR
	public bool LivePreviewCellLimitExceeded(){
		if (float.IsNaN(roomSizeWorldUnits.x) || float.IsInfinity(roomSizeWorldUnits.x) ||
			float.IsNaN(roomSizeWorldUnits.y) || float.IsInfinity(roomSizeWorldUnits.y) ||
			float.IsNaN(worldUnitsInOneGridCell) || float.IsInfinity(worldUnitsInOneGridCell) ||
			roomSizeWorldUnits.x <= 0f || roomSizeWorldUnits.y <= 0f || worldUnitsInOneGridCell <= 0f){
			return false;
		}

		double widthInCells = roomSizeWorldUnits.x / (double)worldUnitsInOneGridCell;
		double heightInCells = roomSizeWorldUnits.y / (double)worldUnitsInOneGridCell;
		double width = System.Math.Round(widthInCells, System.MidpointRounding.ToEven);
		double height = System.Math.Round(heightInCells, System.MidpointRounding.ToEven);
		return width * height > LivePreviewCellLimit;
	}

	public void RefreshLivePreview(){
		Random.State previousRandomState = Random.state;
		gridSpace[,] previousGrid = grid;
		List<walker> previousWalkers = walkers;
		int previousRoomHeight = roomHeight;
		int previousRoomWidth = roomWidth;
		bool previousBossCreated = BossCreado;

		try{
			if (Application.isPlaying || !livePreview || !isActiveAndEnabled ||
				!gameObject.activeInHierarchy || isLivePreviewRootMarker){
				ClearPreviewRoot();
				return;
			}
			if (!ValidateConfiguration()){
				return;
			}
			if (LivePreviewCellLimitExceeded()){
				return;
			}

			EnsurePreviewRoot();
			ClearPreviewObjects();
			GenerateValidatedLevel(previewRoot, true);
			SetPreviewHierarchyFlags(previewRoot.gameObject, true);
		}
		finally{
			try{
				grid = previousGrid;
				walkers = previousWalkers;
				roomHeight = previousRoomHeight;
				roomWidth = previousRoomWidth;
				BossCreado = previousBossCreated;
			}
			finally{
				Random.state = previousRandomState;
			}
		}
	}

	public void ClearLivePreview(){
		Random.State previousRandomState = Random.state;
		try{
			ClearPreviewRoot();
		}
		finally{
			Random.state = previousRandomState;
		}
	}

	void EnsurePreviewRoot(){
		previewRoot = FindPreviewRoot();
		if (previewRoot == null){
			GameObject root = new GameObject(PreviewRootName);
			root.hideFlags = HideFlags.HideAndDontSave;
			previewRoot = root.transform;
			previewRoot.SetParent(transform, false);
			LevelGenerator marker = root.AddComponent<LevelGenerator>();
			marker.isLivePreviewRootMarker = true;
			marker.livePreviewOwner = this;
			marker.livePreviewOwnerGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(this).ToString();
		}
		SetPreviewHierarchyFlags(previewRoot.gameObject, true);
	}

	Transform FindPreviewRoot(){
		Transform found = null;
		for (int i = transform.childCount - 1; i >= 0; i--){
			Transform child = transform.GetChild(i);
			if (!IsOwnedPreviewRoot(child)){
				continue;
			}
			if (found == null){
				found = child;
			}
			else{
				DestroyImmediate(child.gameObject);
			}
		}
		return found;
	}

	bool IsOwnedPreviewRoot(Transform candidate){
		if (candidate == null || candidate.parent != transform || candidate.name != PreviewRootName ||
			candidate.gameObject.hideFlags != HideFlags.HideAndDontSave || candidate.hideFlags != HideFlags.DontSave){
			return false;
		}

		LevelGenerator marker = candidate.GetComponent<LevelGenerator>();
		if (marker == null || !marker.isLivePreviewRootMarker || marker.hideFlags != HideFlags.DontSave){
			return false;
		}

		if (marker.livePreviewOwner != null){
			return marker.livePreviewOwner == this;
		}
		string ownerGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(this).ToString();
		return !string.IsNullOrEmpty(ownerGlobalId) && marker.livePreviewOwnerGlobalId == ownerGlobalId;
	}

	void ClearPreviewObjects(){
		if (previewRoot == null || !IsOwnedPreviewRoot(previewRoot)){
			return;
		}
		for (int i = previewRoot.childCount - 1; i >= 0; i--){
			DestroyImmediate(previewRoot.GetChild(i).gameObject);
		}
	}

	void ClearPreviewRoot(){
		previewRoot = null;
		for (int i = transform.childCount - 1; i >= 0; i--){
			Transform child = transform.GetChild(i);
			if (!IsOwnedPreviewRoot(child)){
				continue;
			}
			if (Application.isPlaying){
				Destroy(child.gameObject);
			}
			else{
				DestroyImmediate(child.gameObject);
			}
		}
	}

	void SetPreviewHierarchyFlags(GameObject root, bool hideRoot){
		Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++){
			GameObject current = transforms[i].gameObject;
			current.hideFlags = hideRoot && current == root ? HideFlags.HideAndDontSave : HideFlags.DontSave;
			Component[] components = current.GetComponents<Component>();
			for (int componentIndex = 0; componentIndex < components.Length; componentIndex++){
				if (components[componentIndex] != null){
					components[componentIndex].hideFlags = HideFlags.DontSave;
				}
			}
		}
	}

#endif

	void Setup(){
		//find grid size
		roomWidth = Mathf.RoundToInt(roomSizeWorldUnits.x / worldUnitsInOneGridCell);
		roomHeight = Mathf.RoundToInt(roomSizeWorldUnits.y / worldUnitsInOneGridCell);
		//create grid
		grid = new gridSpace[roomWidth,roomHeight];
		//set grid's default state
		for (int x = 0; x < roomWidth; x++){
			for (int y = 0; y < roomHeight; y++){
				//make every cell "empty"
				grid[x,y] = gridSpace.empty;
			}
		}
		//set first walker
		//init list
		walkers = new List<walker>();
		//create a walker 
		walker newWalker = new walker();
		newWalker.dir = RandomDirection();

		//find center of grid
		Vector2 spawnPos = new Vector2(Mathf.Floor(roomWidth / 2.0f),
										Mathf.Floor(roomHeight / 2.0f));
		newWalker.pos = spawnPos;
		//add walker to list
		walkers.Add(newWalker);
	}
	void CreateFloors(){
		switch (fillMode){
			case FillMode.RoomsAndCorridors:
				CreateRoomsAndCorridors();
				break;
			case FillMode.CellularAutomata:
				CreateCellularAutomataFloors();
				break;
			default:
				CreateRandomWalkFloors();
				break;
		}
	}

	void CreateRandomWalkFloors(){
		int iterations = 0;//loop will not run forever
		int floorCount = 0;
		do{
			//create floor at position of every walker
			foreach (walker myWalker in walkers){
				int x = (int)myWalker.pos.x;
				int y = (int)myWalker.pos.y;
				if (grid[x,y] != gridSpace.floor){
					grid[x,y] = gridSpace.floor;
					floorCount++;
				}
			}
			//chance: destroy walker
			int numberChecks = walkers.Count; //might modify count while in this loop
			for (int i = 0; i < numberChecks; i++){
				//only if its not the only one, and at a low chance
				if (Random.value < chanceWalkerDestoy && walkers.Count > 1){
					walkers.RemoveAt(i);
					break; //only destroy one per iteration
                    
				}
			}
			//chance: walker pick new direction
			for (int i = 0; i < walkers.Count; i++){
				if (Random.value < chanceWalkerChangeDir){
					walker thisWalker = walkers[i];
					thisWalker.dir = RandomDirection();
					walkers[i] = thisWalker;
					
				}
			}
			//chance: spawn new walker
			numberChecks = walkers.Count; //might modify count while in this loop
			for (int i = 0; i < numberChecks; i++){
				//only if # of walkers < max, and at a low chance
				if (Random.value < chanceWalkerSpawn && walkers.Count < maxWalkers){
					//create a walker 
					walker newWalker = new walker();
					newWalker.dir = RandomDirection();
					newWalker.pos = walkers[i].pos;
					walkers.Add(newWalker);
				}
			}
			//move walkers
			for (int i = 0; i < walkers.Count; i++){
				walker thisWalker = walkers[i];
				thisWalker.pos += thisWalker.dir;
				walkers[i] = thisWalker;				
			}
			//avoid boarder of grid
			for (int i =0; i < walkers.Count; i++){
				walker thisWalker = walkers[i];
				//clamp x,y to leave a 1 space boarder: leave room for walls
				thisWalker.pos.x = Mathf.Clamp(thisWalker.pos.x, 1, roomWidth-2);
				thisWalker.pos.y = Mathf.Clamp(thisWalker.pos.y, 1, roomHeight-2);
				walkers[i] = thisWalker;
				
			}
			//check to exit loop
			if ((float)floorCount / grid.Length >= percentToFill){
                //GameObject player = Instantiate(PlayerObj, new Vector3(0, 0, 0), Quaternion.identity); //spawn player
				break;
			}
			iterations++;
		}while(iterations < MaxGenerationIterations);

		if ((float)floorCount / grid.Length < percentToFill){
			Debug.LogWarning("LevelGenerator: walker generation reached its iteration limit before the target fill percentage. Consider lowering percentToFill or increasing the map size.", this);
		}
	}

	struct Room {
		public int x;
		public int y;
		public int width;
		public int height;

		public int CenterX { get { return x + width / 2; } }
		public int CenterY { get { return y + height / 2; } }
	}

	void CreateRoomsAndCorridors(){
		List<Room> rooms = new List<Room>();
		int maximumRoomWidth = Mathf.Min(maxRoomDimension, roomWidth - 2);
		int maximumRoomHeight = Mathf.Min(maxRoomDimension, roomHeight - 2);
		int attempts = 0;

		while (rooms.Count < roomCount && attempts < MaxRoomPlacementAttempts){
			attempts++;
			int width = Random.Range(minRoomDimension, maximumRoomWidth + 1);
			int height = Random.Range(minRoomDimension, maximumRoomHeight + 1);
			int x = Random.Range(1, roomWidth - width);
			int y = Random.Range(1, roomHeight - height);
			Room candidate = new Room {x = x, y = y, width = width, height = height};
			if (!OverlapsExistingRoom(candidate, rooms)){
				rooms.Add(candidate);
				CarveRoom(candidate);
			}
		}

		for (int i = 1; i < rooms.Count; i++){
			ConnectRooms(rooms[i - 1], rooms[i]);
		}

		if (rooms.Count < roomCount){
			Debug.LogWarning("LevelGenerator: room placement reached its attempt limit after placing " + rooms.Count + " of " + roomCount + " requested rooms.", this);
		}
	}

	bool OverlapsExistingRoom(Room candidate, List<Room> rooms){
		for (int i = 0; i < rooms.Count; i++){
			Room existing = rooms[i];
			if (candidate.x < existing.x + existing.width && candidate.x + candidate.width > existing.x &&
				candidate.y < existing.y + existing.height && candidate.y + candidate.height > existing.y){
				return true;
			}
		}
		return false;
	}

	void CarveRoom(Room room){
		for (int x = room.x; x < room.x + room.width; x++){
			for (int y = room.y; y < room.y + room.height; y++){
				grid[x, y] = gridSpace.floor;
			}
		}
	}

	void ConnectRooms(Room first, Room second){
		int startX = first.CenterX;
		int startY = first.CenterY;
		int endX = second.CenterX;
		int endY = second.CenterY;
		if (Random.value < 0.5f){
			CarveHorizontalCorridor(startX, endX, startY);
			CarveVerticalCorridor(startY, endY, endX);
		}
		else{
			CarveVerticalCorridor(startY, endY, startX);
			CarveHorizontalCorridor(startX, endX, endY);
		}
	}

	void CarveHorizontalCorridor(int startX, int endX, int y){
		for (int x = Mathf.Min(startX, endX); x <= Mathf.Max(startX, endX); x++){
			grid[x, y] = gridSpace.floor;
		}
	}

	void CarveVerticalCorridor(int startY, int endY, int x){
		for (int y = Mathf.Min(startY, endY); y <= Mathf.Max(startY, endY); y++){
			grid[x, y] = gridSpace.floor;
		}
	}

	void CreateCellularAutomataFloors(){
		for (int x = 1; x < roomWidth - 1; x++){
			for (int y = 1; y < roomHeight - 1; y++){
				grid[x, y] = Random.value < cellularAutomataFillChance ? gridSpace.floor : gridSpace.empty;
			}
		}

		gridSpace[,] currentGrid = grid;
		gridSpace[,] nextGrid = new gridSpace[roomWidth, roomHeight];
		for (int iteration = 0; iteration < cellularAutomataIterations; iteration++){
			for (int x = 1; x < roomWidth - 1; x++){
				for (int y = 1; y < roomHeight - 1; y++){
					int floorNeighbors = 0;
					for (int offsetX = -1; offsetX <= 1; offsetX++){
						for (int offsetY = -1; offsetY <= 1; offsetY++){
							if (offsetX == 0 && offsetY == 0){
								continue;
							}
							int neighborX = x + offsetX;
							int neighborY = y + offsetY;
							if (neighborX > 0 && neighborX < roomWidth - 1 &&
								neighborY > 0 && neighborY < roomHeight - 1 &&
								currentGrid[neighborX, neighborY] == gridSpace.floor){
								floorNeighbors++;
							}
						}
					}
					nextGrid[x, y] = floorNeighbors >= cellularAutomataNeighborThreshold ? gridSpace.floor : gridSpace.empty;
				}
			}

			gridSpace[,] previousGrid = currentGrid;
			currentGrid = nextGrid;
			nextGrid = previousGrid;
		}
		grid = currentGrid;

		if (NumberOfFloors() == 0){
			grid[roomWidth / 2, roomHeight / 2] = gridSpace.floor;
		}
		ConnectDisconnectedFloorRegions();
	}

	void ConnectDisconnectedFloorRegions(){
		int cellCount = roomWidth * roomHeight;
		int[] componentIds = new int[cellCount];
		for (int i = 0; i < componentIds.Length; i++){
			componentIds[i] = -1;
		}

		int[] queue = new int[cellCount];
		int componentCount = 0;
		for (int x = 1; x < roomWidth - 1; x++){
			for (int y = 1; y < roomHeight - 1; y++){
				int start = y * roomWidth + x;
				if (grid[x, y] != gridSpace.floor || componentIds[start] >= 0){
					continue;
				}

				int head = 0;
				int tail = 0;
				componentIds[start] = componentCount;
				queue[tail++] = start;
				while (head < tail){
					int current = queue[head++];
					int currentX = current % roomWidth;
					int currentY = current / roomWidth;
					AddFloorComponentNeighbor(currentX - 1, currentY, componentCount, componentIds, queue, ref tail);
					AddFloorComponentNeighbor(currentX + 1, currentY, componentCount, componentIds, queue, ref tail);
					AddFloorComponentNeighbor(currentX, currentY - 1, componentCount, componentIds, queue, ref tail);
					AddFloorComponentNeighbor(currentX, currentY + 1, componentCount, componentIds, queue, ref tail);
				}
				componentCount++;
			}
		}

		if (componentCount <= 1){
			return;
		}

		int[] owner = new int[cellCount];
		int[] parents = new int[cellCount];
		for (int i = 0; i < cellCount; i++){
			owner[i] = -1;
			parents[i] = -1;
		}
		int headIndex = 0;
		int tailIndex = 0;
		for (int x = 1; x < roomWidth - 1; x++){
			for (int y = 1; y < roomHeight - 1; y++){
				int index = y * roomWidth + x;
				if (componentIds[index] >= 0){
					owner[index] = componentIds[index];
					queue[tailIndex++] = index;
				}
			}
		}

		int[] componentParents = new int[componentCount];
		byte[] componentRanks = new byte[componentCount];
		for (int i = 0; i < componentCount; i++){
			componentParents[i] = i;
		}
		int connectedComponents = componentCount;
		while (headIndex < tailIndex && connectedComponents > 1){
			int current = queue[headIndex++];
			int currentX = current % roomWidth;
			int currentY = current / roomWidth;
			TryJoinFloorFronts(current, currentX - 1, currentY, owner, parents, componentParents, componentRanks, ref connectedComponents, queue, ref tailIndex);
			TryJoinFloorFronts(current, currentX + 1, currentY, owner, parents, componentParents, componentRanks, ref connectedComponents, queue, ref tailIndex);
			TryJoinFloorFronts(current, currentX, currentY - 1, owner, parents, componentParents, componentRanks, ref connectedComponents, queue, ref tailIndex);
			TryJoinFloorFronts(current, currentX, currentY + 1, owner, parents, componentParents, componentRanks, ref connectedComponents, queue, ref tailIndex);
		}
	}

	void AddFloorComponentNeighbor(int x, int y, int componentId, int[] componentIds, int[] queue, ref int tail){
		if (x <= 0 || x >= roomWidth - 1 || y <= 0 || y >= roomHeight - 1 || grid[x, y] != gridSpace.floor){
			return;
		}
		int index = y * roomWidth + x;
		if (componentIds[index] >= 0){
			return;
		}
		componentIds[index] = componentId;
		queue[tail++] = index;
	}

	void TryJoinFloorFronts(int current, int neighborX, int neighborY, int[] owner, int[] parents,
		int[] componentParents, byte[] componentRanks, ref int connectedComponents, int[] queue, ref int tailIndex){
		if (neighborX <= 0 || neighborX >= roomWidth - 1 || neighborY <= 0 || neighborY >= roomHeight - 1){
			return;
		}
		int neighbor = neighborY * roomWidth + neighborX;
		if (owner[neighbor] < 0){
			owner[neighbor] = owner[current];
			parents[neighbor] = current;
			queue[tailIndex++] = neighbor;
			return;
		}
		if (owner[neighbor] != owner[current] && UnionFloorComponents(componentParents, componentRanks, owner[current], owner[neighbor])){
			CarveFrontPath(current, parents);
			CarveFrontPath(neighbor, parents);
			connectedComponents--;
		}
	}

	void CarveFrontPath(int index, int[] parents){
		while (index >= 0){
			int x = index % roomWidth;
			int y = index / roomWidth;
			grid[x, y] = gridSpace.floor;
			index = parents[index];
		}
	}

	static bool UnionFloorComponents(int[] parents, byte[] ranks, int first, int second){
		int firstRoot = FindFloorComponentRoot(parents, first);
		int secondRoot = FindFloorComponentRoot(parents, second);
		if (firstRoot == secondRoot){
			return false;
		}
		if (ranks[firstRoot] < ranks[secondRoot]){
			parents[firstRoot] = secondRoot;
		}
		else if (ranks[firstRoot] > ranks[secondRoot]){
			parents[secondRoot] = firstRoot;
		}
		else{
			parents[secondRoot] = firstRoot;
			ranks[firstRoot]++;
		}
		return true;
	}

	static int FindFloorComponentRoot(int[] parents, int component){
		while (parents[component] != component){
			parents[component] = parents[parents[component]];
			component = parents[component];
		}
		return component;
	}

	void CreateWalls(){
		//loop though every grid space
		for (int x = 1; x < roomWidth-1; x++){
			for (int y = 1; y < roomHeight-1; y++){
				//if theres a floor, check the spaces around it
				if (grid[x,y] == gridSpace.floor){
					//if any surrounding spaces are empty, place a wall
					if (grid[x,y+1] == gridSpace.empty){
						grid[x,y+1] = gridSpace.wallUp;
					}
					if (grid[x,y-1] == gridSpace.empty){
						grid[x,y-1] = gridSpace.wallDown;
					}
					if (grid[x+1,y] == gridSpace.empty){
						grid[x+1,y] = gridSpace.wallRight;
					}
					if (grid[x-1,y] == gridSpace.empty){
						grid[x-1,y] = gridSpace.wallLeft;
					}
				}
			}
		}
	}
	void RemoveSingleWalls(){
		//loop though every grid space
		for (int x = 1; x < roomWidth-1; x++){
			for (int y = 1; y < roomHeight-1; y++){
				//if theres a wall, check the spaces around it
				if (grid[x,y] == gridSpace.wall || grid[x,y] == gridSpace.wallUp || grid[x,y] == gridSpace.wallDown){
					//assume all space around wall are floors
					bool allFloors = true;
					//check each side to see if they are all floors
					for (int checkX = -1; checkX <= 1 ; checkX++){
						for (int checkY = -1; checkY <= 1; checkY++){
							if (x + checkX < 0 || x + checkX >= roomWidth || 
								y + checkY < 0 || y + checkY >= roomHeight){
								//skip checks that are out of range
								continue;
							}
							if ((checkX != 0 && checkY != 0) || (checkX == 0 && checkY == 0)){
								//skip corners and center
								continue;
							}
							if (grid[x + checkX,y+checkY] != gridSpace.floor){
								allFloors = false;
							}
						}
					}
					if (allFloors){
						grid[x,y] = gridSpace.floor;
					}
				}
			}
		}
	}
	void SpawnLevel(Transform spawnRoot){
		for (int x = 0; x < roomWidth; x++){
			for (int y = 0; y < roomHeight; y++){
				switch(grid[x,y]){
					case gridSpace.empty:
						Spawn(x,y,emptyObj[Random.Range(0,emptyObj.Length)], spawnRoot);
						break;
					case gridSpace.floor:
						Spawn(x,y,floorObj[Random.Range(0,floorObj.Length)], spawnRoot);
						break;
					case gridSpace.wall:
						
						Spawn(x,y,wallObj[Random.Range(0,wallObj.Length)], spawnRoot);
						break;
					case gridSpace.wallUp:
						Spawn(x,y,wallUpObj[Random.Range(0,wallUpObj.Length)], spawnRoot);
						break;
					case gridSpace.wallDown:
						Spawn(x,y,wallDownObj[Random.Range(0,wallDownObj.Length)], spawnRoot);
						break;
					case gridSpace.wallLeft:
						Spawn(x,y,wallLeftObj[Random.Range(0,wallLeftObj.Length)], spawnRoot);
						break;
					case gridSpace.wallRight:
						Spawn(x,y,wallRightObj[Random.Range(0,wallRightObj.Length)], spawnRoot);
						break;
				}
			}
		}
	}

	void SpawnExit(){
		if (Exit == null){
			Debug.LogError("LevelGenerator: cannot spawn the exit because Exit is not assigned.", this);
			return;
		}

		int x, y;
		if (TryFindRandomFloor(out x, out y)){
			Spawn(x, y, Exit, generatedRoot);
		}
		else{
			Debug.LogWarning("LevelGenerator: cannot spawn the exit because the generated map has no floor cells.", this);
		}
	}

	void SpawnBoss(){
		Debug.Log("Spawning Boss");
		//function that sspawns less enemies that the limit inside the room
		if (bossObj == null || bossObj.Length == 0){
			Debug.LogError("LevelGenerator: cannot spawn the boss because bossObj has no prefabs assigned.", this);
			return;
		}

		int x, y;
		if (TryFindRandomFloor(out x, out y)){
			Spawn(x, y, bossObj[Random.Range(0, bossObj.Length)], generatedRoot);
		}
		else{
			Debug.LogWarning("LevelGenerator: cannot spawn the boss because the generated map has no floor cells.", this);
		}
	}

	bool TryFindRandomFloor(out int floorX, out int floorY){
		floorX = 0;
		floorY = 0;
		if (grid == null){
			return false;
		}

		int floorCount = 0;
		for (int x = 0; x < roomWidth; x++){
			for (int y = 0; y < roomHeight; y++){
				if (grid[x, y] == gridSpace.floor){
					floorCount++;
					if (Random.Range(0, floorCount) == 0){
						floorX = x;
						floorY = y;
					}
				}
			}
		}
		return floorCount > 0;
	}

	Vector2 RandomDirection(){
		//pick random int between 0 and 3
		int choice = Mathf.FloorToInt(Random.value * 3.99f);
		//use that int to chose a direction
		switch (choice){
			case 0:
				return Vector2.down;
			case 1:
				return Vector2.left;
			case 2:
				return Vector2.up;
			default:
				return Vector2.right;
		}
	}
	int NumberOfFloors(){
		int count = 0;
		foreach (gridSpace space in grid){
			if (space == gridSpace.floor){
				count++;
			}
		}
		return count;
	}
	void Spawn(float x, float y, GameObject toSpawn, Transform spawnRoot){
		//find the position to spawn
		Vector3 localPosition = new Vector3(
			(x - (roomWidth - 1) / 2.0f) * worldUnitsInOneGridCell,
			(y - (roomHeight - 1) / 2.0f) * worldUnitsInOneGridCell,
			0f);
		Vector3 spawnPos = transform.TransformPoint(localPosition);
		//spawn object
#if UNITY_EDITOR
		GameObject spawnedObject = Instantiate(toSpawn, spawnPos, Quaternion.identity, spawnRoot);
		if (spawnRoot == previewRoot){
			SetPreviewHierarchyFlags(spawnedObject, false);
		}
#else
		Instantiate(toSpawn, spawnPos, Quaternion.identity, spawnRoot);
#endif
	}

	void EnsureGeneratedRoot(){
		if (generatedRoot != null){
			return;
		}

		GameObject root = new GameObject(GeneratedRootName);
		generatedRoot = root.transform;
		generatedRoot.SetParent(transform, false);
	}

	void ClearGeneratedObjects(){
		for (int i = generatedRoot.childCount - 1; i >= 0; i--){
			GameObject generatedObject = generatedRoot.GetChild(i).gameObject;
			if (Application.isPlaying){
				Destroy(generatedObject);
			}
			else{
				DestroyImmediate(generatedObject);
			}
		}
	}

	bool ValidateConfiguration(){
		if (float.IsNaN(roomSizeWorldUnits.x) || float.IsInfinity(roomSizeWorldUnits.x) ||
			float.IsNaN(roomSizeWorldUnits.y) || float.IsInfinity(roomSizeWorldUnits.y) ||
			roomSizeWorldUnits.x <= 0f || roomSizeWorldUnits.y <= 0f){
			Debug.LogError("LevelGenerator: roomSizeWorldUnits must contain finite, positive x and y dimensions.", this);
			return false;
		}
		if (float.IsNaN(worldUnitsInOneGridCell) || float.IsInfinity(worldUnitsInOneGridCell) || worldUnitsInOneGridCell <= 0f){
			Debug.LogError("LevelGenerator: worldUnitsInOneGridCell must be a finite value greater than zero.", this);
			return false;
		}

		double widthInCells = roomSizeWorldUnits.x / (double)worldUnitsInOneGridCell;
		double heightInCells = roomSizeWorldUnits.y / (double)worldUnitsInOneGridCell;
		if (widthInCells > MaxGridDimension + 0.5d || heightInCells > MaxGridDimension + 0.5d){
			Debug.LogError("LevelGenerator: the requested grid exceeds the maximum dimension of " + MaxGridDimension + " cells. Increase the cell size or reduce the map dimensions.", this);
			return false;
		}

		roomWidth = Mathf.RoundToInt((float)widthInCells);
		roomHeight = Mathf.RoundToInt((float)heightInCells);
		if (roomWidth < 3 || roomHeight < 3){
			Debug.LogError("LevelGenerator: both grid dimensions must be at least 3 cells to leave room for the wall border.", this);
			return false;
		}
		if ((long)roomWidth * roomHeight > MaxGridCells){
			Debug.LogError("LevelGenerator: the requested grid exceeds the maximum of " + MaxGridCells + " cells. Increase the cell size or reduce the map dimensions.", this);
			return false;
		}
		if (!System.Enum.IsDefined(typeof(FillMode), fillMode)){
			Debug.LogError("LevelGenerator: fillMode must select a supported generation mode.", this);
			return false;
		}

		switch (fillMode){
			case FillMode.RandomWalk:
				if (!IsProbability(chanceWalkerChangeDir) || !IsProbability(chanceWalkerSpawn) || !IsProbability(chanceWalkerDestoy)){
					Debug.LogError("LevelGenerator: walker change, spawn, and destroy chances must be finite values from 0 to 1.", this);
					return false;
				}
				if (maxWalkers < 1 || maxWalkers > MaxWalkerCount){
					Debug.LogError("LevelGenerator: maxWalkers must be between 1 and " + MaxWalkerCount + ".", this);
					return false;
				}
				if (float.IsNaN(percentToFill) || float.IsInfinity(percentToFill) || percentToFill <= 0f || percentToFill >= 1f){
					Debug.LogError("LevelGenerator: percentToFill must be a finite value greater than 0 and less than 1.", this);
					return false;
				}

				double maximumFill = ((double)(roomWidth - 2) * (roomHeight - 2)) / ((long)roomWidth * roomHeight);
				if (percentToFill > maximumFill){
					Debug.LogError("LevelGenerator: percentToFill exceeds the maximum floor area available after reserving the wall border. Lower percentToFill or increase the map dimensions.", this);
					return false;
				}
				break;
			case FillMode.RoomsAndCorridors:
				if (roomCount < 1 || roomCount > MaxRoomCount){
					Debug.LogError("LevelGenerator: roomCount must be between 1 and " + MaxRoomCount + ".", this);
					return false;
				}
				if (minRoomDimension < 2 || maxRoomDimension < minRoomDimension || maxRoomDimension > MaxGridDimension){
					Debug.LogError("LevelGenerator: room dimensions must satisfy 2 <= minRoomDimension <= maxRoomDimension <= " + MaxGridDimension + ".", this);
					return false;
				}
				if (minRoomDimension > Mathf.Min(roomWidth - 2, roomHeight - 2)){
					Debug.LogError("LevelGenerator: minRoomDimension must fit inside the map's reserved outer border.", this);
					return false;
				}
				break;
			case FillMode.CellularAutomata:
				if (!IsProbability(cellularAutomataFillChance)){
					Debug.LogError("LevelGenerator: cellularAutomataFillChance must be a finite value from 0 to 1.", this);
					return false;
				}
				if (cellularAutomataIterations < 0 || cellularAutomataIterations > MaxCellularAutomataIterations){
					Debug.LogError("LevelGenerator: cellularAutomataIterations must be between 0 and " + MaxCellularAutomataIterations + ".", this);
					return false;
				}
				if (cellularAutomataNeighborThreshold < 0 || cellularAutomataNeighborThreshold > 8){
					Debug.LogError("LevelGenerator: cellularAutomataNeighborThreshold must be between 0 and 8.", this);
					return false;
				}
				long cellularWork = (long)(roomWidth - 2) * (roomHeight - 2) * cellularAutomataIterations;
				if (cellularWork > MaxCellularAutomataWork){
					Debug.LogError("LevelGenerator: the selected Cellular Automata iteration count exceeds the safe work limit. Reduce the map dimensions or iteration count.", this);
					return false;
				}
				break;
		}

		bool valid = true;
		valid = ValidatePrefabArray("emptyObj", emptyObj) && valid;
		valid = ValidatePrefabArray("floorObj", floorObj) && valid;
		valid = ValidatePrefabArray("wallObj", wallObj) && valid;
		valid = ValidatePrefabArray("wallUpObj", wallUpObj) && valid;
		valid = ValidatePrefabArray("wallDownObj", wallDownObj) && valid;
		valid = ValidatePrefabArray("wallLeftObj", wallLeftObj) && valid;
		valid = ValidatePrefabArray("wallRightObj", wallRightObj) && valid;
		return valid;
	}

	bool IsProbability(float value){
		return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
	}

	bool ValidatePrefabArray(string fieldName, GameObject[] prefabs){
		if (prefabs == null || prefabs.Length == 0){
			Debug.LogError("LevelGenerator: " + fieldName + " must contain at least one prefab.", this);
			return false;
		}
		for (int i = 0; i < prefabs.Length; i++){
			if (prefabs[i] == null){
				Debug.LogError("LevelGenerator: " + fieldName + " contains an unassigned prefab at index " + i + ".", this);
				return false;
			}
		}
		return true;
	}
}