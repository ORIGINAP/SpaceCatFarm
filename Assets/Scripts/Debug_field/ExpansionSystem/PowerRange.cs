using UnityEngine;

public static class PowerRange
{
    public static Vector2 Center(Vector2Int min, Vector2Int max)
        => new((min.x + max.x + 1) * 0.5f, (min.y + max.y + 1) * 0.5f);

    public static float Reach(Vector2Int min, Vector2Int max, int radius)
        => radius + Mathf.Max(max.x - min.x + 1, max.y - min.y + 1) * 0.5f;

    public static float Reach(int w, int h, float radius) => radius + Mathf.Max(w, h) * 0.5f;

    // 가까운 전원 고를 때만 씀
    public static float SqrDistance(Vector2Int srcMin, Vector2Int srcMax, Vector2Int min, Vector2Int max)
    {
        Vector2 c = Center(srcMin, srcMax);
        float dx = Mathf.Clamp(c.x, min.x + 0.5f, max.x + 0.5f) - c.x;
        float dy = Mathf.Clamp(c.y, min.y + 0.5f, max.y + 0.5f) - c.y;
        return dx * dx + dy * dy;
    }

    // 사각형 범위: 설비 둘레 radius칸
    public static bool Contains(Vector2Int srcMin, Vector2Int srcMax, int radius,
                                Vector2Int min, Vector2Int max, out float sqrDistance)
    {
        sqrDistance = SqrDistance(srcMin, srcMax, min, max);
        return GridManager.AreaDistance(srcMin, srcMax, min, max) <= radius;
    }
}
