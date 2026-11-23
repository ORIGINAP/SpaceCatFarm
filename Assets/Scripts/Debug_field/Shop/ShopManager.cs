using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ShopTab { Seeds, Decor, Workshop }

// 상점가: 씨앗 가게 · 꾸미기 가게 · 고양이 공방
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance => Services.Get<ShopManager>();

    [Header("상점에 등록할 아이템 풀")]
    [SerializeField] private FarmingItemData[] itemPool;

    [Header("구매가 배율 (구매가 = basePrice × buyMultiplier)")]
    [SerializeField] private float buyMultiplier = 2f;

    public const int   DecorLevel         = 2;
    public const int   WorkshopLevel      = 3;
    public const float WorkshopMultiplier = 15f;

    // 공방 재료와 열리는 문명 단계 (캘 수 있게 된 다음 단계)
    private static readonly (string name, int level)[] WorkshopItems =
    {
        ("Item_CopperIngot", 3), ("Item_IronIngot", 3), ("item_Alloy", 3),
        ("Item_AluminumIngot", 4), ("Item_LithiumIngot", 4), ("Item_CircuitBoard", 4), ("Item_LightAlloy", 4),
        ("Item_TitaniumIngot", 5), ("Item_TitaniumAlloy", 5), ("Item_ReinforcedAlloy", 5),
        ("Item_UraniumIngot", 6), ("Item_EnergyCell", 6), ("Item_Diamond", 6), ("Item_StarAlloy", 6),
    };

    public IReadOnlyList<FarmingItemData> CurrentStock => _stock;
    private readonly List<FarmingItemData> _stock = new();

    public event System.Action OnStockChanged;

    public event System.Action<FarmingItemData, int> OnItemSold;

    public static ShopTab TabOf(FarmingItemData item)
        => item == null || item.itemType == FarmingItemType.Seed ? ShopTab.Seeds : item.charm > 0 ? ShopTab.Decor : ShopTab.Workshop;

    public static int TabLevel(ShopTab tab) => tab switch
    {
        ShopTab.Decor    => DecorLevel,
        ShopTab.Workshop => WorkshopLevel,
        _                => 0,
    };

    public static string TabName(ShopTab tab) => tab switch
    {
        ShopTab.Decor    => "꾸미기 가게",
        ShopTab.Workshop => "고양이 공방",
        _                => "씨앗 가게",
    };

    public static bool TabOpen(ShopTab tab) => CenterCore.CurrentLevel >= TabLevel(tab);

    public static int WorkshopLevelOf(FarmingItemData item)
    {
        if (item == null) return -1;
        foreach (var (name, level) in WorkshopItems)
            if (item.name == name) return level;
        return -1;
    }

    public int BuyPrice(FarmingItemData item)
    {
        float mult = TabOf(item) switch
        {
            ShopTab.Decor    => 1f,
            ShopTab.Workshop => WorkshopMultiplier,
            _                => buyMultiplier,
        };
        return Mathf.Max(1, Mathf.RoundToInt(item.basePrice * mult));
    }

    public int SellPrice(FarmingItemData item)
    {
        float multiplier = item.isCropProduct ? GameEventManager.CropSellMultiplier : 1f;
        return Mathf.RoundToInt(item.basePrice * multiplier);
    }

    private void Awake()
    {
        if (!Services.Register(this)) { Destroy(gameObject); return; }
        LoadStock();
    }

    private void OnDestroy() => Services.Unregister(this);

    private void LoadStock()
    {
        _stock.Clear();
        var seen = new HashSet<FarmingItemData>();
        if (itemPool != null)
            foreach (var item in itemPool)
                if (IsBuyable(item) && seen.Add(item)) _stock.Add(item);
        foreach (var item in GameDataRegistry.FindAll<FarmingItemData>())
            if (IsBuyable(item) && seen.Add(item)) _stock.Add(item);
        OnStockChanged?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var guids = AssetDatabase.FindAssets("t:FarmingItemData");
        var found = new List<FarmingItemData>();
        foreach (var guid in guids)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FarmingItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && !asset.hideFromShop) found.Add(asset);
        }
        // 개수만 비교하면 교체를 놓침
        if (itemPool == null || itemPool.Length != found.Count
            || !new HashSet<FarmingItemData>(itemPool).SetEquals(found))
        {
            itemPool = found.ToArray();
            EditorUtility.SetDirty(this);
        }
    }
