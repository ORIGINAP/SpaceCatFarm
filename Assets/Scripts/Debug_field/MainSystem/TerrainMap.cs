using System.Collections.Generic;
using UnityEngine;

// 시드 기반 지형, 세이브 안 함
public class TerrainMap
{
    public enum Layer { Patch = 0, Path = 1, Chasm = 2, Mesa = 3 }
    public const int LayerCount = 4;

    private const byte ObstacleMask = (1 << (int)Layer.Chasm) | (1 << (int)Layer.Mesa);

    private const int   ClearRadius   = 10;
    private const float PatchScale    = 0.05f;
    private const float PatchCut      = 0.60f;
    private const float PathScale     = 0.012f;
    private const float PathHalfWidth = 1.0f;
    private const float ChasmScale    = 0.02f;
    private const float ChasmHalfWidth = 1.1f;
    private const float ChasmSegScale = 0.03f;
    private const float ChasmSegCut   = 0.47f;
    private const float MesaScale     = 0.06f;
    private const float MesaCut       = 0.66f;

    private const int   SoilRadiusMin   = 8;
    private const int   SoilRadiusMax   = 12;
    private const float SoilAngleJitter = 0.7f;
    private const float SoilWobbleScale = 0.13f;

    public static TerrainMap Current => Services.Get<TerrainMap>();

    public int MinX { get; }
    public int MinY { get; }
    public int VertsX { get; }
    public int VertsY { get; }

    private readonly byte[] bits;
    // 꼭짓점별 특산 토양
    private readonly byte[] soilBits;
    private readonly List<SoilPatch> soilPatches = new();
    // 꼭짓점별 바이옴
    private readonly byte[] biomeBits;
    private readonly WorldTheme[] sectorBiome = new WorldTheme[SectorCount];
    private float sectorOffset;
    private readonly List<Landmark> landmarks = new();
    // 멀티 입장 스냅샷용
    private readonly HashSet<int> carved = new();
    private readonly int worldMinX, worldMinY, worldMaxX, worldMaxY;

    public IReadOnlyList<SoilPatch> SoilPatches => soilPatches;
    public IReadOnlyList<Landmark> Landmarks => landmarks;
    public WorldTheme StartBiome { get; private set; }

    public readonly struct Landmark
    {
        public readonly int        id;
        public readonly Vector2Int cell;
        public readonly WorldTheme biome;
        public Landmark(int id, Vector2Int cell, WorldTheme biome) { this.id = id; this.cell = cell; this.biome = biome; }
    }

    private const int   SectorCount        = 4;
    private const float CenterBiomeFrac    = 0.28f;
    private const float BiomeWobble        = 0.35f;
    private const int   LandmarksPerSector = 2;
    private const int   LandmarkClear      = 3;

    public event System.Action<List<Vector2Int>> OnCellsChanged;

    private TerrainMap(int minX, int minY, int vertsX, int vertsY, int gridWidth, int gridHeight)
    {
        MinX = minX; MinY = minY; VertsX = vertsX; VertsY = vertsY;
        bits = new byte[vertsX * vertsY];
        soilBits = new byte[vertsX * vertsY];
        biomeBits = new byte[vertsX * vertsY];
        worldMinX = -gridWidth / 2;  worldMaxX = worldMinX + gridWidth - 1;
        worldMinY = -gridHeight / 2; worldMaxY = worldMinY + gridHeight - 1;
    }

