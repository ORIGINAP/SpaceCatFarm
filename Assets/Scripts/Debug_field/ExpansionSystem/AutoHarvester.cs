using UnityEngine;
using System.Collections;

public class AutoHarvester : TileItem, ICoverageArea
{
    public static event System.Action<AutoHarvester, PlantTile> OnAnyMachineHarvest;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnAnyMachineHarvest = null;

    [Header("수확기 설정")]
    [SerializeField] private float harvestInterval = 5f;
    [SerializeField] private float searchRadius    = 3f;
    [SerializeField] private int   targetsPerCycle = 1;

    [Header("작업 범위 표시 (마우스 호버 시)")]
    [SerializeField] private Color coverageColor = new Color(1.00f, 0.70f, 0.20f, 0.12f);

    [Header("실시간 상태")]
    [SerializeField] private float nextHarvestAt;

    public int        CoverageRadiusCells => SearchRadiusInCells();
    public Color      CoverageColor       => coverageColor;

    // 다 자란 작물은 있는데 이어 준 창고에 자리가 없어 쉬는 중
    public bool       StorageFull { get; private set; }

    public override string AbilityText => $"{Interval:0.#}초마다 다 자란 밭 {targetsPerCycle}칸을 수확해요 (초당 {targetsPerCycle / Interval:0.#}칸 · 범위 {RangeSide}×{RangeSide}칸)";

    private int RangeSide => (DropItemData != null ? DropItemData.tileWidth : 1) + CoverageRadiusCells * 2;

    private float Interval => harvestInterval / TechTreeManager.MachineSpeed;
    public override float  WorkRate    => harvestInterval > 0f ? targetsPerCycle / harvestInterval : 0f;

    private Coroutine harvestCoroutine;
    private FlashBeam flashBeam;

    private readonly NearbyTiles nearbyPlants = new(TileType.Plant);

    private void Start()
    {
        if (IsGhost) return;
        // 멀티: 수확은 호스트만
        if (!NetSession.IsReplica) harvestCoroutine = StartCoroutine(HarvestLoop());
        flashBeam = FlashBeam.Attach(transform, FlashBeam.Kind.Harvesting);
        LinkRules.HintOnPlace(this);
    }

    private void OnDestroy()
    {
        if (harvestCoroutine != null) StopCoroutine(harvestCoroutine);
    }

    private IEnumerator HarvestLoop()
    {
        while (true)
        {
            float interval = Interval;
            nextHarvestAt = Time.time + interval;
            yield return new WaitForSeconds(interval);

            for (int i = 0; i < targetsPerCycle; i++)
                TryHarvestOnce();
        }
    }

    private void TryHarvestOnce()
    {
        var target = FindReadyPlantTile();
        if (target == null) return;

        Vector3 targetPos = target.transform.position;
        var results = target.Harvest();
        if (results == null) return;

        flashBeam?.Fire(transform.position, targetPos, FlashBeam.Kind.Harvesting);

        foreach (var result in results)
        {
            if (result.item == null || result.amount <= 0) continue;
            if (result.item.itemType == FarmingItemType.Seed) SupplySeed(result.item, result.amount, target);
            else SupplyResult(result.item, result.amount, targetPos);
        }
        OnAnyMachineHarvest?.Invoke(this, target);
    }

    private int SearchRadiusInCells()
    {
        float cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        return Mathf.Max(1, Mathf.CeilToInt(searchRadius / cellSize));
    }

    private PlantTile FindReadyPlantTile()
    {
        bool blocked = false;
        foreach (var tile in nearbyPlants.Get(this, SearchRadiusInCells()))
        {
            if (tile is not PlantTile plant || plant.CurrentState != PlantTile.PlantState.ReadyToHarvest) continue;
            if (HasRoomForHarvest(plant, out bool viaStorage)) { StorageFull = false; return plant; }
            blocked |= viaStorage;
        }
        StorageFull = blocked;
        if (StorageFull) MachineFarm.NoticeFull(this, "수확기", true);
        return null;
    }

