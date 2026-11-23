using System.Collections.Generic;
using UnityEngine;

// 마을 매력(장식)과 마을 축제
public class TownLife : MonoBehaviour
{
    public static TownLife Instance => Services.Get<TownLife>();

    // 매력이 오를수록 짐값 보너스는 완만하게 최대 30%
    public const float CharmMaxBonus = 0.30f;
    public const float CharmHalf     = 25f;

    public const int   FestivalLevel   = 2;
    public const int   FestivalMinutes = 240;
    public const float FestivalGrowth  = 1.25f;
    public const float FestivalWork    = 1.25f;
    public const float FestivalPay     = 0.30f;
    private static readonly long[] FestivalCosts = { 0, 0, 1000, 3000, 10000, 30000, 80000, 150000 };

    private const float FxInterval    = 1.6f;
    private const float CheerInterval = 0.9f;

    private static readonly Color[] Confetti =
    {
        new(1f, 0.62f, 0.72f), new(1f, 0.86f, 0.45f), new(0.55f, 0.85f, 0.7f), new(0.6f, 0.75f, 1f), new(0.8f, 0.7f, 1f),
    };

    private static GridManager charmGrid;
    private static int charmVersion = -1, charmCache;
    private static readonly List<TileItem> decor = new();

    public static event System.Action OnFestivalHeld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        charmGrid    = null;
        charmVersion = -1;
        charmCache   = 0;
        decor.Clear();
        OnFestivalHeld = null;
    }

    private int   festivalEnd = -1;
    private int   festivalDay = -1;
    private int   extraSeats;
    private bool  wasActive;
    private float nextFx, nextCheer;
    private float charmToastAt = -1f;

    // 이주 지원 좌석: 떠날 때마다 코인을 내고 고양이를 더 데려옴
    public const int SeatLevel = 3;
    public const int MaxSeats  = 3;
    private static readonly long[] SeatCosts = { 0, 0, 0, 1500, 4000, 12000, 30000, 60000 };

    public static bool SeatsUnlocked => CenterCore.CurrentLevel >= SeatLevel;
    public static int  ExtraSeats    => Instance != null ? Instance.extraSeats : 0;
    public static long SeatCost      => SeatCosts[Mathf.Clamp(CenterCore.CurrentLevel, 0, SeatCosts.Length - 1)];

    public void SetSeats(int n)
    {
        n = Mathf.Clamp(n, 0, MaxSeats);
        if (NetSession.IsReplica) { NetOps.Global(NetOpType.ShuttleSeats, null, n); return; }
        extraSeats = n;
    }

    // 좌석값을 냈으면 늘릴 마릿수
    public static int PaySeats(out long paid)
    {
        paid = 0;
        int n = SeatsUnlocked ? ExtraSeats : 0;
        if (n <= 0) return 0;
        long cost = SeatCost * n;
        var cm = CurrencyManager.Instance;
        if (cm == null || !cm.TrySpend(cost)) return 0;
        paid = cost;
        return n;
    }

    private void Awake()
    {
        if (!Services.Register(this)) Destroy(this);
    }

    private void OnDestroy() => Services.Unregister(this);

    private void OnEnable()  => TileItem.OnAnyTilePlaced += OnTilePlaced;
    private void OnDisable() => TileItem.OnAnyTilePlaced -= OnTilePlaced;

    // 장식을 놓는 순간 매력이 오른 걸 보여 줌
    private void OnTilePlaced(TileItem tile)
    {
        var item = tile != null && !tile.IsGhost ? tile.SourceItem : null;
        if (item == null || item.charm <= 0) return;
        var at = tile.transform.position;
        LabFx.Burst(at, Confetti[4], 10, 2.2f, 0.7f);
        LabFx.FloatText(at + Vector3.up * 0.9f, $"매력 ★+{item.charm}", Confetti[4], 3.6f);
        charmToastAt = Time.unscaledTime + 0.8f;
    }

    public static int Charm
    {
        get
        {
            var gm = GridManager.Instance;
            if (gm == null) return 0;
            if (gm == charmGrid && gm.Version == charmVersion) return charmCache;
            charmGrid    = gm;
            charmVersion = gm.Version;
            gm.CollectTiles(TileType.Decoration, decor);
            int sum = 0;
            foreach (var t in decor)
            {
                if (t == null || t.IsGhost) continue;
                var item = t.SourceItem != null ? t.SourceItem : t.DropItemData;
                if (item != null) sum += item.charm;
            }
            charmCache = sum;
            return sum;
        }
    }

    public static float CharmBonusFor(int charm) => charm <= 0 ? 0f : CharmMaxBonus * charm / (charm + CharmHalf);

    public static float CharmBonus => CharmBonusFor(Charm);

    public static int Percent(float bonus) => Mathf.RoundToInt(bonus * 100f);

    private static int Now   => GameTimeManager.Instance != null ? GameTimeManager.Instance.AbsoluteMinute : 0;
    private static int Today => GameTimeManager.Instance != null ? GameTimeManager.Instance.CurrentDay : 0;

    public static bool FestivalActive => Instance != null && Instance.festivalEnd > Now;

    public static float FestivalSecondsLeft
    {
        get
        {
            if (!FestivalActive) return 0f;
            var clock = GameTimeManager.Instance;
            return (Instance.festivalEnd - Now) * (clock != null ? clock.SecondsPerGameMinute : 2f);
        }
    }

    public static float GrowthMult => FestivalActive ? FestivalGrowth : 1f;
    public static float WorkMult   => FestivalActive ? FestivalWork : 1f;
    public static float PayBonus   => FestivalActive ? FestivalPay : 0f;

    public static bool Unlocked => CenterCore.CurrentLevel >= FestivalLevel;

    public static long FestivalCost => FestivalCosts[Mathf.Clamp(CenterCore.CurrentLevel, 0, FestivalCosts.Length - 1)];

    public static bool HeldToday => Instance != null && Instance.festivalDay == Today;

    // 퀘스트 전에 미리 열었어도 완료로
    public static bool EverHeld => Instance != null && Instance.festivalDay >= 0;

    public static string EffectText => $"작물 · 요리 · 제련 ×{FestivalGrowth:0.##} · 짐값 +{Percent(FestivalPay)}%";

    // 열 수 있으면 null
    public static string FestivalBlock()
    {
        if (Instance == null) return "";
        if (!Unlocked) return $"문명 Lv.{FestivalLevel}부터 열 수 있어요";
        if (FestivalActive) return "지금 축제 중이에요";
        if (HeldToday) return "축제는 하루 한 번이에요 — 내일 다시 열어요";
        var cm = CurrencyManager.Instance;
        if (cm == null || !cm.CanAfford(FestivalCost)) return $"코인이 {CurrencyManager.Format(FestivalCost)} 있어야 해요";
        return null;
    }

    public void Hold()
    {
        // 참가자는 호스트가 열어 줌
        if (NetSession.IsReplica) { NetOps.Global(NetOpType.Festival, null, 1); return; }
        if (FestivalBlock() != null) return;
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpend(FestivalCost)) return;
        festivalEnd = Now + FestivalMinutes;
        festivalDay = Today;
        Begin();
        OnFestivalHeld?.Invoke();
        NetHost.NoticeAll("마을 축제가 열렸어요! " + EffectText);
    }

    private void Begin()
    {
        wasActive = true;
        nextFx = nextCheer = 0f;
        Sfx.Play(SfxId.CoreLevelUp, 0.8f);
        BiomeBanner.Show("마을 축제!", CuteUIKit.Strawberry, "주민들이 센터코어에 모였어요", "게임 4시간 동안 " + EffectText);
        var core = CenterCore.Current;
        if (core != null)
            for (int i = 0; i < 4; i++)
                LabFx.Burst(core.transform.position + (Vector3)(Random.insideUnitCircle * 1.2f), Confetti[i % Confetti.Length], 14, 3f, 1f);
    }

    private void Update()
    {
        // 여러 개를 이어 놓으면 마지막에 한 번만 알림
        if (charmToastAt > 0f && Time.unscaledTime >= charmToastAt)
        {
            charmToastAt = -1f;
            NotificationUI.Instance?.Show($"마을 매력 ★{Charm} — 셔틀 짐값 +{Percent(CharmBonus)}%", NotificationUI.GoodNewsColor);
        }

        bool active = FestivalActive;
        if (active != wasActive)
        {
            wasActive = active;
            if (!active) NotificationUI.Instance?.Show("마을 축제가 끝났어요. 주민들이 무척 즐거웠대요!", NotificationUI.GoodNewsColor);
        }
        if (!active) return;

        var core = CenterCore.Current;
        if (core != null && Time.time >= nextFx)
        {
            nextFx = Time.time + FxInterval;
            var at = core.transform.position + (Vector3)(Random.insideUnitCircle * 2.4f);
            LabFx.Burst(at, Confetti[Random.Range(0, Confetti.Length)], 8, 2f, 0.9f);
        }
        if (Time.time >= nextCheer && ResidentCat.All.Count > 0)
        {
            nextCheer = Time.time + CheerInterval;
            ResidentCat.All[Random.Range(0, ResidentCat.All.Count)].Cheer();
        }
    }

    public void CaptureTo(SaveData data)
    {
        data.festivalEnd = festivalEnd;
        data.festivalDay = festivalDay;
        data.extraSeats  = extraSeats;
    }

    public void Restore(SaveData data)
    {
        festivalEnd = data.festivalEnd;
        festivalDay = data.festivalDay;
        extraSeats  = Mathf.Clamp(data.extraSeats, 0, MaxSeats);
        wasActive   = FestivalActive;
    }

    public void SyncFromHost(int end, int day, int seats)
    {
        bool was = FestivalActive;
        festivalEnd = end;
        festivalDay = day;
        extraSeats  = seats;
        if (!was && FestivalActive) Begin();
    }

    public static int SyncEnd => Instance != null ? Instance.festivalEnd : -1;
    public static int SyncDay => Instance != null ? Instance.festivalDay : -1;
}
