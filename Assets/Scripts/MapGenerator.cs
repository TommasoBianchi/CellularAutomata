﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    const int MAX_DIM = 5000;

    #region variables
    [Range(0, MAX_DIM)]
    public int width = 50;
    [Range(0, MAX_DIM)]
    public int height = 50;
    [Range(0, 100)]
    public float fillPercentage = 50;
    [Range(0, 10)]
    public int smoothAmount = 5;
    [Range(0, MAX_DIM / 10)]
    public int borderSize = 3;
    [Range(0, MAX_DIM * MAX_DIM / 10)]
    public int smallestWallRegion = 30;
    [Range(0, MAX_DIM * MAX_DIM / 10)]
    public int smallestRoomRegion = 30;
    [Range(1, 50)]
    public int passagewayRadius = 2;
    [Range(1, 100)]
    public float squareSize = 1;
    [Range(1, 100)]
    public float wallHeight = 5;
    [Range(0, MAX_DIM / 20)]
    public int spawnPointNumber = 3;
    public string seed;
    public bool randomSeed;
    public bool is2D;
    public GameObject player2D;
    public GameObject player3D;
    public GameObject spawnpointWall;
    public Pathfinding pathfinding;

    int[,] map;
    Coord spawnTile;
    GameObject player;
    System.Random randGen;
    List<Coord> spawnPoints;
