using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// 왼쪽 위 퀘스트 패널 + 카드
[RequireComponent(typeof(RectTransform))]
public class QuestUI : MonoBehaviour
{
    public static QuestUI Instance => Services.Get<QuestUI>();

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset fontAsset;

    private const float PanelWidth = 380f;
    private const int   CardRadius = 16;
    private const float ChapterHold = 4.5f;
    private const float DoneHold    = 3f;
    private static readonly Color TextColor         = CuteUIKit.Cocoa;
    private static readonly Color DoneTextColor     = CuteUIKit.MintText;
    private static readonly Color ProgressColor     = CuteUIKit.Lighter(CuteUIKit.MintText, 0.3f);
    private static readonly Color ProgressDoneColor = CuteUIKit.MintText;
    private static readonly Color DoneRowFill       = CuteUIKit.Lighter(CuteUIKit.MintTag, 0.45f);

    private static readonly Color QuestNameBaseColor = CuteUIKit.Cocoa;
    private const string AllDoneText = "모든 퀘스트 완료!";

    private RectTransform   panelRT;
    private CanvasGroup     panelCG;
    private Image           cardFill;
    private Image           cardBorder;
    private TextMeshProUGUI chapterText;
    private TextMeshProUGUI stepText;
    private TextMeshProUGUI goalText;
    private TextMeshProUGUI questNameText;
    private Transform       objectiveContainer;
    private GameObject      hintRow;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI whyText;
    private Button          shortcutBtn;
    private TextMeshProUGUI shortcutText;
    private System.Action   shortcutAction;
    private TextMeshProUGUI toggleBtnText;

    private Coroutine glowCo;
    private Coroutine popCo;

    private readonly List<ObjectiveRow> rows = new();

    private class ObjectiveRow
    {
        public GameObject      root;
        public Image           bg;
        public Image           icon;
        public TextMeshProUGUI label;
        public TextMeshProUGUI count;
        public Image           fill;
    }

    private GameObject logSectionGO;
    private Transform  logContentRT;
    private bool       logOpen = false;

    private RectTransform cardRT;
    private CanvasGroup   cardCG;
    private Transform     cardBody;
    private readonly Queue<QuestDefinition> chapterQueue = new();
    private Coroutine     cardCo;
    private bool          skipCard;

    private RectTransform   doneRT;
    private CanvasGroup     doneCG;
    private TextMeshProUGUI doneText;
    private Coroutine       doneCo;
    private int             doneCount, doneCoins;
    private readonly List<QuestReward> doneItems = new();

    private void Awake()
    {
        if (!Services.Register(this)) { Destroy(gameObject); return; }

        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        foreach (var fx in GetComponents<BaseMeshEffect>()) DestroyImmediate(fx);
        { var c = GetComponent<Image>();               if (c) DestroyImmediate(c); }
        { var c = GetComponent<VerticalLayoutGroup>(); if (c) DestroyImmediate(c); }
        { var c = GetComponent<CanvasGroup>();         if (c) DestroyImmediate(c); }
        { var c = GetComponent<ContentSizeFitter>();   if (c) DestroyImmediate(c); }

        BuildPanel();

        if (transform.parent != null)
            CurrencyHUD.Create(transform.parent,
                panelRT.anchoredPosition + new Vector2(panelRT.sizeDelta.x + 10f, 0f),
                fontAsset);
    }

    private void Start()
    {
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.OnQuestUpdated   += HandleQuestUpdated;
        QuestManager.Instance.OnQuestStarted   += OnQuestStarted;
        QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
        QuestManager.Instance.OnAllQuestsDone  += OnAllDone;
        QuestGuide.OnChanged += RefreshHint;

        RefreshUI();

        var qm = QuestManager.Instance;
        if (GameLaunchContext.IsNewGame && qm.CurrentQuest != null && qm.CurrentQuestIndex == 0 && chapterQueue.Count == 0)
            EnqueueChapter(qm.CurrentQuest);
    }