#endif

    private Dictionary<FarmingItemData, List<CraftingRecipe>> _recipesByOutput;

    public bool IsPurchasable(FarmingItemData item) => item != null && LockReason(item) == null;

    public string LockReason(FarmingItemData item)
    {
        if (item == null) return null;
        int coreLevel = RequiredCoreLevel(item);
        if (coreLevel > CenterCore.CurrentLevel) return $"문명 Lv.{coreLevel} 필요";
        var tier = LockedTier(item);
        return tier.HasValue ? $"{CraftingManager.TierDisplayName(tier.Value)} 제작 필요" : null;
    }

    public int RequiredCoreLevel(FarmingItemData item)
    {
        int level = Mathf.Max(item.unlockCoreLevel, Mathf.Max(TabLevel(TabOf(item)), WorkshopLevelOf(item)));
        var recipes = RecipesFor(item);
        if (recipes != null)
        {
            int lowest = int.MaxValue;
            foreach (var r in recipes) lowest = Mathf.Min(lowest, r.requiredCoreLevel);
            level = Mathf.Max(level, lowest);
        }
        return level;
    }

    private List<CraftingRecipe> RecipesFor(FarmingItemData item)
    {
        var cm = CraftingManager.Instance;
        if (item == null || cm == null) return null;

        if (_recipesByOutput == null)
        {
            _recipesByOutput = new Dictionary<FarmingItemData, List<CraftingRecipe>>();
            foreach (var r in cm.Recipes)
            {
                if (r == null || r.outputItem == null) continue;
                if (!_recipesByOutput.TryGetValue(r.outputItem, out var list))
                {
                    list = new List<CraftingRecipe>();
                    _recipesByOutput.Add(r.outputItem, list);
                }
                list.Add(r);
            }
        }
        return _recipesByOutput.TryGetValue(item, out var recipes) ? recipes : null;
    }

    private CraftingTier? LockedTier(FarmingItemData item)
    {
        var cm = CraftingManager.Instance;
        var recipes = RecipesFor(item);
        if (recipes == null || recipes.Count == 0) return null;

        var lowest = recipes[0].tier;
        foreach (var r in recipes)
        {
            if (cm.IsTierUnlocked(r.tier)) return null;
            if (r.tier < lowest) lowest = r.tier;
        }
        return lowest;
    }

    public static bool IsBuyable(FarmingItemData item)
        => item != null && !item.hideFromShop && item.basePrice > 0
        && (item.itemType == FarmingItemType.Seed || item.charm > 0 || WorkshopLevelOf(item) > 0);

    public static bool IsSellable(FarmingItemData item)
        => item != null && !item.hideFromShop && item.basePrice > 0
        && (item.itemType == FarmingItemType.Seed || item.isCropProduct);

    public bool TryBuy(FarmingItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        var inventory = Inventory.Instance;
        var currency  = CurrencyManager.Instance;
        if (inventory == null || currency == null) return false;
        if (!IsBuyable(item) || !IsPurchasable(item)) return false;

        // int 오버플로 방지
        long total = (long)BuyPrice(item) * amount;

        if (!inventory.CanAddItem(item)) return false;
        if (!currency.TrySpend(total)) return false;
        if (inventory.AddItem(item, amount))
        {
            NetOps.Global(NetOpType.ShopBuy, item.name, amount);
            return true;
        }

        currency.Add(total);
        return false;
    }

    public bool TrySell(FarmingItemData item, int amount = 1)
    {
        if (!IsSellable(item) || amount <= 0) return false;
        var inventory = Inventory.Instance;
        var currency  = CurrencyManager.Instance;
        if (inventory == null || currency == null) return false;
        if (!inventory.ConsumeItem(item, amount)) return false;
        currency.Add((long)SellPrice(item) * amount);
        OnItemSold?.Invoke(item, amount);
        NetOps.Global(NetOpType.ShopSell, item.name, amount);
        return true;
    }
}