    private bool HasRoomForHarvest(PlantTile plant, out bool viaStorage)
    {
        viaStorage = false;
        var drops = plant.CurrentSeed != null ? plant.CurrentSeed.drops : null;
        if (drops == null) return true;

        int need = 0;
        FarmingItemData product = null;
        foreach (var drop in drops)
        {
            if (drop.item == null || drop.item.itemType == FarmingItemType.Seed) continue;
            need += drop.amount;
            if (product == null && drop.item.isCropProduct) product = drop.item;
        }
        // 작물을 받는 창고에 이었으면 창고 자리만 봄 — 가장 많이 나와도 다 들어갈 때만 거두고, 아니면 쉼
        if (product != null && StoresIn(product))
        {
            viaStorage = true;
            need += FarmConditions.MaxExtraCrops(plant, plant.IsFertile) + (product.goldenVariant != null ? 1 : 0);
            return StorageRoom(product) >= need;
        }

        // 씨앗은 갈 곳이 없으면 버리므로 작물만 확인
        foreach (var drop in drops)
        {
            if (drop.item == null || drop.item.itemType == FarmingItemType.Seed) continue;
            if (!HasRoomFor(drop.item)) return false;
        }
        return true;
    }

    private bool HasRoomFor(FarmingItemData item)
    {
        for (int i = 0; i < Links.Count; i++)
            if (Links[i] is ItemStorage storage && storage.Accepts(item) && RoomIn(storage, item) > 0)
                return true;
        return Inventory.Instance != null && Inventory.Instance.CanAddItem(item);
    }

    private bool StoresIn(FarmingItemData item)
    {
        for (int i = 0; i < Links.Count; i++)
            if (Links[i] is ItemStorage storage && storage.Accepts(item)) return true;
        return false;
    }

    private int StorageRoom(FarmingItemData item)
    {
        long room = 0;
        for (int i = 0; i < Links.Count; i++)
            if (Links[i] is ItemStorage storage && storage.Accepts(item))
                room += RoomIn(storage, item);
        return (int)System.Math.Min(room, int.MaxValue);
    }

    private void SupplyResult(FarmingItemData item, int amount, Vector3 fallbackDropPos)
    {
        for (int i = 0; i < Links.Count && amount > 0; i++)
            if (Links[i] is ItemStorage storage && storage.Accepts(item))
                amount -= storage.AddItem(item, Mathf.Min(amount, RoomIn(storage, item)));

        if (amount > 0)
            Inventory.Instance?.AddItemOrDropFrom(transform.position, item, amount, fallbackDropPos);
    }

    // 거둔 씨앗은 그 밭을 돌보는 파종기로 먼저, 남으면 창고 · 가방, 그래도 넘치면 버림
    private void SupplySeed(FarmingItemData seed, int amount, PlantTile plot)
    {
        amount = MachineFarm.ReturnSeed(seed, amount, plot, out bool tended);
        for (int i = 0; i < Links.Count && amount > 0; i++)
            if (Links[i] is ItemStorage storage && storage.Accepts(seed))
                amount -= storage.AddItem(seed, Mathf.Min(amount, RoomIn(storage, seed)));
        // 파종기가 도는 줄은 씨앗이 남아돌아 가방에 안 넣음
        if (amount > 0 && !tended && Inventory.Instance != null && Inventory.Instance.CanAddItem(seed))
            Inventory.Instance.AddItemFrom(transform.position, seed, amount);
    }

    // 남는 씨앗이 작물 자리를 다 차지하지 않게
    private static int RoomIn(ItemStorage storage, FarmingItemData item)
    {
        if (storage.IsUnlimited) return int.MaxValue;
        int free = storage.Capacity - storage.TotalCount;
        if (item.itemType != FarmingItemType.Seed || storage.SeedsOnly) return free;

        int seeds = 0;
        foreach (var stocked in storage.StockedItems)
            if (stocked.itemType == FarmingItemType.Seed) seeds += storage.GetCount(stocked);
        int room = Mathf.Min(storage.Capacity / 5 - storage.GetCount(item), storage.Capacity / 2 - seeds);
        return Mathf.Min(free, room);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, searchRadius);
    }
#endif
}
