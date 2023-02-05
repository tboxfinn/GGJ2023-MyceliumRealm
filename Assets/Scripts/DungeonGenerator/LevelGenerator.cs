using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AI;

public class LevelGenerator : MonoBehaviour {
	enum gridSpace {empty, floor, wall, wallUp, wallDown, wallRight, wallLeft};
	gridSpace[,] grid;
	int roomHeight, roomWidth;
	public Vector2 roomSizeWorldUnits = new Vector2(50,50); // cambia el tamaño del mapa
	float worldUnitsInOneGridCell = 1f;
	struct walker{
		public Vector2 dir;
		public Vector2 pos;
	}
	List<walker> walkers;
    // cosas que puedo cambiar para ver como cambia el mapa
	float chanceWalkerChangeDir = 0.5f, chanceWalkerSpawn = 0.05f;
	float chanceWalkerDestoy = 0.05f;
	int maxWalkers = 10;
	public int maxEnemys = 15, enemiesGenerated;
	float percentToFill = 0.3f; //
	public GameObject[] wallObj, wallUpObj, wallDownObj, wallRightObj, wallLeftObj, floorObj, enemyObj, bossObj;
	public bool BossCreado = false;
	
	void Start () {
		Setup();
		CreateFloors();
		CreateWalls();
		RemoveSingleWalls();
		SpawnLevel();

		//update and create nav mesh
		BossCreado = false;

	}

	void Update(){
		for(int i = 0; i < maxEnemys; i++){
			if(GameObject.FindGameObjectsWithTag("Enemy").Length < maxEnemys ){
				SpawnEnemy();
				enemiesGenerated++;
			}
		}

		if (enemiesGenerated == maxEnemys+1&& BossCreado == false){
			SpawnBoss();
			BossCreado = true;
		}
	}
	void Setup(){
		//find grid size
		roomHeight = Mathf.RoundToInt(roomSizeWorldUnits.x / worldUnitsInOneGridCell);
		roomWidth = Mathf.RoundToInt(roomSizeWorldUnits.y / worldUnitsInOneGridCell);
		//create grid
		grid = new gridSpace[roomWidth,roomHeight];
		//set grid's default state
		for (int x = 0; x < roomWidth-1; x++){
			for (int y = 0; y < roomHeight-1; y++){
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
		Vector2 spawnPos = new Vector2(Mathf.RoundToInt(roomWidth/ 2.0f),
										Mathf.RoundToInt(roomHeight/ 2.0f));
		newWalker.pos = spawnPos;
		//add walker to list
		walkers.Add(newWalker);
	}
	void CreateFloors(){
		int iterations = 0;//loop will not run forever
		do{
			//create floor at position of every walker
			foreach (walker myWalker in walkers){
				grid[(int)myWalker.pos.x,(int)myWalker.pos.y] = gridSpace.floor;
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
			if ((float)NumberOfFloors() / (float)grid.Length > percentToFill){
                //GameObject player = Instantiate(PlayerObj, new Vector3(0, 0, 0), Quaternion.identity); //spawn player
				break;
			}
			iterations++;
		}while(iterations < 100000);
	}
	void CreateWalls(){
		//loop though every grid space
		for (int x = 0; x < roomWidth-1; x++){
			for (int y = 0; y < roomHeight-1; y++){
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
		for (int x = 0; x < roomWidth-1; x++){
			for (int y = 0; y < roomHeight-1; y++){
				//if theres a wall, check the spaces around it
				if (grid[x,y] == gridSpace.wall || grid[x,y] == gridSpace.wallUp || grid[x,y] == gridSpace.wallDown){
					//assume all space around wall are floors
					bool allFloors = true;
					//check each side to see if they are all floors
					for (int checkX = -1; checkX <= 1 ; checkX++){
						for (int checkY = -1; checkY <= 1; checkY++){
							if (x + checkX < 0 || x + checkX > roomWidth - 1 || 
								y + checkY < 0 || y + checkY > roomHeight - 1){
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
	void SpawnLevel(){
		for (int x = 0; x < roomWidth; x++){
			for (int y = 0; y < roomHeight; y++){
				switch(grid[x,y]){
					case gridSpace.empty:
						//Spawn(x,y,emptyObj);
						break;
					case gridSpace.floor:
						Spawn(x,y,floorObj[Random.Range(0,floorObj.Length)]);
						break;
					case gridSpace.wall:
						
						Spawn(x,y,wallObj[Random.Range(0,wallObj.Length)]);
						break;
					case gridSpace.wallUp:
						Spawn(x,y,wallUpObj[Random.Range(0,wallUpObj.Length)]);
						break;
					case gridSpace.wallDown:
						Spawn(x,y,wallDownObj[Random.Range(0,wallDownObj.Length)]);
						break;
					case gridSpace.wallLeft:
						Spawn(x,y,wallLeftObj[Random.Range(0,wallLeftObj.Length)]);
						break;
					case gridSpace.wallRight:
						Spawn(x,y,wallRightObj[Random.Range(0,wallRightObj.Length)]);
						break;
				}
			}
		}
	}

	void SpawnEnemy()
	{
		Debug.Log("Spawning enemies");

		
		//function that sspawns less enemies that the limit inside the room
		
		//random position inside the room
		int x = Random.Range(0, roomWidth);
		int y = Random.Range(0, roomHeight);
		//if the position is a floor, spawn an enemy
		if (grid[x, y] == gridSpace.floor)
		{
			Spawn(x, y, enemyObj[Random.Range(0,enemyObj.Length)]);
		}
		else
		{
			//if the position is not a floor, try again
			SpawnEnemy();
		}	
	}

	void SpawnBoss()
	{
		Debug.Log("Spawning Boss");
		//function that sspawns less enemies that the limit inside the room
		
		//random position inside the room
		int x = Random.Range(0, roomWidth);
		int y = Random.Range(0, roomHeight);
		//if the position is a floor, spawn an enemy
		if (grid[x, y] == gridSpace.floor)
		{
			Spawn(x, y, bossObj[Random.Range(0,bossObj.Length)]);
		}
		else
		{
			//if the position is not a floor, try again
			SpawnBoss();
		}	
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
	void Spawn(float x, float y, GameObject toSpawn){
		//find the position to spawn
		Vector2 offset = roomSizeWorldUnits / 2.0f;
		Vector2 spawnPos = new Vector2(x,y) * worldUnitsInOneGridCell - offset;
		//spawn object
		Instantiate(toSpawn, spawnPos, Quaternion.identity);
	}
	
	
}