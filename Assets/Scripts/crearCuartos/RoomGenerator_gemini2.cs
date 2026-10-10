using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

public class RoomGenerator_gemini2 : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RoomGenConfig config;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap platformTilemap;
    [SerializeField] private CinemachineCamera roomCameraPrefab;
    [SerializeField] private Transform roomsParent;

    [Header("Parámetros del Autómata Celular")]
    [Range(0, 100)] [SerializeField] private int shellNoisePercent = 40;
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

    public RoomData Generate(int index, int seed, RoomData previous)
    {
        var c = config;
        var rng = new Random(seed);

        int entryRow = previous == null ? c.floorThickness : PickDoorRow(c, rng);
        int exitRow = PickDoorRow(c, rng);

        // FORZAR DIVERGENCIA: Si las puertas están a la misma altura baja, elevamos una intencionalmente
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

        // 1. Cascarón limpio
        PaintShellSolid(c, origin, entryRow, exitRow);

        // 2. Construir layout filtrado
        BuildLayoutWithFilters(c, origin, rng, entryRow, exitRow);

        return data;
    }

    private static int PickDoorRow(RoomGenConfig c, Random rng)
    {
        int row = c.doorRows[rng.Next(c.doorRows.Length)];
        return Mathf.Clamp(row, c.floorThickness, c.height - c.ceilingThickness - c.doorHeight);
    }

    // ---------- Cascarón Sólido (Limpio y sin huecos falsos en el techo) ----------

    private void PaintShellSolid(RoomGenConfig c, Vector3Int origin, int entryRow, int exitRow)
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
                {
                    groundTilemap.SetTile(origin + new Vector3Int(x, y, 0), c.solidTile);
                }
            }
        }
    }

    // ---------- Layout con Filtros de Jugabilidad ----------

    private void BuildLayoutWithFilters(RoomGenConfig c, Vector3Int origin, Random rng, int entryRow, int exitRow)
    {
        int floorTop = c.floorThickness - 1;
        var entryPad = new Surface { xMin = c.wallThickness, xMax = c.wallThickness + c.ledgeLength - 1, y = entryRow - 1 };
        var exitPad = new Surface { xMin = c.width - c.wallThickness - c.ledgeLength, xMax = c.width - c.wallThickness - 1, y = exitRow - 1 };

        var walkable = new List<Surface>();
        var obstacles = new List<Surface>();
        Tilemap platMap = platformTilemap != null ? platformTilemap : groundTilemap;

        // Agregar salientes de las puertas
        PaintSurface(groundTilemap, c.solidTile, origin, entryPad);
        walkable.Add(entryPad); obstacles.Add(entryPad);

        PaintSurface(groundTilemap, c.solidTile, origin, exitPad);
        walkable.Add(exitPad); obstacles.Add(exitPad);

        // FORZAR NODO ELEVADO (Punto obligatorio en el aire para romper la linealidad)
        int midX = c.width / 2;
        int minElevatedY = Mathf.Max(entryRow, exitRow) + 2;
        int maxElevatedY = c.height - c.ceilingThickness - c.playerHeight - 2;
        int targetY = Mathf.Clamp(rng.Next(minElevatedY, maxElevatedY + 1), floorTop + c.maxJumpHeight, maxElevatedY);

        int midLen = rng.Next(c.platformLength.x, c.platformLength.y + 1);
        var midPlatform = new Surface { xMin = midX - midLen / 2, xMax = midX + midLen / 2, y = targetY };

        // FILTRO 1 & 5: Validar que el nodo elevado cumpla las alturas y espacio
        if (ValidatePlatform(c, midPlatform, obstacles))
        {
            // Intentar conectar Entrada -> Plataforma Central -> Salida
            List<Surface> path1 = TryChain(c, rng, entryPad, midPlatform, obstacles);
            List<Surface> path2 = TryChain(c, rng, midPlatform, exitPad, obstacles);

            if (path1 != null && path2 != null)
            {
                AddAll(path1, c, origin, platMap, walkable, obstacles);
                AddAll(new List<Surface> { midPlatform }, c, origin, platMap, walkable, obstacles);
                AddAll(path2, c, origin, platMap, walkable, obstacles);
            }
        }

        // AGREGAR PLATAFORMAS EXTRA CON FILTROS STRICTOS
        int extra = rng.Next(c.minPlatforms, c.maxPlatforms + 1);
        
        // FILTRO DE ALTURA MÍNIMA: Las plataformas no pueden estar pegadas al suelo
        int yMin = floorTop + c.maxJumpHeight; 
        int yMax = c.height - c.ceilingThickness - 1 - c.playerHeight;

        for (int i = 0, placed = 0; i < c.placementAttempts && placed < extra; i++)
        {
            int len = rng.Next(c.platformLength.x, c.platformLength.y + 1);
            int xLo = c.wallThickness + 2;
            int xHi = c.width - c.wallThickness - 2 - len;
            if (xHi < xLo || yMax < yMin) break;

            var candidate = new Surface
            {
                xMin = rng.Next(xLo, xHi + 1),
                y = rng.Next(yMin, yMax + 1)
            };
            candidate.xMax = candidate.xMin + len - 1;

            // REGLAS DEL FILTRO DE PLATAFORMAS:
            if (!ValidatePlatform(c, candidate, obstacles)) continue; // Filtro de solapamiento y espacio de cabeza
            if (!IsUsefulPlatform(c, candidate, walkable)) continue;  // Filtro de utilidad (no huérfana)

            AddAll(new List<Surface> { candidate }, c, origin, platMap, walkable, obstacles);
            placed++;
        }
    }

    // ---------- FILTROS DE VALIDACIÓN ----------

    // Valida espacio libre para el jugador y colisión con otras plataformas
    private static bool ValidatePlatform(RoomGenConfig c, Surface candidate, List<Surface> obstacles)
    {
        // 1. Limite vertical mínimo sobre el suelo (Filtro 1)
        if (candidate.y < c.floorThickness + c.maxJumpHeight - 1) return false;

        // 2. Limite vertical máximo respecto al techo
        if (candidate.y > c.height - c.ceilingThickness - c.playerHeight - 1) return false;

        // 3. Chequeo de espacio libre sobre la plataforma (Espacio de cabeza) y solapamientos
        foreach (var p in obstacles)
        {
            bool xOverlap = candidate.xMin <= p.xMax + 1 && candidate.xMax >= p.xMin - 1;
            
            // Forzar una distancia vertical de al menos playerHeight + 2 para poder saltar holgadamente
            if (xOverlap && Mathf.Abs(candidate.y - p.y) < c.playerHeight + 2) 
            {
                return false; 
            }
        }

        return true;
    }

    // Valida que la plataforma realmente sirva para saltar desde o hacia algo existente
    private static bool IsUsefulPlatform(RoomGenConfig c, Surface candidate, List<Surface> existing)
    {
        bool canBeReached = false;

        foreach (var surface in existing)
        {
            int dy = candidate.y - surface.y;
            
            // La plataforma debe estar en un rango alcanzable de salto (no flotando en la nada inalcanzable)
            if (Mathf.Abs(dy) <= c.maxJumpHeight && Mathf.Abs(dy) >= 2)
            {
                int gap = Mathf.Max(0, Mathf.Max(candidate.xMin, surface.xMin) - Mathf.Min(candidate.xMax, surface.xMax) - 1);
                if (gap <= c.maxJumpDistance)
                {
                    canBeReached = true;
                    break;
                }
            }
        }

        return canBeReached;
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
        int yMin = floorTop + c.maxJumpHeight;
        int yMax = c.height - c.ceilingThickness - 1 - c.playerHeight;
        int span = c.maxJumpDistance - 1;
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
                int mag = rng.Next(2, c.maxJumpHeight + 1);
                int dir = toward == 0 ? (rng.Next(2) == 0 ? 1 : -1) : (rng.NextDouble() < 0.8 ? toward : -toward);

                var s = new Surface { xMin = xMin, xMax = xMin + len - 1, y = cur.y + dir * mag };

                if (!ValidatePlatform(c, s, obstacles)) continue;

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
        if (Mathf.Abs(a.y - b.y) > c.maxJumpHeight) return false;
        int gap = Mathf.Max(0, Mathf.Max(a.xMin, b.xMin) - Mathf.Min(a.xMax, b.xMax) - 1);
        return gap <= c.maxJumpDistance;
    }
}