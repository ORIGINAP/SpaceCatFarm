using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 전시 데모 설명 화면: 게임 소개 · 장면 제목 · 지금 보여 주는 기능 · 마무리 요약
// 바탕은 크림 · 코코아, 색은 기능 갈래 구분에만 한 가지씩
public class DemoPanel : MonoBehaviour
{
    public readonly struct Pillar
    {
        public readonly string name, line, icon;
        public readonly Color  color;

        public Pillar(string name, string line, string icon, int color)
        {
            this.name  = name;
            this.line  = line;
            this.icon  = icon;
            this.color = CuteUIKit.Hex(color);
        }
    }

    // 핵심 기능 여섯 갈래
    public static readonly Pillar[] Pillars =
    {
        new("정화 · 개척", "오염된 땅을 넓혀요",   "Item_Purifier",     0x3F78AE),
        new("농사 · 요리", "도구 · 날씨 · 주방",   "Item_StarSickle",   0x3F8A66),
        new("주민 · 셔틀", "고양이 가족을 불러요", "cat",               0xC9485B),
        new("탐험 · 산업", "채굴 · 레일 · 제련",   "Item_CargoRail",    0x8A5A35),
        new("마을 · 성장", "축제 · 연구 · 코어",   "Item_StarFountain", 0x6E57B5),
        new("자동화",      "기계가 농장을 돌려요", "AutoHarvester",     0xA8741A),
    };

    private const float CardW = 400f, Pad = 18f, HeadH = 118f, RowH = 30f, NowH = 66f, RowGap = 4f, FootH = 58f;
    private const float CardBottom = 92f;
    private const float BannerW = 640f, BannerH = 112f, BannerBottom = 150f, BannerHold = 2.4f;
    private const float OvW = 1040f, OvH = 560f;
    private static readonly Color Soft = CuteUIKit.Hex(0xF6EBDD);
    private static readonly string[] CheckRows =
    {
        "........##",
        ".......###",
        "##....###.",
        "###..###..",
        ".######...",
        "..####....",
        "...##.....",
    };

    private static Sprite logo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => logo = null;

