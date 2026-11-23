using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldBackground : MonoBehaviour
{
    private const int    GroundOrder        = -10;
    // 바이옴 경계 -9
    private const int    BiomeEdgeOrder     = -9;
    // 얼룩 -7 길 -6 협곡 -5 대지 -4
    private const int    LayerOrderBase     = -7;
    private const int    DecorOrder         = -3;
    // 대지와 안 겹침
    private const int    SoilOrder          = -4;
    private const int    FadeOrder          = -2;
    private const int    FadeCells          = 14;
    private const int    MarginCells        = FadeCells + 2;
    private const int    FadePxPerCell      = 4;
    // 맵 밖: 검정 대신 어스름 보라 (바닥이 탁해지지 않게)
    private static readonly Color32 EdgeColor = new(104, 94, 118, 255);
    private const float  DetailScale        = 0.11f;
    private const float  DetailCut          = 0.62f;
    private const double DetailChance       = 0.7;
    private const double DecorChance        = 0.05;
    private const double OutsideDecorChance = 0.02;

    private static readonly string[] LayerPrefix = { "patch", "path", "chasm", "mesa" };

    public static WorldBackground Current => Services.Get<WorldBackground>();

    // 바이옴 하나의 타일 한 벌
    private class BiomeSet
    {
        public List<TileBase> bases, details, decors;
        public readonly List<TileBase>[,] layers = new List<TileBase>[TerrainMap.LayerCount, 16];
        public readonly List<TileBase>[,] soils  = new List<TileBase>[4, 16];
        public readonly TileBase[] edges = new TileBase[16];
    }

    private TerrainMap terrain;
    private Tilemap    decorMap;
    private readonly Tilemap[] layerMaps = new Tilemap[TerrainMap.LayerCount];
    private readonly BiomeSet[] sets = new BiomeSet[WorldThemeConfig.Count];

    public static WorldBackground Create(GridManager gm, WorldTheme theme, int seed)
    {
        if (Current != null) Destroy(Current.gameObject);

        var sprites = new Sprite[WorldThemeConfig.Count][];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = Resources.LoadAll<Sprite>(WorldThemeConfig.TerrainFolder((WorldTheme)i));
        if (sprites[(int)theme] == null || sprites[(int)theme].Length == 0)
        {
            Debug.LogWarning($"[WorldBackground] 바닥 그림이 없습니다: Resources/{WorldThemeConfig.TerrainFolder(theme)}");
            return null;
        }

        var bg = new GameObject($"WorldBackground ({WorldThemeConfig.DisplayName(theme)})").AddComponent<WorldBackground>();
        bg.Build(gm, sprites, theme, seed);
        Services.Replace(bg);
        return bg;
    }

    private Texture2D fadeTexture;

    private void OnDestroy()
    {
        Services.Unregister(this);
        if (terrain != null)
        {
            terrain.OnCellsChanged -= RefreshCells;
            TerrainMap.Release(terrain);
        }
        if (fadeTexture != null) Destroy(fadeTexture);
        if (coverSprite != null) Destroy(coverSprite);
    }

    private BiomeSet Set(WorldTheme b) => sets[(int)b] ?? sets[(int)terrain.StartBiome];

    private void Build(GridManager gm, Sprite[][] sprites, WorldTheme start, int seed)
    {
        for (int i = 0; i < sets.Length; i++)
            if (sprites[i] != null && sprites[i].Length > 0) sets[i] = LoadSet(sprites[i]);
        var home = sets[(int)start];
        if (home.bases.Count == 0) return;

        bool obstacleArt = HasFullSet(home, TerrainMap.Layer.Chasm) && HasFullSet(home, TerrainMap.Layer.Mesa);
        terrain = TerrainMap.Generate(gm.GridWidth, gm.GridHeight, MarginCells, seed, obstacleArt, start);
        terrain.OnCellsChanged += RefreshCells;

        float cs = gm.CellSize;
        gameObject.AddComponent<Grid>().cellSize = new Vector3(cs, cs, 0f);
        transform.position = new Vector3(gm.GridOrigin.x, gm.GridOrigin.y, 0f);
        var groundMap = NewTilemap("Ground", GroundOrder);
        var edgeMaps  = new Tilemap[WorldThemeConfig.Count];
        for (int b = 0; b < edgeMaps.Length; b++) edgeMaps[b] = NewTilemap($"BiomeEdge_{(WorldTheme)b}", BiomeEdgeOrder);
        for (int l = 0; l < TerrainMap.LayerCount; l++)
            layerMaps[l] = NewTilemap(LayerPrefix[l], LayerOrderBase + l);
        var soilMap = NewTilemap("Soil", SoilOrder);
        decorMap = NewTilemap("Decor", DecorOrder);

        int halfW = gm.GridWidth / 2, halfH = gm.GridHeight / 2;
        int minX = -halfW - MarginCells, minY = -halfH - MarginCells;
        int w = gm.GridWidth + MarginCells * 2, h = gm.GridHeight + MarginCells * 2;

        var ground = new TileBase[w * h];
        var decor  = new TileBase[w * h];
        var soil   = new TileBase[w * h];
        var edges  = new TileBase[WorldThemeConfig.Count][];
        for (int b = 0; b < edges.Length; b++) edges[b] = new TileBase[w * h];
        var layers = new TileBase[TerrainMap.LayerCount][];
        for (int l = 0; l < layers.Length; l++) layers[l] = new TileBase[w * h];
        var rng = new System.Random(seed);
        float ox = (float)(rng.NextDouble() * 1000.0), oy = (float)(rng.NextDouble() * 1000.0);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            var cell = new Vector2Int(minX + x, minY + y);
            var set = Set(terrain.BiomeAt(cell));
            for (int l = 0; l < layers.Length; l++) layers[l][i] = LayerTile(l, cell.x, cell.y);
            soil[i] = SoilTile(cell.x, cell.y);

            // 바닥은 네 꼭짓점 중 가장 앞 번호 바이옴, 나머지는 경계 타일로 덮음
            var low = LowestBiome(cell);
            var groundSet = Set(low);
            float n = Mathf.PerlinNoise(ox + cell.x * DetailScale, oy + cell.y * DetailScale);
            bool detail = groundSet.details.Count > 0 && n > DetailCut && rng.NextDouble() < DetailChance;
            ground[i] = detail ? Pick(groundSet.details, rng) : Pick(groundSet.bases, rng);
            for (int b = 0; b < WorldThemeConfig.Count; b++)
            {
                if ((WorldTheme)b == low || sets[b] == null) continue;
                int c = terrain.BiomeConfig((WorldTheme)b, cell.x, cell.y);
                if (c != 0) edges[b][i] = c == 15 ? Pick(sets[b].bases, rng) : sets[b].edges[c];
            }

            double decorChance = gm.IsInBounds(cell) ? DecorChance : OutsideDecorChance;
            if (set.decors.Count > 0 && rng.NextDouble() < decorChance && CanHaveDecor(cell)) decor[i] = Pick(set.decors, rng);
        }

        var block = new BoundsInt(minX, minY, 0, w, h, 1);
        groundMap.SetTilesBlock(block, ground);
        for (int b = 0; b < edgeMaps.Length; b++) edgeMaps[b].SetTilesBlock(block, edges[b]);
        for (int l = 0; l < layers.Length; l++) layerMaps[l].SetTilesBlock(block, layers[l]);
        soilMap.SetTilesBlock(block, soil);
        decorMap.SetTilesBlock(block, decor);

        BuildEdgeFade(gm, minX, minY, w, h);
        BiomeLandmark.SpawnAll(terrain);
    }

    private WorldTheme LowestBiome(Vector2Int cell)
    {
        var a = terrain.BiomeAtVertex(cell.x, cell.y);
        var b = terrain.BiomeAtVertex(cell.x + 1, cell.y);
        var c = terrain.BiomeAtVertex(cell.x, cell.y + 1);
        var d = terrain.BiomeAtVertex(cell.x + 1, cell.y + 1);
        var m = a;
        if (b < m) m = b;
        if (c < m) m = c;
        if (d < m) m = d;
        return sets[(int)m] != null ? m : terrain.StartBiome;
    }

    private static BiomeSet LoadSet(Sprite[] sprites)
    {
        var s = new BiomeSet
        {
            bases   = TilesNamed(sprites, "base_"),
            details = TilesNamed(sprites, "detail_"),
            decors  = TilesNamed(sprites, "decor_"),
        };
        for (int l = 0; l < TerrainMap.LayerCount; l++)
        for (int c = 1; c < 16; c++)
            s.layers[l, c] = TilesNamed(sprites, $"{LayerPrefix[l]}_{c:00}");
        for (int t = 1; t < 4; t++)
        for (int c = 1; c < 16; c++)
            s.soils[t, c] = TilesNamed(sprites, $"{Soil.TilePrefix((SoilType)t)}_{c:00}");
        for (int c = 1; c < 15; c++)
        {
            var e = TilesNamed(sprites, $"edge_{c:00}");
            s.edges[c] = e.Count > 0 ? e[0] : null;
        }
        return s;
    }

    private static bool HasFullSet(BiomeSet s, TerrainMap.Layer layer)
    {
        for (int c = 1; c < 16; c++)
            if (s.layers[(int)layer, c] == null || s.layers[(int)layer, c].Count == 0) return false;
        return true;
    }

    private TileBase LayerTile(int layer, int cx, int cy)
    {
        int c = terrain.Config((TerrainMap.Layer)layer, cx, cy);
        if (c == 0) return null;
        var list = Set(terrain.BiomeAt(new Vector2Int(cx, cy))).layers[layer, c];
        if (list == null || list.Count == 0) return null;
        if (list.Count == 1) return list[0];
        uint hash = unchecked((uint)(cx * 73856093) ^ (uint)(cy * 19349663) ^ (uint)(layer * 83492791));
        return list[(int)(hash % (uint)list.Count)];
    }

    private TileBase SoilTile(int cx, int cy)
    {
        int c = terrain.SoilConfig(cx, cy, out var type);
        if (c == 0) return null;
        var list = Set(terrain.BiomeAt(new Vector2Int(cx, cy))).soils[(int)type, c];
        if (list == null || list.Count == 0) return null;
        uint hash = unchecked((uint)(cx * 73856093) ^ (uint)(cy * 19349663));
        return list[(int)(hash % (uint)list.Count)];
    }

    private bool CanHaveDecor(Vector2Int cell)
        => !terrain.IsCellBlocked(cell) && terrain.Config(TerrainMap.Layer.Path, cell.x, cell.y) == 0
           && terrain.SoilConfig(cell.x, cell.y, out _) == 0;

    private void RefreshCells(List<Vector2Int> cells)
    {
        var seen = new HashSet<Vector2Int>();
        foreach (var cell in cells)
        {
            if (!seen.Add(cell)) continue;
            var pos = new Vector3Int(cell.x, cell.y, 0);
            for (int l = 0; l < TerrainMap.LayerCount; l++) layerMaps[l].SetTile(pos, LayerTile(l, cell.x, cell.y));
            if (!CanHaveDecor(cell)) decorMap.SetTile(pos, null);
        }
    }

    private void BuildEdgeFade(GridManager gm, int minX, int minY, int w, int h)
    {
        int texW = w * FadePxPerCell, texH = h * FadePxPerCell;
        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "WorldEdgeFade",
        };

        float wx0 = -gm.GridWidth / 2 - minX, wy0 = -gm.GridHeight / 2 - minY;
        float wx1 = wx0 + gm.GridWidth, wy1 = wy0 + gm.GridHeight;
        var px = new Color32[texW * texH];
        for (int y = 0; y < texH; y++)
        for (int x = 0; x < texW; x++)
        {
            float cx = (x + 0.5f) / FadePxPerCell, cy = (y + 0.5f) / FadePxPerCell;
            float dx = Mathf.Max(0f, Mathf.Max(wx0 - cx, cx - wx1));
            float dy = Mathf.Max(0f, Mathf.Max(wy0 - cy, cy - wy1));
            float t  = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / FadeCells);
            px[y * texW + x] = new Color32(EdgeColor.r, EdgeColor.g, EdgeColor.b, (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, t) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        fadeTexture = tex;

        float cs = gm.CellSize;
        var go = new GameObject("EdgeFade");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(minX * cs, minY * cs, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite         = Sprite.Create(tex, new Rect(0, 0, texW, texH), Vector2.zero, FadePxPerCell / cs);
        sr.sortingOrder   = FadeOrder;
        sr.sharedMaterial = UnlitMaterial();

        const float Far = 500f;
        float x0 = minX * cs, y0 = minY * cs, x1 = x0 + w * cs, y1 = y0 + h * cs;
        Cover(x0 - Far, y0 - Far, x0, y1 + Far);
        Cover(x1, y0 - Far, x1 + Far, y1 + Far);
        Cover(x0, y1, x1, y1 + Far);
        Cover(x0, y0 - Far, x1, y0);
    }

    private Sprite coverSprite;

    private void Cover(float xMin, float yMin, float xMax, float yMax)
    {
        if (coverSprite == null)
            coverSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        var go = new GameObject("EdgeCover");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);
        go.transform.localScale    = new Vector3(xMax - xMin, yMax - yMin, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite         = coverSprite;
        sr.color          = EdgeColor;
        sr.sortingOrder   = FadeOrder;
        sr.sharedMaterial = UnlitMaterial();
    }

    private Tilemap NewTilemap(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var map = go.AddComponent<Tilemap>();
        var r   = go.AddComponent<TilemapRenderer>();
        r.sortingOrder   = sortingOrder;
        r.mode           = TilemapRenderer.Mode.Chunk;
        r.sharedMaterial = UnlitMaterial();
        return map;
    }

    private static List<TileBase> TilesNamed(Sprite[] sprites, string prefix)
    {
        var picked = new List<Sprite>();
        foreach (var s in sprites)
            if (s != null && s.name.StartsWith(prefix)) picked.Add(s);
        picked.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        var tiles = new List<TileBase>(picked.Count);
        foreach (var s in picked)
        {
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite       = s;
            t.colliderType = Tile.ColliderType.None;
            tiles.Add(t);
        }
        return tiles;
    }

    private static TileBase Pick(List<TileBase> list, System.Random rng) => list[rng.Next(list.Count)];

    private static Material unlitMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => unlitMaterial = null;

    public static Material UnlitMaterial()
    {
        if (unlitMaterial != null) return unlitMaterial;
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        return unlitMaterial = new Material(shader);
    }
}
