using UnityEngine;

public enum FarmingItemType { Tile, Seed, Resource, Tool, Equipment }

[CreateAssetMenu(fileName = "NewItem", menuName = "Farming/Item Data")]
public class FarmingItemData : ScriptableObject
{
    public string   itemName;
    [TextArea(2, 4)]
    public string   description;
    public Sprite   icon;
    public FarmingItemType itemType;

    [Header("경제")]
    [Tooltip("아이템 원가")]
    public int basePrice = 10;
    [Tooltip("체크하면 ShopManager 자동 등록 목록에서 제외됩니다")]
    public bool hideFromShop = false;
    [Tooltip("센터코어가 이 문명 단계에 올라야 상점에서 살 수 있습니다")]
    [Min(0)] public int unlockCoreLevel = 0;
    [Tooltip("아이템 티어 (1~4)")]
    [Range(1, MaxTier)] public int tier = 1;

    public const int MaxTier = 4;

    public int Tier => Mathf.Clamp(tier, 1, MaxTier);

    [Tooltip("장식이면 마을 매력 (0 = 장식 아님)")]
    [Min(0)] public int charm = 0;

    [Header("Tile Item")]
    public GameObject tilePrefab;
    [Min(1)] public int tileWidth  = 1;
    [Min(1)] public int tileHeight = 1;

    [Header("Seed Item")]
    public SeedData seedData;

    [Header("Resource")]
    public int   minToolGrade = 1;
    public bool  canUseFuel   = false;
    [Tooltip("1개당 연료량")]
    public float fuelValue    = 20f;
    [Tooltip("작물 수확으로 얻는 아이템이면 체크")]
    public bool  isCropProduct = false;

    [Header("농사 (작물 · 가공품 · 퇴비)")]
    [Tooltip("이 작물의 황금 버전")]
    public FarmingItemData goldenVariant;
    [Tooltip("황금 작물이면 체크")]
    public bool  isGolden = false;
    [Tooltip("퇴비 효과 횟수")]
    [Min(0)] public int fertilizerCharges = 0;

    public bool IsFertilizer => fertilizerCharges > 0;

    [Header("Tool (FarmingItemType = Tool일 때)")]
    [Tooltip("도구 등급")]
    public int   toolGrade    = 1;
    [Tooltip("1회 채굴량 배율 (1.0 = 기본)")]
    public float miningPower  = 1f;
    [Tooltip("초당 채굴 횟수")]
    public float miningSpeed  = 1f;
    [Tooltip("0보다 크면 수확 도구(낫)입니다: 클릭한 칸 주변 이 칸 수(정사각형)까지 다 자란 작물을 한 번에 수확합니다. " +
             "수확 도구는 드릴이 아니므로 채굴기 소켓에 끼울 수 없습니다.")]
    [Min(0)] public int harvestRadius = 0;

    [Tooltip("0보다 크면 물뿌리개: 클릭한 밭 둘레 이 칸 수(정사각형)에 물을 줘 성장이 빨라집니다")]
    [Min(0)] public int waterRadius = 0;
    [Tooltip("물뿌리개 물이 마르기까지 (초)")]
    [Min(0f)] public float waterSeconds = 120f;
    [Tooltip("0보다 크면 탐사 스캐너: 둘레 이 칸 수의 광맥 · 유적을 찾아 지도에 밝힙니다")]
    [Min(0)] public int scanRadius = 0;
    [Tooltip("스캐너 다시 쓰기까지 (초)")]
    [Min(0f)] public float scanCooldown = 6f;

    public bool IsDrill   => itemType == FarmingItemType.Tool && harvestRadius <= 0 && waterRadius <= 0 && scanRadius <= 0;
    public bool IsWaterCan => itemType == FarmingItemType.Tool && waterRadius > 0;
    public bool IsScanner  => itemType == FarmingItemType.Tool && scanRadius > 0;

    [Header("Equipment")]
    [Tooltip("장착 시 부여할 스탯 보너스 목록")]
    public EquipmentStatBonus[] equipBonuses;
}