using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

// Art pass of the procedural rooms: plants stuck to the floor, ceilings and walls, and neon signs in the open areas.
// The run goes from a cave full of plants to a neon city: neonRatio 0 (first room) = only plants,
// 1 (the last RoomDecorationSet.neonOnlyRoomsAtEnd rooms) = only neon, and the rooms in between mix both in that
// proportion. Secret rooms always get neon. Neon signs are spread over the whole run so they repeat as little as
// possible. Everything is only visual: no colliders, nothing the player touches.
public class RoomDecorator
{
    private readonly RoomGenerator gen;
    private readonly RoomDecorationSet set;

    private struct Placed { public Rect rect; }

    // How many times each neon sign has been used in this run (one decorator per run)
    private readonly Dictionary<DecorationPiece, int> neonUses = new Dictionary<DecorationPiece, int>();

    public RoomDecorator(RoomGenerator generator, RoomDecorationSet decorationSet)
    {
        gen = generator;
        set = decorationSet;
    }

    public static RoomDecorationSet LoadDefaultSet() => Resources.Load<RoomDecorationSet>(RoomDecorationSet.ResourcePath);

    public bool IsReady => gen != null && set != null;

    // Room 'index' of a run of 'totalRooms': the plants -> neon mix comes from the set
    public void DecorateRoom(RoomData room, int totalRooms, int seed)
    {
        if (!IsReady || room == null) return;
        DecorateRoom(room, set.NeonRatio(room.index, totalRooms), seed);
    }

    // neonRatio: 0 = plants only, 1 = neon only
    public void DecorateRoom(RoomData room, float neonRatio, int seed)
    {
        if (!IsReady || room == null || room.root == null) return;
        var c = gen.Config;
        var area = new RectInt(room.originCell.x, room.originCell.y, room.width, c.height);

        // Keep the secret door block clean (it has to read as a wall you can break)
        RectInt? keepOut = null;
        if (room.hasSecretRoom)
        {
            RectInt s = room.secretEntrance;
            keepOut = new RectInt(s.x - 1, s.y - 1, s.width + 2, s.height + 2);
        }

        Decorate(room.root, "Decorations", area, neonRatio, seed, set.plantsPerRoom, set.neonsPerRoom, 0, keepOut,
                 $"room {room.index}");
    }

    // area: the secret room's cells (world). Always neon, at least neonsPerSecretRoom.x signs.
    public void DecorateSecretRoom(RectInt area, Transform parent, int seed)
    {
        if (!IsReady || parent == null) return;
        Decorate(parent, "SecretDecorations", area, 1f, seed, Vector2Int.zero, set.neonsPerSecretRoom,
                 set.neonsPerSecretRoom.x, null, "secret room");
    }

    // ---------- core ----------

    private void Decorate(Transform parent, string rootName, RectInt area, float neonRatio, int seed,
                          Vector2Int plantRange, Vector2Int neonRange, int minNeons, RectInt? keepOut, string label)
    {
        var rng = new Random(seed);
        neonRatio = Mathf.Clamp01(neonRatio);

        var root = new GameObject(rootName).transform;
        root.SetParent(parent, false);
        root.position = Vector3.zero;

        int plantCount = RoundRandom(rng, Roll(rng, plantRange) * (1f - neonRatio));
        int neonCount = Mathf.Max(minNeons, RoundRandom(rng, Roll(rng, neonRange) * neonRatio));
        if (neonRatio <= 0f) neonCount = 0;
        if (neonRatio >= 1f) plantCount = 0;
        neonCount = Mathf.Min(neonCount, set.maxNeonsPerRoom);

        // Local cells that the room camera shows (decoration out of frame is wasted)
        Rect visible = gen.VisibleLocalRect(gen.Config, 0.5f);

        var placed = new List<Placed>();
        int plantsDone = 0, neonsDone = 0;

        // Neon first: signs need big free areas, plants fit almost anywhere
        for (int i = 0; i < neonCount; i++)
            if (PlaceNeon(root, area, visible, keepOut, rng, placed)) neonsDone++;

        var spots = FindPlantSpots(area, visible, keepOut);
        for (int i = 0; i < plantCount; i++)
            if (PlacePlant(root, area, spots, rng, placed)) plantsDone++;

        Debug.Log($"[RoomDecorator] {label}: neon ratio {neonRatio:F2} -> {plantsDone}/{plantCount} plants, " +
                  $"{neonsDone}/{neonCount} neon signs");
    }

