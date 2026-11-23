using System.Collections.Generic;
using UnityEngine;

// 바이옴 탐사 유적 — 월드 시드로 놓이고, 연 것만 세이브
public class BiomeLandmark : MonoBehaviour
{
    public const float OpenRange = 2.6f;

    private static readonly List<BiomeLandmark> all = new();
    public static IReadOnlyList<BiomeLandmark> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        glowSprite = null;
    }

    public int        Id    { get; private set; }
    public WorldTheme Biome { get; private set; }
    public bool       Far   { get; private set; }
    public bool       IsOpened => Exploration.IsOpened(Id);

    private SpriteRenderer body, glow;
    private Sprite onSprite, offSprite;

    // 바이옴마다 (광물 · 개수) 두 줄 + 기록 한 줄
    private static (string item, int n)[] Loot(WorldTheme b) => b switch
    {
        WorldTheme.Tundra  => new[] { ("Item_Aluminum", 10), ("Item_Lithium", 6) },
        WorldTheme.Crystal => new[] { ("Item_Titanium", 6), ("Item_Diamond", 3) },
        WorldTheme.Marsh   => new[] { ("Item_Uranium", 5), ("Item_Seed_MilkShroom", 4) },
        _                  => new[] { ("Item_iron", 12), ("Item_Copper", 8) },
    };

    private static string Lore(WorldTheme b, bool far) => b switch
    {
        WorldTheme.Tundra  => far ? "얼음 속 관측 기록: \"이 별은 한때 푸르렀다\"" : "먼저 온 탐사선의 얼어붙은 캡슐",
        WorldTheme.Crystal => far ? "수정 신호기: 별빛을 모아 땅을 되살린 흔적" : "수정에 박힌 고대 에너지 셀",
        WorldTheme.Marsh   => far ? "늪 바닥의 정화 장치 설계 조각" : "이끼에 덮인 보급 캡슐",
        _                  => far ? "모래에 묻힌 옛 이주선 블랙박스" : "모래폭풍에 묻힌 탐사 캡슐",
    };

    public static void SpawnAll(TerrainMap map)
    {
        foreach (var l in all.ToArray()) if (l != null) Destroy(l.gameObject);
        all.Clear();
        var gm = GridManager.Instance;
        if (gm == null || map == null) return;
        foreach (var lm in map.Landmarks)
        {
            if (!gm.IsInBounds(lm.cell)) continue;
            var go = new GameObject($"Landmark_{lm.id}_{lm.biome}");
            go.transform.position = gm.GridToWorldCenter(lm.cell, 2, 2);
            var l = go.AddComponent<BiomeLandmark>();
            l.Id = lm.id;
            l.Biome = lm.biome;
            l.Far = lm.id % 2 == 1;
        }
    }

    private void Awake() => all.Add(this);

    private void Start()
    {
        onSprite  = Resources.Load<Sprite>("Landmark/landmark_on");
        offSprite = Resources.Load<Sprite>("Landmark/landmark_off");
        body = gameObject.AddComponent<SpriteRenderer>();
        body.sortingOrder = 0;
        if (LabArt.SpriteMaterial != null) body.sharedMaterial = LabArt.SpriteMaterial;
        var col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.5f, 1.5f);

        var g = new GameObject("Glow");
        g.transform.SetParent(transform, false);
        glow = g.AddComponent<SpriteRenderer>();
        glow.sprite = LandmarkGlow();
        glow.sortingOrder = -1;
        if (LabArt.SpriteMaterial != null) glow.sharedMaterial = LabArt.SpriteMaterial;
        glow.color = WorldThemeConfig.Accent(Biome);
        g.transform.localScale = Vector3.one * 2.4f;

        Exploration.OnChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        all.Remove(this);
        Exploration.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        if (body == null) return;
        body.sprite = IsOpened ? offSprite : onSprite;
        glow.enabled = !IsOpened;
    }

    private void Update()
    {
        if (glow == null || !glow.enabled) return;
        float k = 0.35f + 0.25f * Mathf.Sin(Time.time * 2.4f + Id);
        var c = glow.color;
        glow.color = new Color(c.r, c.g, c.b, k);
    }

    public static BiomeLandmark At(Vector2 worldPos)
    {
        foreach (var l in all)
            if (l != null && Mathf.Abs(l.transform.position.x - worldPos.x) < 0.8f && Mathf.Abs(l.transform.position.y - worldPos.y) < 0.8f)
                return l;
        return null;
    }

    public static BiomeLandmark Nearest(Vector2 from, bool unopenedOnly)
    {
        BiomeLandmark best = null;
        float bd = float.MaxValue;
        foreach (var l in all)
        {
            if (l == null || (unopenedOnly && l.IsOpened)) continue;
            float d = ((Vector2)l.transform.position - from).sqrMagnitude;
            if (d < bd) { bd = d; best = l; }
        }
        return best;
    }

    public bool TryOpen(Vector3 playerPos)
    {
        if (IsOpened) { NotificationUI.Instance?.Show("이미 조사한 유적이에요"); return false; }
        if (Vector2.Distance(playerPos, transform.position) > OpenRange)
        {
            NotificationUI.Instance?.Show("가까이 가서 조사해요", NotificationUI.WarningColor);
            return false;
        }
        Exploration.MarkOpened(Id);
        ExploredMap.Reveal(GridManager.Instance.WorldToGrid(transform.position), 18);

        long coins = Far ? 1200 : 500;
        CurrencyManager.Instance?.Add(coins);
        var names = new List<string>();
        int mult = Far ? 2 : 1;
        foreach (var (itemName, n) in Loot(Biome))
        {
            var item = Find(itemName);
            if (item == null) continue;
            Inventory.Instance?.AddItemOrDrop(item, n * mult, playerPos);
            names.Add($"{item.itemName} ×{n * mult}");
        }

        Sfx.PlayAt(SfxId.CoreLevelUp, transform.position);
        var accent = WorldThemeConfig.Accent(Biome);
        LabFx.Burst(transform.position + Vector3.up * 0.4f, accent, 26, 4f, 1f);
        LabFx.Ring(transform.position, CuteUIKit.Butter, 2.4f, 0.8f);
        BiomeBanner.Show("유적 조사 완료!", accent, Lore(Biome, Far),
            $"+{coins}코인 · {string.Join(" · ", names)}\n조사한 유적 {Exploration.OpenedCount}/{all.Count}");
        return true;
    }

    private static FarmingItemData Find(string assetName)
    {
        foreach (var i in GameDataRegistry.FindAll<FarmingItemData>())
            if (i != null && i.name == assetName) return i;
        return null;
    }

    private static Sprite glowSprite;

    private static Sprite LandmarkGlow()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
        }
        tex.Apply();
        return glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
