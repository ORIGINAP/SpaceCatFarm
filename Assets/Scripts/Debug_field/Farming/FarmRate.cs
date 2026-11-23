using System.Collections.Generic;
using UnityEngine;

// 작물별 수확 속도로 남은 시간 어림
public static class FarmRate
{
    private const float Window = 120f;

    private static readonly Dictionary<FarmingItemData, Queue<float>> recent = new();
    private static readonly List<TileItem> plots = new();
    private static readonly HashSet<FarmingItemData> farmCrops = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        recent.Clear();
        plots.Clear();
        farmCrops.Clear();
    }

    // static 이벤트라 한 번만 구독
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        PlantTile.OnAnyPlantHarvested -= Record;
        PlantTile.OnAnyPlantHarvested += Record;
    }

    private static void Record(PlantTile plant, HarvestResult[] results)
    {
        if (results == null) return;
        foreach (var r in results)
        {
            if (r?.item == null || !r.item.isCropProduct || r.item.isGolden) continue;
            if (!recent.TryGetValue(r.item, out var q)) recent[r.item] = q = new Queue<float>();
            for (int i = 0; i < r.amount; i++) q.Enqueue(Time.time);
            Trim(q);
        }
    }

    private static void Trim(Queue<float> q)
    {
        while (q.Count > 0 && Time.time - q.Peek() > Window) q.Dequeue();
    }

    private static bool Produces(SeedData seed, FarmingItemData crop)
    {
        if (seed.drops == null) return false;
        foreach (var d in seed.drops)
            if (d.item == crop) return true;
        return false;
    }

    // 요리 말고 밭에서 나는 작물
    public static bool IsFarmCrop(FarmingItemData item)
    {
        if (farmCrops.Count == 0)
            foreach (var seed in GameDataRegistry.FindAll<FarmingItemData>())
            {
                if (seed == null || seed.seedData == null || seed.seedData.drops == null) continue;
                foreach (var d in seed.seedData.drops)
                    if (d.item != null && d.item.isCropProduct && !d.item.isGolden) farmCrops.Add(d.item);
            }
        return item != null && farmCrops.Contains(item);
    }

    // 초당 수확량: 최근 기록과 지금 자라는 밭 중 큰 쪽
    public static float PerSecond(FarmingItemData crop)
    {
        if (crop == null) return 0f;
        float rate = 0f;
        if (recent.TryGetValue(crop, out var q))
        {
            Trim(q);
            rate = q.Count / Window;
        }
        var gm = GridManager.Instance;
        if (gm == null) return rate;
        gm.CollectTiles(TileType.Plant, plots);
        int growing = 0;
        float seconds = 0f;
        foreach (var t in plots)
        {
            if (t is not PlantTile p || p.IsGhost || p.CurrentSeed == null || !Produces(p.CurrentSeed, crop)) continue;
            growing++;
            seconds = p.CurrentSeed.TotalGrowthSeconds;
        }
        return growing > 0 && seconds > 0f ? Mathf.Max(rate, growing / seconds) : rate;
    }

    // 남은 초, 안 자라면 -1
    public static int EtaSeconds(FarmingItemData crop, int missing)
    {
        if (missing <= 0) return 0;
        float rate = PerSecond(crop);
        return rate > 0f ? Mathf.CeilToInt(missing / rate) : -1;
    }

    public static string Format(int seconds)
        => seconds < 60 ? "1분 안" : $"약 {Mathf.CeilToInt(seconds / 60f)}분";
}
