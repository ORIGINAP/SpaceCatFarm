using System.Collections.Generic;
using UnityEngine;

public class LinkLinesView : MonoBehaviour
{
    // 평소엔 땅색에 묻히게, 가리킨 설비 · 연결 중인 설비의 선만 또렷하게
    private static readonly Color ItemLinkColor  = new(0.48f, 0.35f, 0.31f, 0.26f);
    private static readonly Color ItemFocusColor = new(1f, 0.89f, 0.6f, 1f);
    private static readonly Color FuelLinkColor  = new(0.62f, 0.38f, 0.28f, 0.26f);
    private static readonly Color FuelFocusColor = new(1f, 0.72f, 0.52f, 1f);
    private static readonly Color RailColor      = new(0.42f, 0.36f, 0.46f, 0.55f);
    private static readonly Color RailFocusColor = new(0.72f, 0.89f, 1f, 1f);
    private const float RailWidth    = 0.022f;
    private const int   RailOrder    = 4;
    private const float LineWidth    = 0.02f;
    private const float FocusWidth   = 0.04f;
    private const int   SortingOrder = WorldConnectionLine.UnderSpritesOrder;

    private static LinkLinesView instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private readonly List<LineRenderer> pool = new();
    private readonly HashSet<TileItem> visited = new();
    private bool dirty = true;
    private TileItem focus;

    public static void Refresh()
    {
        if (instance == null)
            instance = new GameObject("LinkLinesView").AddComponent<LinkLinesView>();
        instance.dirty = true;
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void LateUpdate()
    {
        var f = LinkModeController.IsActive ? LinkModeController.Source : WorldHover.Tile;
        if (f != focus)
        {
            // 선이 있는 설비로 바뀔 때만 다시 그림
            if (HasLinks(f) || HasLinks(focus)) dirty = true;
            focus = f;
        }
        if (!dirty) return;
        dirty = false;
        Redraw();
    }

    private static bool HasLinks(TileItem t) => t != null && t.Links.Count > 0;

    private void Redraw()
    {
        int used = 0;
        visited.Clear();
        var gm = GridManager.Instance;
        if (gm != null)
        {
            // 발전소 연결은 파이프 쪽에서 그림
            foreach (var tile in gm.GetDynamicTiles())
            {
                if (tile == null) continue;
                visited.Add(tile);
                var links = tile.Links;
                for (int i = 0; i < links.Count; i++)
                {
                    var other = links[i];
                    if (other == null || visited.Contains(other)) continue;
                    bool lit  = focus != null && (tile == focus || other == focus);
                    if (tile is CargoRail || other is CargoRail)
                    {
                        Draw(used++, CargoRails.LinePoint(tile), CargoRails.LinePoint(other),
                             lit ? RailFocusColor : RailColor, lit ? FocusWidth : RailWidth, RailOrder);
                        continue;
                    }
                    bool fuel = tile is AutoFuelPipe || other is AutoFuelPipe || tile is Generator || other is Generator;
                    Color color = fuel ? (lit ? FuelFocusColor : FuelLinkColor) : (lit ? ItemFocusColor : ItemLinkColor);
                    Draw(used++, WorldConnectionLine.UnderSprites(tile.transform.position),
                                 WorldConnectionLine.UnderSprites(other.transform.position),
                                 color, lit ? FocusWidth : LineWidth, SortingOrder);
                }
            }
        }
        for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
    }

    private void Draw(int index, Vector3 from, Vector3 to, Color color, float width, int order)
    {
        LineRenderer line;
        if (index < pool.Count)
        {
            line = pool[index];
            line.enabled = true;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.sortingOrder = order;
        }
        else
        {
            line = WorldConnectionLine.Create(from, to, color, width, order, transform);
            pool.Add(line);
        }
    }
}