    // ---------- plants ----------

    private struct Spot
    {
        public Vector2Int cell;   // local cell (free), next to the rock it grows from
        public DecorationSpot kind;
        public int side;          // walls: -1 = rock on the left, +1 = rock on the right
    }

    private List<Spot> FindPlantSpots(RectInt area, Rect visible, RectInt? keepOut)
    {
        var spots = new List<Spot>();
        for (int x = 1; x < area.width - 1; x++)
            for (int y = 1; y < area.height - 1; y++)
            {
                if (!Inside(visible, x + 0.5f, y + 0.5f) || Blocked(area, keepOut, x, y) || Solid(area, x, y)) continue;
                var cell = new Vector2Int(x, y);
                if (Solid(area, x, y - 1)) spots.Add(new Spot { cell = cell, kind = DecorationSpot.Floor });
                if (Solid(area, x, y + 1)) spots.Add(new Spot { cell = cell, kind = DecorationSpot.Ceiling });
                if (Solid(area, x - 1, y) && !Solid(area, x + 1, y)) spots.Add(new Spot { cell = cell, kind = DecorationSpot.Wall, side = -1 });
                if (Solid(area, x + 1, y) && !Solid(area, x - 1, y)) spots.Add(new Spot { cell = cell, kind = DecorationSpot.Wall, side = 1 });
            }
        return spots;
    }