    // 메뉴 화면 로고를 잡아 둠 (게임 씬엔 없음)
    public static void CaptureLogo()
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            if (s != null && s.name == "logo" && s.texture != null && s.texture.width >= 512) { logo = s; return; }
    }

    private class Row
    {
        public RectTransform   rt;
        public CanvasGroup     line, open;
        public Image           dot, check, bar, icon, tick;
        public TextMeshProUGUI name, bigName, detail;
        public bool            lit;
        public float           show;
    }

    private class Tile
    {
        public CanvasGroup group;
        public Image       done;
    }

    private RectTransform   canvasRT, card, banner, ovCard;
    private CanvasGroup     cardGroup, bannerGroup, ovGroup;
    private TextMeshProUGUI kicker, title, lead, autoLabel, nextLabel;
    private TextMeshProUGUI bannerKicker, bannerTitle, ovSub, ovFoot;
    private readonly List<Row>  rows  = new();
    private readonly List<Tile> tiles = new();
    private int       steps, focus = -1, pillar, shownLeft = -2;
    private bool      recap, cardShown;
    private Vector2   lastCanvas, bannerHome;
    private Coroutine bannerCo, ovCo;

    public static DemoPanel Build(Transform parent, int stepCount, UnityAction restart, UnityAction toggleAuto, UnityAction next)
    {
        var go = new GameObject("DemoPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UIStyleKit.CreateOverlayCanvas(go, 590);
        var p = go.AddComponent<DemoPanel>();
        p.canvasRT = (RectTransform)go.transform;
        p.steps    = stepCount;
        p.BuildCard(restart, toggleAuto, next);
        p.BuildBanner();
        p.BuildOverview();
        return p;
    }

    // ── 장면 ─────────────────────────────────────────────────────────────

    public void Show(int index, int pillarIndex, string icon, string titleText, string leadText,
                     IList<(string icon, string name, string detail)> points)
    {
        pillar = Mathf.Clamp(pillarIndex, 0, Pillars.Length - 1);
        focus  = -1;
        var pl = Pillars[pillar];
        string head = $"{index + 1:00} / {steps}   <color=#{ColorUtility.ToHtmlStringRGB(pl.color)}>{pl.name}</color>";
        kicker.text = head;
        title.text  = titleText;
        lead.text   = leadText;

        foreach (var r in rows) if (r.rt != null) Destroy(r.rt.gameObject);
        rows.Clear();
        for (int i = 0; i < points.Count; i++) rows.Add(MakeRow(points[i]));
        Relayout(true);
        Layout(true);

        if (!cardShown)
        {
            cardShown = true;
            cardGroup.blocksRaycasts = true;
            StartCoroutine(Fade(cardGroup, 1f, 0.35f));
        }
        if (bannerCo != null) StopCoroutine(bannerCo);
        bannerCo = StartCoroutine(BannerCo(head, titleText));
    }

    // 지금 보여 주는 기능: 그 줄만 펼치고 체크
    public void Check(int i)
    {
        if (i < 0 || i >= rows.Count || rows[i].lit) return;
        var r = rows[i];
        r.lit = true;
        focus = i;
        Paint(r);
        r.tick.transform.localScale = Vector3.zero;
        StartCoroutine(CuteUIKit.ScaleTo(r.tick.transform, 1f, 0.28f, 2f));
        Sfx.Play(SfxId.UiConfirm, 0.5f);
    }

    // 펼친 줄의 설명 · 아이콘을 바꿈 (실시간 수치 등)
    public void Note(int i, string detail, Sprite icon = null)
    {
        if (i < 0 || i >= rows.Count) return;
        rows[i].detail.text = detail;
        if (icon != null) SetIcon(rows[i].icon, icon);
    }

    public void Status(bool stepRunning, bool auto, bool last, float restartIn)
    {
        autoLabel.text = auto ? "자동 ON" : "자동 OFF";
        nextLabel.text = stepRunning ? "빨리 ▶▶  (N)" : last ? "처음부터 ↺  (N)" : "다음 ▶  (N)";
        if (!recap) return;

        // 자동 재생이면 카운트다운, 아니면 N 키 안내
        int left = restartIn > 0f ? Mathf.CeilToInt(restartIn) : auto ? 0 : -1;
        if (left == shownLeft) return;
        shownLeft = left;
        ovFoot.text = left > 0  ? $"직접 플레이해 보세요   ·   {left}초 뒤 처음부터"
                    : left == 0 ? "직접 플레이해 보세요"
                                : "직접 플레이해 보세요   ·   N 키로 처음부터";
    }

    // ── 소개 · 마무리 ────────────────────────────────────────────────────

    public void ShowOverview(bool isRecap)
    {
        recap     = isRecap;
        shownLeft = -2;
        ovSub.text  = recap ? "행성 이주 완료!" : "오염된 별을 정화하고, 고양이들의 새 고향을 만드는 농장 게임";
        ovFoot.text = recap ? "직접 플레이해 보세요" : $"{steps}장면으로 하나씩 보여 드릴게요";
        if (ovCo != null) StopCoroutine(ovCo);
        ovCo = StartCoroutine(OverviewIn());
    }

    public void HideOverview()
    {
        recap = false;
        if (ovCo != null) StopCoroutine(ovCo);
        ovCo = StartCoroutine(CuteUIKit.PopOut(ovCard, ovGroup, 0.26f));
    }

    private IEnumerator OverviewIn()
    {
        foreach (var t in tiles)
        {
            t.group.alpha = 0f;
            t.done.gameObject.SetActive(false);
        }
        Sfx.Play(recap ? SfxId.QuestComplete : SfxId.UiOpen, 0.8f);
        yield return CuteUIKit.PopIn(ovCard, ovGroup, 0.36f);
        foreach (var t in tiles)
        {
            StartCoroutine(Fade(t.group, 1f, 0.25f));
            yield return new WaitForSecondsRealtime(0.08f);
        }
        if (!recap) yield break;

        // 다 봤다는 체크
        yield return new WaitForSecondsRealtime(0.3f);
        foreach (var t in tiles)
        {
            t.done.gameObject.SetActive(true);
            t.done.transform.localScale = Vector3.zero;
            StartCoroutine(CuteUIKit.ScaleTo(t.done.transform, 1f, 0.26f, 2f));
            Sfx.Play(SfxId.UiConfirm, 0.4f);
            yield return new WaitForSecondsRealtime(0.12f);
        }
    }

    // ── 매 프레임 ────────────────────────────────────────────────────────

    private void Update()
    {
        Layout(false);
        Relayout(false);
    }

    // 지금 줄만 크게, 나머지는 한 줄 (부드럽게 접고 펼침)
    private void Relayout(bool snap)
    {
        float k = snap ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
        float w = CardW - Pad * 2f, y = -HeadH;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.rt == null) continue;
            r.show = Mathf.Lerp(r.show, i == focus ? 1f : 0f, k);
            float h = Mathf.Lerp(RowH, NowH, r.show);
            r.rt.anchoredPosition = new Vector2(Pad + w * 0.5f, y - h * 0.5f);
            r.rt.sizeDelta = new Vector2(w, h);
            r.line.alpha = 1f - r.show;
            r.open.alpha = r.show;
            y -= h + RowGap;
        }
        card.sizeDelta = new Vector2(CardW, -y + 10f + FootH);
    }

    // 화면 크기 따라 카드 · 배너 크기와 자리
    private void Layout(bool force)
    {
        var size = canvasRT.rect.size;
        if (!force && size == lastCanvas) return;
        lastCanvas = size;

        float top = MobileMode.Active ? 150f : 370f;
        float tallest = HeadH + Mathf.Max(0, rows.Count - 1) * (RowH + RowGap) + NowH + 10f + FootH;
        float cardScale = Mathf.Clamp((size.y - CardBottom - top) / tallest, 0.62f, 1f);
        card.localScale = new Vector3(cardScale, cardScale, 1f);

        // 배너는 카드 왼쪽 빈자리 가운데
        float room = size.x - (CardW * cardScale + 36f);
        float bannerScale = Mathf.Min(MobileMode.Active ? 0.8f : 1f, (room - 16f) / BannerW);
        banner.localScale = new Vector3(bannerScale, bannerScale, 1f);
        float half = BannerW * bannerScale * 0.5f;
        bannerHome = new Vector2(Mathf.Max(half + 8f, Mathf.Min(size.x * 0.5f, room - half - 8f)), BannerBottom);
        if (bannerCo == null) banner.anchoredPosition = bannerHome;
    }

    // ── 기능 카드 (오른쪽) ───────────────────────────────────────────────

    private void BuildCard(UnityAction restart, UnityAction toggleAuto, UnityAction next)
    {
        card = Frame(transform, "Card", 18);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 0f);
        card.anchoredPosition = new Vector2(-20f, CardBottom);
        card.sizeDelta = new Vector2(CardW, HeadH + 10f + FootH);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        cardGroup.blocksRaycasts = false;

        float w = CardW - Pad * 2f;
        kicker = Label(card, 16f, CuteUIKit.CocoaSoft, true);
        Box(kicker.rectTransform, Pad, -14f, w, 22f);
        title = Label(card, 28f, CuteUIKit.Cocoa, true);
        Shrink(title, 21f);
        Box(title.rectTransform, Pad, -36f, w, 38f);
        lead = Label(card, 16.5f, CuteUIKit.CocoaSoft, false);
        Shrink(lead, 13.5f);
        Box(lead.rectTransform, Pad, -76f, w, 24f);
        Box(Img(card, "Divider", null, CuteUIKit.Latte).rectTransform, Pad, -107f, w, 2f);

        // 진행용 버튼은 작고 조용하게
        var restartBtn = CuteUIKit.Button(card, "처음부터 ↺", CuteUIKit.ButtonLatte, restart, 98f, 30f, 13.5f);
        BoxBottom((RectTransform)restartBtn.transform, Pad, 14f, 98f, 30f);
        var autoBtn = CuteUIKit.Button(card, "자동 OFF", CuteUIKit.ButtonLatte, toggleAuto, 90f, 30f, 13.5f);
        BoxBottom((RectTransform)autoBtn.transform, Pad + 106f, 14f, 90f, 30f);
        autoLabel = autoBtn.GetComponentInChildren<TextMeshProUGUI>();
        var nextBtn = CuteUIKit.Button(card, "다음 ▶  (N)", CuteUIKit.ButtonLatte, next, 134f, 30f, 13.5f);
        BoxBottom((RectTransform)nextBtn.transform, CardW - Pad - 134f, 14f, 134f, 30f);
        nextLabel = nextBtn.GetComponentInChildren<TextMeshProUGUI>();
    }

    private Row MakeRow((string icon, string name, string detail) p)
    {
        var go = new GameObject("Point", typeof(RectTransform));
        var r = new Row { rt = (RectTransform)go.transform };
        r.rt.SetParent(card, false);
        r.rt.anchorMin = r.rt.anchorMax = new Vector2(0f, 1f);
        r.rt.pivot = new Vector2(0.5f, 0.5f);

        // 접힌 줄: 점(→ 체크) + 이름
        r.line = Group(r.rt, "Line");
        r.dot = Img(r.line.transform, "Dot", CuteUIKit.Rounded(4), CuteUIKit.CocoaMuted);
        Pin(r.dot.rectTransform, 5f, 8f, 8f);
        r.check = Img(r.line.transform, "Check", CheckSprite(), Color.white);
        Pin(r.check.rectTransform, 1f, 15f, 11f);
        r.name = Label(r.line.transform, 17f, CuteUIKit.CocoaMuted, false);
        r.name.text = p.name;
        Pin(r.name.rectTransform, 26f, CardW - Pad * 2f - 26f, RowH);

        // 펼친 줄: 막대 · 아이콘 · 이름 · 설명 · 체크
        r.open = Group(r.rt, "Open");
        r.bar = Img(r.open.transform, "Bar", CuteUIKit.Rounded(2), Color.white);
        Pin(r.bar.rectTransform, 0f, 4f, NowH - 14f);
        var box = Img(r.open.transform, "IconBox", CuteUIKit.Rounded(10), Soft);
        Pin(box.rectTransform, 14f, 44f, 44f);
        r.icon = Icon(box.rectTransform, 34f);
        SetIcon(r.icon, IconOf(p.icon));
        float textW = CardW - Pad * 2f - 70f - 30f;
        r.bigName = Label(r.open.transform, 20f, CuteUIKit.Cocoa, true);
        r.bigName.text = p.name;
        Shrink(r.bigName, 15f);
        Pin(r.bigName.rectTransform, 70f, textW, 26f, 11f);
        r.detail = Label(r.open.transform, 16f, CuteUIKit.Cocoa, false);
        r.detail.text = p.detail;
        Shrink(r.detail, 12.5f);
        Pin(r.detail.rectTransform, 70f, textW, 22f, -12f);
        r.tick = Img(r.open.transform, "Tick", CheckSprite(), Color.white);
        r.tick.rectTransform.anchorMin = r.tick.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        r.tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        r.tick.rectTransform.anchoredPosition = new Vector2(-14f, 0f);
        r.tick.rectTransform.sizeDelta = new Vector2(20f, 15f);
        r.open.alpha = 0f;
        Paint(r);
        return r;
    }

    private void Paint(Row r)
    {
        var c = Pillars[pillar].color;
        r.dot.enabled   = !r.lit;
        r.check.enabled = r.lit;
        r.check.color   = c;
        r.name.color    = r.lit ? CuteUIKit.Cocoa : CuteUIKit.CocoaMuted;
        r.bar.color     = c;
        r.tick.color    = c;
    }

    // ── 장면 제목 배너 (화면 아래쪽) ─────────────────────────────────────

    private void BuildBanner()
    {
        banner = Frame(transform, "Banner", 20);
        banner.anchorMin = banner.anchorMax = Vector2.zero;
        banner.pivot     = new Vector2(0.5f, 0f);
        banner.sizeDelta = new Vector2(BannerW, BannerH);
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;

        bannerKicker = Label(banner, 18f, CuteUIKit.CocoaSoft, true);
        bannerKicker.alignment = TextAlignmentOptions.Center;
        Box(bannerKicker.rectTransform, 20f, -14f, BannerW - 40f, 26f);
        bannerTitle = Label(banner, 44f, CuteUIKit.Cocoa, true);
        bannerTitle.alignment = TextAlignmentOptions.Center;
        Shrink(bannerTitle, 30f);
        Box(bannerTitle.rectTransform, 20f, -40f, BannerW - 40f, 58f);
    }

    // 살짝 떠오르고, 잠깐 머문 뒤 사라짐
    private IEnumerator BannerCo(string kickerText, string titleText)
    {
        bannerKicker.text = kickerText;
        bannerTitle.text  = titleText;
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            float k = e / 0.3f;
            banner.anchoredPosition = bannerHome + new Vector2(0f, -16f * (1f - k) * (1f - k));
            bannerGroup.alpha = k;
            yield return null;
        }
        banner.anchoredPosition = bannerHome;
        bannerGroup.alpha = 1f;
        // 창이 열리면 가리지 않게 일찍 빠짐
        for (float e = 0f; e < BannerHold; e += Time.unscaledDeltaTime)
        {
            if (e > 0.8f && PlayerTileController.IsAnyUIOpen()) break;
            yield return null;
        }
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            bannerGroup.alpha = 1f - e / 0.3f;
            yield return null;
        }
        bannerGroup.alpha = 0f;
        bannerCo = null;
    }

    // ── 소개 · 마무리 카드 ───────────────────────────────────────────────

    private void BuildOverview()
    {
        var go = new GameObject("Overview", typeof(RectTransform));
        var root = (RectTransform)go.transform;
        root.SetParent(transform, false);
        Stretch(root, 0f);
        ovGroup = go.AddComponent<CanvasGroup>();
        ovGroup.alpha = 0f;
        ovGroup.blocksRaycasts = false;
        ovGroup.interactable   = false;
        Stretch(Img(root, "Dim", null, new Color(0.16f, 0.1f, 0.12f, 0.45f)).rectTransform, 0f);

        ovCard = Frame(root, "Card", 26);
        ovCard.anchorMin = ovCard.anchorMax = ovCard.pivot = new Vector2(0.5f, 0.5f);
        ovCard.anchoredPosition = new Vector2(0f, 10f);
        ovCard.sizeDelta = new Vector2(OvW, OvH);
        ovCard.localScale = Vector3.zero;

        if (logo != null)
        {
            var img = Img(ovCard, "Logo", logo, Color.white);
            img.preserveAspect = true;
            Box(img.rectTransform, (OvW - 380f) * 0.5f, -16f, 380f, 208f);
        }
        else
        {
            var name = Label(ovCard, 60f, CuteUIKit.Cocoa, true);
            name.alignment = TextAlignmentOptions.Center;
            name.text = "Space Cat Farm";
            Box(name.rectTransform, 40f, -70f, OvW - 80f, 100f);
        }
        ovSub = Label(ovCard, 22f, CuteUIKit.Cocoa, true);
        ovSub.alignment = TextAlignmentOptions.Center;
        Box(ovSub.rectTransform, 40f, -230f, OvW - 80f, 32f);
        var heading = Label(ovCard, 15f, CuteUIKit.CocoaSoft, true);
        heading.alignment = TextAlignmentOptions.Center;
        heading.text = "핵심 기능 6가지";
        Box(heading.rectTransform, 40f, -270f, OvW - 80f, 22f);

        // 갈래 타일 3×2: 같은 바탕, 이름 글자색으로만 구분
        const float tw = 310f, th = 92f, gx = 18f, gy = 16f;
        float x0 = (OvW - (tw * 3f + gx * 2f)) * 0.5f;
        for (int i = 0; i < Pillars.Length; i++)
        {
            var pl = Pillars[i];
            var bg = Img(ovCard, "Tile", CuteUIKit.Rounded(14), Soft);
            Box(bg.rectTransform, x0 + i % 3 * (tw + gx), -302f - i / 3 * (th + gy), tw, th);
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            var disc = Img(bg.rectTransform, "Disc", CuteUIKit.Rounded(30), Color.white);
            Box(disc.rectTransform, 14f, -(th - 60f) * 0.5f, 60f, 60f);
            SetIcon(Icon(disc.rectTransform, 42f), IconOf(pl.icon));
            var name = Label(bg.rectTransform, 21f, pl.color, true);
            name.text = pl.name;
            Box(name.rectTransform, 88f, -18f, tw - 128f, 28f);
            var line = Label(bg.rectTransform, 15f, CuteUIKit.CocoaSoft, false);
            line.text = pl.line;
            Shrink(line, 12.5f);
            Box(line.rectTransform, 88f, -50f, tw - 100f, 24f);
            var done = Img(bg.rectTransform, "Done", CheckSprite(), pl.color);
            Box(done.rectTransform, tw - 38f, -16f, 22f, 16f);
            done.gameObject.SetActive(false);
            tiles.Add(new Tile { group = group, done = done });
        }

        ovFoot = Label(ovCard, 17f, CuteUIKit.CocoaSoft, true);
        ovFoot.alignment = TextAlignmentOptions.Center;
        Box(ovFoot.rectTransform, 40f, -(OvH - 46f), OvW - 80f, 26f);
    }

    // ── 도우미 ───────────────────────────────────────────────────────────

    // 아이콘 이름 → 스프라이트 (아이템 이름 또는 HUD 아이콘)
    public static Sprite IconOf(string key)
    {
        switch (key)
        {
            case "cat":    return HudDock.CatIcon;
            case "rocket": return HudDock.RocketIcon;
            case "letter": return HudDock.LetterIcon;
            case "flask":  return HudDock.FlaskIcon;
            case "flag":   return HudDock.FlagIcon;
            case "hammer": return HudDock.HammerIcon;
            case "coin":   return CuteUIKit.Coin();
            case "ship":   return ShuttleUI.ShipSprite;
            case "aurora": return WeatherIcons.Get(WeatherType.Aurora);
            case "meteor": return WeatherIcons.Get(WeatherType.MeteorShower);
            case "rain":   return WeatherIcons.Get(WeatherType.StarRain);
            case "core":
                var stage = CenterCore.Current != null ? CenterCore.Current.CurrentStage : null;
                return stage != null ? stage.spriteOn : null;
        }
        var item = string.IsNullOrEmpty(key) ? null : NetWorld.Item(key);
        return item != null ? item.icon : null;
    }

    private static Sprite CheckSprite() => LabArt.Pixel("demo_check", CheckRows, "##FFFFFF", new Vector2(0.5f, 0.5f));

    private static void SetIcon(Image img, Sprite s)
    {
        img.sprite = s != null ? s : CuteUIKit.Paw();
        img.type   = Image.Type.Simple;
        img.color  = s != null ? Color.white : CuteUIKit.Latte;
        img.preserveAspect = true;
    }

    // 그림자 · 테두리 · 크림 바탕
    private static RectTransform Frame(Transform parent, string name, int radius)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        var shadow = Img(rt, "Shadow", CuteUIKit.SoftShadow(radius, 12), CuteUIKit.Shadow);
        Stretch(shadow.rectTransform, -13f);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        Stretch(Img(rt, "Border", CuteUIKit.Rounded(radius), CuteUIKit.Latte).rectTransform, 0f);
        Stretch(Img(rt, "Fill", CuteUIKit.Rounded(radius - 3), CuteUIKit.Cream).rectTransform, 3f);
        return rt;
    }

    private static CanvasGroup Group(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform, 0f);
        var g = go.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        return g;
    }

    private static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type   = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.color  = color;
        img.raycastTarget = false;
        return img;
    }

    private static Image Icon(RectTransform parent, float size)
    {
        var img = Img(parent, "Icon", null, Color.white);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        return img;
    }

    private static TextMeshProUGUI Label(Transform parent, float size, Color color, bool bold)
    {
        var t = CuteUIKit.Text(parent, "", size, color, bold ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    // 넘치면 글자를 줄여 한 줄에 맞춤
    private static void Shrink(TextMeshProUGUI t, float min)
    {
        t.fontSizeMax      = t.fontSize;
        t.fontSizeMin      = min;
        t.enableAutoSizing = true;
    }

    private static IEnumerator Fade(CanvasGroup g, float to, float duration)
    {
        float from = g.alpha;
        for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
        {
            if (g == null) yield break;
            g.alpha = Mathf.Lerp(from, to, e / duration);
            yield return null;
        }
        if (g != null) g.alpha = to;
    }

    // 부모 왼쪽 위 기준 (피벗은 가운데)
    private static void Box(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x + w * 0.5f, y - h * 0.5f);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모 왼쪽 아래 기준
    private static void BoxBottom(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x + w * 0.5f, y + h * 0.5f);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모 왼쪽 · 세로 가운데 기준 (줄 높이가 바뀌어도 가운데 유지)
    private static void Pin(RectTransform rt, float x, float w, float h, float y = 0f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x + w * 0.5f, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
