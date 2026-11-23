using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RailNetwork
{
    public readonly List<CargoRail> Rails = new();
    // 착륙장 달린 창고 → 본진 가까운 순
    public readonly List<ItemStorage> Storages = new();
    public ItemStorage Hub => Storages.Count > 0 ? Storages[0] : null;
}

// 레일망 구성 + 전초 창고의 물건을 본진 쪽 창고로 운송
public class CargoRails : MonoBehaviour
{
    private const float TickSeconds  = 0.5f;
    private const int   ItemsPerTick = 4;
    private const int   MaxCrates    = 24;
    private const float CrateSpeed   = 6f;
    private const float ArmHeight    = 0.7f;
    private const float BoxHeight    = 0.4f;
    private const int   CrateOrder   = 5;
    // 작물 하나가 차지할 수 있는 본진 창고 몫
    private const int   KindShare    = 4;
    private const float BlockedNoticeSeconds = 180f;
    private const float BlockedFullRatio     = 0.9f;

    // 이보다 가까우면 본진, 멀면 전초
    public const float OutpostCells = 20f;

    private static readonly string[] CrateRows =
    {
        "ooooooo",
        "obbbbbo",
        "oblllbo",
        "obbbbbo",
        "ooooooo",
    };

    public static event Action<FarmingItemData, int> OnDelivered;

