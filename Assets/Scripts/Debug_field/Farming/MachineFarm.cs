using System.Collections.Generic;
using UnityEngine;

// 자동화 진행 상태 (퀘스트 · 문명 조건 공용)
public static class MachineFarm
{
    private static readonly List<TileItem> seeders = new(), harvesters = new(), plots = new(), storages = new();
    private static readonly HashSet<SeedData> kinds = new();
    private static int   cachedKinds;
    private static float cachedAt = -10f;
    private static readonly HashSet<ItemStorage> fullNoticed = new();
    private static float nextFullNotice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        seeders.Clear();
        harvesters.Clear();
        plots.Clear();
        storages.Clear();
        kinds.Clear();
        cachedKinds = 0;
        cachedAt = -10f;
        fullNoticed.Clear();
        nextFullNotice = 0f;
    }

    // 창고가 차서 기계가 쉬면 창고마다 처음 한 번만 알림 (기계 일은 조용히)
    public static void NoticeFull(TileItem machine, string name, bool crops)
    {
        var storage = FullestStorage(machine);
        if (storage == null || fullNoticed.Contains(storage) || Time.unscaledTime < nextFullNotice) return;
        fullNoticed.Add(storage);
        nextFullNotice = Time.unscaledTime + 60f;
        string msg = $"창고 자리가 모자라 {name}가 쉬고 있어요 — 창고를 비우거나 더 지어 이어요" + (crops ? " (작물 판매기를 이으면 알아서 팔아요)" : "");
        NotificationUI.Instance?.Show(msg, NotificationUI.WarningColor);
        NetHost.NoticeAll(msg);
    }

    // 이어 준 창고 중 가장 꽉 찬 곳 (씨앗통은 빼고)
    public static ItemStorage FullestStorage(TileItem tile)
    {
        if (tile == null) return null;
        ItemStorage best = null;
        int bestFree = int.MaxValue;
        foreach (var link in tile.Links)
        {
            if (link is not ItemStorage s || s.SeedsOnly || s.IsUnlimited) continue;
            int free = s.Capacity - s.TotalCount;
            if (free < bestFree) { bestFree = free; best = s; }
        }
        return best;
    }

    // 이 밭을 맡았는데 창고가 차서 쉬는 수확기
    public static AutoHarvester RestingHarvesterFor(PlantTile plot)
    {
        var gm = GridManager.Instance;
        if (gm == null || plot == null) return null;
        gm.CollectTiles(TileType.AutoHarvester, harvesters);
        foreach (var h in harvesters)
            if (h is AutoHarvester harvester && !harvester.IsGhost && harvester.StorageFull
                && GridManager.AreaDistance(harvester.FootprintMin, harvester.FootprintMax, plot.GridPosition.x, plot.GridPosition.y) <= harvester.CoverageRadiusCells)
                return harvester;
        return null;
    }

    // 파종기와 수확기가 함께 돌보는 작물 종류
    public static int CropKinds
    {
        get
        {
            if (Time.unscaledTime - cachedAt < 1f) return cachedKinds;
            cachedAt = Time.unscaledTime;
            CollectKinds();
            cachedKinds = kinds.Count;
            return cachedKinds;
        }
    }

    public static bool IsMachineGrown(SeedData seed)
    {
        _ = CropKinds;
        return seed != null && kinds.Contains(seed);
    }

    private static void CollectKinds()
    {
        kinds.Clear();
        var gm = GridManager.Instance;
        if (gm == null) return;
        gm.CollectTiles(TileType.AutoSeeder, seeders);
        gm.CollectTiles(TileType.AutoHarvester, harvesters);
        foreach (var s in seeders)
        {
            if (s is not AutoSeeder seeder || seeder.IsGhost) continue;
            gm.GetTilesNearArea(seeder.FootprintMin, seeder.FootprintMax, seeder.CoverageRadiusCells, TileType.Plant, plots);
            foreach (var t in plots)
                if (t is PlantTile p && !p.IsGhost && p.CurrentSeed != null && !kinds.Contains(p.CurrentSeed) && CoveredByHarvester(p))
                    kinds.Add(p.CurrentSeed);
        }
    }

    // 파종기가 지금 심는 씨앗
    public static FarmingItemData SeedOf(AutoSeeder seeder)
    {
        if (seeder == null) return null;
        foreach (var s in seeder.StockedItems) return s;
        return seeder.LastSeedItem;
    }

    // 이 밭을 돌보는 같은 씨앗의 파종기로 돌려보내고 남은 수 반환
    public static int ReturnSeed(FarmingItemData seed, int amount, PlantTile plot, out bool tended)
    {
        tended = false;
        var gm = GridManager.Instance;
        if (gm == null || plot == null) return amount;
        gm.CollectTiles(TileType.AutoSeeder, seeders);
        foreach (var t in seeders)
        {
            if (amount <= 0) break;
            if (t is not AutoSeeder s || s.IsGhost || SeedOf(s) != seed) continue;
            if (GridManager.AreaDistance(s.FootprintMin, s.FootprintMax, plot.GridPosition.x, plot.GridPosition.y) > s.CoverageRadiusCells) continue;
            tended = true;
            amount -= s.AddItem(seed, amount);
        }
        return amount;
    }

    // 파종기들에 배정된 작물 종류
    public static int AssignedKinds()
    {
        var gm = GridManager.Instance;
        if (gm == null) return 0;
        gm.CollectTiles(TileType.AutoSeeder, seeders);
        int n = 0;
        for (int i = 0; i < seeders.Count; i++)
        {
            var seed = SeedOf(seeders[i] as AutoSeeder);
            if (seed == null) continue;
            bool dup = false;
            for (int j = 0; j < i && !dup; j++) dup = SeedOf(seeders[j] as AutoSeeder) == seed;
            if (!dup) n++;
        }
        return n;
    }

    // 범위 안 창고 중 농장 기계가 많이 이어진 곳, 같으면 가까운 곳
    public static ItemStorage NearestStorage(TileItem to)
    {
        var gm = GridManager.Instance;
        if (gm == null || to == null) return null;
        gm.CollectTiles(TileType.Storage, storages);
        ItemStorage best = null;
        int bestMachines = -1;
        float bestDist = float.MaxValue;
        foreach (var t in storages)
        {
            if (t is not ItemStorage s || s.IsGhost) continue;
            float d = LinkRules.DistanceCells(to, s);
            if (d > LinkRules.MaxDistanceCells) continue;
            int machines = 0;
            foreach (var link in s.Links)
                if (link is AutoSeeder || link is AutoHarvester) machines++;
            if (machines < bestMachines || (machines == bestMachines && d >= bestDist)) continue;
            best = s;
            bestMachines = machines;
            bestDist = d;
        }
        return best;
    }

    // 이 씨앗이 쌓인, 이을 수 있는 창고
    public static ItemStorage StorageWith(FarmingItemData seed, TileItem to)
    {
        var gm = GridManager.Instance;
        if (gm == null || seed == null || to == null) return null;
        gm.CollectTiles(TileType.Storage, storages);
        foreach (var t in storages)
            if (t is ItemStorage s && !s.IsGhost && s.GetCount(seed) > 0 && LinkRules.DistanceCells(to, s) <= LinkRules.MaxDistanceCells)
                return s;
        return null;
    }

    public static ItemStorage LinkedStorage(TileItem tile)
    {
        if (tile == null) return null;
        foreach (var link in tile.Links)
            if (link is ItemStorage s) return s;
        return null;
    }

    public static void RefreshHarvesters() => GridManager.Instance?.CollectTiles(TileType.AutoHarvester, harvesters);

    public static bool CoveredByHarvester(PlantTile plot)
    {
        foreach (var h in harvesters)
            if (h is AutoHarvester harvester && !harvester.IsGhost
                && GridManager.AreaDistance(harvester.FootprintMin, harvester.FootprintMax, plot.GridPosition.x, plot.GridPosition.y) <= harvester.CoverageRadiusCells)
                return true;
        return false;
    }

    // 파종기 곁 밭 수와 그중 수확기가 닿는 수
    public static void CountPlots(AutoSeeder seeder, out int near, out int covered)
    {
        near = covered = 0;
        var gm = GridManager.Instance;
        if (gm == null || seeder == null) return;
        gm.CollectTiles(TileType.AutoHarvester, harvesters);
        gm.GetTilesNearArea(seeder.FootprintMin, seeder.FootprintMax, seeder.CoverageRadiusCells, TileType.Plant, plots);
        foreach (var t in plots)
        {
            if (t is not PlantTile p || p.IsGhost) continue;
            near++;
            if (CoveredByHarvester(p)) covered++;
        }
    }

    public static bool LinkedToStorage(TileItem tile)
    {
        if (tile == null) return false;
        foreach (var link in tile.Links)
            if (link is ItemStorage) return true;
        return false;
    }

    // item과 같은 종류의 설비인지 (T1 · T2 공통)
    public static bool SameMachine(TileItem tile, FarmingItemData item)
    {
        var kind = item != null && item.tilePrefab != null ? item.tilePrefab.GetComponent<TileItem>() : null;
        return tile != null && !tile.IsGhost && kind != null && tile.GetType() == kind.GetType();
    }

    public static int LinkedCount(FarmingItemData item)
    {
        var gm = GridManager.Instance;
        if (gm == null) return 0;
        int n = 0;
        foreach (var t in gm.GetDynamicTiles())
            if (SameMachine(t, item) && LinkedToStorage(t)) n++;
        return n;
    }

    public static int InStorages(FarmingItemData item)
    {
        var gm = GridManager.Instance;
        if (gm == null || item == null) return 0;
        gm.CollectTiles(TileType.Storage, storages);
        int n = 0;
        foreach (var t in storages)
            if (t is ItemStorage s && !s.IsGhost) n += s.GetCount(item);
        return n;
    }

    // 가방 + 창고
    public static int Stock(FarmingItemData item)
        => (Inventory.Instance != null ? Inventory.Instance.GetCount(item) : 0) + InStorages(item);
}
