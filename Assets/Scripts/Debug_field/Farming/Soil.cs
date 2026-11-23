using System.Collections.Generic;
using UnityEngine;

public enum SoilType : byte { None, Starlight, Spring, Ember }

public readonly struct SoilPatch
{
    public readonly SoilType type;
    public readonly Vector2Int center;
    public readonly int radius;

    public SoilPatch(SoilType type, Vector2Int center, int radius)
    {
        this.type = type;
        this.center = center;
        this.radius = radius;
    }
}

// 특산 토양: 맞는 작물은 빨리 자라고 더 열림
public static class Soil
{
    public const float GrowthMult = 2f;
    public const int   ExtraCrops = 1;

    private static readonly Dictionary<SoilType, string> crops = new();
    private static readonly Dictionary<string, FarmingItemData> seedByCrop = new();
    private static readonly List<TileItem> plots = new();
    private static readonly List<(ulong id, Transform root, int color)> avatars = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        crops.Clear();
        seedByCrop.Clear();
        plots.Clear();
        avatars.Clear();
    }

    public static string Name(SoilType s) => s switch
    {
        SoilType.Starlight => "별빛 토양",
        SoilType.Spring    => "온천 지대",
        SoilType.Ember     => "화산재 땅",
        _                  => "",
    };

    public static string TilePrefix(SoilType s) => s switch
    {
        SoilType.Starlight => "soilstar",
        SoilType.Spring    => "soilspring",
        SoilType.Ember     => "soilember",
        _                  => "",
    };

    public static Color32 MapColor(SoilType s) => s switch
    {
        SoilType.Starlight => new Color32(135, 113, 211, 255),
        SoilType.Spring    => new Color32(85, 194, 177, 255),
        SoilType.Ember     => new Color32(153, 95, 79, 255),
        _                  => new Color32(0, 0, 0, 0),
    };

    public static SoilType Of(FarmingItemData seed)
        => seed != null && seed.seedData != null ? seed.seedData.preferredSoil : SoilType.None;

    public static string CropName(FarmingItemData seed)
    {
        if (seed != null && seed.seedData != null && seed.seedData.drops != null)
            foreach (var d in seed.seedData.drops)
                if (d.item != null && d.item.isCropProduct && !d.item.isGolden) return d.item.itemName;
        return seed != null ? seed.itemName : "";
    }

    // 토양을 타는 작물의 씨앗
    public static FarmingItemData SoilSeedOf(FarmingItemData crop)
    {
        if (crop == null) return null;
        if (seedByCrop.Count == 0)
            foreach (var it in GameDataRegistry.FindAll<FarmingItemData>())
                if (it != null && Of(it) != SoilType.None) seedByCrop[CropName(it)] = it;
        return seedByCrop.TryGetValue(crop.itemName, out var seed) ? seed : null;
    }

    public static SoilType At(Vector2Int cell)
        => TerrainMap.Current != null ? TerrainMap.Current.SoilAtCell(cell) : SoilType.None;

    public static bool Suits(PlantTile plot)
    {
        var seed = plot != null ? plot.CurrentSeed : null;
        return seed != null && seed.preferredSoil != SoilType.None && plot.SoilKind == seed.preferredSoil;
    }

    // 이 땅에서 잘 자라는 작물 이름들
    public static string CropsText(SoilType s)
    {
        if (s == SoilType.None) return "";
        if (crops.TryGetValue(s, out var text)) return text;
        var names = new List<string>();
        foreach (var it in GameDataRegistry.FindAll<FarmingItemData>())
        {
            if (it == null || Of(it) != s) continue;
            names.Add(CropName(it));
        }
        return crops[s] = string.Join(" · ", names);
    }

    public static bool TryNearest(SoilType s, Vector2Int from, out SoilPatch patch)
    {
        patch = default;
        var map = TerrainMap.Current;
        if (map == null) return false;
        int best = int.MaxValue;
        foreach (var p in map.SoilPatches)
        {
            if (p.type != s) continue;
            int d = (p.center - from).sqrMagnitude;
            if (d >= best) continue;
            best = d;
            patch = p;
        }
        return best != int.MaxValue;
    }

    // 지도에서 밝혀졌거나 다른 대원이 가 있으면 찾은 것
    public static bool IsFound(SoilType s)
    {
        var map = TerrainMap.Current;
        var gm = GridManager.Instance;
        if (map == null || gm == null) return false;
        avatars.Clear();
        NetBootstrap.Avatars?.CollectRoots(avatars);
        foreach (var p in map.SoilPatches)
        {
            if (p.type != s) continue;
            if (ExploredMap.IsExplored(p.center.x, p.center.y)) return true;
            foreach (var a in avatars)
                if ((gm.WorldToGrid(a.root.position) - p.center).magnitude <= p.radius) return true;
        }
        return false;
    }

    // 맞는 땅에서 자라는 이 작물 칸 수
    public static int CountSuited(SeedData seed)
    {
        var gm = GridManager.Instance;
        if (gm == null || seed == null) return 0;
        gm.CollectTiles(TileType.Plant, plots);
        int n = 0;
        foreach (var t in plots)
            if (t is PlantTile p && !p.IsGhost && p.CurrentSeed == seed && Suits(p)) n++;
        return n;
    }

    public static int PlotsOn(SoilType s)
    {
        var gm = GridManager.Instance;
        if (gm == null) return 0;
        gm.CollectTiles(TileType.Plant, plots);
        int n = 0;
        foreach (var t in plots)
            if (t is PlantTile p && !p.IsGhost && p.SoilKind == s) n++;
        return n;
    }
}
