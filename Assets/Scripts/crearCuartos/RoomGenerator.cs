using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

// Superficie donde se puede caminar. Coordenadas locales del cuarto (0,0 = esquina inferior izquierda).
// y = fila del tile de la superficie; el jugador se para en la fila y + 1.
public struct Surface
{
    public int xMin, xMax, y;
}

public class RoomData
{
    public int index;
    public Vector3Int originCell;
    public int width;
    public int entryRow;   // fila local donde se para el jugador al cruzar la puerta izquierda
    public int exitRow;    // fila local donde se para el jugador al cruzar la puerta derecha
    public Transform root;
    public CinemachineCamera cam;
    public RoomEntryTrigger entryTrigger;

    // Celda mundo donde se pega el siguiente cuarto (esquina inferior izquierda = origen + (width, exitRow - nextEntryRow)).
    public Vector3Int NextOrigin(int nextEntryRow)
    {
        return new Vector3Int(originCell.x + width, originCell.y + exitRow - nextEntryRow, 0);
    }
}

public class RoomGenerator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RoomGenConfig config;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap platformTilemap;
    [SerializeField] private CinemachineCamera roomCameraPrefab;
    [SerializeField] private Transform roomsParent;

    [Header("Parámetros del Autómata Celular (Bordes Orgánicos)")]
    [Range(0, 100)] [SerializeField] private int shellNoisePercent = 45;
    [SerializeField] private int cellularAutomataIterations = 3;

    [Header("Origen del primer cuarto (celda)")]
    [SerializeField] private Vector3Int startOrigin = Vector3Int.zero;

    [Header("Pruebas")]
    [SerializeField] private int testSeed = 1;

    [ContextMenu("Generar cuarto de prueba")]
    private void GenerateTestRoom()
    {
        Generate(0, testSeed, null);
    }

    [ContextMenu("Generar 3 cuartos de prueba")]
    private void GenerateTestSequence()
    {
        RoomData prev = null;
        for (int i = 0; i < 3; i++)
            prev = Generate(i, testSeed + i, prev);
    }

    [ContextMenu("Limpiar Tilemaps")]
    public void ClearAll()
    {
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (platformTilemap != null) platformTilemap.ClearAllTiles();

        if (roomsParent != null)
        {
            for (int i = roomsParent.childCount - 1; i >= 0; i--)
            {
                Transform child = roomsParent.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
        Debug.Log("[RoomGenerator] Se han limpiado los Tilemaps y destruido los objetos.");
    }

    public RoomData Generate(int index, int seed, RoomData previous)
    {
        var c = config;
        var rng = new Random(seed);

        int entryRow = previous == null ? c.floorThickness : PickDoorRow(c, rng);
        int exitRow = PickDoorRow(c, rng);

        Vector3Int origin = previous == null ? startOrigin : previous.NextOrigin(entryRow);

        var data = new RoomData
        {
            index = index, originCell = origin, width = c.width, entryRow = entryRow, exitRow = exitRow
        };

        var rootGO = new GameObject($"Room_{index}");
        rootGO.transform.SetParent(roomsParent, false);
        data.root = rootGO.transform;

        // 1. Dibujar cascarón orgánico usando Autómata Celular
        PaintOrganicShell(c, origin, entryRow, exitRow, rng);

        // 2. Generar las plataformas mediante el Agente Caminante
        List<Surface> walkable = BuildLayout(c, origin, rng, entryRow, exitRow);

        // data.cam = CreateCamera(c, origin, data.root, previous == null);
        // data.entryTrigger = CreateEntryTrigger(c, origin, data, previous);

        return data;
    }

    private static int PickDoorRow(RoomGenConfig c, Random rng)
    {
        int row = c.doorRows[rng.Next(c.doorRows.Length)];
        return Mathf.Clamp(row, c.floorThickness, c.height - c.ceilingThickness - c.doorHeight);
    }

    // ---------- Cascarón Orgánico (Autómata Celular) ----------

    private void PaintOrganicShell(RoomGenConfig c, Vector3Int origin, int entryRow, int exitRow, Random rng)
    {
        int[,] grid = new int[c.width, c.height];

        // Rellenar bordes con ruido
        for (int x = 0; x < c.width; x++)
        {
            for (int y = 0; y < c.height; y++)
            {
                // Zona exterior estricta (Muro indestructible exterior)
                if (x == 0 || x == c.width - 1 || y == 0 || y == c.height - 1)
                {
                    grid[x, y] = 1;
                    continue;
                }

                // Zona de los bordes del cascarón (Paredes, Techo, Suelo)
                bool isShellZone = x < c.wallThickness + 2 || x >= c.width - c.wallThickness - 2 ||
                                   y < c.floorThickness + 1 || y >= c.height - c.ceilingThickness - 1;

                if (isShellZone)
                {
                    grid[x, y] = (rng.Next(0, 100) < shellNoisePercent) ? 1 : 0;
                }
                else
                {
                    grid[x, y] = 0; // Interior totalmente despejado para el gameplay
                }

                // Asegurar grosor mínimo de suelo y techo
                if (y < c.floorThickness - 1 || y >= c.height - (c.ceilingThickness - 1))
                {
                    grid[x, y] = 1;
                }
            }
        }

        // Suavizado por Autómata Celular en la zona del cascarón
        for (int it = 0; it < cellularAutomataIterations; it++)
        {
            int[,] temp = (int[,])grid.Clone();
            for (int x = 1; x < c.width - 1; x++)
            {
                for (int y = 1; y < c.height - 1; y++)
                {
                    // No tocar la zona central para no obstruir
                    if (x > c.wallThickness + 2 && x < c.width - c.wallThickness - 3 &&
                        y > c.floorThickness + 1 && y < c.height - c.ceilingThickness - 2) continue;

                    int neighbors = GetNeighbors(grid, x, y, c.width, c.height);
                    if (neighbors > 4) temp[x, y] = 1;
                    else if (neighbors < 4) temp[x, y] = 0;
                }
            }
            grid = temp;
        }

        // Abrir puertas (Espacio aéreo forzado para el paso del jugador)
        for (int y = entryRow; y < entryRow + c.doorHeight; y++)
        {
            for (int x = 0; x <= c.wallThickness + 1; x++) grid[x, y] = 0;
        }
        for (int y = exitRow; y < exitRow + c.doorHeight; y++)
        {
            for (int x = c.width - c.wallThickness - 2; x < c.width; x++) grid[x, y] = 0;
        }

        // Renderizar los tiles sólidos
        for (int x = 0; x < c.width; x++)
        {
            for (int y = 0; y < c.height; y++)
            {
                if (grid[x, y] == 1)
                {
                    groundTilemap.SetTile(origin + new Vector3Int(x, y, 0), c.solidTile);
                }
            }
        }
    }

    private int GetNeighbors(int[,] grid, int x, int y, int width, int height)
    {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        {
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) count++;
                else count += grid[nx, ny];
            }
        }
        return count;
    }

    // ---------- Plataformas (Agente Excavador) ----------

    private List<Surface> BuildLayout(RoomGenConfig c, Vector3Int origin, Random rng, int entryRow, int exitRow)
    {
        int floorTop = c.floorThickness - 1;
        var floor = new Surface { xMin = c.wallThickness, xMax = c.width - c.wallThickness - 1, y = floorTop };
        var entryPad = new Surface { xMin = c.wallThickness, xMax = c.wallThickness + c.ledgeLength - 1, y = entryRow - 1 };
        var exitPad = new Surface { xMin = c.width - c.wallThickness - c.ledgeLength, xMax = c.width - c.wallThickness - 1, y = exitRow - 1 };

        var walkable = new List<Surface> { floor };
        var obstacles = new List<Surface>();
        Tilemap platMap = platformTilemap != null ? platformTilemap : groundTilemap;

        if (entryRow > c.floorThickness)
        {
            PaintSurface(groundTilemap, c.solidTile, origin, entryPad);
            walkable.Add(entryPad); obstacles.Add(entryPad);
        }
        if (exitRow > c.floorThickness)
        {
            PaintSurface(groundTilemap, c.solidTile, origin, exitPad);
            walkable.Add(exitPad); obstacles.Add(exitPad);
        }

        // Generar la cadena principal mediante Agente
        if (entryRow > c.floorThickness || exitRow > c.floorThickness)
        {
            List<Surface> main = TryChain(c, rng, entryPad, exitPad, obstacles);
            if (main != null)
            {
                AddAll(main, c, origin, platMap, walkable, obstacles);

                Surface lowest = entryPad.y <= exitPad.y ? entryPad : exitPad;
                foreach (var s in main) if (s.y < lowest.y) lowest = s;

                if (lowest.y - floorTop > c.maxJumpHeight - 1)
                {
                    List<Surface> back = TryChain(c, rng, lowest, floor, obstacles);
                    if (back != null) AddAll(back, c, origin, platMap, walkable, obstacles);
                }
            }
        }

        // Plataformas extra
        int extra = rng.Next(c.minPlatforms, c.maxPlatforms + 1);
        int yMin = floorTop + c.playerHeight + 1;
        int yMax = c.height - c.ceilingThickness - 1 - c.playerHeight;

        for (int i = 0, placed = 0; i < c.placementAttempts && placed < extra; i++)
        {
            int len = rng.Next(c.platformLength.x, c.platformLength.y + 1);
            int xLo = c.wallThickness + 1;
            int xHi = c.width - c.wallThickness - 1 - len;
            if (xHi < xLo || yMax < yMin) break;

            var s = new Surface { xMin = rng.Next(xLo, xHi + 1), y = rng.Next(yMin, yMax + 1) };
            s.xMax = s.xMin + len - 1;

            if (Overlaps(c, s, obstacles) || !IsReachable(c, s, walkable)) continue;

            AddAll(new List<Surface> { s }, c, origin, platMap, walkable, obstacles);
            placed++;
        }

        return walkable;
    }

    private void AddAll(List<Surface> list, RoomGenConfig c, Vector3Int origin, Tilemap map, List<Surface> walkable, List<Surface> obstacles)
    {
        foreach (var s in list)
        {
            PaintSurface(map, c.platformTile, origin, s);
            walkable.Add(s);
            obstacles.Add(s);
        }
    }

    private static void PaintSurface(Tilemap map, TileBase tile, Vector3Int origin, Surface s)
    {
        for (int x = s.xMin; x <= s.xMax; x++)
            map.SetTile(origin + new Vector3Int(x, s.y, 0), tile);
    }

    private static List<Surface> TryChain(RoomGenConfig c, Random rng, Surface from, Surface to, List<Surface> obstacles)
    {
        for (int attempt = 0; attempt < c.placementAttempts; attempt++)
        {
            var chain = new List<Surface>();
            var temp = new List<Surface>(obstacles);
            if (BuildChain(c, rng, from, to, temp, chain)) return chain;
        }
        return null;
    }

    private static bool BuildChain(RoomGenConfig c, Random rng, Surface from, Surface to, List<Surface> obstacles, List<Surface> chain)
    {
        int floorTop = c.floorThickness - 1;
        int yMin = floorTop + c.playerHeight + 1;
        int yMax = c.height - c.ceilingThickness - 1 - c.playerHeight;
        int span = c.maxJumpDistance - 1;
        int maxStep = Mathf.Max(2, c.maxJumpHeight);
        Surface cur = from;

        for (int step = 0; step < c.maxPlatforms; step++)
        {
            if (IsLinked(c, cur, to)) return true;

            bool placed = false;
            for (int attempt = 0; attempt < c.placementAttempts && !placed; attempt++)
            {
                int len = rng.Next(c.platformLength.x, c.platformLength.y + 1);

                float pRight = to.xMin >= cur.xMax ? 0.8f : (to.xMax <= cur.xMin ? 0.2f : 0.5f);
                bool goRight = rng.NextDouble() < pRight;
                int xMin = goRight
                    ? rng.Next(cur.xMax - 1, cur.xMax + span + 2)
                    : rng.Next(cur.xMin - len - span, cur.xMin - len + 2);

                int toward = to.y > cur.y ? 1 : (to.y < cur.y ? -1 : 0);
                int mag = rng.Next(1, maxStep);
                int dir = toward == 0 ? (rng.Next(2) == 0 ? 1 : -1) : (rng.NextDouble() < 0.8 ? toward : -toward);

                var s = new Surface { xMin = xMin, xMax = xMin + len - 1, y = cur.y + dir * mag };

                if (s.xMin < c.wallThickness + 1 || s.xMax > c.width - c.wallThickness - 2) continue;
                if (s.y < yMin || s.y > yMax) continue;
                if (Overlaps(c, s, obstacles)) continue;

                chain.Add(s);
                obstacles.Add(s);
                cur = s;
                placed = true;
            }
            if (!placed) return false;
        }
        return IsLinked(c, cur, to);
    }

    private static bool IsLinked(RoomGenConfig c, Surface a, Surface b)
    {
        if (Mathf.Abs(a.y - b.y) > c.maxJumpHeight - 1) return false;
        int gap = Mathf.Max(0, Mathf.Max(a.xMin, b.xMin) - Mathf.Min(a.xMax, b.xMax) - 1);
        return gap <= c.maxJumpDistance - 1;
    }

    private static bool IsReachable(RoomGenConfig c, Surface s, List<Surface> others)
    {
        foreach (var o in others)
        {
            int dy = s.y - o.y;
            if (dy > c.maxJumpHeight - 1) continue;

            int gap = Mathf.Max(0, Mathf.Max(s.xMin, o.xMin) - Mathf.Min(s.xMax, o.xMax) - 1);
            if (gap <= c.maxJumpDistance - 1) return true;
        }
        return false;
    }

    private static bool Overlaps(RoomGenConfig c, Surface s, List<Surface> obstacles)
    {
        foreach (var p in obstacles)
        {
            bool xOverlap = s.xMin <= p.xMax + 2 && s.xMax >= p.xMin - 2;
            if (xOverlap && Mathf.Abs(s.y - p.y) <= c.playerHeight + 1) return true;
        }
        return false;
    }
}