    public static TerrainMap Generate(int gridWidth, int gridHeight, int marginCells, int seed, bool obstacles, WorldTheme start)
    {
        int minX = -gridWidth / 2 - marginCells, minY = -gridHeight / 2 - marginCells;
        var map = new TerrainMap(minX, minY, gridWidth + marginCells * 2 + 1, gridHeight + marginCells * 2 + 1, gridWidth, gridHeight);
        map.PaintBiomes(seed, gridWidth, gridHeight, start);

        var rng = new System.Random(seed ^ 0x5EED7E);
        var o = new float[10];
        for (int k = 0; k < o.Length; k++) o[k] = (float)(rng.NextDouble() * 200.0);

        int vx = map.VertsX, vy = map.VertsY, n = vx * vy;
        var fPath = new float[n];
        var fChasm = new float[n];
        for (int j = 0; j < vy; j++)
        for (int i = 0; i < vx; i++)
        {
            float x = minX + i, y = minY + j;
            fPath[j * vx + i]  = Fbm(x * PathScale  + o[2], y * PathScale  + o[3]);
            fChasm[j * vx + i] = Fbm(x * ChasmScale + o[4], y * ChasmScale + o[5]);
        }

        for (int j = 0; j < vy; j++)
        for (int i = 0; i < vx; i++)
        {
            float x = minX + i, y = minY + j;
            byte b = 0;
            bool patch = Fbm(x * PatchScale + o[0], y * PatchScale + o[1]) > PatchCut;
            if (patch) b |= Bit(Layer.Patch);
            bool path = ContourDistance(fPath, vx, vy, i, j) < PathHalfWidth;
            if (path) b |= Bit(Layer.Path);

            if (obstacles && !path && x * x + y * y >= ClearRadius * ClearRadius)
            {
                bool chasm = ContourDistance(fChasm, vx, vy, i, j) < ChasmHalfWidth &&
                             Mathf.PerlinNoise(x * ChasmSegScale + o[6], y * ChasmSegScale + o[7]) > ChasmSegCut;
                if (chasm) b |= Bit(Layer.Chasm);
                else if (!patch && Fbm(x * MesaScale + o[8], y * MesaScale + o[9]) > MesaCut) b |= Bit(Layer.Mesa);
            }
            map.bits[j * vx + i] = b;
        }

        if (GameLaunchContext.LegacyWorld) map.PlaceSoilLegacy(seed, gridWidth, gridHeight);
        else map.PlaceSoil(seed, gridWidth, gridHeight);
        map.PlaceLandmarks(seed, gridWidth, gridHeight);

        if (obstacles)
        {
            map.RemoveSpecks();
            map.EnsureConnected(null);
        }
        Services.Replace(map);
        return map;
    }

    public static void Release(TerrainMap map)
    {
        Services.Unregister(map);
    }

    private static byte Bit(Layer layer) => (byte)(1 << (int)layer);

    private static float ContourDistance(float[] f, int vx, int vy, int i, int j)
    {
        int il = Mathf.Max(0, i - 1), ir = Mathf.Min(vx - 1, i + 1), jd = Mathf.Max(0, j - 1), ju = Mathf.Min(vy - 1, j + 1);
        float gx = (f[j * vx + ir] - f[j * vx + il]) / (ir - il);
        float gy = (f[ju * vx + i] - f[jd * vx + i]) / (ju - jd);
        return Mathf.Abs(f[j * vx + i] - 0.5f) / Mathf.Max(1e-4f, Mathf.Sqrt(gx * gx + gy * gy));
    }

    private static float Fbm(float x, float y)
    {
        float v = 0f, amp = 0.5f, sum = 0f, f = 1f;
        for (int k = 0; k < 3; k++)
        {
            v   += Mathf.PerlinNoise(x * f, y * f) * amp;
            sum += amp;
            amp *= 0.5f;
            f   *= 2.03f;
        }
        return v / sum;
    }

    // 가운데 원 = 시작 바이옴, 바깥은 네 갈래 (경계는 잡음으로 구불구불)
    private void PaintBiomes(int seed, int gridWidth, int gridHeight, WorldTheme start)
    {
        var rng = new System.Random(seed ^ 0xB10E);
        StartBiome = start;
        sectorOffset = (float)(rng.NextDouble() * Mathf.PI * 2f);
        var others = new List<WorldTheme>();
        for (int i = 0; i < WorldThemeConfig.Count; i++)
            if ((WorldTheme)i != start) others.Add((WorldTheme)i);
        for (int i = others.Count - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            (others[i], others[k]) = (others[k], others[i]);
        }
        sectorBiome[0] = start;
        for (int i = 1; i < SectorCount; i++) sectorBiome[i] = others[(i - 1) % others.Count];

        float half = Mathf.Min(gridWidth, gridHeight) * 0.5f;
        float ox = (float)(rng.NextDouble() * 300.0), oy = (float)(rng.NextDouble() * 300.0);
        for (int j = 0; j < VertsY; j++)
        for (int i = 0; i < VertsX; i++)
        {
            float x = MinX + i, y = MinY + j;
            float a = Mathf.Atan2(y, x);
            float ring = half * CenterBiomeFrac * (0.85f + 0.3f * Mathf.PerlinNoise(ox + Mathf.Cos(a) * 1.7f, oy + Mathf.Sin(a) * 1.7f));
            WorldTheme b;
            if (x * x + y * y < ring * ring) b = start;
            else
            {
                float warp = (Mathf.PerlinNoise(ox + x * 0.025f, oy + y * 0.025f) - 0.5f) * 2f * BiomeWobble;
                b = sectorBiome[SectorOf(a + warp)];
            }
            biomeBits[j * VertsX + i] = (byte)b;
        }
    }