    private static int builtVersion = -1, builtLinkVersion = -1;
    private static GridManager builtFor;
    private static readonly List<RailNetwork> networks = new();
    private static readonly Dictionary<CargoRail, RailNetwork> byRail = new();
    private static readonly List<TileItem> allRails = new();
    private static readonly Queue<CargoRail> queue = new();
    private static readonly List<ItemStorage> counted = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnDelivered = null;
        builtVersion = builtLinkVersion = -1;
        builtFor = null;
        networks.Clear();
        byRail.Clear();
        allRails.Clear();
        queue.Clear();
        counted.Clear();
    }

    public static void MarkDirty() => builtVersion = -1;

    public static IReadOnlyList<RailNetwork> Networks
    {
        get { EnsureBuilt(); return networks; }
    }

    public static RailNetwork Of(CargoRail rail)
    {
        EnsureBuilt();
        return rail != null && byRail.TryGetValue(rail, out var net) ? net : null;
    }

    public static bool SameNetwork(CargoRail a, CargoRail b)
    {
        var net = Of(a);
        return net != null && net == Of(b);
    }

    public static Vector3 LinePoint(TileItem t)
        => WorldConnectionLine.UnderSprites(t.transform.position + Vector3.up * (t is CargoRail ? ArmHeight : BoxHeight));

    public static float CellsFromCore(TileItem t)
    {
        var core = CenterCore.Current;
        return core != null ? LinkRules.DistanceCells(t, core) : float.MaxValue;
    }

    public static bool IsProducer(TileItem t)
        => t is ResourceExtractor || t is AutoHarvester || t is Smelter || t is CatKitchen;

    public static bool IsOutpost(RailNetwork net, ItemStorage s)
    {
        if (net.Hub == null || s == net.Hub) return false;
        if (CellsFromCore(net.Hub) >= OutpostCells || CellsFromCore(s) < OutpostCells) return false;
        foreach (var link in s.Links)
            if (IsProducer(link)) return true;
        return false;
    }

    // 본진까지 이어진 전초기지 수 (가까이 붙은 창고는 한 곳)
    public static int OutpostCount
    {
        get
        {
            counted.Clear();
            foreach (var net in Networks)
                foreach (var s in net.Storages)
                {
                    if (!IsOutpost(net, s)) continue;
                    bool near = false;
                    foreach (var c in counted)
                        if (LinkRules.DistanceCells(c, s) < OutpostCells) { near = true; break; }
                    if (!near) counted.Add(s);
                }
            return counted.Count;
        }
    }

    public static string StatusOf(CargoRail rail)
    {
        var net = Of(rail);
        if (net == null || net.Storages.Count == 0) return "창고와 이어 주세요";
        if (net.Storages.Count == 1) return "창고 1곳 — 다른 창고까지 기둥을 이어 주세요";
        if (!net.Hub.HasSpace) return "본진 창고가 가득 찼어요";
        return $"창고 {net.Storages.Count}곳 · 본진으로 운송 중";
    }

    private static void EnsureBuilt()
    {
        var gm = GridManager.Instance;
        if (gm == null) { byRail.Clear(); networks.Clear(); builtVersion = -1; builtFor = null; return; }
        if (builtVersion == gm.Version && builtLinkVersion == TileItem.LinkVersion && builtFor == gm) return;
        builtVersion     = gm.Version;
        builtLinkVersion = TileItem.LinkVersion;
        builtFor         = gm;
        byRail.Clear();
        networks.Clear();

        gm.CollectTiles(TileType.CargoRail, allRails);
        foreach (var tile in allRails)
        {
            if (tile is not CargoRail start || start.IsGhost || byRail.ContainsKey(start)) continue;
            var net = new RailNetwork();
            networks.Add(net);
            byRail[start] = net;
            net.Rails.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var rail = queue.Dequeue();
                foreach (var t in rail.Links)
                {
                    if (t is CargoRail other)
                    {
                        if (byRail.ContainsKey(other)) continue;
                        byRail[other] = net;
                        net.Rails.Add(other);
                        queue.Enqueue(other);
                    }
                    else if (t is ItemStorage storage && !net.Storages.Contains(storage))
                        net.Storages.Add(storage);
                }
            }
            net.Storages.Sort(CompareStorages);
        }
    }

    private static int CompareStorages(ItemStorage a, ItemStorage b)
    {
        int pad = HomePad(b).CompareTo(HomePad(a));
        return pad != 0 ? pad : CellsFromCore(a).CompareTo(CellsFromCore(b));
    }

    private static bool HomePad(ItemStorage s)
    {
        if (CellsFromCore(s) >= OutpostCells) return false;
        foreach (var link in s.Links)
            if (link is ShuttlePad) return true;
        return false;
    }

    private sealed class Crate
    {
        public SpriteRenderer sr;
        public readonly List<Vector3> path = new();
        public int segment;
        public float along;
    }

    private readonly List<Crate> crates = new();
    private readonly List<FarmingItemData> stockBuffer = new();
    private readonly Dictionary<CargoRail, CargoRail> cameFrom = new();
    private readonly Queue<CargoRail> walk = new();
    private readonly Dictionary<ItemStorage, int> lastTotal = new();
    private float nextBlockedNotice;
    private Sprite crateSprite;

    private void Start()
    {
        crateSprite = LabArt.Pixel("rail_crate", CrateRows, "o#6C5D76 b#FFD98A l#FFF2C6", new Vector2(0.5f, 0.5f));
        TileItem.OnAnyTilePlaced += HandlePlaced;
        StartCoroutine(NetSession.IsReplica ? ReplicaLoop() : HostLoop());
    }

    private void OnDestroy() => TileItem.OnAnyTilePlaced -= HandlePlaced;

    private static void HandlePlaced(TileItem tile)
    {
        if (tile is not ItemStorage storage || NetSession.IsReplica || SaveManager.IsLoading || SaveManager.RecentlyLoaded) return;
        CargoRail.LinkNewStorage(storage);
    }

    private IEnumerator HostLoop()
    {
        var wait = new WaitForSeconds(TickSeconds);
        while (true)
        {
            yield return wait;
            foreach (var net in Networks)
                for (int i = 1; i < net.Storages.Count; i++)
                {
                    var s = net.Storages[i];
                    if (s != null && Move(net, i) > 0) SpawnCrate(net, s);
                }
        }
    }

    // 참가자: 창고가 줄어들면 화물을 띄움
    private IEnumerator ReplicaLoop()
    {
        var wait = new WaitForSeconds(TickSeconds);
        while (true)
        {
            yield return wait;
            foreach (var net in Networks)
                for (int i = 1; i < net.Storages.Count; i++)
                {
                    var s = net.Storages[i];
                    if (s == null) continue;
                    if (lastTotal.TryGetValue(s, out int last) && s.TotalCount < last) SpawnCrate(net, s);
                    lastTotal[s] = s.TotalCount;
                }
        }
    }

    private int Move(RailNetwork net, int fromIndex)
    {
        var from = net.Storages[fromIndex];
        stockBuffer.Clear();
        stockBuffer.AddRange(from.StockedItems);
        int budget = ItemsPerTick, moved = 0;
        bool waiting = false;
        foreach (var item in stockBuffer)
        {
            if (budget <= 0) break;
            if (item == null || Stays(from, item)) continue;
            waiting = true;
            for (int d = 0; d < fromIndex && budget > 0; d++)
            {
                var to = net.Storages[d];
                if (to == null) continue;
                int room = RoomFor(to, item);
                if (room <= 0) continue;
                int added = to.AddItem(item, Mathf.Min(Mathf.Min(budget, room), from.GetCount(item)));
                if (added <= 0) continue;
                from.RemoveItem(item, added);
                budget -= added;
                moved  += added;
                OnDelivered?.Invoke(item, added);
            }
        }
        if (moved == 0 && waiting) NoticeBlocked(from);
        return moved;
    }

    private static int RoomFor(ItemStorage to, FarmingItemData item)
        => to.IsUnlimited ? int.MaxValue : Mathf.Max(1, to.Capacity / KindShare) - to.GetCount(item);

    private void NoticeBlocked(ItemStorage from)
    {
        if (from.IsUnlimited || from.TotalCount < from.Capacity * BlockedFullRatio || Time.unscaledTime < nextBlockedNotice) return;
        nextBlockedNotice = Time.unscaledTime + BlockedNoticeSeconds;
        NotificationUI.Instance?.Show("본진 창고에 자리가 없어 전초기지 창고가 가득 찼어요 — 본진 쪽 창고를 비우거나 더 이어 주세요", NotificationUI.WarningColor);
    }

    // 씨앗 · 곁의 설비가 쓸 재료는 남김, 착륙장 · 판매기 창고는 종점
    private static bool Stays(ItemStorage from, FarmingItemData item)
    {
        if (item.itemType == FarmingItemType.Seed) return true;
        foreach (var link in from.Links)
        {
            switch (link)
            {
                case ShuttlePad:
                case AutoSeller:
                    return true;
                case Smelter:
                    if (Smelter.RecipeFor(item) != null) return true;
                    break;
                case CatKitchen kitchen when kitchen.Recipe != null:
                    foreach (var ing in kitchen.Recipe.ingredients)
                        if (ing.item == item) return true;
                    break;
                case AutoSeeder:
                    if (item.IsFertilizer) return true;
                    break;
                case Generator:
                case AutoFuelPipe:
                    if (item.canUseFuel) return true;
                    break;
            }
        }
        return false;
    }

    private void SpawnCrate(RailNetwork net, ItemStorage from)
    {
        Crate crate = null;
        foreach (var c in crates)
            if (!c.sr.enabled) { crate = c; break; }
        if (crate == null)
        {
            if (crates.Count >= MaxCrates) return;
            crate = new Crate { sr = LabArt.Make("RailCrate", crateSprite, Vector3.zero, CrateOrder, Color.white, transform) };
            crates.Add(crate);
        }
        if (!BuildPath(net, from, crate.path)) { crate.sr.enabled = false; return; }
        crate.segment = 0;
        crate.along   = 0f;
        crate.sr.transform.position = crate.path[0];
        crate.sr.enabled = true;
    }

    // 출발 창고 → 기둥들 → 본진 창고
    private bool BuildPath(RailNetwork net, ItemStorage from, List<Vector3> path)
    {
        path.Clear();
        cameFrom.Clear();
        walk.Clear();
        foreach (var link in from.Links)
            if (link is CargoRail r && !cameFrom.ContainsKey(r)) { cameFrom[r] = null; walk.Enqueue(r); }

        CargoRail end = null;
        while (walk.Count > 0 && end == null)
        {
            var rail = walk.Dequeue();
            foreach (var link in rail.Links)
            {
                if (link == net.Hub) { end = rail; break; }
                if (link is CargoRail other && !cameFrom.ContainsKey(other)) { cameFrom[other] = rail; walk.Enqueue(other); }
            }
        }
        if (end == null) return false;

        path.Add(LinePoint(net.Hub));
        for (var r = end; r != null; r = cameFrom[r]) path.Add(LinePoint(r));
        path.Add(LinePoint(from));
        path.Reverse();
        return true;
    }

    private void Update()
    {
        foreach (var c in crates)
        {
            if (!c.sr.enabled) continue;
            c.along += CrateSpeed * Time.deltaTime;
            while (c.segment < c.path.Count - 1)
            {
                float len = Vector3.Distance(c.path[c.segment], c.path[c.segment + 1]);
                if (c.along < len) break;
                c.along -= len;
                c.segment++;
            }
            if (c.segment >= c.path.Count - 1) { c.sr.enabled = false; continue; }
            float seg = Vector3.Distance(c.path[c.segment], c.path[c.segment + 1]);
            c.sr.transform.position = Vector3.Lerp(c.path[c.segment], c.path[c.segment + 1], seg > 0f ? c.along / seg : 1f);
        }
    }
}
