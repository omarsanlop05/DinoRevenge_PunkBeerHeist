using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.VisualScripting;
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
    
    [Header("Room camera (used when no prefab is assigned)")]
    [SerializeField] private float roomCameraOrthoSize = 10.125f;
    [SerializeField] private bool addConfiner = true;
    [SerializeField] private CinemachineCamera roomCameraPrefab;
    [SerializeField] private Transform roomsParent;

    [Header("Parámetros del Autómata Celular (Bordes Orgánicos)")]
    [Range(0, 100)] [SerializeField] private int shellNoisePercent = 45;
    [SerializeField] private int cellularAutomataIterations = 3;

    [Header("Origen del primer cuarto (celda)")]
    [SerializeField] private Vector3Int startOrigin = Vector3Int.zero;

    [Header("Pruebas")]
    [SerializeField] private int testSeed = 1;

    [Header("World (giant block)")]
    [SerializeField] private int worldMargin = 40;

    // The whole world starts as one solid block; rooms are carved out of it
    public void FillWorld(int roomCount, int extraWidth)
    {
        var c = config;
        int maxShift = (c.doorRows != null && c.doorRows.Length > 0)
            ? Mathf.Max(c.doorRows) - Mathf.Min(c.doorRows) : 0;

        int xMin = startOrigin.x - worldMargin;
        int xMax = startOrigin.x + roomCount * c.width + extraWidth + worldMargin;
        int yRange = roomCount * maxShift + 2 * c.height + worldMargin;
        int yMin = startOrigin.y - yRange;
        int yMax = startOrigin.y + yRange + c.height;

        var size = new Vector3Int(xMax - xMin, yMax - yMin, 1);
        var tiles = new TileBase[size.x * size.y];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = c.solidTile;

        groundTilemap.SetTilesBlock(new BoundsInt(new Vector3Int(xMin, yMin, 0), size), tiles);
        Debug.Log($"[RoomGenerator] World filled: {size.x} x {size.y} tiles");
    }

    [ContextMenu("Rellenar mundo (bloque gigante)")]
    private void FillWorldTest() => FillWorld(5, 120);

    // Clears the block where a prefab with its own Tilemaps (hallway, boss room) will sit
    // Clears the giant block only where the INSIDE of a hand-made prefab (hallway, boss room) will be.
