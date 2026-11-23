using TMPro;
using UnityEngine;

// 월드 팝업 글자 뒤 어두운 받침판
public class TextPlate : MonoBehaviour
{
    private static readonly Color FillColor = new(0.09f, 0.06f, 0.06f, 0.93f);
    private const float RimAlpha = 0.95f;
    // 글자 크기 4 기준 여백
    private const float PadX = 0.26f, PadY = 0.12f, RimWidth = 0.05f;

    private TextMeshPro    text;
    private SpriteRenderer rim, fill;
    private Color          accent;
    private string         fitted;
    private float          fittedSize = -1f;

    public static void Attach(TextMeshPro t, Color accent)
    {
        if (t == null) return;
        var plate = new GameObject("Plate").AddComponent<TextPlate>();
        plate.transform.SetParent(t.transform, false);
        plate.text   = t;
        plate.accent = accent;
        plate.rim    = plate.Layer("Rim",  CuteUIKit.Rounded(14), t.sortingOrder - 2);
        plate.fill   = plate.Layer("Fill", CuteUIKit.Rounded(11), t.sortingOrder - 1);
        plate.LateUpdate();
    }

    // 어두운 판 위에서 읽히게 밝힘 (테두리는 0.6쯤)
    public static Color Light(Color c, float target = 0.8f)
    {
        float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        if (lum >= target) return c;
        var o = Color.Lerp(c, Color.white, (target - lum) / (1f - lum));
        o.a = c.a;
        return o;
    }

    private SpriteRenderer Layer(string objName, Sprite sprite, int order)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite         = sprite;
        sr.drawMode       = SpriteDrawMode.Sliced;
        sr.sharedMaterial = WorldConnectionLine.SharedMaterial;
        sr.sortingLayerID = text.sortingLayerID;
        sr.sortingOrder   = order;
        return sr;
    }

    private void LateUpdate()
    {
        if (text == null) { Destroy(gameObject); return; }
        if (text.text != fitted || text.fontSize != fittedSize) Fit();
        float a = text.enabled ? text.color.a : 0f;
        var r = accent;    r.a  = RimAlpha * a;
        var f = FillColor; f.a *= a;
        rim.color  = r;
        fill.color = f;
    }

    private void Fit()
    {
        fitted     = text.text;
        fittedSize = text.fontSize;
        text.ForceMeshUpdate();
        bool show = text.textInfo.characterCount > 0;
        rim.enabled = fill.enabled = show;
        if (!show) return;

        // 모서리도 글자 크기 따라
        float k = Mathf.Max(0.1f, text.fontSize / 4f);
        var b = text.textBounds;
        transform.localPosition = new Vector3(b.center.x, b.center.y, 0f);
        transform.localScale    = new Vector3(k, k, 1f);
        var size = new Vector2(b.size.x / k + PadX * 2f, b.size.y / k + PadY * 2f);
        rim.size  = size;
        fill.size = size - Vector2.one * RimWidth * 2f;
    }
}
