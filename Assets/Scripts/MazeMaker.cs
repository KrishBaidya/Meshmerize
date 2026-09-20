using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(MazeMakerMono))]
public class MazeMaker : Editor
{
    // Full grid is (2n+1) x (2n+1). Rooms sit on ODD indices, the cells between
    // them on EVEN indices. Carved cells become the walkable floor; every other
    // plane is destroyed, so the path is what remains.
    const int RoomsWide = 5;
    const int RoomsHigh = 5;
    const int GridWidth = RoomsWide * 2 + 1;   // 21
    const int GridHeight = RoomsHigh * 2 + 1;  // 21
    const float CellSize = 10f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var mazeMaker = (MazeMakerMono)target;

        if (!GUILayout.Button("Generate Maze")) return;

        var existing = GameObject.Find("Maze");
        if (existing != null) DestroyImmediate(existing);

        var mazeParent = new GameObject("Maze");

        // ---- Build the full grid -------------------------------------------
        // Fill order must match Index(): x outer, y inner => index = x * GridHeight + y
        mazeMaker.Planes.Clear();
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                var plane = (GameObject)PrefabUtility.InstantiatePrefab(mazeMaker.plane_prefab);
                plane.AddComponent<BoxCollider>();
                plane.transform.position = CellToWorld(x, y);
                plane.transform.parent = mazeParent.transform;
                mazeMaker.Planes.Add(plane);
            }
        }

        // ---- Recursive-backtracker DFS over room coordinates ---------------
        // Nothing is destroyed here; cells are only flagged as part of the path.
        var isPath = new bool[GridWidth, GridHeight];
        var visited = new bool[RoomsWide, RoomsHigh];
        var directions = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        var stack = new Stack<(int x, int y)>();

        stack.Push((0, 0));
        visited[0, 0] = true;
        isPath[RoomToCell(0), RoomToCell(0)] = true;

        var neighbors = new List<(int x, int y)>(4);

        while (stack.Count > 0)
        {
            var (rx, ry) = stack.Peek();

            neighbors.Clear();
            foreach (var d in directions)
            {
                int nx = rx + d.dx;
                int ny = ry + d.dy;
                if (nx >= 0 && nx < RoomsWide && ny >= 0 && ny < RoomsHigh && !visited[nx, ny])
                    neighbors.Add((nx, ny));
            }

            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var (nrx, nry) = neighbors[Random.Range(0, neighbors.Count)];
            visited[nrx, nry] = true;
            stack.Push((nrx, nry));

            int fromX = RoomToCell(rx), fromY = RoomToCell(ry);
            int toX = RoomToCell(nrx), toY = RoomToCell(nry);

            isPath[toX, toY] = true;                                // the new room
            isPath[(fromX + toX) / 2, (fromY + toY) / 2] = true;    // the link between
        }

        // ---- Entrance / exit tiles leading off the edge ---------------------
        var startRoom = (x: 0, y: 0);
        var endRoom = (x: RoomsWide - 1, y: RoomsHigh - 1);

        ExtendToBoundary(isPath, startRoom.x, startRoom.y);
        ExtendToBoundary(isPath, endRoom.x, endRoom.y);

        // ---- Destroy everything that is not path ---------------------------
        for (int x = 0; x < GridWidth; x++)
            for (int y = 0; y < GridHeight; y++)
                if (!isPath[x, y])
                    RemovePlaneAt(mazeMaker, x, y);

        // ---- Tint the two endpoint tiles ------------------------------------
        TintPlaneAt(mazeMaker, RoomToCell(startRoom.x), RoomToCell(startRoom.y), Color.green);
        TintPlaneAt(mazeMaker, RoomToCell(endRoom.x), RoomToCell(endRoom.y), Color.blue);

        Debug.Log($"Maze generated. Start room: {startRoom}, End room: {endRoom}");
    }

    // Single source of truth for grid -> list index. Must match the fill order above.
    private static int Index(int x, int y) => x * GridHeight + y;

    // Rooms live on odd grid indices: room 0 -> cell 1, room 9 -> cell 19.
    private static int RoomToCell(int room) => room * 2 + 1;

    private static Vector3 CellToWorld(int x, int y, float height = 0f)
        => new Vector3(x * CellSize, height, y * CellSize);

    private static void RemovePlaneAt(MazeMakerMono mazeMaker, int x, int y)
    {
        if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;

        int i = Index(x, y);
        if (i >= 0 && i < mazeMaker.Planes.Count && mazeMaker.Planes[i] != null)
        {
            TintPlaneAt(mazeMaker, x, y, Color.black);
            //DestroyImmediate(mazeMaker.Planes[i]);
            //mazeMaker.Planes[i] = null;
        }
    }

    private static void TintPlaneAt(MazeMakerMono mazeMaker, int x, int y, Color color)
    {
        if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;

        int i = Index(x, y);
        if (i < 0 || i >= mazeMaker.Planes.Count || mazeMaker.Planes[i] == null) return;

        var renderer = mazeMaker.Planes[i].GetComponent<Renderer>();
        if (renderer == null) return;

        // Give this tile its own material so the tint doesn't bleed onto the prefab.
        var mat = new Material(renderer.sharedMaterial) { color = color };
        renderer.sharedMaterial = mat;
    }

    // Rooms never touch the outer edge, so each boundary room has a spare cell
    // outside it that can become a tile leading off the maze.
    private static void ExtendToBoundary(bool[,] isPath, int roomX, int roomY)
    {
        int cellX = RoomToCell(roomX);
        int cellY = RoomToCell(roomY);

        if (roomX == 0) isPath[cellX - 1, cellY] = true;
        if (roomX == RoomsWide - 1) isPath[cellX + 1, cellY] = true;
        if (roomY == 0) isPath[cellX, cellY - 1] = true;
        if (roomY == RoomsHigh - 1) isPath[cellX, cellY + 1] = true;
    }
}