using System.Collections.Generic;
using UnityEngine;

// 전초기지 다음 할 일: 전기 → 창고 → 생산 설비 → 레일 (길잡이 · 봇 공용)
public static class OutpostPlan
{
    public enum Step { HomeStorage, Power, Storage, Producer, Rail, Purify }

    public struct Result
    {
        public Step        step;
        public Vector2Int  cell;
        public Vector2Int  site;
        public ItemStorage storage;
        public SoilType    soil;
        public ResourceBlock ore;
    }

    private const float NewOreCells  = 30f;
    private const int   OpenCellSearch = 10;
    private const int   RelayMargin  = 2;
    private const int   SoilNearCells = 5;

    private static readonly List<TileItem> storages = new();
    private static readonly List<TileItem> farStorages = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        storages.Clear();
        farStorages.Clear();
    }

    public static Result Next(SoilType soil, bool wantOre)
    {
        var gm = GridManager.Instance;
        var r = new Result();
        if (gm == null || CenterCore.Current == null) return r;

        var home = HomeStorage();
        if (home == null)
        {
            r.step = Step.HomeStorage;
            r.cell = OpenCell(CenterCore.Current.FootprintMax + new Vector2Int(3, 3), 2, 2);
            return r;
        }

        CollectFar();
        r.storage = UnfinishedStorage();
        if (r.storage != null)
        {
            r.site = r.storage.GridPosition;
            // 광맥 기지는 곁에 토양이 있어도 정화 · 전기 없이 채굴기만
            r.soil = wantOre ? SoilType.None : SoilNear(r.site);
        }
        else
        {
            r.ore = wantOre ? NewOre() : null;
            if (r.ore != null) r.site = gm.WorldToGrid(r.ore.transform.position);
            else if (TryNewSoil(soil, out var patch) || TryNewSoil(SoilType.None, out patch)) { r.site = patch.center; r.soil = patch.type; }
            else r.site = CenterCore.Current.GridPosition;
        }

        // 농사 기지는 정화부터
        if (r.soil != SoilType.None && !Purify.IsPure(r.site))
        {
            r.step = Step.Purify;
            r.cell = OpenCell(r.site, 2, 2);
            return r;
        }
        // 채굴만 하면 전기 없어도 됨
        if (r.soil != SoilType.None && !Powered(r.site, r.soil))
        {
            r.step = Step.Power;
            r.cell = NextRelayCell(r.site);
            return r;
        }
        if (r.storage == null)
        {
            r.step = Step.Storage;
            r.cell = OpenCell(r.site, 2, 2);
            return r;
        }
        var net = NetOf(r.storage);
        if (!HasProducer(r.storage))
        {
            r.step = Step.Producer;
            r.cell = OpenCell(r.site + new Vector2Int(3, 0), 1, 1);
            return r;
        }
        r.step = Step.Rail;
        r.cell = NextRailCell(r.storage, net, home);
        return r;
    }

    public static ItemStorage HomeStorage()
    {
        var gm = GridManager.Instance;
        if (gm == null) return null;
        gm.CollectTiles(TileType.Storage, storages);
        ItemStorage best = null;
        float bestDist = float.MaxValue;
        foreach (var t in storages)
        {
            if (t is not ItemStorage s || s.IsGhost) continue;
            float d = CargoRails.CellsFromCore(s);
            if (d >= CargoRails.OutpostCells) continue;
            if (HasLink<ShuttlePad>(s)) d -= CargoRails.OutpostCells;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    private static void CollectFar()
    {
        GridManager.Instance.CollectTiles(TileType.Storage, storages);
        farStorages.Clear();
        foreach (var t in storages)
            if (!t.IsGhost && CargoRails.CellsFromCore(t) >= CargoRails.OutpostCells) farStorages.Add(t);
    }

    // 본진에서 멀지만 아직 전초기지가 아닌 창고
    private static ItemStorage UnfinishedStorage()
    {
        ItemStorage best = null;
        int bestLinks = -1;
        foreach (var t in farStorages)
        {
            if (t is not ItemStorage s) continue;
            var net = NetOf(s);
            if (net != null && CargoRails.IsOutpost(net, s)) continue;
            if (s.Links.Count > bestLinks) { bestLinks = s.Links.Count; best = s; }
        }
        return best;
    }

    private static bool HasProducer(ItemStorage s)
    {
        foreach (var link in s.Links)
            if (CargoRails.IsProducer(link)) return true;
        return false;
    }

    private static bool HasLink<T>(ItemStorage s) where T : TileItem
    {
        foreach (var link in s.Links)
            if (link is T) return true;
        return false;
    }

    public static RailNetwork NetOf(ItemStorage s)
    {
        foreach (var net in CargoRails.Networks)
            if (net.Storages.Contains(s)) return net;
        return null;
    }

    private static bool NearFarStorage(Vector2Int cell, float cells)
    {
        foreach (var t in farStorages)
            if ((t.GridPosition - cell).magnitude < cells) return true;
        return false;
    }

    // 드릴 없이 캘 수 있는 가까운 광맥
    private static ResourceBlock NewOre()
    {
        var gm = GridManager.Instance;
        var core = CenterCore.Current.GridPosition;
        ResourceBlock best = null;
        float bestDist = float.MaxValue;
        foreach (var b in ResourceBlock.Active)
        {
            if (b == null || b.RemainingResources <= 0 || b.Data == null || b.Data.minToolGrade > 1) continue;
            var cell = gm.WorldToGrid(b.transform.position);
            float d = (cell - core).magnitude;
            if (d < NewOreCells || d >= bestDist || NearFarStorage(cell, CargoRails.OutpostCells)) continue;
            bestDist = d;
            best = b;
        }
        return best;
    }

    private static bool TryNewSoil(SoilType want, out SoilPatch found)
    {
        found = default;
        var map = TerrainMap.Current;
        if (map == null) return false;
        var core = CenterCore.Current.GridPosition;
        float best = float.MaxValue;
        foreach (var p in map.SoilPatches)
        {
            if (want != SoilType.None && p.type != want) continue;
            float d = (p.center - core).magnitude;
            if (d >= best || NearFarStorage(p.center, p.radius + LinkRules.MaxDistanceCells)) continue;
            best = d;
            found = p;
        }
        return best != float.MaxValue;
    }

    // 토양은 먼 쪽 가장자리까지 전기가 닿아야 함
    public static bool Powered(Vector2Int site, SoilType soil)
    {
        var em = EnergyManager.Instance;
        if (em == null) return true;
        var core = CenterCore.Current;
        int radius = soil == SoilType.None || TerrainMap.Current == null ? 0 : RadiusOf(site);
        Vector2 dir = core != null ? ((Vector2)(site - core.GridPosition)).normalized : Vector2.zero;
        return em.HasEnergy(site + Vector2Int.RoundToInt(dir * radius));
    }

    // 토양 안이나 바로 곁이면 그 토양의 기지
    public static SoilType SoilNear(Vector2Int cell)
    {
        var map = TerrainMap.Current;
        if (map == null) return SoilType.None;
        foreach (var p in map.SoilPatches)
            if ((p.center - cell).magnitude <= p.radius + SoilNearCells) return p.type;
        return SoilType.None;
    }

    private static int RadiusOf(Vector2Int site)
    {
        foreach (var p in TerrainMap.Current.SoilPatches)
            if ((p.center - site).magnitude <= p.radius + LinkRules.MaxDistanceCells) return Mathf.Max(0, p.radius - (int)(p.center - site).magnitude);
        return 0;
    }

    // 가장 가까운 전원에서 현장 쪽으로
    public static Vector2Int NextRelayCell(Vector2Int site)
    {
        var em = EnergyManager.Instance;
        var core = CenterCore.Current;
        Vector2 from = PowerRange.Center(core.FootprintMin, core.FootprintMax);
        float reach = core.DirectSupplyRadius - RelayMargin;
        float remain = Vector2.Distance(from, site) - reach;
        if (em != null)
            foreach (var relay in em.Relays)
            {
                if (relay == null || !relay.IsEnergized) continue;
                Vector2 c = PowerRange.Center(relay.FootprintMin, relay.FootprintMax);
                float rr = relay.SupplyRadius - RelayMargin;
                float rem = Vector2.Distance(c, site) - rr;
                if (rem >= remain) continue;
                remain = rem;
                from = c;
                reach = rr;
            }
        Vector2 dir = ((Vector2)site - from).normalized;
        float step = Mathf.Min(reach, Vector2.Distance(from, site));
        return OpenCell(Vector2Int.RoundToInt(from + dir * step), 3, 3);
    }

    // 현장 망 끝에서 본진 쪽으로 한 칸
    public static Vector2Int NextRailCell(ItemStorage site, RailNetwork net, ItemStorage home)
    {
        var target = (TileItem)home;
        float best = LinkRules.DistanceCells(site, home);
        foreach (var n in CargoRails.Networks)
        {
            if (n == net || n.Hub == null || CargoRails.CellsFromCore(n.Hub) >= CargoRails.OutpostCells) continue;
            foreach (var rail in n.Rails)
            {
                float d = LinkRules.DistanceCells(site, rail);
                if (d < best) { best = d; target = rail; }
            }
        }

        TileItem frontier = site;
        best = LinkRules.DistanceCells(site, target);
        if (net != null)
            foreach (var rail in net.Rails)
            {
                float d = LinkRules.DistanceCells(rail, target);
                if (d < best) { best = d; frontier = rail; }
            }

        Vector2 from = frontier.GridPosition, to = target.GridPosition;
        float dist = Vector2.Distance(from, to);
        float max = frontier is CargoRail ? LinkRules.MaxDistanceCells - 2f : CargoRail.StorageAutoCells - 2f;
        float step = Mathf.Clamp(dist - 5f, 1f, max);
        return OpenCell(Vector2Int.RoundToInt(from + (to - from).normalized * step), 1, 1);
    }

    public static Vector2Int OpenCell(Vector2Int center, int w, int h) => OpenCell(center, w, h, null);

    // 정화된 땅까지 따짐
    public static Vector2Int OpenCell(Vector2Int center, int w, int h, GameObject prefab)
    {
        var gm = GridManager.Instance;
        if (gm == null) return center;
        for (int r = 0; r <= OpenCellSearch; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var a = center + new Vector2Int(dx, dy);
                    if (prefab != null ? gm.CanPlaceAt(a, w, h, prefab) : gm.CanPlaceAt(a, w, h)) return a;
                }
        return center;
    }
}