    private void OnDestroy()
    {
        Services.Unregister(this);
        QuestGuide.OnChanged -= RefreshHint;
        if (cardRT != null) Destroy(cardRT.root.gameObject);
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestUpdated   -= HandleQuestUpdated;
        QuestManager.Instance.OnQuestStarted   -= OnQuestStarted;
        QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
        QuestManager.Instance.OnAllQuestsDone  -= OnAllDone;
    }

    private bool labelsForPad;

    private void Update()
    {
        if (labelsForPad != Pad.Active)
        {
            labelsForPad = Pad.Active;
            if (toggleBtnText != null) toggleBtnText.text = ToggleLabel(logOpen);
            RefreshHint();
        }

        if (cardCo != null && cardCG != null && cardCG.alpha > 0.9f &&
            (GameInput.GetMouseButtonDown(0) || GameInput.GetMouseButtonDown(1) || (Pad.Active && Pad.Down(PadButton.A))))
            skipCard = true;
    }

    private void OnQuestStarted(QuestDefinition def)
    {
        RefreshUI();
        if (logOpen) RefreshLog();
        if (glowCo != null) StopCoroutine(glowCo);
        glowCo = StartCoroutine(NewQuestGlow());
        EnqueueChapter(def);
    }

    private void OnQuestCompleted(QuestDefinition def)
    {
        ShowDone(def);
        // 달성하면 패널이 통 튐
        if (popCo != null) StopCoroutine(popCo);
        panelRT.localScale = Vector3.one * 0.85f;
        popCo = StartCoroutine(CuteUIKit.ScaleTo(panelRT, 1f, 0.3f));
    }

    private void OnAllDone()
    {
        questNameText.text = AllDoneText;
        ClearRows();
        if (logOpen) RefreshLog();
    }

    private void HandleQuestUpdated()
    {
        RefreshUI();
        if (logOpen) RefreshLog();
    }

    private void RefreshUI()
    {
        var quest      = QuestManager.Instance?.CurrentQuest;
        var objectives = QuestManager.Instance?.ActiveObjectives;

        RefreshChapter();
        if (quest == null)
        {
            bool allDone = QuestManager.Instance != null && QuestManager.Instance.AllQuestsDone;
            questNameText.text = allDone ? AllDoneText : "진행 중인 퀘스트 없음";
            ClearRows();
            RefreshHint();
            return;
        }

        questNameText.text = quest.questName;

        int count = objectives?.Count ?? 0;
        while (rows.Count < count) AddRow();
        while (rows.Count > count) RemoveLastRow();

        bool currentMarked = false;
        for (int i = 0; i < count; i++)
        {
            var obj = objectives[i];
            var row = rows[i];
            bool current = !obj.IsComplete && !currentMarked;
            if (current) currentMarked = true;

            float ratio = obj.targetAmount > 0 ? (float)obj.currentAmount / obj.targetAmount : 1f;
            row.label.text  = obj.IsComplete ? $"<s>{obj.GetLabel()}</s>" : current ? $"<b>{obj.GetLabel()}</b>" : obj.GetLabel();
            row.count.text  = $"{obj.currentAmount}/{obj.targetAmount}";
            row.label.color = row.count.color = obj.IsComplete ? DoneTextColor : TextColor;
            row.bg.color    = obj.IsComplete ? DoneRowFill : Color.clear;
            var sprite = QuestIcons.For(obj);
            row.icon.sprite  = sprite;
            row.icon.enabled = sprite != null;
            row.icon.color   = obj.IsComplete ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            CuteUIKit.SetBar(row.fill.rectTransform, ratio);
            row.fill.color = obj.IsComplete ? ProgressDoneColor : ProgressColor;
        }

        RefreshHint();
    }

