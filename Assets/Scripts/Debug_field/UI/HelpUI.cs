using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class HelpUI : MonoBehaviour
{
    [Header("폰트")]
    [SerializeField] private TMP_FontAsset fontAsset;

    private const float WinW = 960f, WinH = 600f, ListW = 228f, RowH = 36f;
    private const float TermW = 116f, TouchTermW = 140f, KeyW = 96f;
    private const float BodySize = 16f, TitleSize = 24f;

    private readonly struct Topic
    {
        public readonly string name, icon, lead;
        public readonly Color tint;
        public readonly (string term, string text)[] rows;

        public Topic(string name, string icon, Color tint, string lead, params (string term, string text)[] rows)
        {
            this.name = name;
            this.icon = icon;
            this.tint = tint;
            this.lead = lead;
            this.rows = rows;
        }
    }

    private const int ControlsPage = 1;

    // 한 쪽에 한 주제, 한 줄에 한 문장 (수치는 각 창 · 설명에서)
    private static readonly Topic[] Topics =
    {
        new("시작하기", "flag", CuteUIKit.ButterSoft, "불모지를 일궈 고향별 고양이들을 데려오는 게임이에요.",
            ("목표", "센터코어를 문명 Lv.7 새 고향까지 키우면 엔딩이에요."),
            ("할 일", "왼쪽 위 퀘스트 패널이 지금 할 일을 알려 줘요."),
            ("화살표", "어디로 가서 무엇을 누를지 화살표가 짚어 줘요."),
            ("여정", "문명 단계와 앞으로 열릴 것을 한눈에 봐요."),
            ("코코", "막히면 AI 조수 코코에게 물어보세요.")),

        new("조작", "paw", CuteUIKit.PinkSoft, ""),

        new("농사", "PlantTile", CuteUIKit.MintTag, "밭에 씨앗을 심고 거두는 게 기본이에요.",
            ("밭", "전기가 닿는 곳에 놓아요."),
            ("심기", "씨앗을 든 채 빈 밭을 눌러요."),
            ("수확", "다 자란 밭을 누르면 작물과 씨앗이 나와요."),
            ("물", "급수탑 곁이나 별비가 오는 밭은 더 빨리 자라요."),
            ("퇴비", "들고 밭을 누르면 더 많이 열려요."),
            ("작물 성향", "작물마다 잘 자라는 환경이 달라요. 씨앗 설명에서 봐요.")),

        new("요리 · 퇴비", "Item_CatKitchen", CuteUIKit.PinkSoft, "작물을 요리하면 더 비싸게 팔려요.",
            ("고양이 주방", "전기 범위 안에 세우고 메뉴를 고른 뒤 재료를 넣어요."),
            ("다 된 요리", "주방을 누르면 받아요. 셔틀 짐으로도 인기예요."),
            ("창고 연결", "재료를 알아서 가져와 요리하고 창고에 넣어 줘요."),
            ("퇴비통", "남는 씨앗과 작물을 넣으면 퇴비가 돼요.")),

        new("전기", "Item_PowerRelay", CuteUIKit.SkyTag, "전기는 맵 한가운데 센터코어에서 나와요.",
            ("센터코어", "연료 없이 주변에 전기를 보내요."),
            ("전력 중계탑", "전기 범위 안에 세우면 범위가 더 넓어져요."),
            ("태양광 패널", "낮에만 가까운 설비에 전기를 줘요."),
            ("필요한 것", "밭 · 제련소 · 주방 · 문명 시설"),
            ("필요 없는 것", "채굴기 · 파종기 · 수확기 · 화물 레일"),
            ("범위 보기", "전기를 쓰는 설비를 들면 범위가 테두리로 보여요.")),

        new("자동화", "AutoHarvester", CuteUIKit.LavenderTag, "설비를 세우고 이어 주면 농장이 알아서 돌아가요.",
            ("자동파종기", "씨앗을 넣어 두면 주변 빈 밭에 심어요."),
            ("자동수확기", "주변의 다 자란 밭을 거둬요."),
            ("자동채굴기", "광석 옆에 세우면 알아서 캐요."),
            ("연결하기", "설비를 눌러 창 맨 아래 [연결하기]를 쓰세요."),
            ("창고", "설비와 이으면 결과물이 창고로 들어가요. 가득 차면 설비가 쉬어요."),
            ("작물 판매기", "이어진 창고의 작물과 요리를 팔아 줘요.")),

        new("채굴 · 제련", "Item_Tier1_Drill", CuteUIKit.LatteLight, "광석을 캐서 주괴로 녹여 재료로 써요.",
            ("캐기", "드릴을 들고 광석을 누르고 있으면 캐져요."),
            ("더 좋은 드릴", "단단한 광석은 더 높은 등급의 드릴이 필요해요."),
            ("제련소", "광석을 든 채 누르면 주괴로 녹여요."),
            ("합금", "제련소 곁에서 제작창 재료 탭으로 만들어요."),
            ("광석 값", "광석은 코인이 되지 않아요. 돈은 농사 · 요리 · 셔틀로 벌어요.")),

        new("제작 · 연구 · 상점", "hammer", CuteUIKit.ButterSoft, "필요한 창은 오른쪽 아래 버튼으로 열어요.",
            ("제작", "민트색 칸은 지금 바로 만들 수 있어요."),
            ("연구", "마을 전체가 좋아져요. 문명이 오르면 더 열려요."),
            ("상점", "씨앗 · 장식 · 재료를 사고 작물과 요리를 팔아요."),
            ("되찾기", "잘못 팔았으면 상점에서 되찾을 수 있어요."),
            ("장비", "가방 위쪽 장비칸에 끼우면 능력이 올라요.")),

        new("셔틀 · 주민", "rocket", CuteUIKit.SkyTag, "짐을 보내면 고향별 고양이 가족이 타고 와요.",
            ("짐 싣기", "착륙장의 셔틀을 눌러 말풍선의 짐을 실어요."),
            ("짐값", "다 실으면 코인을 받고 셔틀이 떠나요."),
            ("주민", "돌아온 셔틀의 고양이 가족은 마을 주민이 돼요."),
            ("어울리기", "말을 걸거나 작물 · 요리를 선물하면 친해져요."),
            ("주민 캠프", "고양이들이 쉬는 곳이에요. 제작창 문명 탭에 있어요."),
            ("창고 연결", "착륙장과 창고를 이으면 짐을 알아서 실어요.")),

        new("코인 · 마을", "Item_CatStatue", CuteUIKit.PinkSoft, "번 코인을 마을에 쓰면 마을이 커지고 다시 코인이 돌아와요.",
            ("버는 곳", "셔틀 짐값과 작물 · 요리 판매로 벌어요."),
            ("꾸미기 가게", "장식을 놓으면 마을 매력이 올라 셔틀 짐값이 늘어요."),
            ("마을 축제", "셔틀 창에서 하루 한 번 열어요. 한동안 작물 · 요리가 빨라지고 짐값이 올라요."),
            ("고양이 공방", "주괴 · 합금을 코인으로 사서 캐고 녹이는 시간을 아껴요."),
            ("이주 지원", "Lv.3부터 셔틀 좌석을 코인으로 늘려 고양이를 더 데려와요."),
            ("발전 자금", "문명 단계가 오를 때도 코인이 들어요.")),

        new("문명 발전", "core", CuteUIKit.LavenderTag, "조건을 채우면 센터코어가 저절로 자라요.",
            ("조건 보기", "화면 위 가운데 문명 알림판을 눌러 보세요."),
            ("문명 시설", "제작창 문명 탭에서 만들고 전기를 넣어 주세요."),
            ("발전 자금", "단계마다 코인도 모아야 해요."),
            ("새로 열려요", "단계가 오르면 새 씨앗 · 제작 · 연구가 열려요.")),

        new("날씨", "rain", CuteUIKit.SkyTag, "시계 아래에서 다음 날씨를 미리 볼 수 있어요.",
            ("별비", "모든 밭에 물이 들어 더 빨리 자라요."),
            ("오로라", "밤에 오면 황금 작물이 잘 나와요."),
            ("유성우", "밤에 오면 곁에 희귀 광석이 떨어져요."),
            ("폭풍", "방어막 · 온실 밖의 작물이 날아가요. 미리 거두세요."),
            ("낮과 밤", "밤에 잘 자라는 작물도, 낮에 잘 자라는 작물도 있어요.")),

        new("전초기지", "Item_CargoRail", CuteUIKit.MintTag, "본진에서 멀리 나가면 더 넓게 농사지을 수 있어요.",
            ("특산 토양", "맞는 작물을 심으면 더 빨리, 더 많이 열려요."),
            ("화물 레일", "기둥을 이어 세우면 창고의 물건을 본진으로 날라요."),
            ("전초기지", "본진에서 먼 창고 · 설비 · 레일을 갖추면 전초기지가 돼요."),
            ("문명 조건", "Lv.5부터 전초기지가 조건에 들어가요.")),

        new("코코 · 저장", "cat", CuteUIKit.PinkSoft, "AI 조수 코코와 저장, 옵션이에요.",
            ("코코", "말로 부탁하면 직접 일해요. \"철 광석 10개 캐줘\""),
            ("외부 서버", "대화는 외부 AI 서버를 거쳐요. 옵션에서 끌 수 있어요."),
            ("자동 저장", "60초마다 저장돼요. 월드는 3개까지 만들 수 있어요."),
            ("옵션", "소리와 화면 설정을 바꿀 수 있어요.")),
    };

    private static readonly (string keys, string text)[] KeysWork =
    {
        ("WASD", "이동"), ("좌클릭", "설치 · 심기 · 캐기 · 수확"), ("우클릭 꾹", "해체"), ("F", "넣기 · 수확 · 이야기"),
        ("1~7", "핫바 고르기"), ("Q|E|휠", "핫바 넘기기"), ("Ctrl+휠", "확대 · 축소"), ("G", "격자"), ("Esc", "창 닫기 · 빈손 · 메뉴"),
    };

    private static readonly (string keys, string text)[] KeysWindows =
    {
        ("Tab", "가방 · 장비"), ("C", "제작"), ("B", "상점"), ("T", "연구"), ("J", "이주 셔틀"),
        ("L", "여정"), ("M", "큰 지도"), ("K", "코코와 대화"), ("Enter", "채팅"), ("P", "메뉴"),
    };

    private static readonly (PadGlyph[] pad, string text)[] PadWork =
    {
        (new[] { PadGlyph.LStick }, "이동"), (new[] { PadGlyph.RStick }, "조준"), (new[] { PadGlyph.RT }, "쓰기 · 꾹 반복"),
        (new[] { PadGlyph.LT }, "꾹: 해체"), (new[] { PadGlyph.A }, "열기 · 넣기 · 이야기"), (new[] { PadGlyph.B }, "빈손 · 창 닫기"),
        (new[] { PadGlyph.LB, PadGlyph.RB }, "핫바"), (new[] { PadGlyph.L3 }, "격자"), (new[] { PadGlyph.R3 }, "확대 · 축소"),
    };

    private static readonly (PadGlyph[] pad, string text)[] PadWindows =
    {
        (new[] { PadGlyph.Y }, "가방"), (new[] { PadGlyph.Up }, "제작"), (new[] { PadGlyph.Right }, "상점"), (new[] { PadGlyph.Left }, "연구"),
        (new[] { PadGlyph.X }, "셔틀 · 꾹: 코코"), (new[] { PadGlyph.Down }, "자세히 · 꾹: 여정"),
        (new[] { PadGlyph.Back }, "지도"), (new[] { PadGlyph.Start }, "메뉴"),
    };

    private static readonly (string term, string text)[] TouchRows =
    {
        ("왼쪽 아래 끌기", "걷기"),
        ("땅 · 광석 · 밭", "누르면 설치 · 심기 · 캐기 · 수확"),
        ("설비 누르기", "그 설비의 창이 열려요."),
        ("고양이 누르기", "이야기 · 든 것 선물"),
        ("해체 버튼", "켜고 설치물을 꾹 누르면 해체"),
        ("핫바 칸", "누르면 고르기, 한 번 더 누르면 빈손"),
        ("두 손가락", "벌리면 확대, 오므리면 축소"),
        ("오른쪽 아래 버튼", "가방 · 제작 · 상점 · 연구 · 메뉴"),
        ("뒤로 가기", "창 닫기 → 빈손 → 메뉴"),
    };

    private enum Scheme { Keyboard, Pad, Touch }

    private static Scheme CurrentScheme => Pad.Active ? Scheme.Pad : MobileMode.Active ? Scheme.Touch : Scheme.Keyboard;

    private RectTransform panelRT;
    private CanvasGroup   panelCG;
    private RectTransform pageRT;
    private bool isPanelOpen = false;
    private Coroutine animCo;
    private Transform toggleButton;

    private Button[] topicButtons;
    private Sprite[] topicIcons;
    private int      shown;
    private Scheme   builtScheme;

    private static HelpUI current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    public static bool AnyOpen => current != null && current.isPanelOpen;

    public static Transform PanelRoot => current != null ? current.panelRT : null;

    public static Transform ToggleButton => current != null ? current.toggleButton : null;

    private void Awake()
    {
        current = this;

        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var selfRT = GetComponent<RectTransform>();
        if (selfRT == null) selfRT = gameObject.AddComponent<RectTransform>();

        selfRT.anchorMin = Vector2.zero;
        selfRT.anchorMax = Vector2.one;
        selfRT.offsetMin = selfRT.offsetMax = Vector2.zero;
        selfRT.pivot = new Vector2(0.5f, 0.5f);

        BuildUI();

        panelRT.localScale = Vector3.zero;
        panelCG.alpha = 0f;
        UIStyleKit.SetScrolling(panelRT, false);
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    private void Update()
    {
        if (!isPanelOpen) return;
        if (EscapeRouter.TryConsume(EscapeRouter.CancelPressed)) { TogglePanel(); return; }
        // 쓰는 입력장치에 맞춰 조작법 교체
        if (shown == ControlsPage && builtScheme != CurrentScheme) BuildPage();
    }

    private void BuildUI()
    {
        BuildToggleButton();

        var panelGO = new GameObject("HelpPanel", typeof(RectTransform));
        panelGO.transform.SetParent(transform, false);
        panelRT = (RectTransform)panelGO.transform;
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = new Vector2(0f, -12f);
        panelRT.sizeDelta = new Vector2(WinW, WinH);
        panelGO.AddComponent<Image>();
        CuteUIKit.DecorateWindow(panelRT, catEars: true, earInset: 140f);
        panelCG = panelGO.AddComponent<CanvasGroup>();
        panelCG.blocksRaycasts = false;
        panelCG.interactable   = false;

        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(18, 18, 14, 16);
        vlg.spacing                = 10f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        BuildTitleBar();

        var body = Group<HorizontalLayoutGroup>(panelRT, "Body", 14f, TextAnchor.UpperLeft);
        body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        ((HorizontalLayoutGroup)body.GetComponent<LayoutGroup>()).childForceExpandHeight = true;
        BuildTopicList(body);
        BuildPagePane(body);

        BuildPage();
        PaintTopics();

        if (DevPanelUI.Enabled) DevPanelUI.Create(transform, fontAsset);
    }

    private void BuildTitleBar()
    {
        var bar = new GameObject("TitleBar", typeof(RectTransform));
        bar.transform.SetParent(panelRT, false);
        bar.AddComponent<LayoutElement>().preferredHeight = 40f;

        var ribbon = CuteUIKit.TitleRibbon(bar.transform, "도움말", null, 18f);
        ribbon.anchorMin = ribbon.anchorMax = ribbon.pivot = new Vector2(0.5f, 0.5f);
        ribbon.anchoredPosition = Vector2.zero;
        var fit = ribbon.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var close = (RectTransform)CuteUIKit.CloseButton(bar.transform, TogglePanel, 28f).transform;
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 0.5f);
        close.anchoredPosition = Vector2.zero;
        close.sizeDelta = new Vector2(28f, 28f);
    }

    private static RectTransform Pane(Transform parent, string name)
    {
        var pane = new GameObject(name, typeof(RectTransform));
        pane.transform.SetParent(parent, false);
        CuteUIKit.Layer(pane.transform, "BoardEdge", CuteUIKit.Rounded(16), CuteUIKit.SlotBorder);
        CuteUIKit.Layer(pane.transform, "BoardFill", CuteUIKit.Rounded(13), CuteUIKit.Lighter(CuteUIKit.Cream, 0.5f), 3f);
        return (RectTransform)pane.transform;
    }

    private void BuildTopicList(Transform body)
    {
        var pane = Pane(body, "TopicList");
        var le = pane.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = ListW;
        le.flexibleWidth = 0f;

        var vlg = pane.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(8, 8, 8, 8);
        vlg.spacing                = 2f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var items = GameDataRegistry.FindAll<FarmingItemData>();
        topicButtons = new Button[Topics.Length];
        topicIcons   = new Sprite[Topics.Length];
        for (int i = 0; i < Topics.Length; i++) BuildTopicRow(pane, i, items);
    }

    private static Sprite IconOf(string id, List<FarmingItemData> items) => id switch
    {
        "flag"   => HudDock.FlagIcon,
        "paw"    => CuteUIKit.Paw(),
        "hammer" => HudDock.HammerIcon,
        "rocket" => HudDock.RocketIcon,
        "cat"    => HudDock.CatIcon,
        "core"   => CoreStageTable.Load()?.Stage(1)?.spriteOn,
        "rain"   => Resources.Load<Sprite>("Weather/rain"),
        _        => items.Find(i => i != null && i.name == id)?.icon,
    };

    // 발바닥 그림은 흰색이라 색을 입힘
    private static Color IconTint(string id) => id == "paw" ? CuteUIKit.Strawberry : Color.white;

    private void BuildTopicRow(Transform pane, int index, List<FarmingItemData> items)
    {
        var go = new GameObject($"Topic_{index}", typeof(RectTransform));
        go.transform.SetParent(pane, false);
        var fill = CuteUIKit.SetRounded(go.AddComponent<Image>(), 11, Color.white);
        fill.raycastTarget = true;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = RowH;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(10, 8, 0, 0);
        hlg.spacing                = 9f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        topicIcons[index] = IconOf(Topics[index].icon, items);
        var rowIcon = UIStyleKit.CreateIcon(go.transform, topicIcons[index], 24f);
        rowIcon.raycastTarget = false;
        rowIcon.color = IconTint(Topics[index].icon);
        Label(go.transform, Topics[index].name, BodySize, FontStyles.Bold, false);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = fill;
        btn.onClick.AddListener(() =>
        {
            ShowTopic(index);
            Sfx.Play(SfxId.UiHover, 0.6f);
        });
        go.AddComponent<UiButtonSfx>().hover = true;
        // 패드는 포인터만 올려도 넘어감
        go.AddComponent<PadHoverHook>().onHover = () => { if (Pad.Active) ShowTopic(index); };
        topicButtons[index] = btn;
    }

    private sealed class PadHoverHook : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public System.Action onHover;
        public void OnPointerEnter(PointerEventData e) => onHover?.Invoke();
        public void OnSelect(BaseEventData e)          => onHover?.Invoke();
    }

    private void BuildPagePane(Transform body)
    {
        var pane = Pane(body, "Page");
        var le = pane.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth  = 1f;
        le.preferredWidth = 0f;

        pageRT = BuildScrollView(pane, new Vector2(20f, 14f), new Vector2(-10f, -16f));
        pageRT.GetComponent<VerticalLayoutGroup>().spacing = 16f;
    }

    private void ShowTopic(int index)
    {
        if (index == shown) return;
        shown = index;
        PaintTopics();
        BuildPage();
    }

    private void PaintTopics()
    {
        for (int i = 0; i < topicButtons.Length; i++)
        {
            bool on = i == shown;
            var c = topicButtons[i].colors;
            c.normalColor      = c.selectedColor = c.disabledColor = on ? Topics[i].tint : Color.clear;
            c.highlightedColor = on ? Topics[i].tint : CuteUIKit.LatteLight;
            c.pressedColor     = CuteUIKit.Latte;
            c.colorMultiplier  = 1f;
            c.fadeDuration     = 0.08f;
            topicButtons[i].colors = c;
        }
    }

    private void BuildPage()
    {
        for (int i = pageRT.childCount - 1; i >= 0; i--)
            DestroyImmediate(pageRT.GetChild(i).gameObject);

        builtScheme = CurrentScheme;
        var topic = Topics[shown];
        if (shown == ControlsPage) BuildControls(topic);
        else
        {
            PageHeader(topic, topic.lead);
            foreach (var (term, text) in topic.rows) TermRow(term, text, TermW);
        }
        pageRT.anchoredPosition = Vector2.zero;
    }

    private void BuildControls(Topic topic)
    {
        if (builtScheme == Scheme.Touch)
        {
            PageHeader(topic, "화면을 눌러서 조작해요.");
            foreach (var (term, text) in TouchRows) TermRow(term, text, TouchTermW);
            return;
        }

        bool pad = builtScheme == Scheme.Pad;
        PageHeader(topic, pad ? "창 안에서 누를 버튼은 화면 아래 막대에 나와요." : "키보드와 마우스");
        var columns = Group<HorizontalLayoutGroup>(pageRT, "Columns", 22f, TextAnchor.UpperLeft);
        var left  = Column(columns);
        var right = Column(columns);

        ColumnTitle(left, "움직이고 일하기");
        ColumnTitle(right, "창 열기");
        if (pad)
        {
            foreach (var (glyphs, text) in PadWork)    PadRow(left, glyphs, text);
            foreach (var (glyphs, text) in PadWindows) PadRow(right, glyphs, text);
        }
        else
        {
            foreach (var (keys, text) in KeysWork)    KeyRow(left, keys, text);
            foreach (var (keys, text) in KeysWindows) KeyRow(right, keys, text);
        }
    }

    private void PageHeader(Topic topic, string lead)
    {
        var row = Group<HorizontalLayoutGroup>(pageRT, "Header", 14f, TextAnchor.MiddleLeft);
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 8);

        var badge = CuteUIKit.Card(row, topic.tint, 16, "Badge");
        badge.raycastTarget = false;
        var ble = badge.gameObject.AddComponent<LayoutElement>();
        ble.minWidth = ble.preferredWidth = ble.minHeight = ble.preferredHeight = 58f;
        var icon = UIStyleKit.CreateIcon(badge.transform, topicIcons[shown], 40f);
        icon.raycastTarget = false;
        icon.color = IconTint(topic.icon);
        icon.rectTransform.sizeDelta = new Vector2(40f, 40f);

        var col = Column(row, 3f);
        Label(col, topic.name, TitleSize, FontStyles.Bold, false);
        Label(col, lead, BodySize);
    }

    private void TermRow(string term, string text, float termWidth)
    {
        var row = Group<HorizontalLayoutGroup>(pageRT, "Row", 12f, TextAnchor.UpperLeft);
        var termLabel = Label(row, term, BodySize, FontStyles.Bold, false);
        var tle = termLabel.gameObject.AddComponent<LayoutElement>();
        tle.minWidth = tle.preferredWidth = termWidth;
        Fill(Label(row, text, BodySize));
    }

    private void ColumnTitle(Transform column, string title)
    {
        var label = Label(column, title, BodySize, FontStyles.Bold, false);
        label.gameObject.AddComponent<LayoutElement>().minHeight = 26f;
    }

    private void KeyRow(Transform column, string keys, string text)
    {
        var slot = KeySlot(column, text);
        foreach (var key in keys.Split('|'))
        {
            CuteUIKit.Pill(slot, key, CuteUIKit.LatteLight, CuteUIKit.Cocoa, 14f, 8, new RectOffset(8, 8, 3, 4), out var label);
            if (fontAsset != null) label.font = fontAsset;
        }
    }

    private void PadRow(Transform column, PadGlyph[] glyphs, string text)
    {
        var slot = KeySlot(column, text);
        foreach (var glyph in glyphs) GamepadHud.Glyph(slot, glyph, 24f);
    }

    private Transform KeySlot(Transform column, string text)
    {
        var row  = Group<HorizontalLayoutGroup>(column, "Key", 10f, TextAnchor.MiddleLeft);
        var slot = Group<HorizontalLayoutGroup>(row, "Keys", 4f, TextAnchor.MiddleLeft);
        var le = slot.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = KeyW;
        le.minHeight = 26f;
        Fill(Label(row, text, BodySize));
        return slot;
    }

    private static Transform Column(Transform parent, float spacing = 6f)
    {
        var col = Group<VerticalLayoutGroup>(parent, "Column", spacing, TextAnchor.UpperLeft);
        col.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
        var le = col.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth  = 1f;
        le.preferredWidth = 0f;
        return col;
    }

    private static void Fill(TextMeshProUGUI label)
    {
        var le = label.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth  = 1f;
        le.preferredWidth = 0f;
    }

    private static RectTransform Group<T>(Transform parent, string name, float spacing, TextAnchor align) where T : HorizontalOrVerticalLayoutGroup
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var group = go.AddComponent<T>();
        group.spacing                = spacing;
        group.childAlignment         = align;
        group.childControlWidth      = true;
        group.childControlHeight     = true;
        group.childForceExpandWidth  = false;
        group.childForceExpandHeight = false;
        return (RectTransform)go.transform;
    }

    private TextMeshProUGUI Label(Transform parent, string text, float size, FontStyles style = FontStyles.Normal, bool wrap = true)
    {
        var t = CuteUIKit.Text(parent, text, size, CuteUIKit.Cocoa, style, TextAlignmentOptions.TopLeft);
        if (wrap)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing      = 8f;
        }
        if (fontAsset != null) t.font = fontAsset;
        return t;
    }

    private void BuildToggleButton()
        => toggleButton = CornerButton(transform, "HelpToggleButton", "?", new Vector2(-25f, -25f), 55f, CuteUIKit.Strawberry, 30f,
                                       fontAsset, TogglePanel).transform;

    internal static Button CornerButton(Transform parent, string name, string label, Vector2 position, float size,
                                        Color color, float fontSize, TMP_FontAsset font, UnityAction onClick)
    {
        var btnGO = new GameObject(name, typeof(RectTransform));
        btnGO.transform.SetParent(parent, false);
        var btnRT = (RectTransform)btnGO.transform;
        btnRT.anchorMin = btnRT.anchorMax = btnRT.pivot = new Vector2(1f, 1f);
        btnRT.anchoredPosition = position;
        btnRT.sizeDelta = new Vector2(size, size);

        int radius = Mathf.FloorToInt(size * 0.5f);
        btnGO.AddComponent<Image>().color = Color.clear;
        var lip = CuteUIKit.Layer(btnGO.transform, "Lip", CuteUIKit.Rounded(radius), CuteUIKit.Darker(color, 0.8f));
        lip.rectTransform.offsetMin = lip.rectTransform.offsetMax = new Vector2(0f, -3f);
        var face = CuteUIKit.Layer(btnGO.transform, "Face", CuteUIKit.Rounded(radius), Color.white);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = face;
        CuteUIKit.ApplyButtonColors(btn, color);
        btn.onClick.AddListener(onClick);
        btnGO.AddComponent<UiButtonSfx>().hover = true;
        btnGO.AddComponent<CutePop>();

        var txt = CuteUIKit.Text(btnGO.transform, label, fontSize, Color.white, FontStyles.Bold);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;
        if (font != null) txt.font = font;
        return btn;
    }

    internal static RectTransform BuildScrollView(RectTransform parent, Vector2 offsetMin, Vector2 offsetMax)
    {
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform));
        scrollGO.transform.SetParent(parent, false);
        var srt = (RectTransform)scrollGO.transform;
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = offsetMin;
        srt.offsetMax = offsetMax;

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal        = false;
        scroll.vertical          = true;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(srt, false);
        var viewRT = (RectTransform)viewportGO.transform;
        viewRT.anchorMin = Vector2.zero;
        viewRT.anchorMax = Vector2.one;
        viewRT.offsetMin = Vector2.zero;
        viewRT.offsetMax = new Vector2(-14f, 0f);
        viewportGO.AddComponent<RectMask2D>();
        viewportGO.AddComponent<Image>().color = Color.clear;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewRT, false);
        var contentRT = (RectTransform)contentGO.transform;
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = Vector2.zero;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(2, 2, 2, 6);
        vlg.spacing                = 8f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewRT;
        scroll.content  = contentRT;

        var barGO = new GameObject("Scrollbar", typeof(RectTransform));
        barGO.transform.SetParent(srt, false);
        var barRT = (RectTransform)barGO.transform;
        barRT.anchorMin = new Vector2(1f, 0f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot     = new Vector2(1f, 0.5f);
        barRT.sizeDelta = new Vector2(8f, 0f);
        barGO.AddComponent<Image>();
        var area = new GameObject("SlidingArea", typeof(RectTransform));
        area.transform.SetParent(barRT, false);
        var areaRT = (RectTransform)area.transform;
        areaRT.anchorMin = Vector2.zero;
        areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = areaRT.offsetMax = Vector2.zero;
        var handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(areaRT, false);
        var handleRT = (RectTransform)handle.transform;
        handleRT.offsetMin = handleRT.offsetMax = Vector2.zero;
        handle.AddComponent<Image>();

        var scrollbar = barGO.AddComponent<Scrollbar>();
        scrollbar.handleRect = handleRT;
        scrollbar.direction  = Scrollbar.Direction.BottomToTop;
        CuteUIKit.StyleScrollbar(scrollbar);
        scroll.verticalScrollbar           = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return contentRT;
    }

    [ContextMenu("Rebuild UI")]
    public void RebuildUI()
    {
        if (animCo != null) { StopCoroutine(animCo); animCo = null; }
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        BuildUI();
        panelRT.localScale = isPanelOpen ? Vector3.one : Vector3.zero;
        panelCG.blocksRaycasts = panelCG.interactable = isPanelOpen;
        panelCG.alpha = isPanelOpen ? 1f : 0f;
    }

    public void TogglePanel()
    {
        // 큰 창은 한 번에 하나
        if (!isPanelOpen) GamepadPanels.CloseAllForMenu();
        isPanelOpen = !isPanelOpen;
        Sfx.Play(isPanelOpen ? SfxId.UiOpen : SfxId.UiClose);
        panelCG.blocksRaycasts = isPanelOpen;
        panelCG.interactable   = isPanelOpen;
        if (isPanelOpen)
        {
            transform.SetAsLastSibling();
            if (builtScheme != CurrentScheme) BuildPage();
        }
        else UIStyleKit.ClearSelectionIn(panelRT);
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(Animate(isPanelOpen));
    }

    public static void CloseIfOpen()
    {
        if (AnyOpen) current.TogglePanel();
    }

    public static void Open()
    {
        if (current != null && !current.isPanelOpen) current.TogglePanel();
    }

    private IEnumerator Animate(bool open)
    {
        if (open)
        {
            UIStyleKit.SetScrolling(panelRT, true);
            CuteUIKit.WiggleEars(this, panelRT);
            yield return CuteUIKit.PopIn(panelRT, panelCG);
        }
        else
        {
            yield return CuteUIKit.PopOut(panelRT, panelCG);
            UIStyleKit.SetScrolling(panelRT, false);
        }
        animCo = null;
    }
}