    private int SectorOf(float angle)
    {
        float t = Mathf.Repeat(angle - sectorOffset, Mathf.PI * 2f) / (Mathf.PI * 2f / SectorCount);
        return Mathf.Clamp((int)t, 0, SectorCount - 1);
    }

    private float SectorMidAngle(WorldTheme biome)
    {
        for (int s = 0; s < SectorCount; s++)
            if (sectorBiome[s] == biome) return sectorOffset + (s + 0.5f) * Mathf.PI * 2f / SectorCount;
        return sectorOffset;
    }

    // 바이옴 방향 (안내용)
    public Vector2 BiomeDirection(WorldTheme biome)
    {
        float a = SectorMidAngle(biome);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    public WorldTheme BiomeAtVertex(int vx, int vy) => TryIndex(vx, vy, out int idx) ? (WorldTheme)biomeBits[idx] : StartBiome;

    // 칸 네 꼭짓점 중 많은 쪽
    public WorldTheme BiomeAt(Vector2Int cell)
    {
        var a = BiomeAtVertex(cell.x, cell.y);
        var b = BiomeAtVertex(cell.x + 1, cell.y);
        var c = BiomeAtVertex(cell.x, cell.y + 1);
        var d = BiomeAtVertex(cell.x + 1, cell.y + 1);
        if (a == b || a == c || a == d) return a;
        if (b == c || b == d) return b;
        return c;
    }

    // 꼭짓점이 이 바이옴인 모서리 (TL 8 · TR 4 · BR 2 · BL 1)
    public int BiomeConfig(WorldTheme biome, int cx, int cy)
        => (BiomeAtVertex(cx, cy + 1) == biome ? 8 : 0) | (BiomeAtVertex(cx + 1, cy + 1) == biome ? 4 : 0) |
           (BiomeAtVertex(cx + 1, cy) == biome ? 2 : 0) | (BiomeAtVertex(cx, cy) == biome ? 1 : 0);

    // 바이옴 갈래마다 탐사 유적 둘
    private void PlaceLandmarks(int seed, int gridWidth, int gridHeight)
    {
        var rng = new System.Random(seed ^ 0x1A4D);
        float half = Mathf.Min(gridWidth, gridHeight) * 0.5f;
        int id = 0;
        for (int s = 0; s < SectorCount; s++)
        {
            float mid = sectorOffset + (s + 0.5f) * Mathf.PI * 2f / SectorCount;
            for (int k = 0; k < LandmarksPerSector; k++, id++)
            {
                Vector2Int cell = default;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float angle = mid + (k == 0 ? -0.32f : 0.32f) + ((float)rng.NextDouble() - 0.5f) * 0.3f;
                    float t = (float)rng.NextDouble();
                    float dist = half * (k == 0 ? Mathf.Lerp(0.5f, 0.66f, t) : Mathf.Lerp(0.72f, 0.88f, t));
                    cell = new Vector2Int(Mathf.RoundToInt(Mathf.Cos(angle) * dist), Mathf.RoundToInt(Mathf.Sin(angle) * dist));
                    bool clear = true;
                    foreach (var p in soilPatches)
                        if ((p.center - cell).magnitude < p.radius + 6) { clear = false; break; }
                    if (clear) break;
                }
                landmarks.Add(new Landmark(id, cell, BiomeAt(cell)));
                for (int y = cell.y - LandmarkClear; y <= cell.y + LandmarkClear + 1; y++)
                for (int x = cell.x - LandmarkClear; x <= cell.x + LandmarkClear + 1; x++)
                    if (TryIndex(x, y, out int idx)) bits[idx] &= unchecked((byte)~ObstacleMask);
            }
        }
    }