#endregion
    double time;

	void Start () {
        GenerateMap();

        InstantiatePlayer(spawnTile, is2D ? player2D : player3D);
	}
	
	public void GenerateMap () {
        map = new int[width, height];
        RandomFillMap();

        for (int i = 0; i < smoothAmount; i++)
		{
		    SmoothMap();	 
		}

        time = AudioSettings.dspTime;
        ProcessMap();
        Debug.Log("Time for ProcessMap = " + (AudioSettings.dspTime - time) * 1000);
        
        int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];

        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize)
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                else
                    borderedMap[x, y] = 1;
            }
        }

        time = AudioSettings.dspTime;
        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, squareSize, wallHeight, is2D);
        Debug.Log("Time for GenerateMesh = " + (AudioSettings.dspTime - time) * 1000);

        SetCeilingAndFloor();
	}

    void RandomFillMap()
    {
        if (randomSeed)
        {
            seed = System.DateTime.Now.GetHashCode().ToString();
        }

        randGen = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < borderSize || x > width - borderSize || y < borderSize || y > height - borderSize)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (randGen.Next(0, 101) < fillPercentage) ? 1 : 0;
                }
            }
        }
    }

    void SmoothMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbouringWalls = NeighbouringWalls(x, y);
                
                if (neighbouringWalls > 4)
                    map[x, y] = 1;
                else if (neighbouringWalls < 4)
                    map[x, y] = 0;
            }
        }
    }

    public int NeighbouringWalls(int gridX, int gridY)
    {
        int count = 0;
        for (int x = gridX - 1; x <= gridX + 1; x++)
        {
            for (int y = gridY - 1; y <= gridY + 1; y++)
            {
                if (IsInMap(x, y))
                {
                    if (!(x == gridX && y == gridY))
                    {
                        count += map[x, y];
                    }
                }
                else
                    count++;
                        
            }
        }

        return count;
    }

    List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    if (IsInMap(x, y) && (x == tile.tileX || y == tile.tileY) && mapFlags[x, y] == 0 && map[x, y] == tileType)
                    {
                        mapFlags[x, y] = 1;
                        queue.Enqueue(new Coord(x, y));
                    }
                }
            }
        }

        return tiles;
    }

    List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (var tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }

        return regions;
    }

    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(1);

        foreach (var wallRegion in wallRegions)
        {
            if(wallRegion.Count < smallestWallRegion)
                foreach (var tile in wallRegion)
                {
                    map[tile.tileX, tile.tileY] = 0;
                }
        }

        List<List<Coord>> roomRegions = GetRegions(0);
        List<Room> rooms = new List<Room>();
        HashSet<Room> roomsToConnect = new HashSet<Room>();

        foreach (var roomRegion in roomRegions)
        {
            if (roomRegion.Count < smallestRoomRegion)
                foreach (var tile in roomRegion)
                {
                    map[tile.tileX, tile.tileY] = 1;
                }
            else
            {
                rooms.Add(new Room(roomRegion, map, this));
                roomsToConnect.Add(new Room(roomRegion, map, this));
            }
        }

        if (rooms.Count > 0)
        {
            Room mainRoom = rooms.Max();
            mainRoom.isMainRoom = true;
            mainRoom.isAccessibleFromMainRoom = true;

            spawnTile = mainRoom.RandomPointInside();

            ConnectRooms(roomsToConnect, mainRoom);

            CreateSpawnpoints(rooms);
        }

        pathfinding = new Pathfinding(map, this);
    }

    List<Passage> ConnectRooms(HashSet<Room> notConnectedRooms, Room mainRoom)
    {
        List<Passage> passages = new List<Passage>();
        HashSet<Room> connectedRooms = new HashSet<Room>();
        connectedRooms.Add(mainRoom);

        while (notConnectedRooms.Count > 0)
        {
            Room bestRoomA = new Room();
            Room bestRoomB = new Room();
            Coord bestTileA = new Coord();
            Coord bestTileB = new Coord();
            float bestDistance = 0;

            foreach (var roomA in notConnectedRooms)
            {

                foreach (var roomB in connectedRooms)
                {
                    foreach (var tileA in roomA.edgeTiles)
                    {
                        foreach (var tileB in roomB.edgeTiles)
                        {
                            float thisDistance = Mathf.Abs(tileA.tileX - tileB.tileX) + Mathf.Abs(tileA.tileY - tileB.tileY);
                            if (thisDistance < bestDistance || bestDistance == 0)
                            {
                                bestDistance = thisDistance;
                                bestTileA = tileA;
                                bestTileB = tileB;
                                bestRoomA = roomA;
                                bestRoomB = roomB;
                            }
                        }
                    }
                }
            }

            passages.Add(CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB));

            notConnectedRooms.Remove(bestRoomA);
            connectedRooms.Add(bestRoomA);
        }

        return passages;
    }

    Passage CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Passage passage = CreatePassage(tileA, tileB);

        Room.ConnectRooms(roomA, roomB, passage);

        return passage;
    }    

    Passage CreatePassage(Coord tileA, Coord tileB, List<Coord> line)
    {
        List<Coord> tiles = new List<Coord>();

        foreach (var tile in line)
        {
            tiles = tiles.Concat(DrawCircle(tile, passagewayRadius)).ToList();
        }

        return new Passage(tileA, tileB, tiles);
    }

    Passage CreatePassage(Coord tileA, Coord tileB)
    {
        List<Coord> line = GetLine(tileA, tileB);

        return CreatePassage(tileA, tileB, line);
    }

    List<Coord> DrawCircle(Coord tile, int radius)
    {
        List<Coord> circle = new List<Coord>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y < radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int realX = tile.tileX + x;
                    int realY = tile.tileY + y;
                    if (IsInMap(realX, realY))
                    {
                        map[realX, realY] = 0;
                        circle.Add(new Coord(realX, realY));
                    }
                }
            }
        }

        return circle;
    }

    List<Coord> GetLine(Coord start, Coord end)
    {
        List<Coord> line = new List<Coord>();

        int x = start.tileX;
        int y = start.tileY;

        int dx = end.tileX - start.tileX;
        int dy = end.tileY - start.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Math.Abs(dx);
        int shortest = Math.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Math.Abs(dy);
            shortest = Math.Abs(dx);
            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulator = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted)
                y += step;
            else
                x += step;

            gradientAccumulator += shortest;

            if (gradientAccumulator >= longest)
            {
                if (inverted)
                    x += gradientStep;
                else
                    y += gradientStep;
                gradientAccumulator -= longest;
            }
        }

        return line;
    }

    public Vector3 CoordToWorldPoint(Coord tile)
    {
        if(!is2D)
            return new Vector3(-(width * squareSize) / 2 + tile.tileX * squareSize + squareSize / 2, 0,
                -(height * squareSize) / 2 + tile.tileY * squareSize + squareSize / 2);
        else
            return new Vector3(-(width * squareSize) / 2 + tile.tileX * squareSize + squareSize / 2,
                -(height * squareSize) / 2 + tile.tileY * squareSize + squareSize / 2, 0);
    }

    public Coord WorldToCoordPoint(Vector3 point)
    {
        if (!is2D)
            return new Coord(Mathf.RoundToInt((point.x + (width * squareSize) / 2 - squareSize / 2) / squareSize),
                Mathf.RoundToInt((point.z + (height * squareSize) / 2 - squareSize / 2) / squareSize));
        else
            return new Coord(Mathf.RoundToInt((point.x + (width * squareSize) / 2 - squareSize / 2) / squareSize),
                Mathf.RoundToInt((point.y + (height * squareSize) / 2 - squareSize / 2) / squareSize));
    }

    bool IsInMap(int x, int y)
    {
        return (x >= 0 && x < width && y >= 0 && y < height);
    }

    void InstantiatePlayer(Coord tile, GameObject playerPrefab)
    {
        if (player != null)
            if(Application.isPlaying)
                Destroy(player);
            else
                DestroyImmediate(player);

        player = Instantiate(playerPrefab, CoordToWorldPoint(tile) + new Vector3(0, -wallHeight + 1, 0), Quaternion.identity) as GameObject;

        if (player.GetComponentInChildren<Camera>() != null && Camera.main != null && player.GetComponentInChildren<Camera>() != Camera.main)
            Camera.main.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void SetCeilingAndFloor()
    {
        Transform ceiling = transform.FindChild("Ceiling");
        Transform floor = transform.FindChild("Floor");

        ceiling.GetComponent<MeshRenderer>().sharedMaterial.mainTextureScale = new Vector2(width / 10f, height / 10f);
        floor.GetComponent<MeshRenderer>().sharedMaterial.mainTextureScale = new Vector2(width / 10f, height / 10f);

        ceiling.localScale = new Vector3((width + 2 * borderSize) / 10.0f, 1, (height + 2 * borderSize) / 10.0f) * squareSize;
        floor.localScale = new Vector3((width + 2 * borderSize) / 10.0f, 1, (height + 2 * borderSize) / 10.0f) * squareSize;

        if (is2D)
        {
            ceiling.rotation = Quaternion.Euler(270, 0, 0);
            ceiling.position = new Vector3(0, 0, -10);
            floor.rotation = Quaternion.Euler(270, 0, 0);
            floor.position = new Vector3(0, 0, 1);
        }
        else
        {
            ceiling.position = new Vector3(0, 0, 0);
            ceiling.rotation = Quaternion.Euler(0, 0, 180);
            floor.position = new Vector3(0, -wallHeight, 0);
            floor.rotation = Quaternion.Euler(0, 0, 0);
        }
    }

    List<Passage> CreateSpawnpoints(List<Room> rooms)
    {
        List<Passage> passages = new List<Passage>();

        foreach (var wall in GameObject.FindGameObjectsWithTag("SpawnpointWall"))
        {
            if(Application.isPlaying)
                Destroy(wall);
            else
                DestroyImmediate(wall);
        }

        spawnPoints = new List<Coord>(spawnPointNumber);
        int randomSide = 0;

        for (int i = 0; i < spawnPointNumber; i++)
        {
            int x = randGen.Next(0, width);
            int y = randGen.Next(0, height);

            Coord spawnPoint = new Coord();

            if (randomSide == 0)
                spawnPoint = new Coord(x, 0);
            else if (randomSide == 1)
                spawnPoint = new Coord(x, height - 1);
            else if (randomSide == 2)
                spawnPoint = new Coord(0, y);
            else
                spawnPoint = new Coord(width - 1, y);

            randomSide = (randomSide + 1) % 4;

            if (!spawnPoints.Contains(spawnPoint))
                spawnPoints.Add(spawnPoint);
            else
            {
                i--;
                continue;
            }

            Coord nearestPoint = new Coord();
            Room nearestRoom = new Room();
            float smallestDistance = 0;
            foreach (var room in rooms)
	        {
                foreach (var tile in room.edgeTiles)
                {
                    float distance = Mathf.Pow(spawnPoint.tileX - tile.tileX, 2) +
                        Mathf.Pow(spawnPoint.tileY - tile.tileY, 2);
                    if (distance < smallestDistance || smallestDistance == 0)
                    {
                        smallestDistance = distance;
                        nearestPoint = tile;
                        nearestRoom = room;
                    }
                }
	        }

            List<Coord> path = GetLine(spawnPoint, nearestPoint);

            int l = path.Count;
            if (l > 5)
                InstantiateSpawnpointWallBetween(path[2], path[3]);
            else if (l > 4)
                InstantiateSpawnpointWallBetween(path[1], path[2]);
            else if (l > 3)
                InstantiateSpawnpointWallBetween(path[0], path[1]);
            else
                InstantiateSpawnpointWallBetween(spawnPoint, nearestPoint);

            Passage passage = CreatePassage(spawnPoint, nearestPoint, path);
            passages.Add(passage);
            nearestRoom.AddPassage(passage);
        }

        return passages;
    }

    void InstantiateSpawnpointWallBetween(Coord tileA, Coord tileB)
    {
        Vector3 pos = new Vector3();
        Quaternion rot = new Quaternion();

        if (tileB.tileX == tileA.tileX + 1 && tileB.tileY == tileA.tileY)         //right
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(squareSize / 2, -wallHeight / 2, 0);
            rot = Quaternion.Euler(0, 0, 0);
        }
        else if (tileB.tileX == tileA.tileX - 1 && tileB.tileY == tileA.tileY)    //left
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(-squareSize / 2, -wallHeight / 2, 0);
            rot = Quaternion.Euler(0, 0, 0);
        }
        else if (tileB.tileX == tileA.tileX && tileB.tileY == tileA.tileY + 1)    //up
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(0, -wallHeight / 2, squareSize / 2);
            rot = Quaternion.Euler(0, 90, 0);
        }
        else if (tileB.tileX == tileA.tileX && tileB.tileY == tileA.tileY - 1)    //down
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(0, -wallHeight / 2, -squareSize / 2);
            rot = Quaternion.Euler(0, 90, 0);
        }
        else if (tileB.tileX == tileA.tileX + 1 && tileB.tileY == tileA.tileY + 1)         //up right
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(squareSize / 2, -wallHeight / 2, squareSize / 2);
            rot = Quaternion.Euler(0, -45, 0);
        }
        else if (tileB.tileX == tileA.tileX - 1 && tileB.tileY == tileA.tileY + 1)    //up left
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(-squareSize / 2, -wallHeight / 2, squareSize / 2);
            rot = Quaternion.Euler(0, 45, 0);
        }
        else if (tileB.tileX == tileA.tileX + 1 && tileB.tileY == tileA.tileY - 1)    //down right
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(squareSize / 2, -wallHeight / 2, squareSize / 2);
            rot = Quaternion.Euler(0, 45, 0);
        }
        else if (tileB.tileX == tileA.tileX - 1 && tileB.tileY == tileA.tileY - 1)    //down left
        {
            pos = CoordToWorldPoint(tileA) + new Vector3(-squareSize / 2, -wallHeight / 2, -squareSize / 2);
            rot = Quaternion.Euler(0, -45, 0);
        }
        else
            Debug.Log(tileB.tileX + ", " + tileB.tileY);

        GameObject wall = Instantiate(spawnpointWall, pos, rot) as GameObject;
        wall.transform.parent = transform.FindChild("SpawnpointWalls");
        wall.transform.localScale = new Vector3(0.01f, wallHeight, 3 * passagewayRadius * squareSize); 
    }

    public List<Vector3> GetSpawnPoints()
    {
        return spawnPoints.Select(x => CoordToWorldPoint(x) + new Vector3(0, -wallHeight + 1, 0)).ToList<Vector3>();
    }

    public class Room : IComparable<Room>
    {
        #region variables
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public List<Passage> passages;
        public int size;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom;
        #endregion

        public Room() { }

        public Room(List<Coord> tiles, int[,] map, MapGenerator mapGen)
        {
            this.tiles = tiles;
            this.size = tiles.Count;
            this.edgeTiles = new List<Coord>();
            this.connectedRooms = new List<Room>();
            this.passages = new List<Passage>();
            this.isAccessibleFromMainRoom = false;
            this.isMainRoom = false;

            int randomAdd = 0;
            int randomAddModulo = Mathf.CeilToInt(2 * Mathf.Sqrt(mapGen.smallestRoomRegion));

            foreach (var tile in tiles)
            {
                bool edgeTileAdded = false;
                for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
                {
                    for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                    {
                        if (!edgeTileAdded && (x == tile.tileX || y == tile.tileY) && map[x, y] == 1)
                        {
                            if(randomAdd == 0)
                                edgeTiles.Add(tile);
                            edgeTileAdded = true;
                            randomAdd = (randomAdd + 1) % randomAddModulo;
                        }
                    }
                }
            }
        }

        public static void ConnectRooms(Room roomA, Room roomB, Passage passage)
        {
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);

            roomA.passages.Add(passage);
            roomB.passages.Add(passage);

            if (roomA.isAccessibleFromMainRoom)
                roomB.SetAccessibleFromMainRoom();
            else if(roomB.isAccessibleFromMainRoom)
                roomA.SetAccessibleFromMainRoom();
        }

        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        public Coord RandomPointInside()
        {
            int i = UnityEngine.Random.Range(0, tiles.Count);

            while(edgeTiles.Contains(tiles[i]))
                i = UnityEngine.Random.Range(0, tiles.Count);

            return tiles[i];
        }

        public void SetAccessibleFromMainRoom()
        {
            if (!isAccessibleFromMainRoom)
                isAccessibleFromMainRoom = true;

            foreach (var room in connectedRooms)
            {
                room.isAccessibleFromMainRoom = true;
            }
        }

        public int CompareTo(Room other)
        {
            return other.size.CompareTo(this.size);
        }

        public void AddPassage(Passage passage)
        {
            passages.Add(passage);
        }
    }

    public struct Coord
    {
        #region variables
        public int tileX;
        public int tileY;
        #endregion

        public Coord(int tileX, int tileY)
        {
            this.tileX = tileX;
            this.tileY = tileY;
        }

        public override string ToString()
        {
            return "(" + tileX + ", " + tileY + ")";
        }

        public bool NextTo(Coord other)
        {
            return Math.Abs(this.tileX - other.tileX) <= 1 && Math.Abs(this.tileY - other.tileY) <= 1;
        }
    }

    public class Passage
    {
        #region variables
        public Coord startTile;
        public Coord endTile;
        public List<Coord> tiles;
        #endregion

        public Passage(Coord start, Coord end, List<Coord> tiles)
        {
            this.startTile = start;
            this.endTile = end;
            this.tiles = tiles;
        }        
    }
}
