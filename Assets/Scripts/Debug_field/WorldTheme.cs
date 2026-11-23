using UnityEngine;

// 바이옴 겸 시작 지역 — 세이브에 정수로 저장, 끝에만 추가
public enum WorldTheme { Wasteland, Tundra, Crystal, Marsh }

public static class WorldThemeConfig
{
    public const int Count = 4;

    public static string DisplayName(WorldTheme theme) => theme switch
    {
        WorldTheme.Tundra  => "혹한지",
        WorldTheme.Crystal => "수정 지대",
        WorldTheme.Marsh   => "이끼 습지",
        _                  => "황무지",
    };

    // 탐험 안내 한 줄
    public static string Blurb(WorldTheme theme) => theme switch
    {
        WorldTheme.Tundra  => "눈 덮인 얼음 벌판 — 알루미늄 · 리튬 광맥",
        WorldTheme.Crystal => "라일락 수정 들판 — 티타늄 · 다이아몬드 광맥, 별빛 토양",
        WorldTheme.Marsh   => "버섯 핀 이끼 늪 — 우라늄 광맥, 샘물 토양",
        _                  => "붉은 모래 언덕 — 철 · 구리 광맥, 불씨 토양",
    };

    public static string StormName(WorldTheme theme) => theme switch
    {
        WorldTheme.Tundra  => "눈보라",
        WorldTheme.Crystal => "수정 가루 폭풍",
        WorldTheme.Marsh   => "포자 폭풍",
        _                  => "모래폭풍",
    };

    public static string StormSubject(WorldTheme theme) => theme switch
    {
        WorldTheme.Tundra  => "눈보라가",
        WorldTheme.Crystal => "수정 가루 폭풍이",
        WorldTheme.Marsh   => "포자 폭풍이",
        _                  => "모래폭풍이",
    };

    public static string TerrainFolder(WorldTheme theme) => $"Terrain/{theme}";

    // 미니맵 · 안내 배너 색
    public static Color Accent(WorldTheme theme) => theme switch
    {
        WorldTheme.Tundra  => new Color(0.62f, 0.80f, 0.96f),
        WorldTheme.Crystal => new Color(0.78f, 0.62f, 0.96f),
        WorldTheme.Marsh   => new Color(0.48f, 0.80f, 0.56f),
        _                  => new Color(0.95f, 0.62f, 0.40f),
    };

    public static Color StormTint(WorldTheme theme) => theme switch
    {
        WorldTheme.Tundra  => new Color(0.56f, 0.66f, 0.82f, 0.42f),
        WorldTheme.Crystal => new Color(0.66f, 0.54f, 0.84f, 0.42f),
        WorldTheme.Marsh   => new Color(0.44f, 0.62f, 0.46f, 0.42f),
        _                  => new Color(0.7f, 0.43f, 0.25f, 0.42f),
    };

    public static Color StormParticle(WorldTheme theme) => theme switch
    {
        WorldTheme.Tundra  => new Color(1f, 1f, 1f, 0.95f),
        WorldTheme.Crystal => new Color(0.92f, 0.84f, 1f, 0.92f),
        WorldTheme.Marsh   => new Color(0.86f, 0.96f, 0.74f, 0.9f),
        _                  => new Color(1f, 0.88f, 0.66f, 0.9f),
    };

    // 특산 토양이 나는 바이옴
    public static WorldTheme HomeOf(SoilType soil) => soil switch
    {
        SoilType.Starlight => WorldTheme.Crystal,
        SoilType.Spring    => WorldTheme.Marsh,
        _                  => WorldTheme.Wasteland,
    };
}
