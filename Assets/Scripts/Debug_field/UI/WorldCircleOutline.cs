using UnityEngine;

// 범위 테두리 — 범위는 전부 사각형
public static class WorldCircleOutline
{
    public static LineRenderer Create(Transform parent, float radius, Color color,
        float width = 0.04f, int segments = 64, int sortingOrder = 50)
        => CreateRect(parent, radius, radius, color, width, sortingOrder);

    public static LineRenderer CreateRect(Transform parent, float halfW, float halfH, Color color,
        float width = 0.04f, int sortingOrder = 50)
    {
        var go = new GameObject("RangeOutline");
        go.transform.SetParent(parent, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace  = false;
        line.loop           = true;
        line.startWidth     = width;
        line.endWidth       = width;
        line.sharedMaterial = WorldConnectionLine.SharedMaterial;
        line.startColor     = color;
        line.endColor       = color;
        line.numCornerVertices = 2;
        line.sortingOrder   = sortingOrder;
        SetRect(line, Vector3.zero, halfW, halfH);
        return line;
    }

    public static void SetRect(LineRenderer line, Vector3 center, float halfW, float halfH)
    {
        line.positionCount = 4;
        line.SetPosition(0, center + new Vector3(-halfW, -halfH));
        line.SetPosition(1, center + new Vector3(-halfW,  halfH));
        line.SetPosition(2, center + new Vector3( halfW,  halfH));
        line.SetPosition(3, center + new Vector3( halfW, -halfH));
    }
}
