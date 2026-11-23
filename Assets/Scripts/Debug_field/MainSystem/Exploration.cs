using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탐험 기록: 찾은 바이옴 · 연 유적 (세이브)
public static class Exploration
{
    private static int biomesFound;
    private static readonly HashSet<int> opened = new();

    public static event System.Action OnChanged;
    public static event System.Action<WorldTheme> OnBiomeFound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        biomesFound = 0;
        opened.Clear();
        OnChanged = null;
        OnBiomeFound = null;
    }

    public static int BiomeCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < WorldThemeConfig.Count; i++) if ((biomesFound & (1 << i)) != 0) n++;
            return n;
        }
    }

    public static int  OpenedCount => opened.Count;
    public static bool HasFound(WorldTheme b) => (biomesFound & (1 << (int)b)) != 0;
    public static bool IsOpened(int landmarkId) => opened.Contains(landmarkId);

    public static void Clear()
    {
        biomesFound = 0;
        opened.Clear();
        OnChanged?.Invoke();
    }

    public static bool MarkFound(WorldTheme b)
    {
        if (HasFound(b)) return false;
        biomesFound |= 1 << (int)b;
        OnChanged?.Invoke();
        OnBiomeFound?.Invoke(b);
        return true;
    }

    public static bool MarkOpened(int id)
    {
        if (!opened.Add(id)) return false;
        OnChanged?.Invoke();
        return true;
    }

    public static void CaptureTo(SaveData data)
    {
        data.biomesFound = biomesFound;
        data.landmarksOpened = new List<int>(opened);
    }

    public static void Restore(SaveData data)
    {
        biomesFound = data.biomesFound;
        opened.Clear();
        if (data.landmarksOpened != null) foreach (int id in data.landmarksOpened) opened.Add(id);
        OnChanged?.Invoke();
    }
}

// 플레이어가 선 바이옴을 보고 새 지역이면 큰 배너
public class BiomeTracker : MonoBehaviour
{
    private const float CheckEvery = 0.4f;
    private const long  FirstVisitCoins = 150;

    private WorldTheme? current;
    private float nextCheck;

    public static WorldTheme? Current => instance != null ? instance.current : null;
    private static BiomeTracker instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static void Ensure()
    {
        if (instance == null) instance = new GameObject("BiomeTracker").AddComponent<BiomeTracker>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + CheckEvery;
        var map = TerrainMap.Current;
        var player = PlayerTileController.Instance;
        var gm = GridManager.Instance;
        if (map == null || player == null || gm == null || SaveManager.IsLoading) return;

        var b = map.BiomeAt(gm.WorldToGrid(player.transform.position));
        if (current == b) return;
        bool first = current == null;
        current = b;
        bool isNew = Exploration.MarkFound(b);
        // 처음 들어온 순간에만 시작 지역 배너 생략
        if (first && b == map.StartBiome) return;
        if (isNew && b != map.StartBiome)
        {
            CurrencyManager.Instance?.Add(FirstVisitCoins);
            BiomeBanner.Show(b, "새 지역 발견!", WorldThemeConfig.Blurb(b) + $"\n탐사 보너스 +{FirstVisitCoins}코인");
            Sfx.Play(SfxId.CoreLevelUp);
        }
        else BiomeBanner.Show(b, null, WorldThemeConfig.Blurb(b));
    }
}

// 화면 위쪽 큰 지역 이름 배너
public class BiomeBanner : MonoBehaviour
{
    private const float Hold = 3.2f, FadeIn = 0.35f, FadeOut = 0.8f;

    private static BiomeBanner instance;
    private CanvasGroup group;
    private Image       band;
    private TextMeshProUGUI kicker, title, sub;
    private float shownAt = -100f;
    private float hold = Hold;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static void Show(WorldTheme biome, string kickerText, string subText)
        => Show(WorldThemeConfig.DisplayName(biome), WorldThemeConfig.Accent(biome), kickerText, subText);

    public static void Show(string titleText, Color accent, string kickerText, string subText, float hold = Hold)
    {
        if (instance == null) instance = Build();
        var b = instance;
        b.title.text  = titleText;
        b.kicker.text = kickerText ?? "";
        b.kicker.gameObject.SetActive(!string.IsNullOrEmpty(kickerText));
        b.sub.text    = subText ?? "";
        b.band.color  = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.6f, 0.82f);
        b.kicker.color = CuteUIKit.Lighter(accent, 0.55f);
        b.shownAt = Time.unscaledTime;
        b.hold    = hold;
    }

    private static BiomeBanner Build()
    {
        var go = new GameObject("BiomeBanner", typeof(RectTransform));
        UIStyleKit.CreateOverlayCanvas(go, 560);
        var bb = go.AddComponent<BiomeBanner>();
        bb.group = go.AddComponent<CanvasGroup>();
        bb.group.alpha = 0f;
        bb.group.blocksRaycasts = false;

        var bandGo = new GameObject("Band", typeof(RectTransform));
        bandGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)bandGo.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -140f);
        rt.sizeDelta = new Vector2(0f, 184f);
        bb.band = bandGo.AddComponent<Image>();
        bb.band.raycastTarget = false;

        bb.kicker = CuteUIKit.Text(bandGo.transform, "", 24, Color.white, FontStyles.Bold);
        Place(bb.kicker.rectTransform, 64f);
        bb.title = CuteUIKit.Text(bandGo.transform, "", 58, Color.white, FontStyles.Bold);
        Place(bb.title.rectTransform, 18f);
        CuteUIKit.Outline(bb.title, 0.18f, new Color32(40, 30, 50, 200));
        bb.sub = CuteUIKit.Text(bandGo.transform, "", 22, new Color(1f, 1f, 1f, 0.92f));
        bb.sub.textWrappingMode = TextWrappingModes.Normal;
        Place(bb.sub.rectTransform, -48f);
        return bb;
    }

    private static void Place(RectTransform rt, float y)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-80f, 60f);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        float t = Time.unscaledTime - shownAt;
        float a = t < FadeIn ? t / FadeIn : t < FadeIn + hold ? 1f : 1f - (t - FadeIn - hold) / FadeOut;
        group.alpha = Mathf.Clamp01(a);
    }
}
