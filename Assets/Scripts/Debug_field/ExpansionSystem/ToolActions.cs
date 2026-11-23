using System.Collections.Generic;
using UnityEngine;

// 물뿌리개 · 탐사 스캐너 (드릴 · 낫 말고 손 도구)
public static class ToolActions
{
    private static readonly Dictionary<Vector2Int, float> wetUntil = new();
    private static float scanReadyAt, nextBusyHint;
    private static readonly List<TileItem> plots = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        wetUntil.Clear();
        scanReadyAt = nextBusyHint = 0f;
        plots.Clear();
    }

    public static bool HandWatered(Vector2Int cell) => wetUntil.TryGetValue(cell, out float t) && t > Time.time;

    public static float ScanCooldownLeft => Mathf.Max(0f, scanReadyAt - Time.time);

    // 밭 둘레에 물 — 한 칸이라도 줬으면 true
    public static bool Water(FarmingItemData can, Vector2Int at, Vector3 from)
    {
        var gm = GridManager.Instance;
        if (gm == null || !(gm.GetTile(at) is PlantTile)) return false;
        int r = can.waterRadius, n = 0;
        float until = Time.time + can.waterSeconds;
        for (int y = at.y - r; y <= at.y + r; y++)
        for (int x = at.x - r; x <= at.x + r; x++)
        {
            var c = new Vector2Int(x, y);
            if (!(gm.GetTile(c) is PlantTile plot)) continue;
            bool fresh = !HandWatered(c) || wetUntil[c] < until - 5f;
            wetUntil[c] = until;
            plot.RefreshNow();
            if (fresh) n++;
        }
        if (n == 0) return false;
        Vector3 p = gm.GridToWorld(at);
        LabFx.Burst(p, new Color(0.55f, 0.85f, 1f), 10 + n, 2.4f, 0.6f);
        LabFx.Ring(p, new Color(0.6f, 0.9f, 1f, 0.8f), 0.6f + r * 0.6f, 0.5f);
        Sfx.PlayAt(SfxId.Plant, p);
        GamepadRumble.Play(Haptic.Place);
        return true;
    }

    public static bool Scan(FarmingItemData scanner, Vector3 from)
    {
        if (Time.time < scanReadyAt)
        {
            if (Time.unscaledTime >= nextBusyHint)
            {
                nextBusyHint = Time.unscaledTime + 1.5f;
                NotificationUI.Instance?.Show($"스캐너 충전 중… {ScanCooldownLeft:0.#}초");
            }
            return false;
        }
        var gm = GridManager.Instance;
        if (gm == null) return false;
        scanReadyAt = Time.time + scanner.scanCooldown;
        int r = scanner.scanRadius;
        float cs = gm.CellSize;
        var center = gm.WorldToGrid(from);
        ExploredMap.Reveal(center, r);
        Purify.Wave(from, 0.5f, r * cs, 1.2f, new Color(1f, 0.85f, 0.45f, 0.9f));
        Sfx.PlayAt(SfxId.Boost, from);

        // 광맥: 종류별로 가장 가까운 덩어리에 핑
        var nearest = new Dictionary<ResourceData, ResourceBlock>();
        foreach (var b in ResourceBlock.Active)
        {
            if (b == null || b.Data == null) continue;
            Vector2 d = (Vector2)(b.transform.position - from);
            if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) > r * cs) continue;
            if (!nearest.TryGetValue(b.Data, out var best) ||
                d.sqrMagnitude < ((Vector2)(best.transform.position - from)).sqrMagnitude) nearest[b.Data] = b;
        }
        var names = new List<string>();
        foreach (var kv in nearest)
        {
            var p = kv.Value.transform.position;
            LabFx.Ring(p, new Color(1f, 0.85f, 0.45f, 0.9f), 1.2f, 1.2f);
            LabFx.FloatText(p + Vector3.up * 0.9f, kv.Key.resourceName, CuteUIKit.Butter, 3f);
            ExploredMap.Reveal(gm.WorldToGrid(p), 3);
            names.Add(kv.Key.resourceName);
        }

        // 가장 가까운 유적: 범위 2배 안이면 방향 · 거리
        string lm = "";
        var near = BiomeLandmark.Nearest(from, true);
        if (near != null)
        {
            Vector2 d = (Vector2)(near.transform.position - from);
            float cells = d.magnitude / cs;
            if (cells <= r * 2.2f)
            {
                ExploredMap.Reveal(gm.WorldToGrid(near.transform.position), 5);
                LabFx.FloatText(from + (Vector3)(d.normalized * 2.2f) + Vector3.up, $"{Compass(d)} 유적 {cells:0}칸", CuteUIKit.Strawberry, 3.6f);
                lm = $" · 유적 신호: {Compass(d)}쪽 {cells:0}칸 ({WorldThemeConfig.DisplayName(near.Biome)})";
            }
        }
        NotificationUI.Instance?.Show(names.Count > 0 ? $"탐사 완료: {string.Join(", ", names)}{lm}" : $"근처에 광맥이 없어요{lm}",
                                      NotificationUI.GoodNewsColor);
        return true;
    }

    public static string Compass(Vector2 d)
    {
        float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        string[] names = { "동", "북동", "북", "북서", "서", "남서", "남", "남동" };
        int i = Mathf.RoundToInt(Mathf.Repeat(a, 360f) / 45f) % 8;
        return names[i];
    }
}