    private bool PlacePlant(Transform root, RectInt area, List<Spot> spots, Random rng, List<Placed> placed)
    {
        DecorationPiece piece = PickWeighted(set.plants, rng);
        if (piece == null || spots.Count == 0) return false;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            Spot s = spots[rng.Next(spots.Count)];
            if ((piece.spots & s.kind) == 0) continue;

            float h = Lerp(rng, piece.height);
            float w = h * Aspect(piece);
            if (!PlantFits(area, s, ref w, ref h, piece)) continue;

            Vector2 center = PlantCenter(s, w, h);
            Rect rect = new Rect(center.x - w / 2f, center.y - h / 2f, w, h);
            if (Overlaps(rect, placed, 0.55f)) continue;

            GameObject go = BuildPiece(piece, h, set.plantSortingLayer, 0, rng);
            go.transform.SetParent(root, false);
            Vector3 corner = gen.CellToWorld(new Vector3Int(area.x, area.y, 0));
            go.transform.position = corner + new Vector3(center.x, center.y, 0f);
            placed.Add(new Placed { rect = rect });
            return true;
        }
        return false;
    }

    // Size checks, in local cell units. The part that goes into the rock (plantSink) is hidden behind the tiles.
    private bool PlantFits(RectInt area, Spot s, ref float w, ref float h, DecorationPiece piece)
    {
        int x = s.cell.x, y = s.cell.y;
        float sink = set.plantSink;
        switch (s.kind)
        {
            case DecorationSpot.Floor:
            {
                // ground under the whole width, air over it
                float cx = x + 0.5f;
                for (int col = Mathf.FloorToInt(cx - w / 2f + 0.25f); col <= Mathf.FloorToInt(cx + w / 2f - 0.25f); col++)
                    if (Solid(area, col, y) || !Solid(area, col, y - 1)) return false;
                for (int k = 0; k < Mathf.CeilToInt(h * (1f - sink)); k++)
                    if (Solid(area, x, y + k)) return false;
                return true;
            }
            case DecorationSpot.Ceiling:
            {
                // hangs down as far as there is air (shortened if needed), rock over the whole width
                float cx = x + 0.5f;
                for (int col = Mathf.FloorToInt(cx - w / 2f + 0.25f); col <= Mathf.FloorToInt(cx + w / 2f - 0.25f); col++)
                    if (Solid(area, col, y) || !Solid(area, col, y + 1)) return false;
                int air = 0;
                while (air < 12 && !Solid(area, x, y - air)) air++;
                float maxH = (air - 1.5f) / (1f - sink);   // keep a gap over the floor / the player's head
                if (maxH < 1.2f) return false;
                if (h > maxH) { w *= maxH / h; h = maxH; }
                return true;
            }
            case DecorationSpot.Wall:
            {
                // rock along the whole height on the wall side, air in front
                float cy = y + 0.5f;
                for (int row = Mathf.FloorToInt(cy - h / 2f + 0.25f); row <= Mathf.FloorToInt(cy + h / 2f - 0.25f); row++)
                    if (!Solid(area, x + s.side, row) || Solid(area, x, row)) return false;
                return true;
            }
        }
        return false;
    }

    private Vector2 PlantCenter(Spot s, float w, float h)
    {
        float sink = set.plantSink;
        int x = s.cell.x, y = s.cell.y;
        switch (s.kind)
        {
            case DecorationSpot.Floor:   return new Vector2(x + 0.5f, y + h / 2f - sink * h);
            case DecorationSpot.Ceiling: return new Vector2(x + 0.5f, y + 1f - h / 2f + sink * h);
            default:
                return s.side < 0
                    ? new Vector2(x + w / 2f - sink * w, y + 0.5f)
                    : new Vector2(x + 1f - w / 2f + sink * w, y + 0.5f);
        }
    }

    // ---------- neon ----------

    private bool PlaceNeon(Transform root, RectInt area, Rect visible, RectInt? keepOut, Random rng, List<Placed> placed)
    {
        // Least used sign of the run so far: a sign only comes back once every other one has been used as often
        DecorationPiece piece = PickLeastUsedNeon(rng);
        if (piece == null) return false;
        neonUses[piece] = UsesOf(piece) + 1;   // counted even if it doesn't fit, so the next pick tries another one

        var c = gen.Config;
        int m = set.neonMargin;
        float h = Lerp(rng, piece.height);

        for (int shrink = 0; shrink < 3; shrink++, h *= 0.8f)   // down to 64 %: smaller signs look lost
        {
            float w = h * Aspect(piece);
            // away from the side walls, where the doors are
            float xMin = Mathf.Max(visible.xMin, c.wallThickness + 2f) + w / 2f + m;
            float xMax = Mathf.Min(visible.xMax, area.width - c.wallThickness - 2f) - w / 2f - m;
            float yMin = Mathf.Max(visible.yMin, c.floorThickness + 1f) + h / 2f + m;
            float yMax = Mathf.Min(visible.yMax, area.height - c.ceilingThickness) - h / 2f - m;
            if (xMin > xMax || yMin > yMax) continue;

            for (int attempt = 0; attempt < 80; attempt++)
            {
                var center = new Vector2(Mathf.Lerp(xMin, xMax, (float)rng.NextDouble()),
                                         Mathf.Lerp(yMin, yMax, (float)rng.NextDouble()));
                var rect = new Rect(center.x - w / 2f, center.y - h / 2f, w, h);
                if (!AreaFree(area, keepOut, rect, m) || Overlaps(rect, placed, 0f, m)) continue;

                GameObject go = BuildPiece(piece, h, set.neonSortingLayer, 0, rng);
                go.transform.SetParent(root, false);
                Vector3 corner = gen.CellToWorld(new Vector3Int(area.x, area.y, 0));
                go.transform.position = corner + new Vector3(center.x, center.y, 0f);
                if (set.neonFlicker && piece.prefab == null) go.AddComponent<NeonSignFlicker>();
                placed.Add(new Placed { rect = rect });
                return true;
            }
        }
        return false;
    }

    // Every cell the rect touches (grown by 'margin') is air
    private bool AreaFree(RectInt area, RectInt? keepOut, Rect r, int margin)
    {
        for (int x = Mathf.FloorToInt(r.xMin) - margin; x <= Mathf.FloorToInt(r.xMax - 0.001f) + margin; x++)
            for (int y = Mathf.FloorToInt(r.yMin) - margin; y <= Mathf.FloorToInt(r.yMax - 0.001f) + margin; y++)
                if (Solid(area, x, y) || Blocked(area, keepOut, x, y)) return false;
        return true;
    }

    // ---------- building the sprites ----------

    // One GameObject per piece, its sprites put back where they are in their texture, scaled to the height.
    // A prefab piece is instantiated as it is (its own sorting layer, material and animation) and only scaled.
    private GameObject BuildPiece(DecorationPiece p, float height, string layer, int order, Random rng)
    {
        if (p.prefab != null && PrefabSize(p.prefab, out Vector2 size))
        {
            GameObject inst = Object.Instantiate(p.prefab);
            if (!string.IsNullOrEmpty(p.name)) inst.name = p.name;
            float k = height / size.y;
            Vector3 s0 = p.prefab.transform.localScale;
            bool fx = p.randomFlipX && rng.NextDouble() < 0.5;
            inst.transform.localScale = new Vector3((fx ? -1f : 1f) * s0.x * k, (p.flipY ? -1f : 1f) * s0.y * k, s0.z);
            return inst;
        }

        var go = new GameObject(string.IsNullOrEmpty(p.name) ? "Decoration" : p.name);
        if (!Bounds(p, out Rect u, out float ppu)) return go;

        foreach (Sprite s in p.sprites)
        {
            if (s == null) continue;
            var child = new GameObject(s.name);
            child.transform.SetParent(go.transform, false);
            Vector2 pivotPx = s.rect.position + s.pivot;
            child.transform.localPosition = (pivotPx - u.center) / ppu;
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = p.color;
            if (!string.IsNullOrEmpty(layer)) sr.sortingLayerName = layer;
            sr.sortingOrder = order;
        }

        float scale = height / (u.height / ppu);
        bool flipX = p.randomFlipX && rng.NextDouble() < 0.5;
        go.transform.localScale = new Vector3(flipX ? -scale : scale, p.flipY ? -scale : scale, 1f);
        return go;
    }

    private static bool Bounds(DecorationPiece p, out Rect union, out float ppu)
    {
        union = default;
        ppu = 100f;
        bool any = false;
        if (p == null) return false;
        if (p.prefab != null)
        {
            if (!PrefabSize(p.prefab, out Vector2 size)) return false;
            union = new Rect(0f, 0f, size.x, size.y);
            ppu = 1f;
            return true;
        }
        if (p.sprites == null) return false;
        foreach (Sprite s in p.sprites)
        {
            if (s == null) continue;
            Rect r = s.rect;
            union = any ? Rect.MinMaxRect(Mathf.Min(union.xMin, r.xMin), Mathf.Min(union.yMin, r.yMin),
                                          Mathf.Max(union.xMax, r.xMax), Mathf.Max(union.yMax, r.yMax)) : r;
            ppu = s.pixelsPerUnit;
            any = true;
        }
        return any && union.height > 0f;
    }

    // Size of a prefab as it is saved: the box around its sprites, times the root's scale
    private static bool PrefabSize(GameObject prefab, out Vector2 size)
    {
        size = Vector2.zero;
        bool any = false;
        Bounds total = default;
        Transform root = prefab.transform;
        foreach (SpriteRenderer sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            Matrix4x4 m = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix * sr.transform.localToWorldMatrix;
            Bounds b = sr.sprite.bounds;
            Vector3 a = m.MultiplyPoint3x4(b.min), c = m.MultiplyPoint3x4(b.max);
            var bb = new Bounds((a + c) / 2f, new Vector3(Mathf.Abs(c.x - a.x), Mathf.Abs(c.y - a.y), 0f));
            if (any) total.Encapsulate(bb); else total = bb;
            any = true;
        }
        if (!any || total.size.y <= 0f) return false;
        size = total.size;
        return true;
    }

    private static float Aspect(DecorationPiece p) => Bounds(p, out Rect u, out _) ? u.width / u.height : 1f;

    // ---------- helpers ----------

    private bool Solid(RectInt area, int x, int y) => gen.IsSolid(new Vector3Int(area.x + x, area.y + y, 0));

    private static bool Blocked(RectInt area, RectInt? keepOut, int x, int y) =>
        keepOut.HasValue && keepOut.Value.Contains(new Vector2Int(x, y));

    private static bool Inside(Rect r, float x, float y) => x >= r.xMin && x <= r.xMax && y >= r.yMin && y <= r.yMax;

    // overlapAllowed: fraction of the smaller rect that may be covered (plants may touch a bit); margin in tiles
    private static bool Overlaps(Rect r, List<Placed> placed, float overlapAllowed, float margin = 0f)
    {
        foreach (Placed p in placed)
        {
            Rect o = p.rect;
            float ix = Mathf.Min(r.xMax + margin, o.xMax) - Mathf.Max(r.xMin - margin, o.xMin);
            float iy = Mathf.Min(r.yMax + margin, o.yMax) - Mathf.Max(r.yMin - margin, o.yMin);
            if (ix <= 0f || iy <= 0f) continue;
            float smaller = Mathf.Min(r.width * r.height, o.width * o.height);
            if (ix * iy > overlapAllowed * smaller) return true;
        }
        return false;
    }

    private static DecorationPiece PickWeighted(List<DecorationPiece> list, Random rng, HashSet<DecorationPiece> skip = null)
    {
        if (list == null || list.Count == 0) return null;
        bool Usable(DecorationPiece p) => p != null && p.weight > 0f && (skip == null || !skip.Contains(p)) && Bounds(p, out _, out _);
        float total = 0f;
        foreach (var p in list) if (Usable(p)) total += p.weight;
        if (total <= 0f) return null;
        double pick = rng.NextDouble() * total;
        foreach (var p in list)
        {
            if (!Usable(p)) continue;
            pick -= p.weight;
            if (pick <= 0) return p;
        }
        return null;
    }

    private int UsesOf(DecorationPiece p) => neonUses.TryGetValue(p, out int n) ? n : 0;

    private DecorationPiece PickLeastUsedNeon(Random rng)
    {
        int fewest = int.MaxValue;
        foreach (DecorationPiece p in set.neons)
            if (p != null && p.weight > 0f && Bounds(p, out _, out _)) fewest = Mathf.Min(fewest, UsesOf(p));
        if (fewest == int.MaxValue) return null;

        var usedMore = new HashSet<DecorationPiece>();
        foreach (DecorationPiece p in set.neons)
            if (p != null && UsesOf(p) != fewest) usedMore.Add(p);
        return PickWeighted(set.neons, rng, usedMore);
    }

    private static int Roll(Random rng, Vector2Int range) =>
        range.y <= range.x ? range.x : rng.Next(range.x, range.y + 1);

    private static float Lerp(Random rng, Vector2 range) => Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());

    // 2.3 -> 2 (70 %) or 3 (30 %): keeps the gradual mix between rooms
    private static int RoundRandom(Random rng, float value)
    {
        int whole = Mathf.FloorToInt(value);
        return whole + (rng.NextDouble() < value - whole ? 1 : 0);
    }
}
