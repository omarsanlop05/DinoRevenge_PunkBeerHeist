using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

// Puts enemies in a generated room in two passes:
//   Pass 1 (PlacePlaceholders): picks random valid spots and records what is around each one
//                               (air, air platform, off the main path, main path ground).
//   Pass 2 (SpawnEnemies):      turns each placeholder into an enemy of the right kind through
//                               FabricaEnemigos, builds the flyer routes and applies the multiplier.
public class EnemyPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomGenerator roomGenerator;
    [SerializeField] private FabricaEnemigos fabrica;

    [Header("Count (scaled by the room multiplier)")]
    [SerializeField] private int minEnemies = 1;
    [SerializeField] private int maxEnemies = 3;
    [Tooltip("Maximum enemies of the same kind in one room")]
    [SerializeField] private int maxPerKind = 2;
    [Tooltip("Multiplier range coming from PlayerScore (min gives the fewest / weakest enemies)")]
    [SerializeField] private Vector2 multiplierRange = new Vector2(0.75f, 1.25f);
    [Tooltip("Random spread (in enemies) added before rounding, so equal scores do not always give the same count")]
    [Range(0f, 1f)] [SerializeField] private float countNoise = 0.75f;

    [Header("Pass 1 - placeholder rules (tiles)")]
    [Tooltip("Columns after the entry wall with no enemies, so the player can see what is coming")]
    [SerializeField] private int safeZoneFromEntry = 10;
    [Tooltip("Chance that a sample is taken in the air instead of on a surface")]
    [Range(0f, 1f)] [SerializeField] private float airChance = 0.35f;
    [Tooltip("Minimum distance between two placeholders")]
    [SerializeField] private float minSpacing = 4f;
    [Tooltip("Empty cells needed above a ground spot")]
    [SerializeField] private int groundClearance = 2;
    [Tooltip("Half size of the empty box needed around an air spot (1 = 3x3)")]
    [SerializeField] private int airRadius = 1;
    [Tooltip("Cells under an air spot that must be empty for it to count as 'in the air'")]
    [SerializeField] private int airEmptyBelow = 3;
    [SerializeField] private int attemptsPerEnemy = 40;

    [Header("Pass 2 - flyer route (tiles)")]
    [Tooltip("How far a flyer may patrol to each side of its placeholder")]
    [SerializeField] private int flyerRange = 6;
    [Tooltip("Below this horizontal span the flyer patrols up and down instead")]
    [SerializeField] private int minFlyerSpan = 3;
    [Tooltip("Height of the optional middle waypoint that turns the line into a triangle")]
    [SerializeField] private int flyerBob = 2;
    [Range(0f, 1f)] [SerializeField] private float flyerTriangleChance = 0.5f;

    [Header("Pass 2 - stats")]
    [Tooltip("Health before the multiplier. Used when the enemy has no EnemyHealth yet")]
    [SerializeField] private float baseHealth = 30f;
    [Tooltip("Random spread applied to helmet / shoes / aura levels")]
    [Range(0f, 1f)] [SerializeField] private float gearSpread = 0.75f;

    [Header("Debug")]
    [Tooltip("Off = only pass 1 runs: placeholders (with kind and flyer route) are left in the room, no enemies are created")]
    [SerializeField] private bool spawnEnemies = true;
    [SerializeField] private bool keepPlaceholders = true;
    [SerializeField] private bool logPlacement = true;

    private struct Candidate
    {
        public Vector2Int local;
        public bool inAir;
        public SurfaceKind surfaceKind;
        public bool onMainPath;
    }

    private void Awake()
    {
        if (fabrica == null) fabrica = FindFirstObjectByType<FabricaEnemigos>();
    }

    // Entry point, called by GameManagerProcedural right after a room is generated.
    // Uses room.difficulty, which already holds the score multiplier the room was built with.
    public List<GameObject> PopulateRoom(RoomData room, int seed)
    {
        if (roomGenerator == null || roomGenerator.Config == null)
        {
            Debug.LogError("[EnemyPlacer] RoomGenerator (with its config) is not assigned.");
            return new List<GameObject>();
        }

        var rng = new Random(seed);
        int count = PickCount(room.difficulty, rng);

        List<EnemyPlaceholder> placeholders = PlacePlaceholders(room, count, rng);   // pass 1
        if (!spawnEnemies) return new List<GameObject>();
        return SpawnEnemies(room, placeholders, rng);                                 // pass 2
    }

    // Placeholders of a room, for whoever spawns the real enemies
    public static EnemyPlaceholder[] GetPlaceholders(RoomData room) =>
        room.root != null ? room.root.GetComponentsInChildren<EnemyPlaceholder>() : new EnemyPlaceholder[0];

    // ---------- Count and levels from the multiplier ----------

    private float Difficulty01(float multiplier) =>
        Mathf.InverseLerp(multiplierRange.x, multiplierRange.y, multiplier);

    private int PickCount(float multiplier, Random rng)
    {
        float raw = Mathf.Lerp(minEnemies, maxEnemies, Difficulty01(multiplier)) + Spread(rng, countNoise);
        int count = Mathf.Clamp(Mathf.RoundToInt(raw), minEnemies, maxEnemies);
        return Mathf.Min(count, 3 * maxPerKind);   // the per-kind cap can never fit more than this
    }

    private int GearLevel(float t, Random rng)
    {
        int max = CatalogoEnemigos.Niveles;
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1, max, t) + Spread(rng, gearSpread)), 1, max);
    }

    private static float Spread(Random rng, float amount) => (float)(rng.NextDouble() * 2.0 - 1.0) * amount;

    // ---------- Pass 1: placeholders ----------

    private List<EnemyPlaceholder> PlacePlaceholders(RoomData room, int count, Random rng)
    {
        RoomGenConfig c = roomGenerator.Config;
        int safeX = c.wallThickness + safeZoneFromEntry;

        var result = new List<EnemyPlaceholder>();
        var perKind = new int[3];
        List<Surface> surfaces = room.surfaces.FindAll(s => s.xMax >= safeX);

        int attempts = Mathf.Max(1, count) * attemptsPerEnemy;
        for (int a = 0; a < attempts && result.Count < count; a++)
        {
            Candidate cand;
            bool sampleAir = surfaces.Count == 0 || rng.NextDouble() < airChance;
            bool ok = sampleAir
                ? TrySampleAir(room, c, safeX, rng, out cand)
                : TrySampleGround(room, surfaces, safeX, rng, out cand);
            if (!ok || TooClose(cand.local, result)) continue;

            EnemyKind kind = EnemyPlaceholder.Classify(cand.inAir, cand.surfaceKind, cand.onMainPath);
            if (perKind[(int)kind] >= maxPerKind) continue;   // max 2 of the same kind

            perKind[(int)kind]++;
            EnemyPlaceholder ph = CreatePlaceholder(room, cand, kind, result.Count);
            if (kind == EnemyKind.Flyer)
                ph.waypoints = BuildFlyerRoute(room, ph, rng);
            result.Add(ph);

            if (logPlacement)
                Debug.Log($"[EnemyPlacer] Room {room.index}: placeholder {kind} at {ph.localCell} " +
                          $"(air {ph.inAir}, surface {ph.surfaceKind}, main path {ph.onMainPath})");
        }

        if (result.Count < count)
            Debug.LogWarning($"[EnemyPlacer] Room {room.index}: only {result.Count} of {count} placeholders fit.");
        return result;
    }

    // A spot standing on one of the room surfaces, outside the safe zone
    private bool TrySampleGround(RoomData room, List<Surface> surfaces, int safeX, Random rng, out Candidate cand)
    {
        cand = default;
        Surface s = PickSurfaceByLength(surfaces, safeX, rng);

        int xMin = Mathf.Max(s.xMin, safeX);
        if (xMin > s.xMax) return false;

        int x = rng.Next(xMin, s.xMax + 1);
        int y = s.y + 1;

        // Check the real tiles: the floor may have been clipped (exit pad) or carved (secret room pit)
        if (!IsSolidLocal(room, x, y - 1)) return false;
        for (int k = 0; k < groundClearance; k++)
            if (IsSolidLocal(room, x, y + k)) return false;

        cand = new Candidate { local = new Vector2Int(x, y), inAir = false, surfaceKind = s.kind, onMainPath = s.onMainPath };
        return true;
    }

    // A spot floating in open air with nothing right under it
    private bool TrySampleAir(RoomData room, RoomGenConfig c, int safeX, Random rng, out Candidate cand)
    {
        cand = default;
        int xLo = safeX + airRadius;
        int xHi = c.width - c.wallThickness - 2 - airRadius;
        int yLo = c.floorThickness + airEmptyBelow + airRadius;
        int yHi = c.height - c.ceilingThickness - 2 - airRadius;
        if (xLo > xHi || yLo > yHi) return false;

        int x = rng.Next(xLo, xHi + 1);
        int y = rng.Next(yLo, yHi + 1);

        if (!IsBoxEmpty(room, x, y, airRadius)) return false;
        for (int k = 1; k <= airEmptyBelow; k++)
            if (IsSolidLocal(room, x, y - airRadius - k)) return false;

        cand = new Candidate { local = new Vector2Int(x, y), inAir = true };
        return true;
    }

    // Longer surfaces get picked more often, so the spots spread evenly along the room
    private static Surface PickSurfaceByLength(List<Surface> surfaces, int safeX, Random rng)
    {
        int total = 0;
        foreach (var s in surfaces) total += UsableLength(s, safeX);

        int roll = rng.Next(Mathf.Max(total, 1));
        foreach (var s in surfaces)
        {
            roll -= UsableLength(s, safeX);
            if (roll < 0) return s;
        }
        return surfaces[surfaces.Count - 1];
    }

    private static int UsableLength(Surface s, int safeX) => Mathf.Max(0, s.xMax - Mathf.Max(s.xMin, safeX) + 1);

    private bool TooClose(Vector2Int local, List<EnemyPlaceholder> placed)
    {
        float min2 = minSpacing * minSpacing;
        foreach (var p in placed)
            if ((p.localCell - local).sqrMagnitude < min2) return true;
        return false;
    }

    private EnemyPlaceholder CreatePlaceholder(RoomData room, Candidate cand, EnemyKind kind, int number)
    {
        Vector3Int cell = room.originCell + new Vector3Int(cand.local.x, cand.local.y, 0);

        var go = new GameObject($"EnemyPlaceholder_{number}");
        go.transform.SetParent(room.root, false);
        go.transform.position = roomGenerator.CellCenterWorld(cell);

        var ph = go.AddComponent<EnemyPlaceholder>();
        ph.roomIndex = room.index;
        ph.cell = cell;
        ph.localCell = cand.local;
        ph.kind = kind;
        ph.inAir = cand.inAir;
        ph.surfaceKind = cand.surfaceKind;
        ph.onMainPath = cand.onMainPath;
        return ph;
    }

    // ---------- Pass 2: enemies ----------

    private List<GameObject> SpawnEnemies(RoomData room, List<EnemyPlaceholder> placeholders, Random rng)
    {
        var spawned = new List<GameObject>();
        if (fabrica == null)
        {
            Debug.LogError("[EnemyPlacer] No FabricaEnemigos in the scene: placeholders were left without enemies.");
            return spawned;
        }

        float multiplier = room.difficulty;
        float t = Difficulty01(multiplier);

        foreach (var ph in placeholders)
        {
            EnemyKind kind = ph.kind;
            int helmet = GearLevel(t, rng);
            int shoes = GearLevel(t, rng);
            int aura = GearLevel(t, rng);

            GameObject enemy = fabrica.Crear(new[] { EnemyPlaceholder.CatalogName(kind) }, helmet, shoes, aura,
                                             out GameObject projectile, ph.transform.position, room.root);
            if (enemy == null) continue;

            ConfigureEnemy(enemy, room, ph, kind, multiplier, helmet, shoes, aura, projectile);
            ph.spawnedEnemy = enemy;
            spawned.Add(enemy);

            if (logPlacement)
                Debug.Log($"[EnemyPlacer] Room {room.index}: spawned '{enemy.name}' on placeholder {ph.name} (x{multiplier:F2})");
        }

        if (!keepPlaceholders)
            foreach (var ph in placeholders) Destroy(ph.gameObject);

        return spawned;
    }

    private void ConfigureEnemy(GameObject enemy, RoomData room, EnemyPlaceholder ph, EnemyKind kind, float multiplier,
                                int helmet, int shoes, int aura, GameObject projectile)
    {
        var info = enemy.GetComponent<ProceduralEnemyInfo>();
        if (info == null) info = enemy.AddComponent<ProceduralEnemyInfo>();
        info.roomIndex = room.index;
        info.kind = kind;
        info.multiplier = multiplier;
        info.helmet = helmet;
        info.shoes = shoes;
        info.aura = aura;
        info.waypoints = ph.waypoints;
        info.projectilePrefab = projectile;

        // The player's attack only hits objects tagged "Enemy" that have EnemyHealth
        enemy.tag = "Enemy";
        var health = enemy.GetComponent<EnemyHealth>();
        float startHealth = health != null ? health.vida : baseHealth;
        if (health == null) health = enemy.AddComponent<EnemyHealth>();
        health.vida = startHealth * multiplier;

        // Hook up the scripts that exist today; the final behaviours can read ProceduralEnemyInfo instead
        var demo = enemy.GetComponent<ComportamientoDemo>();
        if (demo != null) demo.proyectilPrefab = projectile;

        var bat = enemy.GetComponent<BatEnemy>();
        if (bat != null && ph.waypoints != null) bat.patrolPoints = ph.waypoints;
    }

    // ---------- Flyer route ----------

    private Transform[] BuildFlyerRoute(RoomData room, EnemyPlaceholder ph, Random rng)
    {
        RoomGenConfig c = roomGenerator.Config;
        Vector2Int p = ph.localCell;
        int minX = c.wallThickness + safeZoneFromEntry + airRadius;   // the route never enters the safe zone
        int maxX = c.width - c.wallThickness - 2 - airRadius;

        int left = FreeSteps(room, p, Vector2Int.left, flyerRange, minX, maxX);
        int right = FreeSteps(room, p, Vector2Int.right, flyerRange, minX, maxX);

        var points = new List<Vector2Int>();
        if (left + right >= minFlyerSpan)
        {
            // Horizontal patrol, each end somewhere between half and all of the free space
            var a = new Vector2Int(p.x - RandomReach(left, rng), p.y);
            var b = new Vector2Int(p.x + RandomReach(right, rng), p.y);
            points.Add(a);
            points.Add(b);

            // Optional middle point above or below: A -> B -> mid -> A makes a triangle
            if (rng.NextDouble() < flyerTriangleChance)
            {
                int dy = rng.NextDouble() < 0.5 ? flyerBob : -flyerBob;
                var mid = new Vector2Int((a.x + b.x) / 2, p.y + dy);
                if (IsBoxEmpty(room, mid.x, mid.y, airRadius) && IsSegmentClear(room, b, mid) && IsSegmentClear(room, mid, a))
                    points.Add(mid);
            }
        }
        else
        {
            // Not enough room sideways: patrol up and down
            int up = FreeSteps(room, p, Vector2Int.up, flyerRange / 2, minX, maxX);
            int down = FreeSteps(room, p, Vector2Int.down, flyerRange / 2, minX, maxX);
            points.Add(new Vector2Int(p.x, p.y + Mathf.Max(1, RandomReach(up, rng))));
            points.Add(new Vector2Int(p.x, p.y - RandomReach(down, rng)));
        }

        var routeRoot = new GameObject($"FlyerRoute_{ph.name}");
        routeRoot.transform.SetParent(room.root, false);   // not under the enemy, or the points would move with it

        var route = new Transform[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            var wp = new GameObject($"Waypoint_{i}");
            wp.transform.SetParent(routeRoot.transform, false);
            wp.transform.position = roomGenerator.CellCenterWorld(room.originCell + new Vector3Int(points[i].x, points[i].y, 0));
            route[i] = wp.transform;
        }
        return route;
    }

    private static int RandomReach(int free, Random rng) => free <= 0 ? 0 : rng.Next((free + 1) / 2, free + 1);

    // How many cells the flyer can move in a straight line before touching a tile or leaving the allowed columns
    private int FreeSteps(RoomData room, Vector2Int from, Vector2Int dir, int max, int minX, int maxX)
    {
        RoomGenConfig c = roomGenerator.Config;
        int minY = c.floorThickness + airRadius;
        int maxY = c.height - c.ceilingThickness - 1 - airRadius;

        int steps = 0;
        Vector2Int cur = from;
        while (steps < max)
        {
            Vector2Int next = cur + dir;
            if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY) break;
            if (!IsBoxEmpty(room, next.x, next.y, airRadius)) break;
            cur = next;
            steps++;
        }
        return steps;
    }

    private bool IsSegmentClear(RoomData room, Vector2Int a, Vector2Int b)
    {
        int samples = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
        for (int i = 0; i <= samples; i++)
        {
            Vector2 pt = Vector2.Lerp(a, b, samples == 0 ? 0f : (float)i / samples);
            if (IsSolidLocal(room, Mathf.RoundToInt(pt.x), Mathf.RoundToInt(pt.y))) return false;
        }
        return true;
    }

    // ---------- Tile queries (room local coordinates) ----------

    private bool IsSolidLocal(RoomData room, int x, int y) =>
        roomGenerator.IsSolid(room.originCell + new Vector3Int(x, y, 0));

    private bool IsBoxEmpty(RoomData room, int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
            for (int y = cy - radius; y <= cy + radius; y++)
                if (IsSolidLocal(room, x, y)) return false;
        return true;
    }
}