// It flood-fills the cells that are empty in the prefab's own Tilemaps, starting from the given world
// points (anchors), so the prefab's walls and the solid block around it stay untouched.
public void CarveInterior(GameObject instance, params Vector3[] worldSeeds)
{
    const int maxCells = 20000;
    Vector3 eps = new Vector3(0.01f, 0.01f, 0f);

    var maps = new List<Tilemap>();
    bool any = false;
    Vector3Int cmin = Vector3Int.zero, cmax = Vector3Int.zero;

    foreach (var tm in instance.GetComponentsInChildren<Tilemap>())
    {
        tm.CompressBounds();   // cellBounds keeps stale empty space after tiles are erased
        BoundsInt b = tm.cellBounds;
        if (b.size.x <= 0 || b.size.y <= 0) continue;

        maps.Add(tm);
        Vector3Int a = groundTilemap.WorldToCell(tm.CellToWorld(b.min) + eps);
        Vector3Int z = groundTilemap.WorldToCell(tm.CellToWorld(b.max) + eps) - Vector3Int.one;   // max is exclusive

        if (!any) { cmin = a; cmax = z; any = true; }
        else { cmin = Vector3Int.Min(cmin, a); cmax = Vector3Int.Max(cmax, z); }
    }

    if (!any)
    {
        Debug.LogWarning($"[RoomGenerator] {instance.name} has no tiles in its own Tilemaps. Nothing carved.");
        return;
    }

    bool InBounds(Vector3Int c) => c.x >= cmin.x && c.x <= cmax.x && c.y >= cmin.y && c.y <= cmax.y;

    bool IsPrefabTile(Vector3Int c)
    {
        Vector3 w = groundTilemap.GetCellCenterWorld(c);
        foreach (var tm in maps)
            if (tm.HasTile(tm.WorldToCell(w))) return true;
        return false;
    }

    var visited = new HashSet<Vector3Int>();
    var queue = new Queue<Vector3Int>();
    foreach (var seed in worldSeeds)
    {
        Vector3Int s = groundTilemap.WorldToCell(seed + eps);
        if (InBounds(s) && !IsPrefabTile(s) && visited.Add(s)) queue.Enqueue(s);
    }

    if (queue.Count == 0)
    {
        Debug.LogWarning($"[RoomGenerator] {instance.name}: no valid start cell. Check that the anchors sit on empty cells inside the prefab.");
        return;
    }

    Vector3Int[] dirs = { new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0) };
    while (queue.Count > 0)
    {
        Vector3Int cur = queue.Dequeue();
        foreach (var d in dirs)
        {
            Vector3Int n = cur + d;
            if (!InBounds(n) || IsPrefabTile(n) || !visited.Add(n)) continue;
            queue.Enqueue(n);
        }

        if (visited.Count > maxCells)
        {
            Debug.LogError($"[RoomGenerator] {instance.name}: the empty area leaks (more than {maxCells} cells). " +
                           "The prefab is not closed by its walls. Nothing carved.");
            return;
        }
    }

    foreach (var cell in visited) ClearCell(cell);
    Debug.Log($"[RoomGenerator] Carved {visited.Count} cells for {instance.name} (bounds {cmin} .. {cmax})");
}

    public Vector3 GetSpawnPoint(RoomData room)
    {
        Vector3Int cell = room.originCell + new Vector3Int(config.wallThickness + 2, room.entryRow + 1, 0);
        Debug.Log($"[RoomGenerator] Spawn cell {cell} still solid: {groundTilemap.HasTile(cell)}");
        return groundTilemap.GetCellCenterWorld(cell);
    }

    public RoomGenConfig Config => config;

    // True if the cell has a tile in the ground or the platform Tilemap
    public bool IsSolid(Vector3Int cell) =>
        groundTilemap.HasTile(cell) || (platformTilemap != null && platformTilemap.HasTile(cell));

    public Vector3 CellCenterWorld(Vector3Int cell) => groundTilemap.GetCellCenterWorld(cell);

    public Vector3 CellToWorld(Vector3Int cell) => groundTilemap.CellToWorld(cell);
    public Vector3Int WorldToCell(Vector3 world) => groundTilemap.WorldToCell(world);

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

    private const int MinPlatformGap = 1;      // hueco mínimo (tiles) entre plataformas
    private const int MaxLayoutAttempts = 40;  // reintentos de layout completo

    // previous = cuarto anterior (null para el primero). El origen se calcula solo para que las puertas
    // coincidan en altura, aunque un cuarto termine arriba y el siguiente empiece abajo.
    public RoomData Generate(int index, int seed, RoomData previous, float difficulty = 1f, bool makeSecret = false)
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

        var mainFloors = new List<Surface>();
        
        var data = new RoomData
        {
            index = index, originCell = origin, width = c.width, entryRow = entryRow, exitRow = exitRow,
            difficulty = difficulty
        };

        var rootGO = new GameObject($"Room_{index}");
        rootGO.transform.SetParent(roomsParent, false);
        data.root = rootGO.transform;

        // Level layout
        //PaintShell(c, origin, entryRow, exitRow);
        PaintOrganicShell(c, origin, entryRow, exitRow, rng, previous != null);
        
        // Enemies
        List<Surface> walkable = BuildLayout(c, origin, rng, entryRow, exitRow, mainFloors);
        data.surfaces = walkable;   // EnemyPlacer reads these (tagged by kind and main path)

        // Camera
        // Camera + camera changers
        data.cam = CreateCamera(c, origin, data.root, previous == null);
        data.entryTrigger = CreateEntryTrigger(c, origin, data, previous);   
        if (previous != null)
            CreateBackChanger(c, data, previous);                            

        // Lógica del Cuarto Secreto insertada al final antes del return
        if (makeSecret)
            data.hasSecretRoom = GenerateSecretRoom(c, data, rng);

        return data;
    }


    // ---------- Secret room: a full-size room under the main one, hidden until the door is broken ----------
    //
    //  Entrance : one side wall of the main room is widened into a block of rock on the floor, and the breakable
    //             door (destructibleDoorPrefab) is embedded in its face, always inside the room camera's frame.
    //  Tunnel   : behind the door a short hall, a well with a 2-row step, and a tunnel sloping down in steps of
    //             1 row (1-3 columns wide) that comes out on a ledge in the top of the secret room.
    //  Room     : same size as a normal room (config.width x config.height), right under the main room, with the
    //             organic shell of the cellular automaton. Platforms 2 rows apart go from the ledge down to the
    //             floor (the last one is a rock step), and the beer is on a small pad on the floor. Every fall ends
    //             on the floor, and the floor always leads back up.
    //  Everything is planned and validated now, and carved only when the door breaks.

    // Tiles to change when the secret room is revealed. null = empty cell.
    private class SecretPlan
    {
        public readonly Dictionary<Vector3Int, TileBase> ground = new Dictionary<Vector3Int, TileBase>();
        public readonly Dictionary<Vector3Int, TileBase> platforms = new Dictionary<Vector3Int, TileBase>();
        public Vector3Int beerCell;
    }

    private void PlanSolid(SecretPlan plan, Vector3Int cell, TileBase tile)
    {
        plan.ground[cell] = tile;
        if (platformTilemap != null) plan.platforms[cell] = null;
    }

    private void PlanClear(SecretPlan plan, Vector3Int cell)
    {
        plan.ground[cell] = null;
        if (platformTilemap != null) plan.platforms[cell] = null;
    }

    private void PlanPlatform(SecretPlan plan, Vector3Int cell, TileBase tile)
    {
        if (platformTilemap != null) { plan.platforms[cell] = tile; plan.ground[cell] = null; }
        else plan.ground[cell] = tile;
    }

    // Part of a room (local cell coordinates) that its static camera shows, shrunk by 'margin' tiles.
    // The room camera sits in the room center; with orthographic size 10.125 at 16:9 it shows 20.25 of the 24 rows,
    // so the lowest ~1.9 rows (where most floors are) are out of frame.
    public Rect VisibleLocalRect(RoomGenConfig c, float margin = 0.5f)
    {
        Vector3 cell = groundTilemap.layoutGrid.cellSize;
        float ortho = roomCameraOrthoSize;   // CreateCamera applies this same size to every room camera
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float halfH = ortho / cell.y;
        float halfW = ortho * aspect / cell.x;
        float cx = c.width * 0.5f, cy = c.height * 0.5f;
        return Rect.MinMaxRect(Mathf.Max(0f, cx - halfW) + margin, Mathf.Max(0f, cy - halfH) + margin,
                               Mathf.Min(c.width, cx + halfW) - margin, Mathf.Min(c.height, cy + halfH) - margin);
    }

    // Entrance block: the side wall is widened into a block of rock on the floor, u = 0..6 from the wall into the
    // room, rows fy+1..fy+4. The door is embedded in its face (u = 6, 3 rows), with 1 row of rock over it.
    private const int SecretBlockWidth = 7;
    private const int SecretBlockHeight = 4;
    private const int SecretDoorRows = 3;
    // Feet row where the tunnel comes out into the secret room, in main-room coordinates (secret room row 18)
    private const int SecretTunnelExitFeet = -6;
    // Platforms of the secret room: 2 rows apart (the player jumps ~3.3) with 2 empty columns between them
    private const int SecretStep = 2;
    private const int SecretGap = 2;

    private bool GenerateSecretRoom(RoomGenConfig c, RoomData data, Random rng)
    {
        Vector3Int secretOrigin = data.originCell + new Vector3Int(0, -c.height, 0);   // right under the main room

        // The space under the room must still be untouched world block
        for (int x = 0; x < c.width; x++)
            for (int y = 0; y < c.height; y++)
                if (!IsSolid(secretOrigin + new Vector3Int(x, y, 0)))
                {
                    Debug.LogWarning("[RoomGenerator] Secret room skipped: the space under the room is already carved.");
                    return false;
                }

        Rect vis = VisibleLocalRect(c, 0.1f);
        bool firstLeft = rng.Next(2) == 0;
        for (int k = 0; k < 2; k++)
        {
            bool left = k == 0 ? firstLeft : !firstLeft;
            if (!CanUseSecretSide(c, data, left, vis, out int fy)) continue;
            for (int attempt = 0; attempt < 8; attempt++)
                if (TryBuildSecretRoom(c, data, rng, left, fy, vis)) return true;
        }

        Debug.LogWarning("[RoomGenerator] Secret room skipped: neither side wall has room for the entrance.");
        return false;
    }

    // Local x of column u (u = 0 touches the side wall, grows into the room)
    private static int SecretX(RoomGenConfig c, bool left, int u) =>
        left ? c.wallThickness + u : c.width - 1 - c.wallThickness - u;

    // Can the side wall be widened into the entrance block without getting in the way of the room?
    private bool CanUseSecretSide(RoomGenConfig c, RoomData data, bool left, Rect vis, out int fy)
    {
        fy = -1;
        const int T = SecretBlockWidth, H = SecretBlockHeight;
        int xa = Mathf.Min(SecretX(c, left, 0), SecretX(c, left, T + 2));
        int xb = Mathf.Max(SecretX(c, left, 0), SecretX(c, left, T + 2));

        // One flat floor from the wall to 3 columns past the block (room to stand in front of the door)
        bool found = false;
        foreach (Surface s in data.surfaces)
            if (s.kind == SurfaceKind.Floor && s.xMin <= xa && s.xMax >= xb) { fy = s.y; found = true; break; }
        if (!found) return false;

        // The door of the room on that side must stay clear (block + 3 rows over it)
        int doorRow = left ? data.entryRow : data.exitRow;
        if (doorRow <= fy + H + 3 && doorRow + c.doorHeight - 1 >= fy + 1) return false;

        // Nothing else (pads, platforms, pillars) over the block, so no route needs that space
        int bxa = Mathf.Min(SecretX(c, left, 0), SecretX(c, left, T)), bxb = Mathf.Max(SecretX(c, left, 0), SecretX(c, left, T));
        foreach (Surface s in data.surfaces)
        {
            if (s.kind == SurfaceKind.Floor) continue;
            if (s.y >= fy + 1 && s.y <= fy + H + 3 && s.xMin <= bxb && s.xMax >= bxa) return false;
        }

        // Free space in front of the door for the player
        Vector3Int o = data.originCell;
        for (int u = T; u <= T + 2; u++)
            for (int y = fy + 1; y <= fy + SecretDoorRows; y++)
                if (IsSolid(o + new Vector3Int(SecretX(c, left, u), y, 0))) return false;

        // The whole door inside the camera frame
        int doorX = SecretX(c, left, T - 1);
        return doorX >= vis.xMin && doorX + 1 <= vis.xMax && fy + 1 >= vis.yMin && fy + 1 + SecretDoorRows <= vis.yMax;
    }

    private bool TryBuildSecretRoom(RoomGenConfig c, RoomData data, Random rng, bool left, int fy, Rect vis)
    {
        const int T = SecretBlockWidth, H = SecretBlockHeight, E = SecretTunnelExitFeet;
        Vector3Int mo = data.originCell;
        Vector3Int so = mo + new Vector3Int(0, -c.height, 0);
        Transform parent = data.root;
        int dir = left ? 1 : -1;                     // +u in x
        int floorTop = c.floorThickness - 1;         // secret room floor surface (its own coordinates)

        int X(int u) => SecretX(c, left, u);
        bool InBlock(int x, int y)
        {
            int u = left ? x - c.wallThickness : c.width - 1 - c.wallThickness - x;
            return u >= 0 && u < T && y >= fy + 1 && y <= fy + H;
        }

        // ---------- 1. Tunnel, in main-room cells (y < 0 is the secret room) ----------
        var carved = new HashSet<Vector2Int>();
        void Carve(int u, int y) => carved.Add(new Vector2Int(X(u), y));

        // Door hole and hall inside the block, at floor level
        for (int u = 4; u <= T - 1; u++)
            for (int y = fy + 1; y <= fy + SecretDoorRows; y++) Carve(u, y);
        // Well against the wall: a 2-wide step 2 rows down (u 0-1) and a 2-wide drop to the tunnel (u 2-3)
        for (int u = 0; u <= 1; u++)
            for (int y = fy - 1; y <= fy + SecretDoorRows; y++) Carve(u, y);
        for (int u = 2; u <= 3; u++)
            for (int y = fy - 3; y <= fy + SecretDoorRows; y++) Carve(u, y);

        // Sloped tunnel going back under the hall and the room floor: steps of 1 row, 1-3 columns wide (random,
        // narrower when the floor is high so the tunnel still leaves room for the platforms), 3 rows high with a
        // random extra row here and there so the ceiling is not a perfect stair
        int levels = fy - 3 - E + 1;
        int maxCols = c.width - 2 * c.wallThickness - 13 - SecretGap;   // the ledge and one platform must fit after it
        if (levels > maxCols) return false;
        var widths = new int[levels];
        int total = 0;
        for (int i = 0; i < levels; i++) { widths[i] = rng.Next(2, 4); total += widths[i]; }
        for (int w = 3; w > 1 && total > maxCols; w--)
            for (int i = 0; i < levels && total > maxCols; i++)
                if (widths[i] == w) { widths[i]--; total--; }

        var tunnelFloors = new List<Surface>();      // secret-room coordinates, used to keep platforms away
        int uu = 4;
        for (int i = 0; i < levels; i++)
        {
            int feet = fy - 3 - i;
            for (int k = 0; k < widths[i]; k++, uu++)
            {
                int top = feet + 2;
                if (feet + 3 <= fy - 1 && rng.NextDouble() < 0.35) top++;
                for (int y = feet; y <= top; y++) Carve(uu, y);
                tunnelFloors.Add(new Surface { xMin = X(uu), xMax = X(uu), y = feet - 1 + c.height });
            }
        }
        int uEnd = uu - 1;                            // last tunnel column (its feet are at E)

        // Ledge where the tunnel comes out, and the cells in front of the opening (not walled in)
        var opening = new HashSet<Vector2Int>();
        for (int u = uEnd + 1; u <= uEnd + 3; u++)
            for (int y = E; y <= E + 3; y++) opening.Add(new Vector2Int(X(u), y));

        // ---------- 2. Validate the main room part and find the rock that walls the tunnel in ----------
        var forced = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in carved)
        {
            if (cell.y >= 0 && !InBlock(cell.x, cell.y) && !IsSolid(mo + new Vector3Int(cell.x, cell.y, 0)))
                return false;                         // would open a hole into the main room
            if (cell.x < 1 || cell.x > c.width - 2) return false;

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (carved.Contains(n) || opening.Contains(n)) continue;
                    if (n.x == X(T)) continue;        // in front of the door: the room itself
                    if (n.y >= 0)
                    {
                        if (!InBlock(n.x, n.y) && !IsSolid(mo + new Vector3Int(n.x, n.y, 0))) return false;
                    }
                    else forced.Add(n);               // inside the secret room: walled in with rock
                }
        }

        // ---------- 3. Platforms from the tunnel ledge down to the floor ----------
        var ledge = new Surface
        {
            xMin = Mathf.Min(X(uEnd + 1), X(uEnd + 3)), xMax = Mathf.Max(X(uEnd + 1), X(uEnd + 3)),
            y = E - 1 + c.height
        };
        List<Surface> ladder = PlanSecretLadder(c, rng, ledge, dir, tunnelFloors);
        if (ladder == null) return false;
        Surface block = ladder[ladder.Count - 1];    // the last one stands on the floor as a rock step

        // Beer on a small pad on the floor, inside the camera frame, away from the step
        int pxLo = Mathf.Max(c.wallThickness + 3, Mathf.CeilToInt(vis.xMin));
        int pxHi = Mathf.Min(c.width - c.wallThickness - 4, Mathf.FloorToInt(vis.xMax) - 1) - 2;
        Surface pad = default;
        bool padOk = false;
        for (int attempt = 0; attempt < 50 && !padOk && pxLo <= pxHi; attempt++)
        {
            int px = rng.Next(pxLo, pxHi + 1);
            pad = new Surface { xMin = px, xMax = px + 2, y = floorTop + SecretStep };
            if (pad.xMin <= block.xMax + 2 && pad.xMax >= block.xMin - 2) continue;
            padOk = true;
            foreach (var s in ladder)
                if (s.xMin - 1 <= pad.xMax && pad.xMin <= s.xMax + 1 && s.y - pad.y < c.playerHeight + 2) padOk = false;
            foreach (var s in tunnelFloors)
                if (s.xMin - 1 <= pad.xMax && pad.xMin <= s.xMax + 1 && s.y - pad.y < c.playerHeight + 2) padOk = false;
        }
        if (!padOk) return false;

        // ---------- 4. Everything is valid: paint the entrance block now, plan the rest ----------
        for (int u = 0; u < T; u++)
            for (int y = fy + 1; y <= fy + H; y++)
            {
                bool doorHole = u == T - 1 && y <= fy + SecretDoorRows;
                Vector3Int cell = mo + new Vector3Int(X(u), y, 0);
                groundTilemap.SetTile(cell, doorHole ? null : c.solidTile);
                if (platformTilemap != null) platformTilemap.SetTile(cell, null);
            }

        var plan = new SecretPlan();
        Vector3Int S(int x, int y) => so + new Vector3Int(x, y, 0);

        // Room shell: organic edges from the cellular automaton, closed, solid floor
        int[,] grid = BuildOrganicShellGrid(c, 0, 0, rng, false, false);
        for (int x = 0; x < c.width; x++)
            for (int y = 0; y < c.height; y++)
            {
                if (grid[x, y] == 1 || y <= floorTop) PlanSolid(plan, S(x, y), c.solidTile);
                else PlanClear(plan, S(x, y));
            }

        foreach (Vector2Int n in forced) PlanSolid(plan, mo + new Vector3Int(n.x, n.y, 0), c.solidTile);
        foreach (Vector2Int cell in carved)
        {
            if (cell.x == X(T - 1) && cell.y >= fy + 1 && cell.y <= fy + SecretDoorRows) continue;   // door hole: already empty
            PlanClear(plan, mo + new Vector3Int(cell.x, cell.y, 0));
        }
        foreach (Vector2Int cell in opening) PlanClear(plan, mo + new Vector3Int(cell.x, cell.y, 0));
        for (int x = ledge.xMin; x <= ledge.xMax; x++) PlanSolid(plan, S(x, ledge.y), c.solidTile);

        foreach (Surface s in ladder)
        {
            bool isBlock = s.Equals(block);
            for (int x = s.xMin; x <= s.xMax; x++)
            {
                for (int y = s.y + 1; y <= s.y + c.playerHeight + 1; y++) PlanClear(plan, S(x, y));
                if (isBlock)
                    for (int y = floorTop + 1; y <= s.y; y++) PlanSolid(plan, S(x, y), c.solidTile);
                else
                    PlanPlatform(plan, S(x, s.y), c.platformTile);
            }
        }

        for (int x = pad.xMin; x <= pad.xMax; x++)
        {
            for (int y = 0; y <= pad.y; y++) PlanSolid(plan, S(x, y), c.solidTile);
            for (int y = pad.y + 1; y <= pad.y + c.playerHeight + 1; y++) PlanClear(plan, S(x, y));
        }
        plan.beerCell = S(pad.xMin + 1, pad.y + 1);

        // ---------- 5. Cameras: the secret camera inside, the main one again back in the entrance ----------
        Vector3 cellSize = groundTilemap.layoutGrid.cellSize;
        CinemachineCamera secretCam = CreateCamera(c, so, parent, false);
        secretCam.name = $"SecretCam_{parent.name}";

        Vector3 secretCenter = groundTilemap.CellToWorld(so)
                             + new Vector3(c.width * cellSize.x * 0.5f, c.height * cellSize.y * 0.5f, 0f);
        CreateSecretChanger(parent, "SecretCamEnter", secretCenter,
                            new Vector2(c.width * cellSize.x, c.height * cellSize.y), secretCam, data.cam);

        // Exit zone: the well and the hall behind the door
        int zx = Mathf.Min(X(0), X(T - 1)), zy = fy - 3, zw = T, zh = SecretDoorRows + 4;
        Vector3 exitCenter = groundTilemap.CellToWorld(mo + new Vector3Int(zx, zy, 0))
                           + new Vector3(zw * cellSize.x * 0.5f, zh * cellSize.y * 0.5f, 0f);
        CreateSecretChanger(parent, "SecretCamExit", exitCenter,
                            new Vector2(zw * cellSize.x, zh * cellSize.y), data.cam, secretCam);

        // Enemies must not be placed on the block or in front of the door
        data.secretEntrance = new RectInt(Mathf.Min(X(0), X(T + 2)), fy + 1, T + 3, H + 3);

        // ---------- 6. Door, filling its hole in the block ----------
        if (c.destructibleDoorPrefab != null)
        {
            float holeX = groundTilemap.CellToWorld(mo + new Vector3Int(X(T - 1), 0, 0)).x + cellSize.x * 0.5f;
            float floorTopY = groundTilemap.CellToWorld(mo + new Vector3Int(0, fy + 1, 0)).y;

            GameObject doorGO = Instantiate(c.destructibleDoorPrefab, new Vector3(holeX, floorTopY, 0f),
                                            Quaternion.identity, parent);
            FitToSize(doorGO, cellSize.x, SecretDoorRows * cellSize.y);
            PlaceOnFloor(doorGO, holeX, floorTopY);

            var revealGO = new GameObject("SecretReveal");
            revealGO.transform.SetParent(parent, false);
            revealGO.AddComponent<SecretRoomReveal>().Init(doorGO, () => RevealSecretRoom(plan, parent));
        }
        else
        {
            Debug.LogWarning("[RoomGenerator] destructibleDoorPrefab is not assigned: the secret room is revealed immediately.");
            RevealSecretRoom(plan, parent);
        }

        Debug.Log($"[RoomGenerator] Secret room planned under {parent.name}: door in the {(left ? "left" : "right")} " +
                  $"wall block at x={X(T - 1)}, floor y={fy}, tunnel to x={X(uEnd)}, {ladder.Count} platforms, beer at {plan.beerCell}");
        return true;
    }

    // Platforms from the tunnel ledge down to the secret room floor, built top-down so the top one always links
    // to the ledge. Each one is 2 rows under the previous with 2 empty columns between them; at the room edge
    // the chain turns around right next to the previous one, under the free columns before it, so the jump up
    // never hits a platform. The last one becomes a rock step on the floor. Returns null if it does not fit.
    private List<Surface> PlanSecretLadder(RoomGenConfig c, Random rng, Surface ledge, int dir, List<Surface> fixedSurfaces)
    {
        int floorTop = c.floorThickness - 1;
        int xLo = c.wallThickness + 3, xHi = c.width - c.wallThickness - 4;
        int minSep = c.playerHeight + 2;             // rows between two platforms one over the other

        for (int attempt = 0; attempt < 40; attempt++)
        {
            var list = new List<Surface>();
            Surface prev = ledge;
            int d = dir;
            bool ok = true;

            for (int y = ledge.y - SecretStep; ok; y -= SecretStep)
            {
                bool isBlock = y <= floorTop + SecretStep;
                if (isBlock) y = Mathf.Max(y, floorTop + 1);
                int len = rng.Next(3, 6);

                Surface s = d > 0
                    ? new Surface { xMin = prev.xMax + 1 + SecretGap, xMax = prev.xMax + SecretGap + len, y = y }
                    : new Surface { xMin = prev.xMin - SecretGap - len, xMax = prev.xMin - 1 - SecretGap, y = y };
                if (s.xMin < xLo || s.xMax > xHi)
                {
                    if (list.Count == 0) { ok = false; break; }   // the first one has to move away from the tunnel
                    d = -d;
                    s = d > 0
                        ? new Surface { xMin = prev.xMax + 1, xMax = prev.xMax + len, y = y }
                        : new Surface { xMin = prev.xMin - len, xMax = prev.xMin - 1, y = y };
                    if (s.xMin < xLo || s.xMax > xHi) { ok = false; break; }
                }

                list.Add(s);
                prev = s;
                if (isBlock) break;
            }
            if (!ok) continue;

            // Validation: platforms over each other need head room, and every jump up needs a free window
            var all = new List<Surface>(fixedSurfaces) { ledge };
            all.AddRange(list);
            int fixedCount = fixedSurfaces.Count + 1;  // indices < fixedCount are the tunnel floors and the ledge

            for (int i = fixedCount; i < all.Count && ok; i++)
            {
                for (int j = 0; j < all.Count && ok; j++)
                {
                    if (j == i || (j >= fixedCount && j < i)) continue;   // each pair of platforms once
                    bool consecutive = j == i - 1 || j == i + 1;          // ledge -> first, and each next one
                    if (consecutive) continue;
                    Surface a = all[i], b = all[j];
                    bool overlap = a.xMin - 1 <= b.xMax && b.xMin - 1 <= a.xMax;
                    if (overlap && Mathf.Abs(a.y - b.y) < minSep) ok = false;
                }

                // Jump window from this platform up to the previous one (or the ledge)
                Surface lower = all[i], upper = all[i - 1];
                int wxa = upper.xMin > lower.xMax ? lower.xMax - 1 : upper.xMax;
                int wxb = upper.xMin > lower.xMax ? upper.xMin : lower.xMin + 1;
                for (int j = 0; j < all.Count && ok; j++)
                {
                    if (j == i || j == i - 1) continue;
                    Surface o = all[j];
                    if (o.y > lower.y && o.y <= upper.y + c.playerHeight + 1 && o.xMin <= wxb && o.xMax >= wxa) ok = false;
                }
            }

            if (ok) return list;
        }
        return null;
    }

    // Scales an instance so its sprite measures width x height (world units)
    private static void FitToSize(GameObject go, float width, float height)
    {
        var r = go.GetComponentInChildren<Renderer>();
        if (r == null) return;
        Vector3 size = r.bounds.size;
        if (size.x <= 0f || size.y <= 0f) return;
        Vector3 s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x * width / size.x, s.y * height / size.y, s.z);
    }

    // Moves the door so its sprite is centered on x and its bottom sits on the floor surface
    private static void PlaceOnFloor(GameObject go, float centerX, float floorY)
    {
        var r = go.GetComponentInChildren<Renderer>();
        if (r == null) return;
        Bounds b = r.bounds;
        go.transform.position += new Vector3(centerX - b.center.x, floorY - b.min.y, 0f);
    }

    // Called when the door is broken: carve the shaft and the room, then drop the beer
    private void RevealSecretRoom(SecretPlan plan, Transform parent)
    {
        foreach (var kv in plan.ground) groundTilemap.SetTile(kv.Key, kv.Value);
        if (platformTilemap != null)
            foreach (var kv in plan.platforms) platformTilemap.SetTile(kv.Key, kv.Value);

        PlayerScore.Instance?.RegisterSecretRoomFound();

        if (config.beerPrefab != null)
            Instantiate(config.beerPrefab, groundTilemap.GetCellCenterWorld(plan.beerCell), Quaternion.identity, parent);
        else
            Debug.LogWarning("[RoomGenerator] RoomGenConfig.beerPrefab is not assigned: the secret room has no beer.");
    }

    private void ClearCell(Vector3Int cell)
    {
        groundTilemap.SetTile(cell, null);
        if (platformTilemap != null) platformTilemap.SetTile(cell, null);
    }

    private static void CreateSecretChanger(Transform parent, string objName, Vector3 center, Vector2 size,
                                            CinemachineCamera zone, CinemachineCamera previous)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.position = center;

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;

        var changer = go.AddComponent<CameraChanger>();
        changer.zoneCamera = zone;
        changer.previousCamera = previous;
        changer.awardsRoomPoints = false;       // secret rooms must not give room points
        changer.freezePlayerOnSwitch = false;   // the player is falling or climbing, don't freeze
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

    private void PaintOrganicShell(RoomGenConfig c, Vector3Int origin, int entryRow, int exitRow, Random rng, bool hasEntryDoor = true)
    {
        int[,] grid = BuildOrganicShellGrid(c, entryRow, exitRow, rng, hasEntryDoor, true);

        // Paso D: Renderizar en el Tilemap
        int carved = 0;
        for (int x = 0; x < c.width; x++)
        {
            for (int y = 0; y < c.height; y++)
            {
                 Vector3Int cell = origin + new Vector3Int(x, y, 0);
                bool solid = grid[x, y] == 1;
                groundTilemap.SetTile(cell, solid ? c.solidTile : null);
                if (!solid) carved++;
            }
        }

        Debug.Log($"[RoomGenerator] Room shell at {origin}: {carved} cells carved");
    }

    // Steps A-C of the shell (noise + cellular automaton + door openings), without painting.
    // 1 = solid, 0 = empty. The secret room uses it with both doors closed.
    private int[,] BuildOrganicShellGrid(RoomGenConfig c, int entryRow, int exitRow, Random rng,
                                         bool hasEntryDoor, bool hasExitDoor)
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
        if (hasEntryDoor)
        {
            int entryTop = Mathf.Min(entryRow + c.doorHeight, c.height);
            for (int y = entryRow; y < entryTop; y++)
            {
                for (int x = 0; x <= c.wallThickness + 1; x++) grid[x, y] = 0;
            }
        }
        
        if (hasExitDoor)
        {
            int exitTop = Mathf.Min(exitRow + c.doorHeight, c.height);
            for (int y = exitRow; y < exitTop; y++)
            {
                for (int x = c.width - c.wallThickness - 2; x < c.width; x++) grid[x, y] = 0;
            }
        }

        return grid;
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

    private List<Surface> BuildLayout(RoomGenConfig c, Vector3Int origin, 
    Random rng, int entryRow, int exitRow,
    List<Surface> floorsOut = null)    
    {
        int floorTop = c.floorThickness - 1;
        var entryPad = new Surface { xMin = c.wallThickness, xMax = c.wallThickness + c.ledgeLength - 1, y = entryRow - 1 };
        var exitPad = new Surface { xMin = c.width - c.wallThickness - c.ledgeLength, xMax = c.width - c.wallThickness - 1, y = exitRow - 1 };
        Tilemap platMap = platformTilemap != null ? platformTilemap : groundTilemap;

        List<Surface> pillars = null, floors = null, obstacles = null, solids = null, main = null;
        bool found = false;

        for (int attempt = 0; attempt < MaxLayoutAttempts && !found; attempt++)
        {
            // Los últimos 5 intentos usan 0 pilares (cuarto más simple) como red de seguridad.
            int numPillars = attempt < MaxLayoutAttempts - 5 ? rng.Next(1, 3) : 0;
            found = TryPlanLayout(c, rng, entryRow, exitRow, entryPad, exitPad, floorTop, numPillars,
                                out pillars, out floors, out obstacles, out solids, out main);
        }

        if (!found)
            Debug.LogWarning("[RoomGenerator] No se encontró un layout con camino garantizado. Revisa maxJump* en la config.");

        if (floorsOut != null) floorsOut.AddRange(floors);

        var walkable = new List<Surface>();
        bool entryAtFloor = entryRow <= c.floorThickness;

        // High door pads are ledges in the air; the player always stands on them
        if (entryRow > c.floorThickness) { PaintSurface(groundTilemap, c.solidTile, origin, entryPad); walkable.Add(Tag(entryPad, SurfaceKind.Ledge, true)); }
        if (exitRow > c.floorThickness) { PaintSurface(groundTilemap, c.solidTile, origin, exitPad); walkable.Add(Tag(exitPad, SurfaceKind.Ledge, true)); }

        for (int i = 0; i < floors.Count; i++)
        {
            Surface f = floors[i];
            for (int x = f.xMin; x <= f.xMax; x++)
            {
                int highestY = f.y;
                if (x >= exitPad.xMin && highestY > exitPad.y) highestY = exitPad.y;
                for (int y = 0; y <= highestY; y++)
                    groundTilemap.SetTile(origin + new Vector3Int(x, y, 0), c.solidTile);
            }

            // The route is entry -> chains -> pillars -> last floor -> exit. The floors between pillars are
            // pits the player jumps over; the first floor is only walked when the entry door is at floor level.
            bool isLast = i == floors.Count - 1;
            bool onPath = isLast || (i == 0 && entryAtFloor);
            walkable.Add(Tag(f, SurfaceKind.Floor, onPath));
        }

        foreach (var p in pillars)
        {
            for (int x = p.xMin; x <= p.xMax; x++)
                for (int y = 0; y <= p.y; y++)
                    groundTilemap.SetTile(origin + new Vector3Int(x, y, 0), c.solidTile);
            walkable.Add(Tag(p, SurfaceKind.Pillar, true));
        }

        foreach (var s in main)
        {
            PaintSurface(platMap, c.platformTile, origin, s);
            walkable.Add(Tag(s, SurfaceKind.Platform, true));
        }

        return walkable;
    }

    private static Surface Tag(Surface s, SurfaceKind kind, bool onMainPath)
    {
        s.kind = kind;
        s.onMainPath = onMainPath;
        return s;
    }

    private bool TryPlanLayout(RoomGenConfig c, Random rng, int entryRow, int exitRow,
                           Surface entryPad, Surface exitPad, int floorTop, int numPillars,
                           out List<Surface> pillars, out List<Surface> floors,
                           out List<Surface> obstacles, out List<Surface> solids, out List<Surface> main)
    {
        pillars = new List<Surface>();
        floors = new List<Surface>();
        obstacles = new List<Surface>();
        solids = new List<Surface>();
        main = new List<Surface>();

        if (entryRow > c.floorThickness) { obstacles.Add(entryPad); solids.Add(entryPad); }
        if (exitRow > c.floorThickness) { obstacles.Add(exitPad); solids.Add(exitPad); }

        int currentFloorY = Mathf.Max(entryPad.y - c.maxJumpHeight, floorTop);
        int currentLeft = c.wallThickness;
        int maxPillarY = c.height - c.ceilingThickness - 1 - c.playerHeight;

        if (numPillars > 0)
        {
            // --- Reparto de X con "presupuesto de huecos" ---
            int freeLeft = entryPad.xMax + 1;
            int freeRight = exitPad.xMin - 1;
            int free = freeRight - freeLeft + 1;
            int minGap = c.maxJumpDistance; // cada hueco >= maxJumpDistance: siempre cabe al menos 1 plataforma

            var widths = new int[numPillars];
            int sumW = 0;
            for (int i = 0; i < numPillars; i++) { widths[i] = rng.Next(3, 6); sumW += widths[i]; }

            int slack = free - sumW - (numPillars + 1) * minGap;
            if (slack < 0) // si no cabe, pilares angostos
            {
                for (int i = 0; i < numPillars; i++) widths[i] = 3;
                slack = free - 3 * numPillars - (numPillars + 1) * minGap;
            }
            if (slack < 0) return false; // este número de pilares no cabe en el cuarto

            var gaps = new int[numPillars + 1];
            for (int i = 0; i < gaps.Length; i++) gaps[i] = minGap;
            for (int s = 0; s < slack; s++) gaps[rng.Next(gaps.Length)]++; // el espacio sobrante se reparte al azar

            int x = freeLeft;
            for (int i = 0; i < numPillars; i++)
            {
                x += gaps[i];
                int pY = currentFloorY + c.maxJumpHeight + rng.Next(1, 4);
                if (pY > maxPillarY) return false; // el pilar chocaría con el techo

                var p = new Surface { xMin = x, xMax = x + widths[i] - 1, y = pY };
                pillars.Add(p);
                floors.Add(new Surface { xMin = currentLeft, xMax = p.xMin - 1, y = currentFloorY });

                currentFloorY = pY - c.maxJumpHeight;
                currentLeft = p.xMax + 1;
                x += widths[i];
            }
        }

        floors.Add(new Surface { xMin = currentLeft, xMax = c.width - c.wallThickness - 1, y = currentFloorY });

        foreach (var f in floors) solids.Add(f);
        foreach (var p in pillars) { obstacles.Add(p); solids.Add(p); }

        // --- Entrada -> pilar 1 -> pilar 2 ---
        var checkpoints = new List<Surface> { entryPad };
        checkpoints.AddRange(pillars);

        for (int i = 0; i < checkpoints.Count - 1; i++)
        {
            var path = TryChain(c, rng, checkpoints[i], checkpoints[i + 1], obstacles, solids);
            if (path == null) return false;          // antes: se ignoraba en silencio
            main.AddRange(path);
            obstacles.AddRange(path);                 // las siguientes cadenas no chocan con ésta
        }

        // --- Último pilar -> suelo final -> salida ---
        // Tras el último pilar caes al suelo final (caminable). Solo hace falta cadena si la salida
        // queda más alta de lo que se puede saltar desde ese suelo.
        Surface lastFloor = floors[floors.Count - 1];
        if (exitPad.y > lastFloor.y + c.maxJumpHeight)
        {
            var landing = new Surface { xMin = lastFloor.xMin, xMax = Mathf.Min(lastFloor.xMin + 2, lastFloor.xMax), y = lastFloor.y };
            var path = TryChain(c, rng, landing, exitPad, obstacles, solids);
            if (path == null) return false;
            main.AddRange(path);
            obstacles.AddRange(path);
        }

        return true;
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
                int gapX = rng.Next(c.minJumpDistance, c.maxJumpDistance); 
                
                // Probabilidad de Zig-Zag: Mayormente hacia adelante, pero permite retrocesos (80/20)
                float pRight = to.xMin >= cur.xMax ? 0.8f : (to.xMax <= cur.xMin ? 0.2f : 0.5f);
                bool goRight = rng.NextDouble() < pRight;
                
                int xMin = goRight ? cur.xMax + gapX + 1: cur.xMin - gapX - len;

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
        if (xOverlap) return true;

        int gap = Mathf.Max(0, Mathf.Max(a.xMin, b.xMin) - Mathf.Min(a.xMax, b.xMax) - 1);
        return gap <= c.maxJumpDistance - 1; // margen de 1 tile, igual que ya haces en IsReachable
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
                if (gap < MinPlatformGap && Mathf.Abs(s.y - p.y) <= c.maxJumpHeight) return true;
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

    // Enemies are placed by EnemyPlacer (pass 3: placeholders, pass 4: spawn) after the room is generated.

    // ---------- Cámara y trigger ----------

    private CinemachineCamera CreateCamera(RoomGenConfig c, Vector3Int origin, Transform parent, bool isFirst)
    {
        Vector3 cell = groundTilemap.layoutGrid.cellSize;
        Vector3 corner = groundTilemap.CellToWorld(origin);
        Vector3 center = corner + new Vector3(c.width * cell.x * 0.5f, c.height * cell.y * 0.5f, -10f);

        CinemachineCamera cam;
        if (roomCameraPrefab != null)
        {
            cam = Instantiate(roomCameraPrefab, center, Quaternion.identity, parent);
        }
        else
        {
            // Static camera in the room center: no tracking target, no procedural components
            var camGO = new GameObject();
            camGO.transform.SetParent(parent, false);
            camGO.transform.position = center;
            cam = camGO.AddComponent<CinemachineCamera>();
        }

        // Always use the RoomGenerator value, also with the prefab (its own lens size is ignored),
        // so the camera size is set in one place only
        var lens = cam.Lens;
        lens.OrthographicSize = roomCameraOrthoSize;
        cam.Lens = lens;

        cam.name = $"RoomCam_{parent.name}";
        cam.Priority = isFirst ? 10 : 0;

        if (addConfiner)
            SetupConfiner(cam, c, center, parent);

        return cam;
    }

    // Confiner2D needs a Collider2D bounding shape, otherwise it shows the warning from your screenshot
    private void SetupConfiner(CinemachineCamera cam, RoomGenConfig c, Vector3 center, Transform parent)
    {
        Vector3 cell = groundTilemap.layoutGrid.cellSize;

        var boundsGO = new GameObject($"CamBounds_{parent.name}");
        boundsGO.transform.SetParent(parent, false);
        boundsGO.transform.position = new Vector3(center.x, center.y, 0f);

        var box = boundsGO.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(c.width * cell.x, c.height * cell.y);

        var confiner = cam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
            confiner = cam.gameObject.AddComponent<CinemachineConfiner2D>();

        confiner.BoundingShape2D = box;
        confiner.Damping = 0.3f;
        confiner.SlowingDistance = 3f;
        confiner.InvalidateBoundingShapeCache();
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

    // Changer placed just inside the RIGHT door of the previous room.
    // Walking back through the door switches the camera from this room to the previous one.
    private CameraChanger CreateBackChanger(RoomGenConfig c, RoomData data, RoomData previous)
    {
        Vector3 cell = groundTilemap.layoutGrid.cellSize;
        Vector3 prevCorner = groundTilemap.CellToWorld(previous.originCell);

        var go = new GameObject($"CameraChangerBack_{data.index}");
        go.transform.SetParent(data.root, false);
        go.transform.position = prevCorner + new Vector3(
            (previous.width - c.wallThickness - 0.5f) * cell.x,
            c.height * cell.y * 0.5f,
            0f);

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(cell.x, c.height * cell.y);

        var changer = go.AddComponent<CameraChanger>();
        changer.zoneCamera = previous.cam;       // the camera we go TO
        changer.previousCamera = data.cam;       // the camera we leave
        changer.awardsRoomPoints = false;        // going back must not give room points
        return changer;
    }
}