    private void RefreshChapter()
    {
        var qm = QuestManager.Instance;
        var chapter = qm != null ? qm.CurrentChapter : null;
        if (chapter == null)
        {
            chapterText.text = qm != null && qm.AllQuestsDone ? "모든 장 완료" : "진행 중인 퀘스트";
            stepText.transform.parent.gameObject.SetActive(false);
            goalText.gameObject.SetActive(false);
            return;
        }
        chapterText.text = $"{qm.CurrentChapterIndex + 1}장 · {chapter.title}";
        stepText.transform.parent.gameObject.SetActive(true);
        stepText.text = $"{qm.StepInChapter}/{qm.StepsInChapter}";
        // 챕터 목표는 자세히 펼쳤을 때만
        goalText.gameObject.SetActive(logOpen && !string.IsNullOrEmpty(chapter.goal));
        goalText.text = $"목표 · {chapter.goal}";
    }

    private void RefreshHint()
    {
        if (hintText == null) return;
        var quest = QuestManager.Instance?.CurrentQuest;
        var guide = QuestGuide.Current;
        string text = !string.IsNullOrEmpty(guide.action) ? guide.action : quest != null ? quest.HintText : "";
        hintRow.SetActive(quest != null && !string.IsNullOrEmpty(text));
        hintText.text = text;
        bool why = logOpen && quest != null && !string.IsNullOrEmpty(guide.why);
        whyText.gameObject.SetActive(why);
        if (why) whyText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(CuteUIKit.Strawberry)}>다음:</color> {guide.why}";

