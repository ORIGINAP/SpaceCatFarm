using System.Collections.Generic;
using UnityEngine;

// 화물 레일 기둥: 가까운 기둥 · 창고와 저절로 이어짐
public class CargoRail : TileItem
{
    public const float StorageAutoCells = 8f;
    private const float PreviewRefresh = 0.15f;
    private static readonly Color PreviewOn  = new(0.55f, 0.86f, 1f, 0.9f);
    private static readonly Color RangeColor = new(0.55f, 0.86f, 1f, 0.5f);

    private static readonly List<TileItem> nearby = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => nearby.Clear();

    public override string AbilityText =>
        $"{LinkRules.MaxDistanceCells:0}칸 안의 기둥, {StorageAutoCells:0}칸 안의 창고와 저절로 이어져요";

    private LineRenderer rangeOutline, previewLine;
    private SpriteRenderer ghostSprite;
    private float nextPreview;

    private void Start()
    {
        if (IsGhost)
        {
            rangeOutline = PowerRangeOverlay.CreateOutline(null, 1, 1, LinkRules.MaxDistanceCells, RangeColor);
            previewLine  = WorldConnectionLine.Create(Vector3.zero, Vector3.zero, PreviewOn, 0.04f, 6);
            ghostSprite  = GetComponent<SpriteRenderer>();
            return;
        }
        if (!NetSession.IsReplica && !SaveManager.IsLoading && !SaveManager.RecentlyLoaded)
        {
            AutoLink();
            if (Links.Count == 0) NotificationUI.Instance?.Show($"화물 레일: {LinkRules.MaxDistanceCells:0}칸 안에 이을 기둥이 없어요 — 더 가까이 세워 주세요");
        }
        CargoRails.MarkDirty();
    }

    private void LateUpdate()
    {
        if (rangeOutline == null) return;
        rangeOutline.transform.position = transform.position;
        bool show = ghostSprite != null && ghostSprite.enabled;
        rangeOutline.enabled = show;
        if (!show) { previewLine.enabled = false; return; }
        if (Time.unscaledTime < nextPreview) return;
        nextPreview = Time.unscaledTime + PreviewRefresh;
        var near = NearestPartner();
        previewLine.enabled = near != null;
        if (near == null) return;
        previewLine.SetPosition(0, CargoRails.LinePoint(this));
        previewLine.SetPosition(1, CargoRails.LinePoint(near));
    }

    private void OnDestroy()
    {
        if (rangeOutline != null) Destroy(rangeOutline.gameObject);
        if (previewLine != null) Destroy(previewLine.gameObject);
    }

    // 설치하면 이어질 가장 가까운 기둥 · 창고
    private TileItem NearestPartner()
    {
        var gm = GridManager.Instance;
        if (gm == null) return null;
        var c = gm.WorldToGrid(transform.position);
        TileItem best = null;
        float bestDist = float.MaxValue;
        gm.CollectTiles(TileType.CargoRail, nearby);
        foreach (var t in nearby)
        {
            if (t == this || t.IsGhost) continue;
            float d = (t.GridPosition - c).magnitude;
            if (d <= LinkRules.MaxDistanceCells && d < bestDist) { bestDist = d; best = t; }
        }
        gm.CollectTiles(TileType.Storage, nearby);
        foreach (var t in nearby)
        {
            if (t.IsGhost) continue;
            float d = (t.GridPosition - c).magnitude;
            if (d <= StorageAutoCells && d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    private void AutoLink()
    {
        var gm = GridManager.Instance;
        if (gm == null) return;

        gm.CollectTiles(TileType.CargoRail, nearby);
        nearby.Sort((a, b) => LinkRules.DistanceCells(this, a).CompareTo(LinkRules.DistanceCells(this, b)));
        foreach (var t in nearby)
        {
            if (t == this || t.IsGhost) continue;
            if (LinkRules.DistanceCells(this, t) > LinkRules.MaxDistanceCells) break;
            if (t is CargoRail other && !CargoRails.SameNetwork(this, other)) Link(this, other);
        }

        gm.CollectTiles(TileType.Storage, nearby);
        ItemStorage best = null;
        float bestDist = StorageAutoCells + 0.01f;
        foreach (var t in nearby)
        {
            if (t is not ItemStorage s || s.IsGhost) continue;
            float d = LinkRules.DistanceCells(this, s);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best != null) Link(this, best);
    }

    // 나중에 놓은 창고도 가까운 기둥과 이음
    public static void LinkNewStorage(ItemStorage storage)
    {
        var gm = GridManager.Instance;
        if (gm == null || storage == null || storage.IsGhost) return;
        gm.CollectTiles(TileType.CargoRail, nearby);
        CargoRail best = null;
        float bestDist = StorageAutoCells + 0.01f;
        foreach (var t in nearby)
        {
            if (t is not CargoRail r || r.IsGhost) continue;
            float d = LinkRules.DistanceCells(r, storage);
            if (d < bestDist) { bestDist = d; best = r; }
        }
        if (best != null) Link(best, storage);
    }
}