    // 정화 개편 전 배치 (옛 세이브의 밭 자리 유지)
    private void PlaceSoilLegacy(int seed, int gridWidth, int gridHeight)
    {
        var rng = new System.Random(seed ^ 0x50114D);
        int half = Mathf.Min(gridWidth, gridHeight) / 2;
        int radius = Mathf.Clamp(half / 10, SoilRadiusMin, SoilRadiusMax);
        float baseAngle = (float)(rng.NextDouble() * Mathf.PI * 2f);
        float nx = (float)(rng.NextDouble() * 200.0), ny = (float)(rng.NextDouble() * 200.0);
        float step = Mathf.PI * 2f / 3f;
        var kinds = new[] { SoilType.Starlight, SoilType.Spring, SoilType.Ember };
        for (int ring = 0; ring < 2; ring++)
        {
            for (int i = kinds.Length - 1; i > 0; i--)
            {
                int k = rng.Next(i + 1);
                (kinds[i], kinds[k]) = (kinds[k], kinds[i]);
            }
            for (int i = 0; i < kinds.Length; i++)
            {
                float angle = baseAngle + step * i + (ring == 1 ? step * 0.5f : 0f) + ((float)rng.NextDouble() - 0.5f) * SoilAngleJitter;
                float t = (float)rng.NextDouble();
                float dist = half * (ring == 0 ? Mathf.Lerp(0.38f, 0.52f, t) : Mathf.Lerp(0.68f, 0.85f, t));
                var patch = new SoilPatch(kinds[i], new Vector2Int(Mathf.RoundToInt(Mathf.Cos(angle) * dist), Mathf.RoundToInt(Mathf.Sin(angle) * dist)), radius);
                soilPatches.Add(patch);
                PaintSoil(patch, nx, ny);
            }
        }
    }

