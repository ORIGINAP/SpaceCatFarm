using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class FloatingNotification : MonoBehaviour
{
    public static FloatingNotification Instance => Services.Get<FloatingNotification>();

    [Header("설정")]
    [SerializeField] private float displayTime  = 1.8f;
    [SerializeField] private float spawnOffsetY = 1.2f;
    [SerializeField] private float labelScale   = 0.008f;

    private const int   MaxLines    = 4;
    // 같은 아이템은 한 줄로 합침
    private const float MergeWindow = 1.0f;
    private const float FontSize    = 30f;
    private const float LineGap     = 38f;
    private const float GlowPadX    = 16f;
    private const float IconSize    = 32f;
    private const float IconGap     = 5f;
    private const float GlowAlpha   = 0.92f;
    private const float RimAlpha    = 0.9f;
    private const float RimWidth    = 3f;
    private const float SlideSpeed  = 14f;
    private const float FadeInTime  = 0.12f;
    private const float FadeOutFrom = 0.65f;
    private static readonly float[] StackAlpha = { 1f, 0.8f, 0.55f, 0.3f };
    private static readonly Color OutlineColor = new(0.12f, 0.08f, 0.07f, 1f);
    private static readonly Color GlowColor    = new(0.09f, 0.06f, 0.06f, 1f);

    private sealed class Line
    {
        public RectTransform   rect;
        public TextMeshProUGUI text;
        public Image           rim;
        public Image           glow;
        public Image           icon;
        public Color           accent;
        public FarmingItemData item;
        public int             amount;
        public float           born;
        public float           y;
    }

    private readonly List<Line> lines = new();
    private Transform     playerTransform;
    private RectTransform root;

    private void Awake()
    {
        if (!Services.Register(this)) { Destroy(gameObject); return; }
    }

    private void OnDestroy()
    {
        Services.Unregister(this);
        if (root != null) Destroy(root.gameObject);
    }

    private void Start()
    {
        var p = Player.Local;
        if (p != null) playerTransform = p.transform;
    }

    public void ShowItem(FarmingItemData item, int amount, Color color)
    {
        if (item == null || playerTransform == null) return;
        EnsureRoot();

        foreach (var l in lines)
        {
            if (l.item != item || Time.time - l.born > MergeWindow) continue;
            l.amount += amount;
            l.born    = Time.time;
            SetText(l, Label(item, l.amount));
            return;
        }

        var line = NewLine(color);
        line.icon.sprite  = item.icon;
        line.icon.enabled = item.icon != null;
        line.item   = item;
        line.amount = amount;
        line.born   = Time.time;
        line.y      = -LineGap * 0.5f;
        SetText(line, Label(item, amount));
        lines.Insert(0, line);
        while (lines.Count > MaxLines)
        {
            Destroy(lines[^1].rect.gameObject);
            lines.RemoveAt(lines.Count - 1);
        }
    }

    private static string Label(FarmingItemData item, int amount) => $"+{amount} {item.itemName}";

    private static void SetText(Line l, string message)
    {
        l.text.text = message;
        float icon = l.icon.enabled ? IconSize + IconGap : 0f;
        l.text.rectTransform.offsetMin = new Vector2(GlowPadX + icon, 0f);
        l.text.rectTransform.offsetMax = new Vector2(-GlowPadX, 0f);
        float w = l.text.GetPreferredValues(message).x + icon + GlowPadX * 2f;
        l.rect.sizeDelta = new Vector2(w, LineGap);
    }

    private void LateUpdate()
    {
        if (root == null) return;
        if (playerTransform != null)
            root.position = playerTransform.position + Vector3.up * spawnOffsetY;

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (Time.time - lines[i].born < displayTime) continue;
            Destroy(lines[i].rect.gameObject);
            lines.RemoveAt(i);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            float t = (Time.time - l.born) / displayTime;
            float fade = t < FadeInTime / displayTime ? t * displayTime / FadeInTime
                       : t > FadeOutFrom ? 1f - (t - FadeOutFrom) / (1f - FadeOutFrom)
                       : 1f;
            float a = Mathf.Clamp01(fade) * StackAlpha[Mathf.Min(i, StackAlpha.Length - 1)];
            l.text.alpha = a;
            l.icon.color = new Color(1f, 1f, 1f, a);
            var g = GlowColor; g.a = GlowAlpha * a;
            l.glow.color = g;
            var r = l.accent; r.a = RimAlpha * a;
            l.rim.color = r;

            l.y = Mathf.Lerp(l.y, i * LineGap, 1f - Mathf.Exp(-SlideSpeed * Time.deltaTime));
            l.rect.anchoredPosition = new Vector2(0f, l.y);
        }
    }

    private void EnsureRoot()
    {
        if (root != null) return;
        var go = new GameObject("HeadNotifications", typeof(RectTransform));
        root = (RectTransform)go.transform;
        root.localScale = Vector3.one * labelScale;
        root.sizeDelta  = new Vector2(400f, LineGap);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;
        canvas.worldCamera  = Camera.main;
    }

    private Line NewLine(Color color)
    {
        var go = new GameObject("Line", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);

        // 글자색 테두리 + 어두운 판
        var rim  = PlateLayer(rt, "Rim", 14, 0f);
        var glow = PlateLayer(rt, "Glow", 11, RimWidth);

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(rt, false);
        var txtRT = (RectTransform)txtGO.transform;
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize         = FontSize;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget    = false;
        // 머티리얼 사본 누수 방지
        CuteUIKit.Outline(tmp, 0.25f, OutlineColor);
        tmp.alpha            = 0f;

        var iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(rt, false);
        var iconRT = (RectTransform)iconGO.transform;
        iconRT.anchorMin = iconRT.anchorMax = iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.sizeDelta        = new Vector2(IconSize, IconSize);
        iconRT.anchoredPosition = new Vector2(GlowPadX, 0f);
        var icon = iconGO.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        icon.color          = new Color(1f, 1f, 1f, 0f);

        return new Line { rect = rt, text = tmp, rim = rim, glow = glow, icon = icon, accent = color };
    }

    private static Image PlateLayer(RectTransform parent, string objName, int radius, float inset)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        var img = go.AddComponent<Image>();
        img.sprite        = CuteUIKit.Rounded(radius);
        img.type          = Image.Type.Sliced;
        img.color         = Color.clear;
        img.raycastTarget = false;
        return img;
    }
}
