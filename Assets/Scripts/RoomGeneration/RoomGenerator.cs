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
    [SerializeField] private float roomCameraOrthoSize = 11.125f;
    [SerializeField] private bool addConfiner = true;
    [SerializeField] private CinemachineCamera roomCameraPrefab;
    [SerializeField] private Transform roomsParent;

    [Header("Parámetros del Autómata Celular (Bordes Orgánicos)")]
    [Range(0, 100)] [SerializeField] private int shellNoisePercent = 40;
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

    // Where the player's FEET go (bottom-center of the body) when the run starts.
    // The cellular-automaton shell moves the floor up and down from seed to seed, so a fixed cell is not enough:
    // the old spawn (one cell over the entry row) left the 2 x 2 body inside a floor bump with some seeds.
    // This looks, from the entrance wall inwards and from the floor up, for the first spot where the whole body
    // fits (bodyCols x bodyRows free cells) with solid ground right under it.
    public Vector3 GetSpawnFeet(RoomData room, Vector2 bodySize)
    {
        var c = config;
        Vector3 cell = groundTilemap.layoutGrid.cellSize;
        int cols = Mathf.Max(1, Mathf.CeilToInt(bodySize.x / cell.x - 0.001f));
        int rows = Mathf.Max(1, Mathf.CeilToInt((bodySize.y + 0.05f) / cell.y));

        // First pass starts 2 columns in, clear of the room's entry trigger (on the first inner column);
        // the second pass is the whole room, just in case.
        for (int pass = 0; pass < 2; pass++)
        for (int x = c.wallThickness + (pass == 0 ? 2 : 0); x + cols <= room.width - c.wallThickness; x++)
            for (int y = c.floorThickness; y + rows <= c.height - c.ceilingThickness; y++)
            {
                Vector3Int at = room.originCell + new Vector3Int(x, y, 0);
                if (!BodyFits(at, cols, rows) || !HasGroundUnder(at, cols)) continue;

                Vector3 corner = groundTilemap.CellToWorld(at);
                Debug.Log($"[RoomGenerator] Spawn at cell {at} ({cols} x {rows} body)");
                return new Vector3(corner.x + cols * cell.x * 0.5f, corner.y + 0.02f, 0f);
            }

        // Nothing found (should not happen): the old spot, on the entry row
        Vector3Int fallback = room.originCell + new Vector3Int(c.wallThickness + 2, room.entryRow, 0);
        Debug.LogWarning($"[RoomGenerator] No free spawn spot found in room {room.index}; using {fallback}");
        Vector3 fc = groundTilemap.CellToWorld(fallback);
        return new Vector3(fc.x + cell.x, fc.y + 0.02f, 0f);
    }

    // Rebuilds the ground / platform colliders right now. Unity only updates a TilemapCollider2D (and its
    // CompositeCollider2D) later in the frame, so the physics steps that run before that still see the old shape.
    // When the scene is loaded from another one the first frame is long (seconds of loading), Unity runs many
    // physics steps in it, and the player fell through the just-carved floor before the collider existed.
    public void RefreshColliders()
    {
        foreach (Tilemap tm in new[] { groundTilemap, platformTilemap })
        {
            if (tm == null) continue;
            TilemapCollider2D tc = tm.GetComponent<TilemapCollider2D>();
            if (tc != null) tc.ProcessTilemapChanges();
            CompositeCollider2D cc = tm.GetComponent<CompositeCollider2D>();
            if (cc != null) cc.GenerateGeometry();
        }
        Physics2D.SyncTransforms();
    }

    // Kept for older callers: the cell center of a 1 x 1 spawn
    public Vector3 GetSpawnPoint(RoomData room)
    {
        Vector3 feet = GetSpawnFeet(room, Vector2.one);
        return feet + new Vector3(0f, groundTilemap.layoutGrid.cellSize.y * 0.5f, 0f);
    }

    private bool BodyFits(Vector3Int corner, int cols, int rows)
    {
        for (int dx = 0; dx < cols; dx++)
            for (int dy = 0; dy < rows; dy++)
                if (IsSolid(corner + new Vector3Int(dx, dy, 0))) return false;
        return true;
    }

    private bool HasGroundUnder(Vector3Int corner, int cols)
    {
        for (int dx = 0; dx < cols; dx++)
            if (IsSolid(corner + new Vector3Int(dx, -1, 0))) return true;
        return false;
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
        playerPhysics = ReadPlayerPhysics();   // the secret room checks use the real jump of the player
        RoomData data = null;

        // A secret room only fits some layouts (a flat floor against a side wall, that door out of the way...),
        // so a room that must have one is rolled again (doors + shell + platforms) until it fits.
        // The first attempt uses the plain seed, so rooms without a secret room look exactly as before.
        int attempts = makeSecret ? Mathf.Max(1, secretRoomAttempts) : 1;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var rng = new Random(attempt == 0 ? seed : unchecked(seed * 486187739 + attempt * 7919));
            data = BuildRoom(c, index, rng, previous, difficulty, makeSecret && attempt > 0);

            if (!makeSecret) break;
            data.hasSecretRoom = GenerateSecretRoom(c, data, rng);
            if (data.hasSecretRoom)
            {
                if (attempt > 0) Debug.Log($"[RoomGenerator] Room {index}: secret room fitted on attempt {attempt + 1}");
                break;
            }
            if (attempt == attempts - 1)
            {
                Debug.LogWarning($"[RoomGenerator] Room {index}: no layout fitted a secret room after {attempts} attempts.");
                break;
            }
            DiscardRoom(c, data);   // put the world block back and try another layout
        }

        // Camera changers go in after the final layout is chosen
        data.entryTrigger = CreateEntryTrigger(c, data.originCell, data, previous);
        if (previous != null)
            CreateBackChanger(c, data, previous);

        return data;
    }

    [Header("Secret rooms")]
    [Tooltip("Times a room that must have a secret room is generated again until the secret room fits")]
    [SerializeField] private int secretRoomAttempts = 30;
    [Tooltip("Chance, on the re-rolls, that the doors are picked to suit the secret entrance " +
             "(entry at floor level and a high exit, so the exit wall has a flat floor next to it)")]
    [Range(0f, 1f)] [SerializeField] private float secretFriendlyDoorsChance = 0.75f;

    // Doors, shell, platforms and camera of one room (no triggers, no secret room)
    private RoomData BuildRoom(RoomGenConfig c, int index, Random rng, RoomData previous, float difficulty, bool secretFriendly)
    {
        int entryRow = previous == null ? c.floorThickness : PickDoorRow(c, rng);
        int exitRow = PickDoorRow(c, rng);

        if (entryRow == exitRow && entryRow <= c.floorThickness + 2)
        {
            exitRow = c.doorRows[rng.Next(1, c.doorRows.Length)];
        }

        // The entrance block needs a flat floor against a side wall with that wall's door well above it.
        // A floor-level entry keeps the floors low and a high exit leaves the exit wall free under its ledge.
        if (secretFriendly && c.doorRows != null && c.doorRows.Length > 0 && rng.NextDouble() < secretFriendlyDoorsChance)
        {
            if (previous != null) entryRow = Mathf.Clamp(Mathf.Min(c.doorRows), c.floorThickness, c.height - c.ceilingThickness - c.doorHeight);
            exitRow = Mathf.Clamp(Mathf.Max(c.doorRows), c.floorThickness, c.height - c.ceilingThickness - c.doorHeight);
        }

        Vector3Int origin = previous == null ? startOrigin : previous.NextOrigin(entryRow);

        var data = new RoomData
        {
            index = index, originCell = origin, width = c.width, entryRow = entryRow, exitRow = exitRow,
            difficulty = difficulty
        };

        var rootGO = new GameObject($"Room_{index}");
        rootGO.transform.SetParent(roomsParent, false);
        data.root = rootGO.transform;

        // Level layout
        PaintOrganicShell(c, origin, entryRow, exitRow, rng, previous != null);

        // Surfaces (EnemyPlacer reads these, tagged by kind and main path)
        data.surfaces = BuildLayout(c, origin, rng, entryRow, exitRow, new List<Surface>());

        data.cam = CreateCamera(c, origin, data.root, previous == null);
        return data;
    }

    // Undoes BuildRoom: the room cells become world block again and its objects are destroyed
    private void DiscardRoom(RoomGenConfig c, RoomData data)
    {
        for (int x = 0; x < c.width; x++)
            for (int y = 0; y < c.height; y++)
            {
                Vector3Int cell = data.originCell + new Vector3Int(x, y, 0);
                groundTilemap.SetTile(cell, c.solidTile);
                if (platformTilemap != null) platformTilemap.SetTile(cell, null);
            }

        if (data.root == null) return;
        data.root.gameObject.SetActive(false);   // Destroy waits for the end of the frame; hide it right now
        if (Application.isPlaying) Destroy(data.root.gameObject);
        else DestroyImmediate(data.root.gameObject);
    }


    // ---------- Secret room: a full-size room under the main one, hidden until the door is broken ----------
    //
    //  Entrance : one side wall of the main room is widened into a block of rock on the floor, and the breakable
    //             door (destructibleDoorPrefab) is embedded in its face, always inside the room camera's frame.
    //  Tunnel   : behind the door a short hall, a well with a 2-row step, and a tunnel sloping down in steps of
    //             1 row (1-3 columns wide) that comes out on a ledge in the top of the secret room.
    //  Room     : same size as a normal room (config.width x config.height), right under the main room, with the
    //             organic shell of the cellular automaton. A ladder of platforms 2-3 rows apart goes from the ledge
    //             down to the floor (the last one is a rock step), and the beer is on a small pad on the floor or up
    //             on a side platform. Every fall ends on the floor, and the floor always leads back up.
    //  Style    : every secret room rolls its own look (SecretStyle): tunnel exit height, wall noise, step height,
    //             platform length, how often the ladder turns, side platforms, floor bumps and where the beer is.
    //  Camera   : SecretCameraZone switches between the secret and the main camera by the player's position.
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
    // The room camera sits in the room center; with orthographic size 11.125 at 16:9 it shows 22.25 of the 24 rows,
    // so only the lowest ~0.9 rows are out of frame; horizontally it shows ~39.6 columns, the whole 38-wide room.
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
    // Tunnel: rows of head room over each step and narrowest step (columns)
    private const int SecretTunnelRows = 4;
    private const int SecretTunnelMinStep = 2;
    // Player body in tiles for the reachability check (the real collider is 1.2 x 1.8)
    private const int PlayerBodyCols = 2;
    // Platforms of the secret room: at least 2 rows apart (the player jumps ~3.3), at most 2 empty columns between them
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

    // Look of one secret room. A new one is rolled for every attempt, so no two secret rooms look alike.
    private class SecretStyle
    {
        public int exitFeet;          // feet row where the tunnel comes out, main-room coordinates (< 0)
        public int shellNoise;        // cellular automaton noise % for this room's walls
        public int shellIterations;
        public int maxStep;           // rows between two ladder platforms: SecretStep..maxStep
        public int minLen, maxLen;    // ladder platform length
        public float turnChance;      // chance to turn around in the middle of the room, not only at the walls
        public bool beerOnBalcony;    // beer up on a side platform instead of on the floor
        public int balconies;         // extra side platforms (one of them holds the beer when beerOnBalcony)
        public int mounds;            // rock bumps on the floor

        public override string ToString() =>
            $"tunnel exit {exitFeet}, noise {shellNoise}%x{shellIterations}, steps 2-{maxStep}, lengths {minLen}-{maxLen}, " +
            $"turns {turnChance:P0}, {balconies} side platforms, {mounds} mounds, beer {(beerOnBalcony ? "up high" : "on the floor")}";
    }

    private SecretStyle RollSecretStyle(RoomGenConfig c, Random rng)
    {
        bool shortOnes = rng.NextDouble() < 0.5;
        var s = new SecretStyle
        {
            exitFeet = -rng.Next(5, 10),   // ledge at secret row 14..18 (at 19 the ceiling is too close to jump back up)
            shellNoise = rng.Next(35, 56),
            shellIterations = rng.Next(2, 5),
            maxStep = rng.NextDouble() < 0.5 ? SecretStep : Mathf.Max(SecretStep, Mathf.Min(3, c.maxJumpHeight)),
            minLen = shortOnes ? 2 : 3,
            maxLen = shortOnes ? 4 : 6,
            turnChance = (float)(rng.NextDouble() * 0.45),
            beerOnBalcony = rng.NextDouble() < 0.5,
            balconies = rng.Next(0, 4),
            mounds = rng.Next(0, 4),
        };
        if (s.beerOnBalcony) s.balconies = Mathf.Max(1, s.balconies);
        return s;
    }

    private bool TryBuildSecretRoom(RoomGenConfig c, RoomData data, Random rng, bool left, int fy, Rect vis)
    {
        const int T = SecretBlockWidth, H = SecretBlockHeight;
        SecretStyle style = RollSecretStyle(c, rng);
        int E = style.exitFeet;
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

        // Sloped tunnel going back under the hall and the room floor: steps of 1 row, 2-3 columns wide (never 1:
        // the player is 1.2 tiles wide), 4 rows high (the player is 1.8 tall and has to hop up every step on the
        // way back) with a random extra row here and there so the ceiling is not a perfect stair. It never goes
        // above fy - 1, so the main room floor stays whole.
        int levels = fy - 3 - E + 1;
        int maxCols = c.width - 2 * c.wallThickness - 13 - SecretGap;   // the ledge and one platform must fit after it
        if (levels < 1 || levels > maxCols) return false;
        var widths = new int[levels];
        int total = 0;
        for (int i = 0; i < levels; i++) { widths[i] = rng.Next(SecretTunnelMinStep, 4); total += widths[i]; }
        if (total > maxCols) return false;            // too long for this room even with the narrowest steps
        for (int w = 3; w > SecretTunnelMinStep && total > maxCols; w--)
            for (int i = 0; i < levels && total > maxCols; i++)
                if (widths[i] == w) { widths[i]--; total--; }

        var tunnelFloors = new List<Surface>();      // secret-room coordinates, used to keep platforms away
        int uu = 4;
        for (int i = 0; i < levels; i++)
        {
            int feet = fy - 3 - i;
            for (int k = 0; k < widths[i]; k++, uu++)
            {
                int top = Mathf.Min(feet + SecretTunnelRows - 1, fy - 1);
                if (top + 1 <= fy - 1 && rng.NextDouble() < 0.35) top++;
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
                    if (n.x == X(T) && n.y >= 0) continue;   // in front of the door: the room itself (main room only;
                                                             // under it this column is the tunnel's wall and floor)
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
        List<Surface> ladder = PlanSecretLadder(c, rng, style, ledge, dir, tunnelFloors);
        if (ladder == null) return false;
        Surface block = ladder[ladder.Count - 1];    // the last one stands on the floor as a rock step

        // Side platforms off the ladder; with beerOnBalcony the first one holds the beer
        var extras = new List<Surface>();
        bool beerUp = false;
        Surface beerSurface = default;
        for (int k = 0; k < style.balconies; k++)
        {
            if (!TryPlanBalcony(c, rng, ledge, ladder, extras, tunnelFloors, out Surface b)) continue;
            extras.Add(b);
            if (style.beerOnBalcony && !beerUp) { beerUp = true; beerSurface = b; }
        }

        // Beer on the floor: a small pad inside the camera frame, away from the step (also the fallback when no
        // side platform fitted)
        int pxLo = Mathf.Max(c.wallThickness + 3, Mathf.CeilToInt(vis.xMin));
        int pxHi = Mathf.Min(c.width - c.wallThickness - 4, Mathf.FloorToInt(vis.xMax) - 1) - 2;
        Surface pad = default;
        bool padOk = false;
        if (!beerUp)
        {
            var above = new List<Surface>(ladder);
            above.AddRange(tunnelFloors);
            above.AddRange(extras);
            for (int attempt = 0; attempt < 50 && !padOk && pxLo <= pxHi; attempt++)
            {
                int px = rng.Next(pxLo, pxHi + 1);
                pad = new Surface { xMin = px, xMax = px + 2, y = floorTop + SecretStep };
                if (pad.xMin <= block.xMax + 2 && pad.xMax >= block.xMin - 2) continue;
                padOk = true;
                foreach (var s in above)
                    if (s.xMin - 1 <= pad.xMax && pad.xMin <= s.xMax + 1 && s.y - pad.y < c.playerHeight + 2) padOk = false;
            }
            if (!padOk) return false;
            beerSurface = pad;
        }

        // Rock bumps on the floor (1-2 rows, the player just hops over them)
        var mounds = new List<Surface>();
        for (int k = 0; k < style.mounds; k++)
            if (TryPlanMound(c, rng, ledge, ladder, extras, tunnelFloors, mounds, padOk ? (Surface?)pad : null, out Surface m))
                mounds.Add(m);

        // ---------- 4. Plan the tiles (entrance block now, the rest when the door breaks) ----------
        var blockCells = new Dictionary<Vector3Int, TileBase>();
        for (int u = 0; u < T; u++)
            for (int y = fy + 1; y <= fy + H; y++)
            {
                bool doorHole = u == T - 1 && y <= fy + SecretDoorRows;
                blockCells[mo + new Vector3Int(X(u), y, 0)] = doorHole ? null : c.solidTile;
            }

        var plan = new SecretPlan();
        Vector3Int S(int x, int y) => so + new Vector3Int(x, y, 0);

        // Room shell: organic edges from the cellular automaton (this room's own noise), closed, solid floor
        int[,] grid = BuildOrganicShellGrid(c, 0, 0, rng, false, false, style.shellNoise, style.shellIterations);
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

        var floating = new List<Surface>(ladder);
        floating.AddRange(extras);
        foreach (Surface s in floating)
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

        foreach (Surface m in mounds)
            for (int x = m.xMin; x <= m.xMax; x++)
            {
                for (int y = floorTop + 1; y <= m.y; y++) PlanSolid(plan, S(x, y), c.solidTile);
                for (int y = m.y + 1; y <= m.y + c.playerHeight + 1; y++) PlanClear(plan, S(x, y));
            }

        if (padOk)
            for (int x = pad.xMin; x <= pad.xMax; x++)
            {
                for (int y = 0; y <= pad.y; y++) PlanSolid(plan, S(x, y), c.solidTile);
                for (int y = pad.y + 1; y <= pad.y + c.playerHeight + 1; y++) PlanClear(plan, S(x, y));
            }
        plan.beerCell = S((beerSurface.xMin + beerSurface.xMax) / 2, beerSurface.y + 1);

        // Last check on the final tiles: the player can walk / jump from the door to the beer AND back
        Vector3Int doorCell = mo + new Vector3Int(X(T - 1), fy + 1, 0);
        bool Solid(Vector3Int cell)
        {
            if (plan.ground.TryGetValue(cell, out TileBase g))
                return g != null || (plan.platforms.TryGetValue(cell, out TileBase pl) && pl != null);
            if (blockCells.TryGetValue(cell, out TileBase b)) return b != null;
            return IsSolid(cell);
        }
        var area = new RectInt(so.x, so.y, c.width, 2 * c.height);
        if (!CanWalk(c, Solid, area, doorCell, plan.beerCell) || !CanWalk(c, Solid, area, plan.beerCell, doorCell))
        {
            Debug.Log("[RoomGenerator] Secret room attempt dropped: the beer is not reachable both ways.");
            return false;
        }

        // Valid: paint the entrance block now
        foreach (var kv in blockCells)
        {
            groundTilemap.SetTile(kv.Key, kv.Value);
            if (platformTilemap != null) platformTilemap.SetTile(kv.Key, null);
        }

        // ---------- 5. Cameras: one zone decides by the player's position (secret room vs main room) ----------
        Vector3 cellSize = groundTilemap.layoutGrid.cellSize;
        CinemachineCamera secretCam = CreateCamera(c, so, parent, false);
        secretCam.name = $"SecretCam_{parent.name}";

        Vector3 secretCorner = groundTilemap.CellToWorld(so);
        var zoneGO = new GameObject("SecretCameraZone");
        zoneGO.transform.SetParent(parent, false);
        zoneGO.AddComponent<SecretCameraZone>().Init(secretCam, data.cam,
            secretCorner.x, secretCorner.x + c.width * cellSize.x,
            secretCorner.y, groundTilemap.CellToWorld(mo).y);

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

        LastSecretStyle = $"{style}; got {ladder.Count} ladder, {extras.Count} side, {mounds.Count} mounds, beer {(beerUp ? "up" : "floor")}";
        Debug.Log($"[RoomGenerator] Secret room planned under {parent.name}: door in the {(left ? "left" : "right")} " +
                  $"wall block at x={X(T - 1)}, floor y={fy}, tunnel to x={X(uEnd)}, {LastSecretStyle}, beer at {plan.beerCell}");
        return true;
    }

    // Description of the last secret room built (logs and tests)
    public string LastSecretStyle { get; private set; }

    // Movement numbers of the player, read from the PlayerController in the scene (so the check follows whatever
    // is set in its inspector). Used by CanWalk to know how far a jump really goes.
    private struct PlayerPhysics
    {
        public float jumpSpeed, gravityUp, gravityDown, runSpeed, bodyHeight;
    }

    private PlayerPhysics playerPhysics;

    private static PlayerPhysics ReadPlayerPhysics()
    {
        float g = Mathf.Abs(Physics2D.gravity.y);
        // Defaults = the values of the ProceduralScene player
        var p = new PlayerPhysics { jumpSpeed = 18f, gravityUp = g * 5f, gravityDown = g * 14f, runSpeed = 8f, bodyHeight = 1.8f };

        var pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc == null) return p;
        var rb = pc.GetComponent<Rigidbody2D>();
        float mass = rb != null && rb.mass > 0f ? rb.mass : 1f;
        float scale = rb != null ? rb.gravityScale : 1f;
        p.jumpSpeed = pc.jumpForce / mass;                                  // AddForce(Impulse)
        p.gravityUp = g * (scale + pc.lowJumpMultiplier - 1f);              // PlayerController.ApplyGravity, going up
        p.gravityDown = g * (scale + pc.fallMultiplier - 1f);               // ... and falling
        p.runSpeed = pc.moveSpeed;
        if (pc.GetComponent<Collider2D>() is BoxCollider2D box)
            p.bodyHeight = box.size.y * Mathf.Abs(pc.transform.lossyScale.y);
        return p;
    }

    // Columns the player can move sideways while the feet are at least 'dy' rows over the take-off floor, with the
    // head stopped by a ceiling 'riseCap' rows over the feet (it bumps and falls). This is the L-shaped jump of
    // CanWalk: up, across, down. 0 = the jump can't get that high.
    private static int JumpReach(PlayerPhysics ph, int dy, float riseCap)
    {
        const float dt = 0.004f;
        float h = 0f, v = ph.jumpSpeed, t = 0f, tAbove = -1f;
        for (int i = 0; i < 1500; i++)
        {
            v -= (v > 0f ? ph.gravityUp : ph.gravityDown) * dt;
            h += v * dt;
            t += dt;
            if (v > 0f && h >= riseCap) { h = riseCap; v = 0f; }   // head hits the ceiling
            if (tAbove < 0f && h >= dy + 0.05f) tAbove = t;
            if (v < 0f && h < dy + 0.05f) break;
        }
        if (tAbove < 0f) return 0;
        // Rounded down with no extra: jumps right at the limit are left out (a player can miss them)
        return Mathf.FloorToInt(ph.runSpeed * (t - tAbove));
    }

    // Platformer reachability on a tile grid. The body is PlayerBodyCols x playerHeight cells; p is its bottom-left
    // cell and it stands when any cell under it is solid. Moves: walk one column (and fall), or jump: straight up
    // 'dy' rows, then across as many columns as the real jump allows at that height (JumpReach, counting the
    // ceiling over the take-off spot), then fall.
    private bool CanWalk(RoomGenConfig c, System.Func<Vector3Int, bool> solid, RectInt area,
                         Vector3Int from, Vector3Int to)
    {
        int cols = PlayerBodyCols, rows = Mathf.Max(1, c.playerHeight);
        bool Free(Vector3Int p)
        {
            for (int dx = 0; dx < cols; dx++)
                for (int dy = 0; dy < rows; dy++)
                    if (solid(p + new Vector3Int(dx, dy, 0))) return false;
            return true;
        }
        bool Floor(Vector3Int p)
        {
            for (int dx = 0; dx < cols; dx++)
                if (solid(p + new Vector3Int(dx, -1, 0))) return true;
            return false;
        }
        bool Stand(Vector3Int p) => Free(p) && Floor(p);
        bool Inside(Vector3Int p) => p.x >= area.xMin && p.x < area.xMax && p.y >= area.yMin && p.y < area.yMax;
        Vector3Int Land(Vector3Int p)
        {
            while (Inside(p) && !Floor(p)) p += Vector3Int.down;
            return p;
        }

        const int OpenSky = 12;                       // rows of head room that count as "no ceiling"
        var reach = new Dictionary<(int, int), int>();
        int Reach(int dy, int freeRows)
        {
            if (!reach.TryGetValue((dy, freeRows), out int r))
                reach[(dy, freeRows)] = r = JumpReach(playerPhysics, dy, freeRows >= OpenSky ? 99f : freeRows - playerPhysics.bodyHeight);
            return r;
        }

        var seen = new HashSet<Vector3Int>();
        var queue = new Queue<Vector3Int>();
        void Push(Vector3Int p) { if (Inside(p) && Stand(p) && seen.Add(p)) queue.Enqueue(p); }

        Push(Land(from));
        Push(Land(from + Vector3Int.left));   // the body is 2 wide: it can also stand with "from" as its right half
        while (queue.Count > 0)
        {
            Vector3Int p = queue.Dequeue();
            if (to.x >= p.x - 1 && to.x <= p.x + cols && Mathf.Abs(p.y - to.y) <= 1) return true;

            for (int dx = -1; dx <= 1; dx += 2)
            {
                var w = p + new Vector3Int(dx, 0, 0);
                if (Free(w)) Push(Land(w));
            }

            // Head room over the take-off spot
            int free = 0;
            while (free < OpenSky && FreeRow(p.y + free)) free++;

            for (int dy = 1; ; dy++)
            {
                int side = Reach(dy, free);
                if (side <= 0 || !Free(new Vector3Int(p.x, p.y + dy, 0))) break;
                for (int dir = -1; dir <= 1; dir += 2)
                    for (int dx = 1; dx <= side; dx++)
                    {
                        var q = new Vector3Int(p.x + dir * dx, p.y + dy, 0);
                        if (!Free(q)) break;
                        Push(Land(q));
                    }
            }

            bool FreeRow(int y)
            {
                for (int dx = 0; dx < cols; dx++)
                    if (solid(new Vector3Int(p.x + dx, y, 0))) return false;
                return true;
            }
        }
        return false;
    }

    // Next ladder platform 'gap' empty columns away from prev, in direction d
    private static Surface NextLadderStep(Surface prev, int d, int gap, int len, int y) =>
        d > 0 ? new Surface { xMin = prev.xMax + 1 + gap, xMax = prev.xMax + gap + len, y = y }
              : new Surface { xMin = prev.xMin - gap - len, xMax = prev.xMin - 1 - gap, y = y };

    // Platforms from the tunnel ledge down to the secret room floor, built top-down so the top one always links
    // to the ledge. Each one is 2-3 rows under the previous with 1-2 empty columns between them (1 when 3 rows).
    // When it turns around (at the room edge, or at random with style.turnChance) the next one goes right next
    // to the previous one, under the free columns before it. The last one becomes a rock step on the floor.
    // Returns null if no valid chain is found.
    private List<Surface> PlanSecretLadder(RoomGenConfig c, Random rng, SecretStyle style, Surface ledge, int dir,
                                           List<Surface> fixedSurfaces)
    {
        int floorTop = c.floorThickness - 1;
        int xLo = c.wallThickness + 3, xHi = c.width - c.wallThickness - 4;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            var list = new List<Surface>();
            Surface prev = ledge;
            int d = dir;
            bool ok = true;

            for (int y = ledge.y; ok;)
            {
                int step = rng.Next(SecretStep, style.maxStep + 1);
                int gap = step > SecretStep ? 0 : rng.Next(1, SecretGap + 1);   // 3 rows up: right next to it
                y -= step;
                bool isBlock = y <= floorTop + SecretStep;
                if (isBlock) y = Mathf.Max(y, floorTop + 1);
                int len = rng.Next(style.minLen, style.maxLen + 1);

                bool turn = list.Count > 0 && rng.NextDouble() < style.turnChance;
                Surface s = default;
                if (!turn)
                {
                    s = NextLadderStep(prev, d, gap, len, y);
                    if (s.xMin < xLo || s.xMax > xHi)
                    {
                        if (list.Count == 0) { ok = false; break; }   // the first one has to move away from the tunnel
                        turn = true;
                    }
                }
                if (turn)
                {
                    d = -d;
                    s = NextLadderStep(prev, d, 0, len, y);
                    if (s.xMin < xLo || s.xMax > xHi) { ok = false; break; }
                }

                list.Add(s);
                prev = s;
                if (isBlock) break;
            }
            if (ok && ValidSecretLadder(c, ledge, list, fixedSurfaces)) return list;
        }
        return null;
    }

    // Platforms over each other need head room, and every jump up needs a free window
    private static bool ValidSecretLadder(RoomGenConfig c, Surface ledge, List<Surface> list, List<Surface> fixedSurfaces)
    {
        int minSep = c.playerHeight + 2;             // rows between two platforms one over the other
        var all = new List<Surface>(fixedSurfaces) { ledge };
        all.AddRange(list);
        int fixedCount = fixedSurfaces.Count + 1;  // indices < fixedCount are the tunnel floors and the ledge

        for (int i = fixedCount; i < all.Count; i++)
        {
            for (int j = 0; j < all.Count; j++)
            {
                if (j == i || (j >= fixedCount && j < i)) continue;   // each pair of platforms once
                bool consecutive = j == i - 1 || j == i + 1;          // ledge -> first, and each next one
                if (consecutive) continue;
                Surface a = all[i], b = all[j];
                bool overlap = a.xMin - 1 <= b.xMax && b.xMin - 1 <= a.xMax;
                if (overlap && Mathf.Abs(a.y - b.y) < minSep) return false;
            }

            // Jump window from this platform up to the previous one (or the ledge)
            if (WindowBlocked(c, all[i], all[i - 1], all, i, i - 1)) return false;
        }
        return true;
    }

    // Columns and rows the player crosses jumping from 'lower' up to 'upper'
    private static void JumpWindow(Surface lower, Surface upper, out int xa, out int xb)
    {
        xa = upper.xMin > lower.xMax ? lower.xMax - 1 : upper.xMax;
        xb = upper.xMin > lower.xMax ? upper.xMin : lower.xMin + 1;
        if (xa > xb) { int t = xa; xa = xb; xb = t; }
    }

    private static bool InWindow(RoomGenConfig c, Surface o, Surface lower, Surface upper)
    {
        JumpWindow(lower, upper, out int xa, out int xb);
        return o.y > lower.y && o.y <= upper.y + c.playerHeight + 1 && o.xMin <= xb && o.xMax >= xa;
    }

    // True if any surface of 'all' (except the indices skipA / skipB) is in the way of the jump lower -> upper
    private static bool WindowBlocked(RoomGenConfig c, Surface lower, Surface upper, List<Surface> all, int skipA, int skipB)
    {
        for (int j = 0; j < all.Count; j++)
        {
            if (j == skipA || j == skipB) continue;
            if (InWindow(c, all[j], lower, upper)) return true;
        }
        return false;
    }

    // A side platform next to one of the ladder platforms (same height or up to 2 rows higher, 1-2 columns away),
    // reachable with one jump and in the way of nothing: head room over and under it, no ladder jump crosses it,
    // well away from the tunnel and the rock step.
    private bool TryPlanBalcony(RoomGenConfig c, Random rng, Surface ledge, List<Surface> ladder, List<Surface> extras,
                                List<Surface> tunnelFloors, out Surface balcony)
    {
        balcony = default;
        if (ladder.Count < 2) return false;
        int floorTop = c.floorThickness - 1;
        int xLo = c.wallThickness + 3, xHi = c.width - c.wallThickness - 4;
        int minSep = c.playerHeight + 2;
        Surface block = ladder[ladder.Count - 1];

        // The tunnel is walled in with rock that is not in any surface list: keep side platforms off its columns
        int tMinX = int.MaxValue, tMaxX = int.MinValue, tMinY = int.MaxValue;
        foreach (Surface f in tunnelFloors) { tMinX = Mathf.Min(tMinX, f.xMin); tMaxX = Mathf.Max(tMaxX, f.xMax); tMinY = Mathf.Min(tMinY, f.y); }

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Surface r = ladder[rng.Next(0, ladder.Count - 1)];       // any ladder platform but the rock step
            int len = rng.Next(2, 5);
            int dy = rng.Next(0, SecretStep + 1);
            int gap = dy == SecretStep ? 1 : rng.Next(1, 3);
            Surface s = rng.Next(2) == 0
                ? new Surface { xMin = r.xMax + 1 + gap, xMax = r.xMax + gap + len, y = r.y + dy }
                : new Surface { xMin = r.xMin - gap - len, xMax = r.xMin - 1 - gap, y = r.y + dy };

            if (s.xMin < xLo || s.xMax > xHi || s.y < floorTop + minSep || s.y > ledge.y) continue;
            if (!Clear(s, ledge, minSep)) continue;
            int wa = Mathf.Min(s.xMin, r.xMin), wb = Mathf.Max(s.xMax, r.xMax);   // the platform and the jump to it
            if (tunnelFloors.Count > 0 && wa <= tMaxX + 2 && wb >= tMinX - 2 && s.y + c.playerHeight + 1 >= tMinY - 1) continue;

            bool ok = true;
            foreach (Surface o in ladder)
            {
                if (o.Equals(r)) continue;
                if (o.Equals(block)) { if (Overlaps1(s, o) && s.y < o.y + minSep) ok = false; }
                else if (!Clear(s, o, minSep)) ok = false;
            }
            foreach (Surface o in extras) if (!Clear(s, o, minSep)) ok = false;
            foreach (Surface o in tunnelFloors) if (!Clear(s, o, minSep + 2)) ok = false;
            if (!ok) continue;

            // It must not be in the way of any ladder jump (ledge -> first, and each next one) ...
            Surface up = ledge;
            foreach (Surface low in ladder)
            {
                if (InWindow(c, s, low, up)) ok = false;
                up = low;
            }
            // ... and the jump r -> s must be free too
            Surface lower = s.y > r.y ? r : s, upper = s.y > r.y ? s : r;
            var others = new List<Surface>(ladder) { ledge };
            others.AddRange(extras);
            others.AddRange(tunnelFloors);
            foreach (Surface o in others)
                if (!o.Equals(r) && InWindow(c, o, lower, upper)) ok = false;
            if (!ok) continue;

            balcony = s;
            return true;
        }
        return false;
    }

    // A rock bump on the floor, 2-4 wide and 1-2 high, away from the rock step and the beer pad, with head room
    private bool TryPlanMound(RoomGenConfig c, Random rng, Surface ledge, List<Surface> ladder, List<Surface> extras,
                              List<Surface> tunnelFloors, List<Surface> mounds, Surface? pad, out Surface mound)
    {
        mound = default;
        int floorTop = c.floorThickness - 1;
        int xLo = c.wallThickness + 3, xHi = c.width - c.wallThickness - 4;
        int minSep = c.playerHeight + 2;
        Surface block = ladder[ladder.Count - 1];

        for (int attempt = 0; attempt < 20; attempt++)
        {
            int w = rng.Next(2, 5);
            int x = rng.Next(xLo, xHi - w + 2);
            var m = new Surface { xMin = x, xMax = x + w - 1, y = floorTop + rng.Next(1, 3) };
            if (m.xMax > xHi) continue;
            if (m.xMin <= block.xMax + 3 && m.xMax >= block.xMin - 3) continue;
            if (pad.HasValue && m.xMin <= pad.Value.xMax + 2 && m.xMax >= pad.Value.xMin - 2) continue;

            bool ok = true;
            foreach (Surface o in mounds) if (m.xMin <= o.xMax + 2 && m.xMax >= o.xMin - 2) ok = false;
            var above = new List<Surface>(ladder) { ledge };
            above.AddRange(extras);
            above.AddRange(tunnelFloors);
            foreach (Surface o in above) if (Overlaps1(m, o) && o.y - m.y < minSep) ok = false;
            if (!ok) continue;

            mound = m;
            return true;
        }
        return false;
    }

    // Columns of a and b touch or overlap (one column of margin)
    private static bool Overlaps1(Surface a, Surface b) => a.xMin - 1 <= b.xMax && b.xMin - 1 <= a.xMax;

    // a and b are far enough apart vertically where their columns overlap
    private static bool Clear(Surface a, Surface b, int minSep) => !Overlaps1(a, b) || Mathf.Abs(a.y - b.y) >= minSep;

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
                                         bool hasEntryDoor, bool hasExitDoor, int noisePercent = -1, int iterations = -1)
    {
        if (noisePercent < 0) noisePercent = shellNoisePercent;
        if (iterations < 0) iterations = cellularAutomataIterations;
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
                    grid[x, y] = (rng.Next(0, 100) < noisePercent) ? 1 : 0;
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
        for (int it = 0; it < iterations; it++)
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