using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class NotificationUI : MonoBehaviour
{
    public static NotificationUI Instance => Services.Get<NotificationUI>();

    [Header("채팅창")]
    [SerializeField] private int     maxLines    = 8;
    [SerializeField] private float   fontSize    = 14f;
    [SerializeField] private float   chatWidth   = 380f;
    [SerializeField] private Vector2 chatMargin  = new Vector2(20f, 20f);

    private static readonly Color SystemColor    = CuteUIKit.Cream;
    public static readonly Color WarningColor   = CuteUIKit.Hex(0xFF9AA8);
    public static readonly Color GoodNewsColor  = CuteUIKit.Butter;
    private static readonly Color TileColor      = CuteUIKit.Hex(0xBFE0FF);
    private static readonly Color SeedColor      = CuteUIKit.Hex(0xAEEBCB);
    private static readonly Color ResourceColor  = CuteUIKit.Hex(0xF2CFA6);
    private static readonly Color ToolColor      = CuteUIKit.Butter;
    private static readonly Color EquipmentColor = CuteUIKit.Hex(0xD6CBFF);
    private static readonly Color AdvancedColor  = CuteUIKit.Hex(0xFFA7BC);
    private static readonly Color OutlineColor   = new(0.12f, 0.08f, 0.07f, 0.85f);
    private static readonly Color BackdropColor  = new(0.09f, 0.06f, 0.06f, 0.85f);

    // 글자를 감싸는 판 여백
    private const float PlatePad = 10f;
    private const float FadePx   = 70f;

    private const float LineLife = 10f;
    private const float LineFade = 1.6f;

    private GameObject       chatPanel;
    private CanvasGroup      chatGroup;
    private TextMeshProUGUI  logText;
    private RectTransform    backdropRT;
    private float            textHeight;

    private struct Line
    {
        public string text;
        public Color  color;
        public float  time;
    }

    private readonly List<Line> lines = new();
    private readonly System.Text.StringBuilder sb = new();
    // 텍스트는 LateUpdate에서 한 번만
    private bool  linesDirty;
    private bool  shownAll;
    private float nextRedraw;

    private const float ItemMergeWindow = 1f;
    private FarmingItemData lastItem;
    private int             lastItemAmount;
    private float           lastItemTime;
    private string          lastItemText;

    private void Awake()
    {
        if (!Services.Register(this)) { Destroy(gameObject); return; }

        var selfRT = GetComponent<RectTransform>();
        if (selfRT == null) selfRT = gameObject.AddComponent<RectTransform>();
        selfRT.anchorMin = Vector2.zero;
        selfRT.anchorMax = Vector2.one;
        selfRT.offsetMin = selfRT.offsetMax = Vector2.zero;

        BuildChatPanel();
    }

    private void Start()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnItemAdded += HandleItemAdded;
    }

    private void OnDestroy()
    {
        Services.Unregister(this);
        if (Inventory.Instance != null)
            Inventory.Instance.OnItemAdded -= HandleItemAdded;
    }

    private void HandleItemAdded(FarmingItemData item, int amount)
    {
        if (Inventory.QuietAdd) return;
        var color = ColorFor(item);
        // 흔한 아이템은 로그에 안 쌓음
        if (IsRoutine(item))
        {
            FloatingNotification.Instance?.ShowItem(item, amount, color);
            return;
        }
        bool merge = item == lastItem && Time.unscaledTime - lastItemTime <= ItemMergeWindow
                  && lines.Count > 0 && lines[^1].text == lastItemText;
        if (merge) lines.RemoveAt(lines.Count - 1);
        lastItemAmount = merge ? lastItemAmount + amount : amount;
        lastItem       = item;
        lastItemTime   = Time.unscaledTime;

        lastItemText = $"{item.itemName} x{lastItemAmount} 획득!";
        AddLine(lastItemText, color);
        FloatingNotification.Instance?.ShowItem(item, amount, color);
    }

    private static bool IsRoutine(FarmingItemData item)
    {
        if (item == null || item.isGolden) return false;
        if (item.itemType == FarmingItemType.Seed) return true;
        if (item.itemType != FarmingItemType.Resource) return false;
        var cm = CraftingManager.Instance;
        if (cm != null)
            foreach (var r in cm.Recipes)
                if (r != null && r.outputItem == item && r.IsKitchenRecipe) return false;
        return true;
    }

    private void BuildChatPanel()
    {
        textHeight = maxLines * fontSize * 1.3f;

        chatPanel = new GameObject("ChatPanel");
        chatPanel.transform.SetParent(transform, false);
        var panelRT = chatPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = Vector2.zero;
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(chatMargin.x + chatWidth + 90f, chatMargin.y + textHeight + 50f);

        var backdrop = new GameObject("Backdrop", typeof(RectTransform));
        backdrop.transform.SetParent(chatPanel.transform, false);
        backdropRT = (RectTransform)backdrop.transform;
        backdropRT.anchorMin = backdropRT.anchorMax = backdropRT.pivot = Vector2.zero;
        backdropRT.anchoredPosition = chatMargin - Vector2.one * PlatePad;
        var bg = backdrop.AddComponent<Image>();
        bg.sprite        = CuteUIKit.Rounded(10);
        bg.type          = Image.Type.Sliced;
        bg.color         = BackdropColor;
        bg.raycastTarget = false;

        var mask = new GameObject("FadeMask", typeof(RectTransform));
        mask.transform.SetParent(chatPanel.transform, false);
        var maskRT = (RectTransform)mask.transform;
        maskRT.anchorMin = maskRT.anchorMax = maskRT.pivot = Vector2.zero;
        maskRT.anchoredPosition = new Vector2(chatMargin.x, chatMargin.y - FadePx);
        maskRT.sizeDelta        = new Vector2(chatWidth, textHeight + FadePx);
        mask.AddComponent<RectMask2D>().softness = new Vector2Int(0, Mathf.RoundToInt(FadePx));

        var txtGO = new GameObject("LogText", typeof(RectTransform));
        txtGO.transform.SetParent(mask.transform, false);
        var txtRT = (RectTransform)txtGO.transform;
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(0f, FadePx);
        txtRT.offsetMax = Vector2.zero;
        logText = txtGO.AddComponent<TextMeshProUGUI>();
        logText.text               = "";
        logText.fontSize           = fontSize;
        logText.color              = SystemColor;
        logText.alignment          = TextAlignmentOptions.BottomLeft;
        logText.textWrappingMode   = TextWrappingModes.Normal;
        logText.overflowMode       = TextOverflowModes.Overflow;
        logText.richText           = true;
        logText.raycastTarget      = false;
        logText.outlineWidth       = 0.18f;
        logText.outlineColor       = OutlineColor;

        chatGroup = chatPanel.AddComponent<CanvasGroup>();
        chatGroup.blocksRaycasts = false;
        chatGroup.interactable   = false;
        chatPanel.SetActive(false);
    }

    public void Show(string message, Color? color = null)
        => AddLine(message, color.HasValue ? Readable(color.Value) : DefaultColorFor(message));

    // 같은 알림 반복은 ×N으로
    private const float RepeatWindow = 6f;
    private string lastMessage;
    private int    lastRepeat;
    private float  lastMessageTime;

    private void AddLine(string message, Color color)
    {
        if (logText == null) return;

        float now = Time.unscaledTime;
        bool repeat = message == lastMessage && now - lastMessageTime <= RepeatWindow && lines.Count > 0;
        lastRepeat      = repeat ? lastRepeat + 1 : 1;
        lastMessage     = message;
        lastMessageTime = now;
        if (repeat)
        {
            lines[^1] = new Line { text = $"{message} (×{lastRepeat})", color = color, time = now };
            linesDirty = true;
            return;
        }
        lines.Add(new Line { text = message, color = color, time = now });
        if (lines.Count > maxLines) lines.RemoveRange(0, lines.Count - maxLines);

        linesDirty = true;
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        bool all = TextInputFocus.Active;
        if (!linesDirty && all == shownAll && now < nextRedraw) return;
        linesDirty = false;
        shownAll   = all;

        sb.Clear();
        float top = 0f;
        nextRedraw = float.MaxValue;
        foreach (var line in lines)
        {
            float age   = now - line.time;
            float alpha = all ? 1f : 1f - Mathf.Clamp01((age - LineLife) / LineFade);
            if (alpha <= 0f) continue;
            if (alpha < 1f) nextRedraw = now;
            else if (!all)  nextRedraw = Mathf.Min(nextRedraw, line.time + LineLife);
            top = Mathf.Max(top, alpha);

            var c = line.color;
            c.a *= alpha;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(c)).Append('>').Append(line.text).Append("</color>");
        }

        bool any = sb.Length > 0;
        if (any)
        {
            logText.text = sb.ToString();
            FitBackdrop();
        }
        chatGroup.alpha = top;
        if (chatPanel.activeSelf != any) chatPanel.SetActive(any);
    }

    // 판을 글자 크기에 맞춤
    private void FitBackdrop()
    {
        var size = logText.GetPreferredValues(logText.text, chatWidth, 0f);
        backdropRT.sizeDelta = new Vector2(Mathf.Min(size.x, chatWidth), Mathf.Min(size.y, textHeight)) + Vector2.one * PlatePad * 2f;
    }

    private Color ColorFor(FarmingItemData item)
    {
        if (IsAdvancedItem(item)) return AdvancedColor;

        return item.itemType switch
        {
            FarmingItemType.Tile      => TileColor,
            FarmingItemType.Seed      => SeedColor,
            FarmingItemType.Resource  => ResourceColor,
            FarmingItemType.Tool      => ToolColor,
            FarmingItemType.Equipment => EquipmentColor,
            _                         => SystemColor,
        };
    }

    private static Color DefaultColorFor(string message)
    {
        if (string.IsNullOrEmpty(message)) return SystemColor;
        return SystemColor;
    }

    private static Color Readable(Color c)
    {
        if (0.299f * c.r + 0.587f * c.g + 0.114f * c.b >= 0.5f) return c;

        Color.RGBToHSV(c, out float h, out float s, out _);
        if (s < 0.2f) return SystemColor;

        float deg = h * 360f;
        if (deg < 15f || deg >= 335f) return WarningColor;
        if (deg < 70f)                return GoodNewsColor;
        if (deg < 165f)               return SeedColor;
        if (deg < 255f)               return TileColor;
        if (deg < 300f)               return EquipmentColor;
        return AdvancedColor;
    }

    private static bool IsAdvancedItem(FarmingItemData item)
    {
        var cm = CraftingManager.Instance;
        if (cm == null) return false;

        foreach (var r in cm.Recipes)
            if (r != null && r.outputItem == item && r.tier == CraftingTier.Advanced) return true;

        return false;
    }
}
