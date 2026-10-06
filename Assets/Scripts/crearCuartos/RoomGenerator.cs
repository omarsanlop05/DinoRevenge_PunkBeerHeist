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
    [Tooltip("Opcional. Si lo dejas vacío, las plataformas se pintan en el Tilemap de suelo")]
    [SerializeField] private Tilemap platformTilemap;
    [SerializeField] private CinemachineCamera roomCameraPrefab;
    [SerializeField] private Transform roomsParent;

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

    // previous = cuarto anterior (null para el primero). El origen se calcula solo para que las puertas
    // coincidan en altura, aunque un cuarto termine arriba y el siguiente empiece abajo.
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

        PaintShell(c, origin, entryRow, exitRow);
        List<Surface> walkable = BuildLayout(c, origin, rng, entryRow, exitRow);
        SpawnEnemies(c, origin, walkable, rng, data.root);
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

    // ---------- Plataformas ----------

    private List<Surface> BuildLayout(RoomGenConfig c, Vector3Int origin, Random rng, int entryRow, int exitRow)
    {
        int floorTop = c.floorThickness - 1;
        var floor = new Surface { xMin = c.wallThickness, xMax = c.width - c.wallThickness - 1, y = floorTop };
        var entryPad = new Surface { xMin = c.wallThickness, xMax = c.wallThickness + c.ledgeLength - 1, y = entryRow - 1 };
        var exitPad = new Surface { xMin = c.width - c.wallThickness - c.ledgeLength, xMax = c.width - c.wallThickness - 1, y = exitRow - 1 };

        var walkable = new List<Surface> { floor };   // donde se puede caminar (spawns y alcanzabilidad)
        var obstacles = new List<Surface>();          // lo que ocupa espacio (para dejar holgura al jugador)
        Tilemap platMap = platformTilemap != null ? platformTilemap : groundTilemap;

        // Salientes sólidos para puertas altas
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

        // Camino obligatorio de puerta a puerta cuando alguna es alta
        if (entryRow > c.floorThickness || exitRow > c.floorThickness)
        {
            List<Surface> main = TryChain(c, rng, entryPad, exitPad, obstacles);
            if (main == null)
            {
                Debug.LogWarning($"[RoomGenerator] No se pudo crear camino entre puertas. Revisa maxJumpHeight/maxJumpDistance.");
            }
            else
            {
                AddAll(main, c, origin, platMap, walkable, obstacles);

                // Si todo el camino quedó lejos del suelo, agrega una escalera de regreso para no atrapar al jugador
                Surface lowest = entryPad.y <= exitPad.y ? entryPad : exitPad;
                foreach (var s in main) if (s.y < lowest.y) lowest = s;

                if (lowest.y - floorTop > c.maxJumpHeight - 1)
                {
                    List<Surface> back = TryChain(c, rng, lowest, floor, obstacles);
                    if (back == null)
                        Debug.LogWarning("[RoomGenerator] No se pudo crear camino de regreso al suelo.");
                    else
                        AddAll(back, c, origin, platMap, walkable, obstacles);
                }
            }
        }

        // Plataformas extra opcionales
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

    // Reintenta varias veces construir una cadena de plataformas de "from" a "to". null si no lo logra.
    private static List<Surface> TryChain(RoomGenConfig c, Random rng, Surface from, Surface to, List<Surface> obstacles)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var chain = new List<Surface>();
            var temp = new List<Surface>(obstacles);
            if (BuildChain(c, rng, from, to, temp, chain)) return chain;
        }
        return null;
    }

    // Camino aleatorio: cada paso es un salto posible en ambos sentidos (subir y regresar), con sesgo hacia el destino.
    private static bool BuildChain(RoomGenConfig c, Random rng, Surface from, Surface to,
                                   List<Surface> obstacles, List<Surface> chain)
    {
        int floorTop = c.floorThickness - 1;
        int yMin = floorTop + c.playerHeight + 1;
        int yMax = c.height - c.ceilingThickness - 1 - c.playerHeight;
        int span = c.maxJumpDistance - 1;
        int maxStep = Mathf.Max(2, c.maxJumpHeight);
        Surface cur = from;

        for (int step = 0; step < 16; step++)
        {
            if (IsLinked(c, cur, to)) return true;

            bool placed = false;
            for (int attempt = 0; attempt < 30 && !placed; attempt++)
            {
                int len = rng.Next(c.platformLength.x, c.platformLength.y + 1);

                float pRight = to.xMin >= cur.xMax ? 0.75f : (to.xMax <= cur.xMin ? 0.25f : 0.5f);
                bool goRight = rng.NextDouble() < pRight;
                int xMin = goRight
                    ? rng.Next(cur.xMax - 1, cur.xMax + span + 2)
                    : rng.Next(cur.xMin - len - span, cur.xMin - len + 2);

                int toward = to.y > cur.y ? 1 : (to.y < cur.y ? -1 : 0);
                int mag = rng.Next(1, maxStep);   // 1 .. maxJumpHeight-1
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

    // Dos superficies están enlazadas si se puede saltar de una a otra (con 1 tile de margen) en cualquier sentido.
    private static bool IsLinked(RoomGenConfig c, Surface a, Surface b)
    {
        if (Mathf.Abs(a.y - b.y) > c.maxJumpHeight - 1) return false;
        int gap = Mathf.Max(0, Mathf.Max(a.xMin, b.xMin) - Mathf.Min(a.xMax, b.xMax) - 1);
        return gap <= c.maxJumpDistance - 1;
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

    // Evita plataformas pegadas en vertical que no dejen espacio para el jugador.
    private static bool Overlaps(RoomGenConfig c, Surface s, List<Surface> obstacles)
    {
        foreach (var p in obstacles)
        {
            bool xOverlap = s.xMin <= p.xMax + 1 && s.xMax >= p.xMin - 1;
            if (xOverlap && Mathf.Abs(s.y - p.y) <= c.playerHeight) return true;
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