    // 특산 토양은 제 바이옴 갈래 안쪽 · 바깥쪽에 하나씩
    private void PlaceSoil(int seed, int gridWidth, int gridHeight)
    {
        var rng = new System.Random(seed ^ 0x50114D);
        int half = Mathf.Min(gridWidth, gridHeight) / 2;
        int radius = Mathf.Clamp(half / 10, SoilRadiusMin, SoilRadiusMax);
        float nx = (float)(rng.NextDouble() * 200.0), ny = (float)(rng.NextDouble() * 200.0);
        var kinds = new[] { SoilType.Starlight, SoilType.Spring, SoilType.Ember };

        for (int ring = 0; ring < 2; ring++)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                float side = ring == 0 ? -0.22f : 0.22f;
                float angle = SectorMidAngle(WorldThemeConfig.HomeOf(kinds[i])) + side + ((float)rng.NextDouble() - 0.5f) * SoilAngleJitter * 0.4f;
                float t = (float)rng.NextDouble();
                float dist = half * (ring == 0 ? Mathf.Lerp(0.38f, 0.52f, t) : Mathf.Lerp(0.68f, 0.85f, t));
                var patch = new SoilPatch(kinds[i], new Vector2Int(Mathf.RoundToInt(Mathf.Cos(angle) * dist), Mathf.RoundToInt(Mathf.Sin(angle) * dist)), radius);
                soilPatches.Add(patch);
                PaintSoil(patch, nx, ny);
            }
        }
    }

    // 토양 자리는 협곡 · 대지를 걷어냄
    private void PaintSoil(SoilPatch p, float nx, float ny)
    {
        int clear = p.radius + p.radius / 4 + 3;
        for (int y = p.center.y - clear; y <= p.center.y + clear; y++)
        for (int x = p.center.x - clear; x <= p.center.x + clear; x++)
        {
            if (!TryIndex(x, y, out int idx)) continue;
            float d = Vector2.Distance(new Vector2(x, y), p.center);
            if (d > clear) continue;
            bits[idx] &= unchecked((byte)~ObstacleMask);
            float wobble = 0.82f + 0.36f * Mathf.PerlinNoise(x * SoilWobbleScale + nx, y * SoilWobbleScale + ny);
            if (d < p.radius * wobble && soilBits[idx] == 0) soilBits[idx] = (byte)p.type;
        }
    }

    public SoilType SoilAtVertex(int vx, int vy) => TryIndex(vx, vy, out int idx) ? (SoilType)soilBits[idx] : SoilType.None;

    // 꼭짓점 둘 이상 덮이면 그 토양
    public SoilType SoilAtCell(Vector2Int cell)
    {
        int c = SoilConfig(cell.x, cell.y, out var soil);
        int n = (c & 1) + ((c >> 1) & 1) + ((c >> 2) & 1) + ((c >> 3) & 1);
        return n >= 2 ? soil : SoilType.None;
    }

    public int SoilConfig(int cx, int cy, out SoilType soil)
    {
        soil = SoilType.None;
        int cfg = 0;
        for (int k = 0; k < 4; k++)
        {
            var s = SoilAtVertex(cx + (k == 1 || k == 2 ? 1 : 0), cy + (k < 2 ? 1 : 0));
            if (s == SoilType.None) continue;
            soil = s;
            cfg |= 8 >> k;
        }
        return cfg;
    }

    private void RemoveSpecks()
    {
        var clear = new List<int>();
        for (int j = 1; j < VertsY - 1; j++)
        for (int i = 1; i < VertsX - 1; i++)
        {
            int idx = j * VertsX + i;
            byte ob = (byte)(bits[idx] & ObstacleMask);
            if (ob == 0) continue;
            bool alone = true;
            for (int dj = -1; dj <= 1 && alone; dj++)
            for (int di = -1; di <= 1 && alone; di++)
                if ((di != 0 || dj != 0) && (bits[(j + dj) * VertsX + i + di] & ob) != 0) alone = false;
            if (alone) clear.Add(idx);
        }
        foreach (int idx in clear) bits[idx] &= unchecked((byte)~ObstacleMask);
    }

    private bool TryIndex(int vx, int vy, out int idx)
    {
        int i = vx - MinX, j = vy - MinY;
        idx = j * VertsX + i;
        return i >= 0 && j >= 0 && i < VertsX && j < VertsY;
    }

    public bool Has(Layer layer, int vx, int vy) => TryIndex(vx, vy, out int idx) && (bits[idx] & Bit(layer)) != 0;

    public bool IsObstacleVertex(int vx, int vy) => TryIndex(vx, vy, out int idx) && (bits[idx] & ObstacleMask) != 0;

    public bool IsCellBlocked(Vector2Int cell)
        => IsObstacleVertex(cell.x, cell.y) || IsObstacleVertex(cell.x + 1, cell.y) ||
           IsObstacleVertex(cell.x, cell.y + 1) || IsObstacleVertex(cell.x + 1, cell.y + 1);

    public int Config(Layer layer, int cx, int cy)
        => (Has(layer, cx, cy + 1) ? 8 : 0) | (Has(layer, cx + 1, cy + 1) ? 4 : 0) |
           (Has(layer, cx + 1, cy) ? 2 : 0) | (Has(layer, cx, cy) ? 1 : 0);

    private void CarveCell(int cx, int cy, List<Vector2Int> changed)
    {
        bool any = false;
        for (int dy = 0; dy <= 1; dy++)
        for (int dx = 0; dx <= 1; dx++)
        {
            if (!TryIndex(cx + dx, cy + dy, out int idx) || (bits[idx] & ObstacleMask) == 0) continue;
            bits[idx] &= unchecked((byte)~ObstacleMask);
            carved.Add(idx);
            any = true;
        }
        if (!any || changed == null) return;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            changed.Add(new Vector2Int(cx + dx, cy + dy));
    }

    public void CarveAroundObjects()
    {
        var gm = GridManager.Instance;
        if (gm == null) return;
        var changed = new List<Vector2Int>();

        foreach (var tile in Object.FindObjectsByType<TileItem>(FindObjectsSortMode.None))
        {
            if (tile == null || tile.IsGhost || tile.OccupiedCells == null) continue;
            for (int y = tile.FootprintMin.y - 1; y <= tile.FootprintMax.y + 1; y++)
            for (int x = tile.FootprintMin.x - 1; x <= tile.FootprintMax.x + 1; x++)
                CarveCell(x, y, changed);
        }
        foreach (var block in ResourceBlock.Active)
        {
            if (block == null) continue;
            Rect r = block.SolidRect;
            CarveWorldRect(gm, new Rect(r.xMin - gm.CellSize, r.yMin - gm.CellSize, r.width + gm.CellSize * 2, r.height + gm.CellSize * 2), changed);
        }
        var player = PlayerTileController.Instance;
        if (player != null)
        {
            Vector2 p = player.transform.position;
            CarveWorldRect(gm, new Rect(p.x - gm.CellSize * 1.5f, p.y - gm.CellSize * 1.5f, gm.CellSize * 3f, gm.CellSize * 3f), changed);
        }

        EnsureConnected(changed);
        if (changed.Count > 0) OnCellsChanged?.Invoke(changed);
    }

    // 데모: 사각 구역의 협곡 · 대지를 걷어냄
    public void ClearRect(Vector2Int min, Vector2Int max)
    {
        var changed = new List<Vector2Int>();
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
            CarveCell(x, y, changed);
        if (changed.Count > 0) OnCellsChanged?.Invoke(changed);
    }

    public List<int> CarvedVertices() => new(carved);

    public void ApplyCarved(List<int> vertices)
    {
        if (vertices == null) return;
        var changed = new List<Vector2Int>();
        foreach (int idx in vertices)
        {
            if (idx < 0 || idx >= bits.Length || (bits[idx] & ObstacleMask) == 0) continue;
            bits[idx] &= unchecked((byte)~ObstacleMask);
            carved.Add(idx);
            int vx = MinX + idx % VertsX, vy = MinY + idx / VertsX;
            for (int dy = -2; dy <= 1; dy++)
            for (int dx = -2; dx <= 1; dx++)
                changed.Add(new Vector2Int(vx + dx, vy + dy));
        }
        if (changed.Count > 0) OnCellsChanged?.Invoke(changed);
    }

    private void CarveWorldRect(GridManager gm, Rect r, List<Vector2Int> changed)
    {
        Vector2Int a = gm.WorldToGrid(new Vector3(r.xMin, r.yMin)), b = gm.WorldToGrid(new Vector3(r.xMax, r.yMax));
        for (int y = a.y; y <= b.y; y++)
        for (int x = a.x; x <= b.x; x++)
            CarveCell(x, y, changed);
    }

    private void EnsureConnected(List<Vector2Int> changed)
    {
        int w = worldMaxX - worldMinX + 1, h = worldMaxY - worldMinY + 1, n = w * h;
        var blocked = new bool[n];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            blocked[y * w + x] = IsCellBlocked(new Vector2Int(worldMinX + x, worldMinY + y));

        var comp = new int[n];
        for (int k = 0; k < n; k++) comp[k] = -1;
        var queue = new Queue<int>();
        int comps = 0;
        var compStart = new List<int>();
        for (int s = 0; s < n; s++)
        {
            if (blocked[s] || comp[s] >= 0) continue;
            comp[s] = comps; queue.Enqueue(s); compStart.Add(s);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue(), cx = c % w, cy = c / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (blocked[ni] || comp[ni] >= 0) continue;
                    comp[ni] = comps; queue.Enqueue(ni);
                }
            }
            comps++;
        }
        if (comps <= 1) return;

        int center = (0 - worldMinY) * w + (0 - worldMinX);
        int main = comp[center] >= 0 ? comp[center] : 0;

        var dist = new int[n];
        var parent = new int[n];
        for (int k = 0; k < n; k++) { dist[k] = int.MaxValue; parent[k] = -1; }
        var deque = new LinkedList<int>();
        for (int k = 0; k < n; k++)
            if (comp[k] == main) { dist[k] = 0; deque.AddLast(k); }
        while (deque.Count > 0)
        {
            int c = deque.First.Value; deque.RemoveFirst();
            int cx = c % w, cy = c / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int ni = ny * w + nx, cost = blocked[ni] ? 1 : 0;
                if (dist[c] + cost >= dist[ni]) continue;
                dist[ni] = dist[c] + cost; parent[ni] = c;
                if (cost == 0) deque.AddFirst(ni); else deque.AddLast(ni);
            }
        }

        var best = new int[comps];
        for (int k = 0; k < comps; k++) best[k] = -1;
        for (int k = 0; k < n; k++)
        {
            int cid = comp[k];
            if (cid < 0 || cid == main) continue;
            if (best[cid] < 0 || dist[k] < dist[best[cid]]) best[cid] = k;
        }
        for (int cid = 0; cid < comps; cid++)
        {
            for (int c = best[cid]; c >= 0 && c < n && comp[c] != main; c = parent[c])
            {
                if (!blocked[c]) continue;
                CarveCell(worldMinX + c % w, worldMinY + c / w, changed);
                blocked[c] = false;
            }
        }
    }
}
