using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MapGen : MonoBehaviour {
    public int width;
    public int height;

    public string seed;
    public bool UseRandomSeed;

    public int AutomataSteps;

    [Range(0, 100)]
    public int RandomFillPercent;

    public bool LimitRoomSize = false;
    public int ThresholdSize = 12;

    public int CorridorWidth = 3;

    public GameObject WallCube;
    int[,] map;

    void Start() {
        GenerateMap();
        DrawWalls();
    }

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            GenerateMap();
            DrawWalls();
        }
    }

    void GenerateMap() {
        map = new int[width, height];
        RandomFillMap();

        for (int i = 0; i < AutomataSteps; i++) {
            SmoothMap();
        }

        RegionProcesses();

        EdgeDetection();
        RemoveNonEdge();
    }

    void RegionProcesses() {
        List<List<Vector2Int>> typedRegions = GetRegions(LimitRoomSize);

        List<Room> survivingRooms = new();

        foreach (List<Vector2Int> typedRegion in typedRegions) {
            if (typedRegion.Count < ThresholdSize) {
                foreach (Vector2Int tile in typedRegion) {
                    map[tile.x, tile.y] = (map[tile.x, tile.y] == 1) ? 0 : 1;
                }
            } else {
                survivingRooms.Add(new Room(typedRegion, map));
            }
        }
        ConnectClosestRooms(survivingRooms);
    }

    void ConnectClosestRooms(List<Room> allRooms) {
        int bestDistance = 0;
        Vector2Int bestTileA = new();
        Vector2Int bestTileB = new();
        Room bestRoomA = new();
        Room bestRoomB = new();

        bool possibleConnectionFound = false;

        foreach (Room roomA in allRooms) {
            possibleConnectionFound = false;
            foreach (Room roomB in allRooms) {
                if (roomA == roomB) continue; // don't bother linking to the same room
                if (roomA.IsConnected(roomB)) { // if roomA's already connnected there's no need to look for roomB
                    possibleConnectionFound = false;
                    break;
                }
                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++) {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++) {
                        Vector2Int tileA = roomA.edgeTiles[tileIndexA];
                        Vector2Int tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)Mathf.Pow(tileA.x - tileB.x, 2) + (int)Mathf.Pow(tileA.y - tileB.y, 2);

                        if (distanceBetweenRooms < bestDistance || !possibleConnectionFound) {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }
            if (possibleConnectionFound) {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }
    }

    void CreatePassage(Room roomA, Room roomB, Vector2Int tileA, Vector2Int tileB) {
        Room.ConnectRooms(roomA, roomB);
        // Debug.DrawLine(Vec2ToWorldPoint(tileA), Vec2ToWorldPoint(tileB), Color.cyan, 120);
        int dx = tileA.x - tileB.x;
        int dy = tileA.y - tileB.y;
        float steps;
        if (Math.Abs(dx) > Math.Abs(dy)) {
            steps = Math.Abs(dx);
        } else {
            steps = Math.Abs(dy);
        }
        float Xinc = dx / steps;
        float Yinc = dy / steps;

        float x = tileB.x, y = tileB.y;
        for (int i = 0; i <= steps + 1; i++) {
            x += Xinc;
            y += Yinc;
            map[Mathf.CeilToInt(x), Mathf.CeilToInt(y)] = 0;
            map[Mathf.CeilToInt(x) + 1, Mathf.CeilToInt(y)] = 0;
            map[Mathf.CeilToInt(x) - 1, Mathf.CeilToInt(y)] = 0;
            map[Mathf.CeilToInt(x), Mathf.CeilToInt(y) + 1] = 0;
            map[Mathf.CeilToInt(x), Mathf.CeilToInt(y) - 1] = 0;
        }
    }

    Vector3 Vec2ToWorldPoint(Vector2Int tile) {
        return new Vector3(-width/2 + .5f + tile.x, 2, -height/2 + .5f + tile.y);
    }

    List<List<Vector2Int>> GetRegions(bool tileType) {
        List<List<Vector2Int>> regions = new();
        bool[,] mapFlags = new bool[width,height]; // true if we've examined this tile before

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (!mapFlags[x, y] && (map[x, y] == 1) == tileType) {
                    List<Vector2Int> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (Vector2Int tile in newRegion) {
                        mapFlags[tile.x, tile.y] = true;
                    }
                } 
            }
        }
        return regions;
    }

    List<Vector2Int> GetRegionTiles(int startX, int startY) {
        List<Vector2Int> tiles = new();
        bool[,] mapFlags = new bool[width,height]; // true if we've examined this tile before
        bool tileType = (map[startX, startY] == 1);

        Queue<Vector2Int> queue = new();
        queue.Enqueue(new Vector2Int(startX, startY));
        mapFlags[startX, startY] = true;

        while (queue.Count > 0) {
            Vector2Int tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.x - 1; x <= tile.x + 1; x++) {
                for (int y = tile.y - 1; y <= tile.y + 1; y++) {
                    if (IsInMapRange(x, y) && ( y == tile.y || x == tile.x )) {
                        if (!mapFlags[x, y] && (map[x, y] == 1) == tileType) {
                            mapFlags[x, y] = true;
                            queue.Enqueue(new Vector2Int(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    bool IsInMapRange(int x, int y) {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    void RandomFillMap() {
        if (UseRandomSeed) {
            seed = Time.time.ToString();
        }

        System.Random pseudoRandom = new(seed.GetHashCode());

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1) {
                    map[x, y] = 1;
                } else {
                    map[x, y] = (pseudoRandom.Next(0, 100) < RandomFillPercent) ? 1 : 0;
                }
            }
        }
    }

    void SmoothMap() {
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                int neighborWallTiles = GetSurroundingWallCount(x, y);

                // the rules of the automata
                
                if (neighborWallTiles > 4) {
                    map[x, y] = 1;
                } else if (neighborWallTiles < 4) {
                    map[x, y] = 0;
                }
                /*
                if (map[x, y] == 1 && neighborWallTiles < 2) {
                    map[x, y] = 0;
                } else if (map[x, y] == 1 && neighborWallTiles > 3) {
                    map[x, y] = 0;
                } else if (map[x, y] == 0 && neighborWallTiles == 3) {
                    map[x, y] = 1;
                }
                */
            }
        }
    }

    void EdgeDetection() {
        for (int x = 1; x < width - 1; x++) {
            for (int y = 1; y < height - 1; y++) {
                if (GetSurroundingWallCount(x, y) == 8) {
                    map[x, y] = -1;
                }
            }
        }
    }

    void RemoveNonEdge() {
        for (int x = 1; x < width - 1; x++) {
            for (int y = 1; y < height - 1; y++) {
                if (map[x, y] == -1) {
                    map[x, y] = 0;
                } 
            }
        }
    }

    int GetSurroundingWallCount(int gridX, int gridY) {
        int wallCount = 0;
        for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++) {
            for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++) {
                if (IsInMapRange(neighborX, neighborY)) {
                    if (neighborX != gridX || neighborY != gridY) {
                        wallCount += (map[neighborX, neighborY] == -1) ? 1 : map[neighborX, neighborY];
                    }
                } else {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }

    void DrawWalls() {
        if (map != null) {
            GameObject[] allWalls = GameObject.FindGameObjectsWithTag("Wall");
            foreach (GameObject w in allWalls) {
                if (w.transform.name.Contains("(Clone)"))
                    Destroy(w);
            }
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    /*
                    Gizmos.color = (map[x, y] == 1) ? Color.black : Color.white;
                    Vector3 pos = new Vector3(-width/2 + x + .5f, 0, -height/2 + y + .5f);
                    Gizmos.DrawCube(pos, Vector3.one);
                    */
                    if (map[x, y] == 1) {
                        GameObject wall = Instantiate<GameObject>(WallCube);
                        Vector3 pos = new(-width/2 + x + .5f, 0, -height/2 + y + .5f);
                        wall.transform.position = pos;
                    }
                }
            }
        }
    }

    class Room {
        public List<Vector2Int> tiles;
        public List<Vector2Int> edgeTiles;
        public List<Room> adjacentRooms;
        public int roomSize;

        public Room() {}

        public Room(List<Vector2Int> roomTiles, int[,] map) {
            tiles = roomTiles;
            roomSize = tiles.Count;
            adjacentRooms = new List<Room>();

            edgeTiles = new List<Vector2Int>();
            foreach (Vector2Int tile in tiles) {
                for (int x = tile.x - 1; x <= tile.x + 1; x++) {
                    for (int y = tile.y - 1; y <= tile.y + 1; y++) {
                        if (x == tile.x || y == tile.y) {
                            if (map[x, y] == 1) {
                                edgeTiles.Add(tile);
                            }
                        }
                    }
                }
            }
        }

        public static void ConnectRooms(Room roomA, Room roomB) {
            roomA.adjacentRooms.Add(roomB);
            roomB.adjacentRooms.Add(roomA);
        }

        public bool IsConnected(Room otherRoom) {
            return adjacentRooms.Contains(otherRoom);
        }
    }
}