        shortcutAction = quest != null ? guide.shortcut : null;
        // 패드는 전용 버튼으로 바로 염
        shortcutBtn.gameObject.SetActive(shortcutAction != null && !Pad.Active);
        if (shortcutAction != null) shortcutText.text = guide.shortcutLabel;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRT);
    }

    private void OnShortcut() => shortcutAction?.Invoke();

    public void ToggleLog() => SetLogOpen(!logOpen);

    private void SetLogOpen(bool open)
    {
        logOpen = open;
        logSectionGO.SetActive(open);
        toggleBtnText.text = ToggleLabel(open);
        if (open) RefreshLog();
        RefreshChapter();
        RefreshHint();
    }

    private static string ToggleLabel(bool open)
        => (Pad.Active ? "↓ " : "") + (open ? "접기" : "자세히");

    private void RefreshLog()
    {
        for (int i = logContentRT.childCount - 1; i >= 0; i--)
            Destroy(logContentRT.GetChild(i).gameObject);

        var quest = QuestManager.Instance?.CurrentQuest;
        if (quest == null) return;

        var entryGO = new GameObject("LogEntry", typeof(RectTransform));
        entryGO.transform.SetParent(logContentRT, false);
        var vlg = entryGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding               = new RectOffset(12, 12, 10, 11);
        vlg.spacing               = 6f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight= false;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = true;
        entryGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (!string.IsNullOrEmpty(quest.talk))
            NewText(entryGO.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(CuteUIKit.SkyText)}><b>{AINpc.NpcName}</b></color>  {quest.talk}", 13f,
                    TextColor, FontStyles.Normal, wrap: true).name = "Talk";
        if (!string.IsNullOrEmpty(quest.description))
            NewText(entryGO.transform, quest.description, 13f, CuteUIKit.CocoaSoft, FontStyles.Normal, wrap: true).name = "Desc";
        if (!string.IsNullOrEmpty(quest.tutorialTip))
            NewText(entryGO.transform, quest.tutorialTip, 13f, TextColor, wrap: true).name = "Tip";

        if (quest.rewards.Count > 0 || quest.rewardCoins > 0)
            BuildRewardRow(entryGO.transform, quest);
    }

    private void BuildRewardRow(Transform parent, QuestDefinition quest, float iconSize = 22f)
        => BuildRewardRow(parent, quest.rewards, quest.rewardCoins, iconSize);

    private void BuildRewardRow(Transform parent, List<QuestReward> rewards, int rewardCoins, float iconSize)
    {
        var rowGO = new GameObject("Rewards", typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing               = 5f;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight= false;
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childAlignment        = TextAnchor.MiddleLeft;
        rowGO.AddComponent<LayoutElement>().preferredHeight = iconSize + 8f;

        var tag = CuteUIKit.Pill(rowGO.transform, "보상", CuteUIKit.ButtonButter,
                                 CuteUIKit.TextOn(CuteUIKit.ButtonButter), 11f, 9, new RectOffset(8, 8, 2, 2), out _);
        ApplyFont(tag);

        foreach (var reward in rewards)
            if (reward.item != null) BuildRewardChip(rowGO.transform, reward.item, reward.amount, iconSize);
        if (rewardCoins > 0) BuildRewardChip(rowGO.transform, null, rewardCoins, iconSize);
    }

    private void BuildRewardChip(Transform parent, FarmingItemData item, int amount, float iconSize)
    {
        var chip = CuteUIKit.Card(parent, CuteUIKit.Cream, 11, "Chip");
        chip.raycastTarget = false;
        var chipHLG = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipHLG.padding               = new RectOffset(4, 8, 2, 2);
        chipHLG.spacing               = 3f;
        chipHLG.childForceExpandWidth = false;
        chipHLG.childForceExpandHeight= false;
        chipHLG.childControlWidth     = true;
        chipHLG.childControlHeight    = true;
        chipHLG.childAlignment        = TextAnchor.MiddleLeft;

        var icon = NewIcon(chip.transform, item != null ? item.icon : CuteUIKit.Coin(), item != null ? iconSize : iconSize * 0.8f);
        icon.enabled = icon.sprite != null;

        NewText(chip.transform, item != null ? $"×{amount}" : CurrencyManager.Format(amount), 12f,
                item != null ? TextColor : CuteUIKit.AmberText, FontStyles.Bold).name = "Amount";
    }

    // 평소 퀘스트는 패널에서만 바뀌고, 큰 카드는 장이 시작될 때만
    private void EnqueueChapter(QuestDefinition def)
    {
        var chain = QuestChain.Load();
        if (def == null || chain == null || chain.ChapterOf(def, out int step) < 0 || step != 0) return;
        if (cardRT == null) BuildCard();
        chapterQueue.Enqueue(def);
        if (cardCo == null) cardCo = StartCoroutine(ChapterLoop());
    }

    private IEnumerator ChapterLoop()
    {
        yield return null;
        while (chapterQueue.Count > 0)
        {
            while (CardBlocked) yield return null;

            var def = chapterQueue.Dequeue();
            // 이미 지나간 장은 건너뜀
            if (QuestManager.Instance != null && QuestManager.Instance.CurrentQuest != def) continue;

            cardRT.sizeDelta = new Vector2(600f, 0f);
            FillChapterCard(def);
            cardRT.gameObject.SetActive(true);
            cardRT.anchoredPosition = CardHome;
            cardRT.localScale = Vector3.one;
            skipCard = false;
            Sfx.Play(SfxId.UiOpen, 0.8f);
            yield return CuteUIKit.PopIn(cardRT, cardCG, 0.3f);

            for (float t = 0f; t < ChapterHold && !(skipCard && t > 0.35f); t += Time.unscaledDeltaTime)
            {
                if (LevelUpUI.IsOpen || PlayerTileController.IsAnyUIOpen()) break;
                yield return null;
            }

            yield return CuteUIKit.PopOut(cardRT, cardCG, 0.18f);
            cardRT.gameObject.SetActive(false);
        }
        cardCo = null;
    }

    private static readonly Vector2 CardHome = new(0f, -118f);

    private static bool CardBlocked => TutorialUI.IsActive || LevelUpUI.IsOpen || PlayerTileController.IsAnyUIOpen();

    private void ShowDone(QuestDefinition def)
    {
        // 연달아 끝나면 한 줄로 합침
        if (doneCo != null) StopCoroutine(doneCo);
        else { doneCount = doneCoins = 0; doneItems.Clear(); }

        doneCount++;
        doneCoins += def.rewardCoins;
        foreach (var r in def.rewards)
        {
            if (r.item == null) continue;
            var same = doneItems.Find(x => x.item == r.item);
            if (same != null) same.amount += r.amount;
            else doneItems.Add(new QuestReward { item = r.item, amount = r.amount });
        }

        doneText.text = doneCount > 1 ? $"퀘스트 {doneCount}개 완료!" : $"{def.questName} 완료!";
        for (int i = doneRT.childCount - 1; i >= 0; i--)
        {
            var c = doneRT.GetChild(i).gameObject;
            if (c == doneText.gameObject) continue;
            c.SetActive(false);
            Destroy(c);
        }
        foreach (var r in doneItems) BuildRewardChip(doneRT, r.item, r.amount, 20f);
        if (doneCoins > 0) BuildRewardChip(doneRT, null, doneCoins, 20f);

        doneCo = StartCoroutine(DoneRoutine());
    }

    private IEnumerator DoneRoutine()
    {
        doneRT.gameObject.SetActive(true);
        for (float t = doneCG.alpha * 0.2f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            doneCG.alpha = t / 0.2f;
            yield return null;
        }
        doneCG.alpha = 1f;
        yield return new WaitForSecondsRealtime(DoneHold);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            doneCG.alpha = 1f - t / 0.4f;
            yield return null;
        }
        doneCG.alpha = 0f;
        doneRT.gameObject.SetActive(false);
        doneCo = null;
    }

    private void FillChapterCard(QuestDefinition def)
    {
        ClearCardBody();
        var chain = QuestChain.Load();
        int ch = chain != null ? chain.ChapterOf(def, out _) : -1;
        if (ch < 0) return;
        var chapter = chain.chapters[ch];

        var head = Row(cardBody, TextAnchor.MiddleCenter, 8f);
        var ribbon = CuteUIKit.TitleRibbon(head, ch == 0 ? "모험 시작!" : "새로운 장", null, 15f);
        ApplyFont(ribbon);
        var step = CuteUIKit.Pill(head, $"{ch + 1}장 / {chain.chapters.Count}장", CuteUIKit.LatteLight, CuteUIKit.Cocoa, 12f, 10,
                                  new RectOffset(9, 9, 3, 3), out _);
        ApplyFont(step);

        var title = NewText(cardBody, $"{ch + 1}장 · {chapter.title}", 30f, CuteUIKit.Strawberry, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Center;

        var stages = CoreStageTable.Load();
        if (stages != null && ch + 2 <= stages.MaxLevel)
        {
            var row = Row(cardBody, TextAnchor.LowerCenter, 18f);
            StageFigure(row, stages.Stage(ch + 1), $"지금 · Lv.{ch + 1}", 1.6f, false);
            var arrow = NewText(row, ">>", 22f, CuteUIKit.Strawberry, FontStyles.Bold);
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.gameObject.AddComponent<LayoutElement>().minHeight = 60f;
            StageFigure(row, stages.Stage(ch + 2), $"목표 · Lv.{ch + 2}", 1.6f, true);
        }

        if (!string.IsNullOrEmpty(chapter.goal))
        {
            var goal = NewText(cardBody, $"목표 · {chapter.goal}", 16f, CuteUIKit.Cocoa, FontStyles.Bold, wrap: true);
            goal.alignment = TextAlignmentOptions.Center;
        }
        if (!string.IsNullOrEmpty(chapter.intro)) CocoRow(cardBody, chapter.intro, 66f, 16f);

        var skip = NewText(cardBody, Pad.Active ? "A: 넘기기" : "클릭하면 넘겨요 · 여정(L)에서 다시 볼 수 있어요", 11f, CuteUIKit.CocoaMuted);
        skip.alignment = TextAlignmentOptions.Center;
    }

    private void StageFigure(Transform parent, CoreStage stage, string caption, float scale, bool goal)
    {
        var col = new GameObject("Stage", typeof(RectTransform));
        col.transform.SetParent(parent, false);
        var vl = col.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 4f;
        vl.childAlignment = TextAnchor.LowerCenter;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = vl.childForceExpandHeight = false;

        var sprite = stage != null ? stage.spriteOn : null;
        if (sprite != null) NewIcon(col.transform, sprite, sprite.rect.height * scale).GetComponent<LayoutElement>().preferredWidth = sprite.rect.width * scale;
        var cap = NewText(col.transform, caption, 12f, CuteUIKit.CocoaSoft, FontStyles.Bold);
        cap.alignment = TextAlignmentOptions.Center;
        var name = NewText(col.transform, stage != null ? stage.title : "", 16f, goal ? CuteUIKit.Strawberry : CuteUIKit.Cocoa, FontStyles.Bold);
        name.alignment = TextAlignmentOptions.Center;
    }

    private void CocoRow(Transform parent, string line, float portrait, float fontSize, bool stretch = true)
        => CocoUI.Row(parent, line, portrait, fontSize, stretch, fontAsset);

    private void ClearCardBody()
    {
        for (int i = cardBody.childCount - 1; i >= 0; i--)
        {
            var c = cardBody.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }
    }

    private void BuildCard()
    {
        var root = new GameObject("QuestCardCanvas", typeof(RectTransform));
        UIStyleKit.CreateOverlayCanvas(root, 440).GetComponent<GraphicRaycaster>().enabled = false;

        var go = new GameObject("QuestCard", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        cardRT = (RectTransform)go.transform;
        cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 1f);
        cardRT.anchoredPosition = CardHome;
        cardRT.sizeDelta = new Vector2(520f, 0f);
        go.AddComponent<Image>().raycastTarget = false;
        CuteUIKit.DecorateWindow(cardRT, catEars: true, earInset: 90f);
        cardCG = go.AddComponent<CanvasGroup>();
        cardCG.blocksRaycasts = false;
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(22, 22, 16, 14);
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(go.transform, false);
        var bvl = body.AddComponent<VerticalLayoutGroup>();
        bvl.spacing = 8f;
        bvl.childControlWidth = bvl.childControlHeight = true;
        bvl.childForceExpandWidth = true;
        bvl.childForceExpandHeight = false;
        cardBody = body.transform;
        foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        cardRT.gameObject.SetActive(false);
    }

    // 새 퀘스트: 패널이 한 번만 은은하게
    private IEnumerator NewQuestGlow()
    {
        const float duration = 1.1f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetCardColors(Color.Lerp(CuteUIKit.Cream, CuteUIKit.ButterSoft, Mathf.Sin(t / duration * Mathf.PI)), CuteUIKit.Latte);
            yield return null;
        }
        SetCardColors(CuteUIKit.Cream, CuteUIKit.Latte);
    }

    private void SetCardColors(Color fill, Color border)
    {
        if (cardFill   != null) cardFill.color   = fill;
        if (cardBorder != null) cardBorder.color = border;
    }

    private void BuildPanel()
    {
        panelRT = GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 1f);
        panelRT.anchorMax        = new Vector2(0f, 1f);
        panelRT.pivot            = new Vector2(0f, 1f);
        panelRT.anchoredPosition = new Vector2(20f, -20f);
        panelRT.sizeDelta        = new Vector2(PanelWidth, 0f);

        CuteUIKit.DecorateWindow(panelRT, catEars: false, radius: CardRadius, border: 2);
        cardBorder = FindLayerImage("Cute_Border");
        cardFill   = FindLayerImage("Cute_Fill");

        panelCG = gameObject.AddComponent<CanvasGroup>();

        var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding               = new RectOffset(14, 14, 10, 12);
        vlg.spacing               = 5f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight= false;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = true;

        var csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildHeader();
        goalText = NewText(transform, "", 13f, CuteUIKit.CocoaSoft, FontStyles.Normal, wrap: true);
        goalText.name = "ChapterGoal";
        goalText.gameObject.SetActive(false);

        questNameText = NewText(transform, "퀘스트 로딩 중", 20f, QuestNameBaseColor, FontStyles.Bold, wrap: true);
        questNameText.name = "QuestName";

        var containerGO = new GameObject("Objectives", typeof(RectTransform));
        containerGO.transform.SetParent(transform, false);
        var cvlg = containerGO.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing               = 4f;
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight= false;
        cvlg.childControlWidth     = true;
        cvlg.childControlHeight    = true;
        containerGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        objectiveContainer = containerGO.transform;

        BuildHintRow();
        BuildLogSection();
        BuildDoneStrip();
    }

    private void BuildHeader()
    {
        var headerGO = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(transform, false);
        var hlg = headerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        chapterText = NewText(headerGO.transform, "진행 중인 퀘스트", 13f, CuteUIKit.CocoaSoft, FontStyles.Bold);
        chapterText.name = "Chapter";

        var spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(headerGO.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var step = CuteUIKit.Pill(headerGO.transform, "1/1", CuteUIKit.LatteLight, CuteUIKit.Cocoa, 12f, 9,
                                  new RectOffset(8, 8, 2, 3), out stepText);
        ApplyFont(step);
        step.gameObject.SetActive(false);

        var hit = headerGO.AddComponent<Image>();
        hit.color = Color.clear;
        var btn = headerGO.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(JourneyUI.Toggle);
        headerGO.AddComponent<UiButtonSfx>();
        hlg.spacing = 6f;

        // 모바일은 손가락 크기로
        var toggle = CuteUIKit.Pill(headerGO.transform, ToggleLabel(false), CuteUIKit.ButtonLatte, CuteUIKit.Cocoa, 12f, 9,
                                    MobileMode.Active ? new RectOffset(16, 16, 8, 9) : new RectOffset(8, 8, 2, 3), out toggleBtnText);
        toggle.name = "ToggleBtn";
        ApplyFont(toggle);
        toggle.GetComponent<Image>().raycastTarget = true;
        var toggleButton = toggle.gameObject.AddComponent<Button>();
        toggleButton.transition = Selectable.Transition.None;
        toggleButton.onClick.AddListener(ToggleLog);
        toggle.gameObject.AddComponent<UiButtonSfx>();
        toggle.gameObject.AddComponent<CutePop>();
    }

    private void BuildHintRow()
    {
        hintRow = new GameObject("Hint", typeof(RectTransform));
        hintRow.transform.SetParent(transform, false);
        var vlg = hintRow.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(2, 2, 2, 0);
        vlg.spacing = 5f;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        hintText = NewText(hintRow.transform, "", 15f, CuteUIKit.Cocoa, FontStyles.Bold, wrap: true);
        hintText.name = "HintText";
        hintText.richText = true;
        hintText.lineSpacing = 4f;

        whyText = NewText(hintRow.transform, "", 12f, CuteUIKit.CocoaSoft, FontStyles.Normal, wrap: true);
        whyText.name = "WhyText";
        whyText.richText = true;

        shortcutBtn = CuteUIKit.Button(hintRow.transform, "바로가기", CuteUIKit.ButtonMint, OnShortcut, -1f, 26f, 13f);
        shortcutBtn.name = "Shortcut";
        shortcutText = shortcutBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (fontAsset != null) shortcutText.font = fontAsset;
        shortcutBtn.gameObject.SetActive(false);
        hintRow.SetActive(false);
    }

    private void BuildLogSection()
    {
        logSectionGO = new GameObject("QuestLog", typeof(RectTransform));
        logSectionGO.transform.SetParent(transform, false);
        logSectionGO.SetActive(false);

        var logBg = logSectionGO.AddComponent<Image>();
        CuteUIKit.SetRounded(logBg, 12, CuteUIKit.LatteLight);
        logBg.raycastTarget = false;

        var vlg = logSectionGO.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight= false;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = true;
        logSectionGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        logContentRT = logSectionGO.transform;
    }

    // 완료 알림: 패널 바로 아래 한 줄
    private void BuildDoneStrip()
    {
        var card = CuteUIKit.Card(transform, CuteUIKit.MintTag, 12, "QuestDone");
        card.raycastTarget = false;
        card.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        doneRT = card.rectTransform;
        doneRT.anchorMin = doneRT.anchorMax = Vector2.zero;
        doneRT.pivot = new Vector2(0f, 1f);
        doneRT.anchoredPosition = new Vector2(0f, -8f);

        var hlg = card.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(11, 9, 5, 6);
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var fit = card.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        doneCG = card.gameObject.AddComponent<CanvasGroup>();
        doneCG.blocksRaycasts = false;
        doneCG.alpha = 0f;
        doneText = NewText(card.transform, "", 14f, CuteUIKit.MintText, FontStyles.Bold);
        doneText.name = "DoneText";
        doneRT.gameObject.SetActive(false);
    }

    private void AddRow()
    {
        var row = new ObjectiveRow();

        row.bg = CuteUIKit.Card(objectiveContainer, Color.clear, 10, "ObjRow");
        row.bg.raycastTarget = false;
        row.root = row.bg.gameObject;
        var hlg = row.root.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(2, 4, 3, 3);
        hlg.spacing                = 7f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        row.icon = NewIcon(row.root.transform, null, 24f);

        var col = new GameObject("Col", typeof(RectTransform));
        col.transform.SetParent(row.root.transform, false);
        col.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var cvl = col.AddComponent<VerticalLayoutGroup>();
        cvl.spacing = 3f;
        cvl.childControlWidth = cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true;
        cvl.childForceExpandHeight = false;

        var line = Row(col.transform, TextAnchor.MiddleLeft, 6f);
        row.label = NewText(line, "", 15f, TextColor);
        row.label.name         = "Label";
        row.label.overflowMode = TextOverflowModes.Ellipsis;
        row.label.richText     = true;
        row.label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        row.count = NewText(line, "", 14f, TextColor, FontStyles.Bold);
        row.count.name      = "Count";
        row.count.alignment = TextAlignmentOptions.Right;
        row.count.gameObject.AddComponent<LayoutElement>().minWidth = 44f;

        var barFill = CuteUIKit.Bar(col.transform, 6f, CuteUIKit.LatteLight, ProgressColor);
        row.fill = barFill.GetComponent<Image>();
        CuteUIKit.SetBar(barFill, 0f);

        rows.Add(row);
    }

    private void RemoveLastRow()
    {
        if (rows.Count == 0) return;
        var last = rows[rows.Count - 1];
        Destroy(last.root);
        rows.RemoveAt(rows.Count - 1);
    }

    private void ClearRows()
    {
        while (rows.Count > 0) RemoveLastRow();
    }

    private TextMeshProUGUI NewText(Transform parent, string text, float size, Color color,
                                    FontStyles style = FontStyles.Normal, bool wrap = false)
    {
        var t = CuteUIKit.Text(parent, text, size, color, style, TextAlignmentOptions.Left);
        if (wrap) t.textWrappingMode = TextWrappingModes.Normal;
        if (fontAsset != null) t.font = fontAsset;
        return t;
    }

    private static Image NewIcon(Transform parent, Sprite sprite, float size)
    {
        var go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;
        img.enabled        = sprite != null;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = size;
        le.minWidth = le.minHeight = size;
        return img;
    }

    private static Transform Row(Transform parent, TextAnchor align, float spacing)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = spacing;
        hlg.childAlignment         = align;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        return go.transform;
    }

    private void ApplyFont(Component root)
    {
        if (fontAsset == null) return;
        foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true)) t.font = fontAsset;
    }

    private Image FindLayerImage(string layerName)
    {
        var t = transform.Find(layerName);
        return t != null ? t.GetComponent<Image>() : null;
    }

    [ContextMenu("Rebuild UI")]
    public void RebuildUI()
    {
        rows.Clear();
        objectiveContainer = null;
        questNameText = null;
        panelCG = null;
        cardFill = cardBorder = null;
        logSectionGO = null;
        logContentRT = null;
        logOpen = false;
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        foreach (var fx in GetComponents<BaseMeshEffect>()) DestroyImmediate(fx);
        var img = GetComponent<Image>();              if (img) DestroyImmediate(img);
        var vlg = GetComponent<VerticalLayoutGroup>();if (vlg) DestroyImmediate(vlg);
        var cg  = GetComponent<CanvasGroup>();        if (cg)  DestroyImmediate(cg);
        var csf = GetComponent<ContentSizeFitter>(); if (csf) DestroyImmediate(csf);
        BuildPanel();
    }
}
