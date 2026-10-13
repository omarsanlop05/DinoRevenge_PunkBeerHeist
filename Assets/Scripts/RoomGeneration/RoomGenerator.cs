using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

public class RoomGenerator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RoomGenConfig config;
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("Opcional. Si lo dejas vacío, las plataformas se pintan en el Tilemap de suelo")]
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
        {
            prev = Generate(i, testSeed + i, prev);
            Debug.Log("Creando cuarto " + i);
        }
    }

    [ContextMenu("Limpiar Tilemaps")]
    public void ClearAll()
    {
        // 1. Limpiar los tiles de los Tilemaps
        if (groundTilemap != null)
        {
            groundTilemap.ClearAllTiles();
        }

        if (platformTilemap != null)
        {
            platformTilemap.ClearAllTiles();
        }

        // 2. Destruir todos los GameObjects de los cuartos generados (cámaras, triggers, enemigos)
        if (roomsParent != null)
        {
            // Se itera en reversa para evitar problemas al eliminar elementos de la jerarquía
            for (int i = roomsParent.childCount - 1; i >= 0; i--)
            {
                Transform child = roomsParent.GetChild(i);

                // Usa DestroyImmediate si se ejecuta desde el Editor (fuera de Play Mode), o Destroy si está en Play Mode
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        Debug.Log("[RoomGenerator] Se han limpiado los Tilemaps y destruido los objetos de cuartos anteriores.");
    }

    // previous = cuarto anterior (null para el primero). El origen se calcula solo para que las puertas
    // coincidan en altura, aunque un cuarto termine arriba y el siguiente empiece abajo.
    public RoomData Generate(int index, int seed, RoomData previous)
    {
        var c = config;
        var rng = new Random(seed);

        int entryRow = previous == null ? c.floorThickness : PickDoorRow(c, rng);
        int exitRow = PickDoorRow(c, rng);

        if (entryRow == exitRow && entryRow <= c.floorThickness + 2)
        {
            exitRow = c.doorRows[rng.Next(1, c.doorRows.Length)];
        }

        Vector3Int origin = previous == null ? startOrigin : previous.NextOrigin(entryRow);

        var data = new RoomData
        {
            index = index, originCell = origin, width = c.width, entryRow = entryRow, exitRow = exitRow
        };

        var rootGO = new GameObject($"Room_{index}");
        rootGO.transform.SetParent(roomsParent, false);
        data.root = rootGO.transform;

        // Level layout
        //PaintShell(c, origin, entryRow, exitRow);
        PaintOrganicShell(c, origin, entryRow, exitRow, rng);
        
        // Enemies
        List<Surface> walkable = BuildLayout(c, origin, rng, entryRow, exitRow);
        //SpawnEnemies(c, origin, walkable, rng, data.root);

        // Camera
        data.cam = CreateCamera(c, origin, data.root, previous == null);
        data.entryTrigger = CreateEntryTrigger(c, origin, data, previous);

        return data;
    }

    private static int PickDoorRow(RoomGenConfig c, Random rng)
    {
        int row = c.doorRows[rng.Next(c.doorRows.Length)];
        return Mathf.Clamp(row, c.floorThickness, c.height - c.ceilingThickness - c.doorHeight);
    }

    // ---------- Cascarón ----------

    private void PaintShell(RoomGenConfig c, Vector3Int origin, int entryRow, int exitRow)
    {
        for (int x = 0; x < c.width; x++)
        {
            for (int y = 0; y < c.height; y++)
            {
                bool left = x < c.wallThickness;
                bool right = x >= c.width - c.wallThickness;
                bool inSideWall = left || right;
                bool shell = inSideWall || y < c.floorThickness || y >= c.height - c.ceilingThickness;

                int doorRow = left ? entryRow : exitRow;
                bool door = inSideWall && y >= doorRow && y < doorRow + c.doorHeight;

                if (shell && !door)
                    groundTilemap.SetTile(origin + new Vector3Int(x, y, 0), c.solidTile);
            }
        }
    }

    private void PaintOrganicShell(RoomGenConfig c, Vector3Int origin, int entryRow, int exitRow, Random rng)
    {
        int[,] grid = new int[c.width, c.height];

        // Paso A: Inicializar matriz con ruido en la franja del cascarón
        for (int x = 0; x < c.width; x++)
        {
            for (int y = 0; y < c.height; y++)
            {
                // Borde exterior absoluto (Límite infranqueable de la sala)
                if (x == 0 || x == c.width - 1 || y == 0 || y == c.height - 1)
                {
                    grid[x, y] = 1;
                    continue;
                }

                // Zona donde operará el autómata celular (muros, techo y suelo)
                bool isShellZone = x < c.wallThickness + 2 || x >= c.width - c.wallThickness - 2 ||
                                y < c.floorThickness + 1 || y >= c.height - c.ceilingThickness - 1;

                if (isShellZone)
                {
                    grid[x, y] = (rng.Next(0, 100) < shellNoisePercent) ? 1 : 0;
                }
                else
                {
                    grid[x, y] = 0; // Centro libre para las plataformas y el gameplay
                }

                // Mantenemos una capa base sólida en la parte inferior y superior
                if (y < c.floorThickness - 1 || y >= c.height - (c.ceilingThickness - 1))
                {
                    grid[x, y] = 1;
                }
            }
        }

        // Paso B: Aplicar reglas del Autómata Celular (Suavizado)
        for (int it = 0; it < cellularAutomataIterations; it++)
        {
            int[,] temp = (int[,])grid.Clone();
            for (int x = 1; x < c.width - 1; x++)
            {
                for (int y = 1; y < c.height - 1; y++)
                {
                    // Salta el área de juego principal para no bloquear pasos
                    if (x > c.wallThickness + 2 && x < c.width - c.wallThickness - 3 &&
                        y > c.floorThickness + 1 && y < c.height - c.ceilingThickness - 2) continue;

                    int neighbors = GetNeighbors(grid, x, y, c.width, c.height);
                    if (neighbors > 4) temp[x, y] = 1;
                    else if (neighbors < 4) temp[x, y] = 0;
                }
            }
            grid = temp;
        }

        // Paso C: Despejar marcos de las puertas
        for (int y = entryRow; y < entryRow + c.doorHeight; y++)
        {
            for (int x = 0; x <= c.wallThickness + 1; x++) grid[x, y] = 0;
        }
        for (int y = exitRow; y < exitRow + c.doorHeight; y++)
        {
            for (int x = c.width - c.wallThickness - 2; x < c.width; x++) grid[x, y] = 0;
        }

        // Paso D: Renderizar en el Tilemap
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

    // Método auxiliar de Autómata Celular (Vecindario de Moore)
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

    // ---------- Plataformas ----------

    private List<Surface> BuildLayout(RoomGenConfig c, Vector3Int origin, Random rng, int entryRow, int exitRow)
    {
        int floorTop = c.floorThickness - 1;
        var entryPad = new Surface { xMin = c.wallThickness, xMax = c.wallThickness + c.ledgeLength - 1, y = entryRow - 1 };
        var exitPad = new Surface { xMin = c.width - c.wallThickness - c.ledgeLength, xMax = c.width - c.wallThickness - 1, y = exitRow - 1 };

        var walkable = new List<Surface>();
        var obstacles = new List<Surface>();
        var solids = new List<Surface>(); 
        Tilemap platMap = platformTilemap != null ? platformTilemap : groundTilemap;

        if (entryRow > c.floorThickness) { PaintSurface(groundTilemap, c.solidTile, origin, entryPad); walkable.Add(entryPad); obstacles.Add(entryPad); solids.Add(entryPad); }
        if (exitRow > c.floorThickness) { PaintSurface(groundTilemap, c.solidTile, origin, exitPad); walkable.Add(exitPad); obstacles.Add(exitPad); solids.Add(exitPad); }

        // REGLA 4: Ya solamente puede haber 1 o 2 pilares
        int numPillars = rng.Next(1, 3);
        int startX = c.wallThickness + c.ledgeLength + 2;
        int endX = c.width - c.wallThickness - c.ledgeLength - 2;
        int stepX = (endX - startX) / numPillars;

        List<Surface> pillars = new List<Surface>();
        List<Surface> floors = new List<Surface>();
        
        int currentLeft = c.wallThickness;
        
        // BUCLE METROIDVANIA: El primer suelo garantiza poder saltar de regreso a la puerta si te caes.
        int currentFloorY = entryPad.y - c.maxJumpHeight;
        if (currentFloorY < floorTop) currentFloorY = floorTop;

        for (int i = 0; i < numPillars; i++)
        {
            // REGLA 2: Altura del pilar = suelo izquierdo + maxJumpHeight + (1 o más)
            int extraHeight = rng.Next(1, 4); 
            int pHeightY = currentFloorY + c.maxJumpHeight + extraHeight;

            int pWidth = rng.Next(3, 6); // REGLA 3 (Extra): Pilares y plataformas entre 3 y 5 de ancho
            
            int minX = startX + (i * stepX) + c.maxJumpDistance + 2;
            int maxX = startX + ((i + 1) * stepX) - pWidth - c.maxJumpDistance;
            if (maxX < minX) maxX = minX;

            int pX = rng.Next(minX, maxX + 1);
            var pSurface = new Surface { xMin = pX, xMax = pX + pWidth - 1, y = pHeightY };
            pillars.Add(pSurface);

            // Suelo a la Izquierda del pilar
            var fLeft = new Surface { xMin = currentLeft, xMax = pX - 1, y = currentFloorY };
            floors.Add(fLeft);

            // REGLA 3: Suelo a la Derecha = Punta del pilar - maxJumpHeight (Para saltar de regreso si te caes)
            currentFloorY = pHeightY - c.maxJumpHeight;
            currentLeft = pX + pWidth;
        }

        // Suelo Final
        var fLast = new Surface { xMin = currentLeft, xMax = c.width - c.wallThickness - 1, y = currentFloorY };
        floors.Add(fLast);

        // Renderizar Suelos
        foreach (var f in floors)
        {
            for (int x = f.xMin; x <= f.xMax; x++)
            {
                int highestY = f.y;
                if (x >= exitPad.xMin && highestY > exitPad.y) highestY = exitPad.y;

                for (int y = 0; y <= highestY; y++)
                    groundTilemap.SetTile(origin + new Vector3Int(x, y, 0), c.solidTile);
            }
            walkable.Add(f);
            solids.Add(f);
        }

        // Renderizar Pilares
        foreach (var p in pillars)
        {
            for (int x = p.xMin; x <= p.xMax; x++)
            {
                for (int y = 0; y <= p.y; y++)
                    groundTilemap.SetTile(origin + new Vector3Int(x, y, 0), c.solidTile);
            }
            walkable.Add(p);
            obstacles.Add(p); // Se agrega a obstáculos para que las plataformas nunca choquen con los pilares
            solids.Add(p);
        }

        // REGLA 1: SIEMPRE haya un camino de la entrada a la salida pasando por los pilares
        List<Surface> checkpoints = new List<Surface>();
        checkpoints.Add(entryPad);
        checkpoints.AddRange(pillars);
        checkpoints.Add(exitPad);

        List<Surface> main = new List<Surface>();

        for (int i = 0; i < checkpoints.Count - 1; i++)
        {
            var path = TryChain(c, rng, checkpoints[i], checkpoints[i + 1], obstacles, solids);
            if (path != null) main.AddRange(path);
        }

        if (main.Count > 0) AddAll(main, c, origin, platMap, walkable, obstacles);

        return walkable;
    }

    private static List<Surface> TryChain(RoomGenConfig c, Random rng, Surface from, Surface to, List<Surface> obstacles, List<Surface> solids)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var chain = new List<Surface>();
            var temp = new List<Surface>(obstacles);
            if (BuildChain(c, rng, from, to, temp, chain, solids)) return chain;
        }
        return null;
    }

    private static bool BuildChain(RoomGenConfig c, Random rng, Surface from, Surface to,
                                   List<Surface> obstacles, List<Surface> chain, List<Surface> solids)
    {
        Surface cur = from;
        int yMax = c.height - c.ceilingThickness - 1 - c.playerHeight; // Límite del techo para no salirse de la cámara

        // AUMENTO VITAL: Permitir hasta 30 saltos para cruzar abismos largos (ignorando c.maxPlatforms limitantes)
        for (int step = 0; step < 30; step++)
        {
            if (IsLinked(c, cur, to)) return true;

            bool placed = false;
            for (int attempt = 0; attempt < c.placementAttempts && !placed; attempt++)
            {
                int len = rng.Next(3, 6); // Plataformas entre 3 y 5
                int gapX = rng.Next(c.minJumpDistance, c.maxJumpDistance + 1); 
                
                // Probabilidad de Zig-Zag: Mayormente hacia adelante, pero permite retrocesos (80/20)
                float pRight = to.xMin >= cur.xMax ? 0.8f : (to.xMax <= cur.xMin ? 0.2f : 0.5f);
                bool goRight = rng.NextDouble() < pRight;
                
                int xMin = goRight ? cur.xMax + gapX : cur.xMin - gapX - len;

                // Lógica Inteligente de Y: Permitir saltos planos (0) si el objetivo está lejos.
                // Esto evita chocar con el techo o el suelo al viajar distancias largas.
                List<int> yOptions = new List<int>();
                int diffY = to.y - cur.y;
                bool farX = (to.xMin - cur.xMax > c.maxJumpDistance) || (cur.xMin - to.xMax > c.maxJumpDistance);
                
                if (farX) yOptions.Add(0); // Si está lejos, siempre es válido ir recto
                
                if (diffY > 0) {
                    yOptions.Add(c.maxJumpHeight);
                    yOptions.Add(c.maxJumpHeight - 1);
                }
                else if (diffY < 0) {
                    yOptions.Add(-c.maxJumpHeight);
                    yOptions.Add(-(c.maxJumpHeight - 1));
                }
                else if (!farX) {
                    yOptions.Add(0); // Si está a la misma altura y cerca
                }

                int dy = yOptions[rng.Next(yOptions.Count)];
                var s = new Surface { xMin = xMin, xMax = xMin + len - 1, y = cur.y + dy};

                // Descartar si choca con las paredes o el TECHO
                if (s.xMin < c.wallThickness + 1 || s.xMax > c.width - c.wallThickness - 2) continue;
                if (s.y > yMax) continue;

                // REGLA: No puede haber plataformas a menos de playerHeight + 1 del suelo sólido
                int localSolidY = GetSolidYAt(s.xMin, s.xMax, solids, c.floorThickness - 1);
                if (s.y < localSolidY + c.playerHeight + 1) continue;

                if (Overlaps(c, s, obstacles)) continue;

                chain.Add(s);
                obstacles.Add(s);
                cur = s;
                placed = true;
            }
            if (!placed) return false; // Si después de intentar no puede poner ni una, aborta este intento
        }
        return IsLinked(c, cur, to);
    }

    private static int GetSolidYAt(int xMin, int xMax, List<Surface> solids, int floorTop)
    {
        int highestY = floorTop;
        foreach(var solid in solids) {
            if (xMin <= solid.xMax && xMax >= solid.xMin) {
                if (solid.y > highestY) highestY = solid.y;
            }
        }
        return highestY;
    }

    private static bool IsLinked(RoomGenConfig c, Surface a, Surface b)
    {
        if (Mathf.Abs(a.y - b.y) > c.maxJumpHeight) return false;
        
        bool xOverlap = a.xMin <= b.xMax && a.xMax >= b.xMin;
        if (xOverlap) return true; // Para validar conectividad, el overlap está permitido.

        int gap = Mathf.Max(0, Mathf.Max(a.xMin, b.xMin) - Mathf.Min(a.xMax, b.xMax) - 1);
        return gap >= c.minJumpDistance && gap <= c.maxJumpDistance;
    }

    private static bool Overlaps(RoomGenConfig c, Surface s, List<Surface> obstacles)
    {
        foreach (var p in obstacles)
        {
            bool xOverlap = s.xMin <= p.xMax && s.xMax >= p.xMin;
            
            if (xOverlap)
            {
                // REGLA DE ZIG-ZAG PERFECTO: 
                // Se permite empalmar en X SOLO si están separados por más de maxJumpHeight.
                // Así actúan como "pisos" de un edificio sin ahogar al jugador.
                if (Mathf.Abs(s.y - p.y) <= c.maxJumpHeight + 1) return true;
            }
            else
            {
                // Mantiene el espaciado correcto si rozan horizontalmente (gap menor a minJumpDistance)
                int gap = Mathf.Max(0, Mathf.Max(s.xMin, p.xMin) - Mathf.Min(s.xMax, p.xMax) - 1);
                if (gap < c.minJumpDistance && Mathf.Abs(s.y - p.y) <= c.maxJumpHeight) return true;
            }
        }
        return false;
    }

    private void AddAll(List<Surface> list, RoomGenConfig c, Vector3Int origin, Tilemap map,
                        List<Surface> walkable, List<Surface> obstacles)
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

    // Para plataformas extra: basta con poder llegar subiendo desde alguna superficie existente.
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

    // ---------- Enemigos ----------

    private void SpawnEnemies(RoomGenConfig c, Vector3Int origin, List<Surface> surfaces, Random rng, Transform parent)
    {
        if (c.enemyPrefabs == null || c.enemyPrefabs.Length == 0) return;

        int count = rng.Next(c.enemyCount.x, c.enemyCount.y + 1);
        var used = new HashSet<Vector2Int>();

        for (int tries = 0; tries < count * 10 && used.Count < count; tries++)
        {
            Surface s = surfaces[rng.Next(surfaces.Count)];
            int x = rng.Next(s.xMin, s.xMax + 1);
            if (x - c.wallThickness < c.safeZoneFromEntry) continue;

            var cell = new Vector2Int(x, s.y + 1);
            if (!used.Add(cell)) continue;

            Vector3 world = groundTilemap.GetCellCenterWorld(origin + new Vector3Int(cell.x, cell.y, 0));
            GameObject prefab = c.enemyPrefabs[rng.Next(c.enemyPrefabs.Length)];
            Instantiate(prefab, world, Quaternion.identity, parent);
        }
    }

    // ---------- Cámara y trigger ----------

    private CinemachineCamera CreateCamera(RoomGenConfig c, Vector3Int origin, Transform parent, bool isFirst)
    {
        Vector3 cell = groundTilemap.layoutGrid.cellSize;
        Vector3 corner = groundTilemap.CellToWorld(origin);
        Vector3 center = corner + new Vector3(c.width * cell.x * 0.5f, c.height * cell.y * 0.5f, -10f);

        CinemachineCamera cam = Instantiate(roomCameraPrefab, center, Quaternion.identity, parent);
        cam.name = $"RoomCam_{parent.name}";
        cam.Priority = isFirst ? 10 : 0;
        return cam;
    }

    private RoomEntryTrigger CreateEntryTrigger(RoomGenConfig c, Vector3Int origin, RoomData data, RoomData previous)
    {
        Vector3 cell = groundTilemap.layoutGrid.cellSize;
        Vector3 corner = groundTilemap.CellToWorld(origin);

        var go = new GameObject($"EntryTrigger_{data.index}");
        go.transform.SetParent(data.root, false);
        go.transform.position = corner + new Vector3((c.wallThickness + 0.5f) * cell.x, c.height * cell.y * 0.5f, 0f);

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(cell.x, c.height * cell.y);

        if (previous != null)
        {
            var changer = go.AddComponent<CameraChanger>();
            changer.zoneCamera = data.cam;
            changer.previousCamera = previous.cam;
        }

        var trigger = go.AddComponent<RoomEntryTrigger>();
        trigger.roomIndex = data.index;
        return trigger;
    }
}