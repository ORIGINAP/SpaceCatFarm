using UnityEngine;

// 커서(마우스 · 패드) 아래 설비 — 프레임마다 한 번만 찾음
public static class WorldHover
{
    private static int      frame = -1;
    private static TileItem tile;
    private static Camera   cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        frame = -1;
        tile  = null;
        cam   = null;
    }

    public static TileItem Tile
    {
        get
        {
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                tile  = Find();
            }
            return tile;
        }
    }

    // 범위 원: 가리키거나 같은 설비를 들고 있을 때만
    public static bool ShowRange(TileItem owner)
    {
        if (owner == null) return false;
        if (Tile == owner) return true;
        var held = PlayerTileController.Instance != null ? PlayerTileController.Instance.SelectedItem : null;
        return held != null && held.tilePrefab != null && held.tilePrefab.GetComponent(owner.GetType()) != null;
    }

    private static TileItem Find()
    {
        var gm = GridManager.Instance;
        if (gm == null || GameInput.PointerOverUI) return null;
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;

        Vector3 mp = InputManager.Instance != null ? InputManager.Instance.CursorScreenPos : (Vector3)GameInput.MousePosition;
        mp.z = -cam.transform.position.z;
        var t = gm.GetTile(gm.WorldToGrid(cam.ScreenToWorldPoint(mp)));
        return t != null && !t.IsGhost ? t : null;
    }
}
