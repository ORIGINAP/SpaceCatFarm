using UnityEngine;

// 정화탑 — 둘레 사각형 땅을 서서히 정화 (전기 필요 없음, 오염된 땅에도 세울 수 있음)
public class Purifier : TileItem
{
    [Header("정화")]
    [Tooltip("설비 둘레 정화 반경 (칸)")]
    [SerializeField, Min(1)] private int   radius      = 7;
    [Tooltip("다 퍼지는 데 걸리는 시간 (초)")]
    [SerializeField, Min(0.1f)] private float growSeconds = 5f;
    [SerializeField] private Color rangeColor = new(0.59f, 0.93f, 1f, 0.85f);
    [Tooltip("빛 소용돌이 프레임")]
    [SerializeField] private Sprite[] frames;

    private float shown;
    private int   drawn = -1;
    private LineRenderer rangeRing;
    private SpriteRenderer sprite;
    private Vector3 baseScale;

    public int Radius      => radius;
    public int RadiusShown => Mathf.FloorToInt(shown);
    public bool IsDone     => shown >= radius;

    public override string AbilityText
    {
        get
        {
            var size = FootprintMax - FootprintMin + Vector2Int.one;
            return $"둘레 {radius}칸 ({size.x + radius * 2}×{size.y + radius * 2}칸)의 오염된 땅을 정화해요\n정화된 땅에만 밭 · 건물을 세울 수 있어요 (전기 필요 없음)";
        }
    }

    private void Start()
    {
        sprite = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        var gm = GridManager.Instance;
        int w = DropItemData != null ? DropItemData.tileWidth : 2;
        int h = DropItemData != null ? DropItemData.tileHeight : 2;
        float cs = gm != null ? gm.CellSize : 0.75f;
        rangeRing = WorldCircleOutline.CreateRect(transform, (w * 0.5f + radius) * cs, (h * 0.5f + radius) * cs,
                                                  IsGhost ? PowerRangeOverlay.PreviewColor : rangeColor, 0.05f, 46);
        if (IsGhost) return;

        bool instant = SaveManager.IsLoading || SaveManager.RecentlyLoaded || NetOps.Applying;
        shown = instant ? radius : 0f;
        Purify.Register(this);
        if (!instant)
        {
            Purify.Wave(transform.position, (w * 0.5f) * cs, (w * 0.5f + radius) * cs, growSeconds, rangeColor);
            LabFx.Burst(transform.position + Vector3.up * 0.3f, rangeColor, 18, 3f, 0.8f);
            Sfx.PlayAt(SfxId.Place, transform.position);
        }
    }

    private void OnDestroy()
    {
        if (!IsGhost) Purify.Unregister(this);
    }

    public void Snap()
    {
        shown = radius;
        drawn = radius;
    }

    private void Update()
    {
        if (rangeRing != null) rangeRing.enabled = IsGhost || WorldHover.ShowRange(this);
        if (IsGhost) return;

        if (frames != null && frames.Length > 1 && sprite != null)
            sprite.sprite = frames[(int)(Time.time * (shown < radius ? 8f : 2.5f)) % frames.Length];

        if (shown < radius)
        {
            shown = Mathf.Min(radius, shown + radius / growSeconds * Time.deltaTime);
            if (RadiusShown != drawn)
            {
                drawn = RadiusShown;
                Purify.MarkDirty();
            }
        }
        // 일하는 동안 통통
        float k = shown < radius ? 0.04f : 0.012f;
        float s = 1f + Mathf.Sin(Time.time * (shown < radius ? 9f : 2.2f)) * k;
        transform.localScale = new Vector3(baseScale.x * s, baseScale.y * (2f - s), baseScale.z);
        if (sprite != null && shown < radius)
            sprite.color = Color.Lerp(Color.white, new Color(0.85f, 1f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 12f));
        else if (sprite != null) sprite.color = Color.white;
    }
}
