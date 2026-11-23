using System.Collections.Generic;
using UnityEngine;

// 정화된 땅 = 센터코어 + 정화탑 사각형 합집합 (설치물에서 계산, 세이브 안 함)
public class Purify : MonoBehaviour
{
    // 문명 Lv.1~7 센터코어 정화 반경 (코어 둘레 칸)
    private static readonly int[] CoreRadiusByLevel = { 16, 20, 24, 28, 32, 36, 42 };
    private const int   PxPerCell     = 4;
    private const float RebuildEvery  = 0.08f;
    private const float CoreGrowSpeed = 5f;
    private const int   SortingOrder  = -2;

    private static readonly Color32 Haze    = new(70, 48, 100, 150);
    private static readonly Color32 Barrier = new(150, 236, 255, 190);

    private static Purify instance;
    private static readonly List<Purifier> purifiers = new();
    private static readonly Dictionary<GameObject, bool> needsCache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        purifiers.Clear();
        needsCache.Clear();
        OnChanged = null;
    }

    public static event System.Action OnChanged;

    public static bool Active => instance != null;
    public static int  Version => instance != null ? instance.version : 0;
    public static float CoreRadiusShown => instance != null ? instance.coreShown : 0f;

    public static int CoreRadius(int level) => CoreRadiusByLevel[Mathf.Clamp(level - 1, 0, CoreRadiusByLevel.Length - 1)];

    private int minX, minY, w, h;
    private bool[] pure;
    private float coreShown = -1f;
    private int   coreDrawn = -1;
    private bool  dirty = true;
    private float nextRebuild;
    private int   version;
    private Texture2D tex;
    private Color32[] px;
    private SpriteRenderer sr;

    public static void Ensure(GridManager gm)
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = new GameObject("PurifyField").AddComponent<Purify>();
        instance.Init(gm);
    }

    private void Init(GridManager gm)
    {
        w = gm.GridWidth; h = gm.GridHeight;
        minX = -w / 2; minY = -h / 2;
        pure = new bool[w * h];
        tex = new Texture2D(w * PxPerCell, h * PxPerCell, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "PurifyHaze",
        };
        px = new Color32[tex.width * tex.height];
        float cs = gm.CellSize;
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, PxPerCell / cs);
        sr.sortingOrder = SortingOrder;
        sr.sharedMaterial = WorldBackground.UnlitMaterial();
        transform.position = new Vector3(gm.GridOrigin.x + minX * cs, gm.GridOrigin.y + minY * cs, 0f);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (tex != null) Destroy(tex);
        if (sr != null && sr.sprite != null) Destroy(sr.sprite);
    }

    public static void Register(Purifier p)
    {
        if (!purifiers.Contains(p)) purifiers.Add(p);
        MarkDirty();
    }

    public static void Unregister(Purifier p)
    {
        if (purifiers.Remove(p)) MarkDirty();
    }

    public static void MarkDirty()
    {
        if (instance != null) instance.dirty = true;
    }

    public static IReadOnlyList<Purifier> Purifiers => purifiers;

    // 정화 반경을 바로 끝까지 (로드 · 데모)
    public static void SnapAll()
    {
        if (instance == null) return;
        var core = CenterCore.Current;
        if (core != null) instance.coreShown = CoreRadius(core.Level);
        foreach (var p in purifiers) if (p != null) p.Snap();
        instance.dirty = true;
        instance.Rebuild();
    }

    public static bool IsPure(Vector2Int cell)
    {
        var f = instance;
        if (f == null) return true;
        int x = cell.x - f.minX, y = cell.y - f.minY;
        if (x < 0 || y < 0 || x >= f.w || y >= f.h) return false;
        return f.pure[y * f.w + x];
    }

    // 정화 안 해도 되는 것: 개척 설비 (정화탑 · 중계탑 · 레일 · 채굴기 · 창고)
    public static bool Needs(GameObject prefab)
    {
        if (prefab == null) return false;
        if (needsCache.TryGetValue(prefab, out bool yes)) return yes;
        yes = prefab.GetComponent<Purifier>() == null && prefab.GetComponent<PowerRelay>() == null &&
              prefab.GetComponent<CargoRail>() == null && prefab.GetComponent<ResourceExtractor>() == null &&
              prefab.GetComponent<ItemStorage>() == null;
        needsCache[prefab] = yes;
        return yes;
    }

    public static bool Allows(GameObject prefab, List<Vector2Int> cells)
    {
        if (instance == null || !Needs(prefab)) return true;
        foreach (var c in cells) if (!IsPure(c)) return false;
        return true;
    }

    public static int PureCellCount
    {
        get
        {
            if (instance == null) return 0;
            int n = 0;
            foreach (bool b in instance.pure) if (b) n++;
            return n;
        }
    }

    private void Update()
    {
        var core = CenterCore.Current;
        if (core != null)
        {
            float target = CoreRadius(core.Level);
            if (coreShown < 0f || SaveManager.IsLoading) coreShown = target;
            else if (!Mathf.Approximately(coreShown, target))
            {
                coreShown = Mathf.MoveTowards(coreShown, target, CoreGrowSpeed * Time.deltaTime);
                if (Mathf.FloorToInt(coreShown) != coreDrawn) dirty = true;
            }
        }
        if (!dirty || Time.unscaledTime < nextRebuild) return;
        nextRebuild = Time.unscaledTime + RebuildEvery;
        Rebuild();
    }

    private void Rebuild()
    {
        dirty = false;
        System.Array.Clear(pure, 0, pure.Length);
        var core = CenterCore.Current;
        if (core != null)
        {
            coreDrawn = Mathf.FloorToInt(Mathf.Max(0f, coreShown));
            Mark(core.FootprintMin, core.FootprintMax, coreDrawn);
        }
        foreach (var p in purifiers)
            if (p != null && !p.IsGhost) Mark(p.FootprintMin, p.FootprintMax, p.RadiusShown);
        Paint();
        version++;
        OnChanged?.Invoke();
    }

    private void Mark(Vector2Int min, Vector2Int max, int r)
    {
        int x0 = Mathf.Max(0, min.x - r - minX), x1 = Mathf.Min(w - 1, max.x + r - minX);
        int y0 = Mathf.Max(0, min.y - r - minY), y1 = Mathf.Min(h - 1, max.y + r - minY);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            pure[y * w + x] = true;
    }

    private bool PureAt(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || pure[y * w + x];

    // 오염된 칸 = 보랏빛 안개, 정화 경계 = 하늘색 막 1px
    private void Paint()
    {
        int tw = w * PxPerCell;
        var clear = new Color32(0, 0, 0, 0);
        for (int cy = 0; cy < h; cy++)
        for (int cx = 0; cx < w; cx++)
        {
            bool p = pure[cy * w + cx];
            uint hash = unchecked((uint)(cx * 73856093) ^ (uint)(cy * 19349663));
            var haze = Haze;
            haze.a = (byte)(Haze.a - 10 + (int)(hash % 21));
            bool l = !p && PureAt(cx - 1, cy), r = !p && PureAt(cx + 1, cy);
            bool d = !p && PureAt(cx, cy - 1), u = !p && PureAt(cx, cy + 1);
            for (int sy = 0; sy < PxPerCell; sy++)
            for (int sx = 0; sx < PxPerCell; sx++)
            {
                Color32 c;
                if (p) c = clear;
                else if ((l && sx == 0) || (r && sx == PxPerCell - 1) || (d && sy == 0) || (u && sy == PxPerCell - 1)) c = Barrier;
                else c = haze;
                px[(cy * PxPerCell + sy) * tw + cx * PxPerCell + sx] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
    }

    // 정화가 퍼지는 사각 물결
    public static void Wave(Vector3 center, float fromHalf, float toHalf, float seconds, Color color)
        => LabFx.Run(WaveRoutine(center, fromHalf, toHalf, seconds, color));

    private static System.Collections.IEnumerator WaveRoutine(Vector3 center, float fromHalf, float toHalf, float seconds, Color color)
    {
        var go = new GameObject("PurifyWave");
        var line = WorldCircleOutline.CreateRect(go.transform, fromHalf, fromHalf, color, 0.09f, 46);
        go.transform.position = center;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            float half = Mathf.Lerp(fromHalf, toHalf, 1f - (1f - k) * (1f - k));
            WorldCircleOutline.SetRect(line, Vector3.zero, half, half);
            var c = color;
            c.a = color.a * (1f - k * k);
            line.startColor = line.endColor = c;
            yield return null;
        }
        Destroy(go);
    }
}
