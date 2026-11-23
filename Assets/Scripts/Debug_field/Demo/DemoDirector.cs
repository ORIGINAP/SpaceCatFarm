using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 졸업 전시 데모 — 메인 메뉴 [전시 데모]로 저장 안 하는 새 월드에서 차례대로 시연 (한 바퀴 약 7분, 설명 화면은 DemoPanel)
public class DemoDirector : MonoBehaviour
{
    private const int   DemoSeed      = 2024;
    private const float AutoHold      = 3f;
    private const float EndingRestart = 20f;
    // 데모 걷기: 이보다 멀면 순간이동
    private const float WalkSpeed     = 4.5f;
    private const float WalkMax       = 14f;

    // 장면: 갈래 · 아이콘 · 제목 · 한 줄 설명 · 보여 줄 기능들
    private class Step
    {
        public int    pillar;
        public string icon, title, lead;
        public (string icon, string name, string detail)[] points;
        public System.Func<IEnumerator> run;
    }

    private static DemoDirector instance;
    // 처음부터 다시 할 때 자동 재생 유지
    private static bool keepAuto;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        keepAuto = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // 메뉴로 나가면 데모 끝
        if (s.name == "MainMenu")
        {
            GameLaunchContext.DemoMode = false;
            keepAuto = false;
        }
        TryCreate();
    }

    private static void TryCreate()
    {
        if (!GameLaunchContext.DemoMode || instance != null || GridManager.Instance == null) return;
        instance = new GameObject("DemoDirector").AddComponent<DemoDirector>();
    }

    // 메뉴에서 시작 (저장 안 하는 미들 월드, 시드 고정)
    public static void Launch()
    {
        DemoPanel.CaptureLogo();
        GameLaunchContext.DemoMode          = true;
        GameLaunchContext.SelectedSlot      = 0;
        GameLaunchContext.IsNewGame         = true;
        GameLaunchContext.LaunchedFromMenu  = false;
        GameLaunchContext.PendingWorldSize  = WorldSize.Middle;
        GameLaunchContext.PendingWorldTheme = WorldTheme.Wasteland;
        GameLaunchContext.PendingWorldSeed  = DemoSeed;
        SceneManager.LoadScene("InGame");
    }

    private readonly List<Step> steps = new();
    private int   index = -1;
    private bool  running, hurry, auto;
    private float restartAt = -1f;
    private Coroutine stepCo;
    private float baseFov = -1f;
    private DemoPanel panel;

    private GridManager Gm => GridManager.Instance;
    private Transform   PlayerT => PlayerTileController.Instance != null ? PlayerTileController.Instance.transform : null;

    private void Start()
    {
        Define();
        panel = DemoPanel.Build(transform, steps.Count, Restart, ToggleAuto, Next);
        auto = keepAuto;
        keepAuto = false;
        StartCoroutine(Boot());
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        StopWalking();
    }

    private IEnumerator Boot()
    {
        yield return new WaitForSeconds(0.6f);
        TutorialUI.CloseNow();
        Next();
    }

    // ── 진행 ─────────────────────────────────────────────────────────────

    private void Next()
    {
        if (running) { hurry = true; return; }
        if (index + 1 >= steps.Count) { Restart(); return; }
        index++;
        stepCo = StartCoroutine(RunStep(steps[index]));
    }

    private IEnumerator RunStep(Step s)
    {
        running = true;
        hurry = false;
        restartAt = -1f;
        // 첫 장면 앞에 게임 소개
        if (index == 0) yield return Overview();
        panel.Show(index, s.pillar, s.icon, s.title, s.lead, s.points);
        Sfx.Play(SfxId.UiOpen);
        yield return s.run();
        StopWalking();
        running = false;
        hurry = false;
        if (!auto) yield break;
        bool last = index + 1 >= steps.Count;
        float hold = last ? EndingRestart : AutoHold;
        if (last) restartAt = Time.unscaledTime + hold;
        yield return new WaitForSecondsRealtime(hold);
        if (auto) Next();
    }

    private void Restart()
    {
        keepAuto = auto;
        Launch();
    }

    private void Update()
    {
        if (GameInput.GetKeyDown(KeyCode.N) || GameInput.GetKeyDown(KeyCode.PageDown) || Pad.ActiveDown(PadButton.R3)) Next();
        if (GameInput.GetKeyDown(KeyCode.PageUp)) ToggleAuto();

        float now = Time.unscaledTime;
        panel.Status(running, auto, index + 1 >= steps.Count, auto && restartAt > 0f ? restartAt - now : -1f);
    }

    private void ToggleAuto()
    {
        auto = !auto;
        if (auto && !running) Next();
    }

    // 패널의 기능 줄에 불을 켬
    private void Check(int point) => panel.Check(point);

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !hurry; t += Time.deltaTime) yield return null;
    }

    // ── 도우미 ───────────────────────────────────────────────────────────

    private static FarmingItemData Item(string name) => NetWorld.Item(name);

    private TileItem Place(string itemName, Vector2Int cell)
    {
        var item = Item(itemName);
        if (item == null || item.tilePrefab == null || Gm == null) return null;
        return Gm.PlaceTile(cell, item.tilePrefab, item.tileWidth, item.tileHeight, 0, item);
    }

    // 근처 빈자리에 놓음
    private TileItem PlaceNear(string itemName, Vector2Int cell)
    {
        var item = Item(itemName);
        if (item == null || item.tilePrefab == null) return null;
        return Place(itemName, OutpostPlan.OpenCell(cell, item.tileWidth, item.tileHeight, item.tilePrefab));
    }

    private void Teleport(Vector2Int cell) => Teleport(Gm.GridToWorld(cell));

    private void Teleport(Vector3 world)
    {
        var p = PlayerT;
        if (p == null) return;
        p.position = new Vector3(world.x, world.y, p.position.z);
        LabFx.Dust(world, 0.8f);
    }

    private IEnumerator WalkTo(Vector2Int cell) => WalkTo(Gm.GridToWorld(cell));

    // 가까우면 걸어가고, 멀거나 막히면 순간이동
    private IEnumerator WalkTo(Vector3 world)
    {
        var p = PlayerT;
        var player = Player.Local;
        var input = InputManager.Instance;
        if (p == null) yield break;
        float dist = Vector2.Distance(p.position, world);
        if (player == null || input == null || hurry || dist > WalkMax) { Teleport(world); yield break; }

        float speed = player.moveSpeed;
        player.moveSpeed = Mathf.Max(speed, WalkSpeed);
        float limit = dist / player.moveSpeed + 1.5f, stuck = 0f;
        Vector2 last = p.position;
        for (float t = 0f; t < limit && !hurry; t += Time.deltaTime)
        {
            Vector2 d = world - p.position;
            if (d.magnitude < 0.12f) break;
            input.ScriptedMove = d.magnitude > 0.4f ? d.normalized : d / 0.4f;
            yield return null;
            stuck = Vector2.Distance(p.position, last) < 0.004f ? stuck + Time.deltaTime : 0f;
            last = p.position;
            if (stuck > 0.35f) break;
        }
        input.ScriptedMove = Vector2.zero;
        player.moveSpeed = speed;
        if (Vector2.Distance(p.position, world) > 0.6f) Teleport(world);
    }

    private static void StopWalking()
    {
        if (InputManager.Instance != null) InputManager.Instance.ScriptedMove = Vector2.zero;
    }

    private IEnumerator Fov(float target, float seconds = 1.2f)
    {
        var cam = FindFirstObjectByType<CinemachineCamera>();
        if (cam == null) yield break;
        if (baseFov < 0f) baseFov = cam.Lens.FieldOfView;
        float from = cam.Lens.FieldOfView;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            var lens = cam.Lens;
            lens.FieldOfView = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds));
            cam.Lens = lens;
            yield return null;
        }
        var l = cam.Lens;
        l.FieldOfView = target;
        cam.Lens = l;
    }

    private void Say(Vector2Int cell, string text, Color color, float size = 4f)
        => LabFx.FloatText(Gm.GridToWorld(cell) + Vector3.up * 0.8f, text, color, size);

    private static void Say(Vector3 world, string text, Color color, float size = 4f)
        => LabFx.FloatText(world + Vector3.up * 0.8f, text, color, size);

    private void Hotbar(int slot, string itemName, int count)
    {
        var item = Item(itemName);
        if (item != null) Inventory.Instance?.SetSlotDirect(slot, item, count);
    }

    private void Hold(int slot) => TilePlacementUI.Instance?.SelectSlot(slot);

    // 가방에 모자란 만큼 채움
    private static void Give(FarmingItemData item, int count)
    {
        var inv = Inventory.Instance;
        if (inv == null || item == null) return;
        int need = count - inv.GetCount(item);
        if (need > 0) inv.AddItem(item, need, false);
    }

    private static readonly System.Reflection.MethodInfo TryHarvest =
        typeof(PlayerTileController).GetMethod("TryHarvest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    private static bool Harvest(Vector2Int center)
        => TryHarvest != null && PlayerTileController.Instance != null
           && (bool)TryHarvest.Invoke(PlayerTileController.Instance, new object[] { center });

    // 시연 자리: 협곡 · 대지 · 광석을 치움 (다음 프레임에 사라짐)
    private IEnumerator ClearArea(Vector2Int min, Vector2Int max)
    {
        TerrainMap.Current?.ClearRect(min - Vector2Int.one, max + Vector2Int.one);
        var cs = Gm.CellSize;
        Vector3 a = Gm.GridToWorld(min) - Vector3.one * cs, c = Gm.GridToWorld(max) + Vector3.one * cs;
        foreach (var block in new List<ResourceBlock>(ResourceBlock.Active))
        {
            if (block == null) continue;
            var p = block.transform.position;
            if (p.x >= a.x - 1.2f && p.x <= c.x + 1.2f && p.y >= a.y - 1.2f && p.y <= c.y + 1.2f) Destroy(block.gameObject);
        }
        yield return null;
        yield return null;
        Physics2D.SyncTransforms();
    }

    private Vector2Int Core => CenterCore.Current != null ? CenterCore.Current.GridPosition : Vector2Int.zero;

    // 게임 시계를 빠르게 돌림 (하늘색이 따라 바뀜)
    private IEnumerator TimeLapse(int hour, float seconds)
    {
        var clock = GameTimeManager.Instance;
        if (clock == null) yield break;
        int day = clock.CurrentDay;
        int from = clock.CurrentHour * 60 + clock.CurrentMinute, to = hour * 60;
        if (to <= from) to += 24 * 60;
        for (float t = 0f; t < seconds && !hurry; t += Time.deltaTime)
        {
            SetClock(clock, day, Mathf.RoundToInt(Mathf.Lerp(from, to, t / seconds)));
            yield return null;
        }
        SetClock(clock, day, to);
    }

    private static void SetClock(GameTimeManager clock, int day, int minutes)
        => clock.RestoreTime(day + minutes / 1440, minutes / 60 % 24, minutes % 60);

    // ── 셔틀 · 주민 ──────────────────────────────────────────────────────

    private static ShuttleSave Shuttle(out ShuttlePad pad)
    {
        pad = null;
        var svc = ShuttleService.Instance;
        if (svc == null) return null;
        foreach (var s in svc.Shuttles)
        {
            pad = ShuttleService.PadOf(s);
            if (pad != null) return s;
        }
        return null;
    }

    // 셔틀이 바라는 짐을 가방에 채워 실어 보냄 (보낸 척 수)
    private static int SendShuttles()
    {
        var svc = ShuttleService.Instance;
        if (svc == null) return 0;
        int sent = 0;
        foreach (var s in new List<ShuttleSave>(svc.Shuttles))
        {
            if (s.away || s.cargo.Count == 0 || ShuttleService.PadOf(s) == null) continue;
            foreach (var line in s.cargo) Give(Item(line.item), line.Missing);
            svc.LoadFromBag(s);
            if (s.away) sent++;
        }
        return sent;
    }

    // 뛰어내리기 끝난 고양이 (선물이면 아직 선물 안 받은 고양이)
    private ResidentCat PickCat(bool forGift)
    {
        ResidentCat best = null;
        float bestD = float.MaxValue;
        foreach (var c in ResidentCat.All)
        {
            if (c == null || !c.Interactable) continue;
            if (forGift && c.Resident.giftedAt > 0) continue;
            float d = Vector2.Distance(c.transform.position, PlayerT.position);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    private IEnumerator WaitForCat(bool forGift, float seconds)
    {
        for (float t = 0f; t < seconds && !hurry && PickCat(forGift) == null; t += 0.25f)
            yield return new WaitForSeconds(0.25f);
    }

    // 고양이 곁으로 가서 말 걸기 · 선물
    private IEnumerator Visit(ResidentCat cat, FarmingItemData gift)
    {
        if (cat == null) yield break;
        var side = cat.transform.position.x >= PlayerT.position.x ? Vector3.left : Vector3.right;
        yield return WalkTo(cat.transform.position + side * 1.1f);
        if (cat == null) yield break;
        if (gift != null) Give(gift, 1);
        ResidentBond.Interact(cat, gift, PlayerT.position);
    }

    // ── 단계 ─────────────────────────────────────────────────────────────

    private void Define()
    {
        steps.Add(new Step
        {
            pillar = 0, icon = "core", title = "오염된 행성에 착륙",
            lead   = "고향별을 떠나 새 고향을 찾아왔어요",
            points = new[]
            {
                ("rocket",        "선발대 착륙", "보랏빛 안개 속 불모지"),
                ("core",          "에너지 코어", "하늘색 테두리 안만 살 수 있어요"),
                ("Item_Purifier", "오염 안개",   "정화해야 쓸 수 있어요"),
            },
            run = Opening,
        });
        steps.Add(new Step
        {
            pillar = 0, icon = "Item_Purifier", title = "땅 정화",
            lead   = "정화한 땅에만 지을 수 있어요",
            points = new[]
            {
                ("PlantTile",          "오염된 땅", "설치할 수 없어요"),
                ("Item_Purifier",      "정화탑",    "정화가 사각형으로 퍼져요"),
                ("Item_Seed_BlueStar", "개척",      "정화된 땅에 밭 짓기"),
            },
            run = Purification,
        });
        steps.Add(new Step
        {
            pillar = 1, icon = "Item_StarSickle", title = "농사와 도구",
            lead   = "도구 하나로 넓은 밭을 돌봐요",
            points = new[]
            {
                ("Item_Seed_BlueStar", "씨앗 심기",     "블루스타 · 크리스탈 베리"),
                ("Item_Sprinkler",     "스프링클 건",   "5×5칸 한 번에 물주기"),
                ("Item_DeepScanner",   "심우주 스캐너", "광맥 · 유적 탐지"),
                ("Item_StarSickle",    "별빛 낫",       "7×7칸 한 번에 수확"),
            },
            run = FarmTools,
        });
        steps.Add(new Step
        {
            pillar = 2, icon = "rocket", title = "셔틀이 데려온 첫 가족",
            lead   = "짐을 보내면 고양이 가족이 와요",
            points = new[]
            {
                ("letter", "고향별의 부탁", "셔틀마다 원하는 짐"),
                ("coin",   "짐 싣고 출발",  "짐값으로 코인"),
                ("cat",    "가족 도착",     "고양이가 주민이 돼요"),
            },
            run = FirstFamily,
        });
        steps.Add(new Step
        {
            pillar = 2, icon = "cat", title = "주민과 친해지기",
            lead   = "말 걸고 선물하면 친해져요",
            points = new[]
            {
                ("cat",               "말 걸기",       "매일 하트 +1"),
                ("BlueStar",          "좋아하는 선물", "하트 쑥 · 답례 선물"),
                ("Item_ResidentCamp", "주민 캠프",     $"잠자리 {ResidentCamp.Beds}칸"),
            },
            run = Friends,
        });
        steps.Add(new Step
        {
            pillar = 1, icon = "Item_CatKitchen", title = "고양이 주방",
            lead   = "요리하면 짐값도 호감도 올라요",
            points = new[]
            {
                ("Item_CatKitchen",   "고양이 주방", "전기로 자동 요리"),
                ("Item_CatnipCookie", "레시피",      "캣닢 2 + 블루스타 1 → 쿠키"),
                ("cat",               "요리 선물",   "주민이 제일 좋아해요"),
            },
            run = Kitchen,
        });
        steps.Add(new Step
        {
            pillar = 1, icon = "aurora", title = "밤하늘과 날씨",
            lead   = "날씨마다 특별한 일이 생겨요",
            points = new[]
            {
                ("aurora",                   "오로라",    "황금 작물 확률 UP"),
                ("Item_Golden_CrystalBerry", "작물 도감", "황금 작물 · 요리 수집"),
                ("meteor",                   "유성우",    "운석 광석이 떨어져요"),
                ("rain",                     "별비",      "모든 밭에 물주기"),
            },
            run = NightSky,
        });
        steps.Add(new Step
        {
            pillar = 3, icon = "Item_DeepScanner", title = "네 바이옴 탐험",
            lead   = "바이옴마다 나는 자원이 달라요",
            points = new[]
            {
                ("flag",             "네 바이옴", "황무지 · 혹한지 · 수정 · 습지"),
                ("Item_DeepScanner", "유적 탐지", "스캐너로 찾기"),
                ("letter",           "탐사 유적", "자원과 옛 기록"),
            },
            run = Explore,
        });
        steps.Add(new Step
        {
            pillar = 3, icon = "Item_CargoRail", title = "채굴 · 화물 레일 · 제련",
            lead   = "캐고, 나르고, 녹여요",
            points = new[]
            {
                ("T1_ResourceExtractor", "자동채굴기", "광맥 곁에서 계속 캐요"),
                ("Item_CargoRail",       "화물 레일",  "전초기지 → 본진"),
                ("Item_Smelter",         "제련소",     "광석 → 주괴"),
                ("hammer",               "제작대",     "주괴로 기계 만들기"),
            },
            run = Industry,
        });
        steps.Add(new Step
        {
            pillar = 2, icon = "Item_CommTower", title = "셔틀 함대",
            lead   = "통신탑마다 셔틀이 늘어요",
            points = new[]
            {
                ("Item_CommTower",  "통신탑",       "셔틀 +1척 (최대 3척)"),
                ("Item_ShuttlePad", "새 셔틀 도착", "새 착륙장으로"),
                ("rocket",          "함께 출발",    "여러 척이 함께 왕복"),
            },
            run = Fleet,
        });
        steps.Add(new Step
        {
            pillar = 4, icon = "Item_StarFountain", title = "마을 꾸미기와 축제",
            lead   = "꾸밀수록 마을이 활기차요",
            points = new[]
            {
                ("Item_StarLamp",     "꾸미기 상점", "분수 · 가로등 · 꽃밭"),
                ("Item_StarFountain", "마을 매력",   "매력만큼 짐값 UP"),
                ("Item_CatTower",     "축제",        "생산 속도 UP"),
            },
            run = Festival,
        });
        steps.Add(new Step
        {
            pillar = 4, icon = "flask", title = "연구",
            lead   = "연구할수록 농장이 강해져요",
            points = new[]
            {
                ("flask",         "작물 성장",        "더 빨리 자라요"),
                ("AutoHarvester", "자동화 연구",      "기계 속도 UP"),
                ("rocket",        "셔틀 · 대원 연구", "짐값 · 이동 속도"),
            },
            run = Research,
        });
        steps.Add(new Step
        {
            pillar = 4, icon = "core", title = "자라나는 에너지 코어",
            lead   = "주민이 늘면 코어가 자라요",
            points = new[]
            {
                ("cat",  "문명 레벨",     "주민이 늘면 UP"),
                ("core", "코어가 자라요", "레벨마다 모습이 바뀌어요"),
                ("flag", "여정",          "레벨마다 새 시설"),
            },
            run = CoreGrowth,
        });
        steps.Add(new Step
        {
            pillar = 5, icon = "AutoHarvester", title = "자동화 3단계",
            lead   = "같은 씨앗, 단계별 기계 속도 비교",
            points = new[]
            {
                ("AutoSeeder",    "1단계", "7×7칸 · 초당 0.8칸"),
                ("AutoSeeder_T2", "2단계", "14×14칸 · 초당 4칸"),
                ("AutoSeeder_T3", "3단계", "27×27칸 · 초당 24칸"),
            },
            run = Automation,
        });
        steps.Add(new Step
        {
            pillar = 0, icon = "Item_LaunchPad", title = "새 고향",
            lead   = "남은 고양이들이 모두 도착해요",
            points = new[]
            {
                ("Item_LaunchPad", "이주선 착륙장",  "마지막 시설"),
                ("ship",           "이주선 착륙",    "남은 고양이들이 도착"),
                ("cat",            "행성 이주 완료", "플레이해 주셔서 고마워요!"),
            },
            run = Ending,
        });
    }

    // 첫 장면 앞: 로고와 핵심 기능 여섯 갈래 (뒤로 행성이 멀어짐)
    private IEnumerator Overview()
    {
        Hotbar(0, "Item_StarSickle", 1);
        Hotbar(1, "Item_Sprinkler", 1);
        Hotbar(2, "Item_DeepScanner", 1);
        Hotbar(3, "Item_Purifier", 20);
        Hotbar(4, "PlantTile", 99);
        Hotbar(5, "Item_Seed_BlueStar", 99);
        Hotbar(6, "Item_Tier4_Drill", 1);
        CurrencyManager.Instance?.Add(50000);
        Teleport(Core + new Vector2Int(1, -4));
        panel.ShowOverview(false);
        StartCoroutine(Fov(96f, 3f));
        for (float t = 0f; t < 9f && !hurry; t += Time.deltaTime)
        {
            FoldChapterCard();
            yield return null;
        }
        panel.HideOverview();
        yield return Wait(0.5f);
        // 소개만 건너뛰고 1장면은 그대로
        hurry = false;
    }

    private static readonly System.Reflection.FieldInfo SkipCard =
        typeof(QuestUI).GetField("skipCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    // 게임의 '모험 시작' 장 카드는 소개 화면 뒤에 겹치니 바로 접음
    private static void FoldChapterCard()
    {
        var quest = QuestUI.Instance;
        if (quest != null && SkipCard != null) SkipCard.SetValue(quest, true);
    }

    private IEnumerator Opening()
    {
        yield return Wait(0.8f);
        Say(PlayerT.position, "선발대 착륙!", CuteUIKit.SkyText);
        Check(0);
        yield return Wait(2.6f);
        var core = CenterCore.Current;
        if (core != null)
        {
            LabFx.Ring(core.transform.position, CuteUIKit.SkyText, 3.2f, 1.1f);
            Say(core.transform.position + Vector3.up * 1.4f, "에너지 코어", CuteUIKit.SkyText, 4.6f);
        }
        Check(1);
        yield return Wait(2.8f);
        // 정화 테두리 밖은 보랏빛 오염 안개
        Say(Core + new Vector2Int(Purify.CoreRadius(CenterCore.CurrentLevel) + 4, 2), "오염 안개 — 정화해야 써요", CuteUIKit.LavenderText, 4.6f);
        Check(2);
        yield return Wait(3.2f);
    }

    private IEnumerator Purification()
    {
        int edge = Purify.CoreRadius(CenterCore.CurrentLevel) + 2;
        var stand = Core + new Vector2Int(edge - 3, -3);
        Teleport(stand);
        yield return Fov(70f);
        Hold(4);
        yield return Wait(1.2f);

        var outside = Core + new Vector2Int(edge + 4, 1);
        yield return ClearArea(Core + new Vector2Int(edge - 2, -3), outside + new Vector2Int(3, 3));
        if (Place("PlantTile", outside) == null)
        {
            Say(outside, "오염된 땅 — 설치 불가!", CuteUIKit.Strawberry);
            LabFx.Ring(Gm.GridToWorld(outside), CuteUIKit.Strawberry, 0.8f, 0.6f);
            Sfx.Play(SfxId.UiError);
            Check(0);
        }
        yield return Wait(2.5f);

        Hold(3);
        yield return Wait(0.6f);
        var tower = Core + new Vector2Int(edge + 1, -1);
        if (Place("Item_Purifier", OutpostPlan.OpenCell(tower, 2, 2)) != null)
        {
            Say(tower, "정화탑 설치!", CuteUIKit.Butter);
            Check(1);
        }
        yield return Wait(5.5f);

        Hold(4);
        int made = 0;
        for (int dx = 0; dx < 3; dx++)
        for (int dy = 0; dy < 3; dy++)
        {
            if (Place("PlantTile", outside + new Vector2Int(dx, dy)) != null) made++;
            yield return Wait(0.08f);
        }
        if (made > 0)
        {
            Say(outside, "정화된 땅엔 지을 수 있어요", CuteUIKit.MintText);
            Check(2);
        }
        yield return Wait(3f);
    }

    // 블루스타 밭 옆에 크리스탈 베리 밭 (퇴비 · 밤 장면에서 오로라 아래 수확)
    private Vector2Int FarmCenter  => Core + new Vector2Int(-8, -9);
    private Vector2Int BerryCenter => Core + new Vector2Int(-14, -9);

    private List<PlantTile> PlotSquare(Vector2Int center, int half)
    {
        var plots = new List<PlantTile>();
        for (int dy = -half; dy <= half; dy++)
        for (int dx = -half; dx <= half; dx++)
        {
            var cell = center + new Vector2Int(dx, dy);
            var t = Place("PlantTile", cell) as PlantTile ?? Gm.GetTile(cell) as PlantTile;
            if (t != null) plots.Add(t);
        }
        return plots;
    }

    private IEnumerator FarmTools()
    {
        var center = FarmCenter;
        yield return ClearArea(BerryCenter - new Vector2Int(3, 5), center + new Vector2Int(3, 3));
        Teleport(center + new Vector2Int(-3, -4));
        yield return Fov(62f);
        var seed = Item("Item_Seed_BlueStar");
        var plots = new List<PlantTile>();
        Hold(4);
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            var cell = center + new Vector2Int(dx, dy);
            var t = Place("PlantTile", cell) as PlantTile ?? Gm.GetTile(cell) as PlantTile;
            if (t != null) plots.Add(t);
            yield return Wait(0.03f);
        }
        yield return Wait(0.6f);
        Hold(5);
        foreach (var p in plots)
        {
            if (seed != null && seed.seedData != null) p.PlantSeed(seed.seedData);
            yield return Wait(0.04f);
        }

        // 별빛 작물은 밤에 잘 자람
        var berry = Item("Item_Seed_CrystalBerry");
        var compost = Item("Item_Compost");
        foreach (var p in PlotSquare(BerryCenter, 2))
        {
            if (compost != null) p.Fertilize(compost);
            if (berry != null && berry.seedData != null) p.PlantSeed(berry.seedData);
        }
        Say(BerryCenter + Vector2Int.up * 3, "크리스탈 베리 + 퇴비", CuteUIKit.LavenderText);
        Check(0);
        yield return Wait(0.8f);

        Hold(1);
        yield return Wait(0.5f);
        var sprinkler = Item("Item_Sprinkler");
        if (sprinkler != null && ToolActions.Water(sprinkler, center, PlayerT.position))
        {
            Say(center, "스프링클 건: 5×5칸 물주기!", CuteUIKit.SkyText);
            Check(1);
        }

        Hold(2);
        yield return Wait(2f);
        var scanner = Item("Item_DeepScanner");
        if (scanner != null && ToolActions.Scan(scanner, PlayerT.position)) Check(2);

        for (float t = 0f; t < 30f && !hurry; t += 0.25f)
        {
            if (plots.TrueForAll(p => p == null || p.CurrentState == PlantTile.PlantState.ReadyToHarvest)) break;
            yield return new WaitForSeconds(0.25f);
        }
        Hold(0);
        yield return WalkTo(center + new Vector2Int(0, -3));
        if (Harvest(center))
        {
            Say(center, "별빛 낫: 7×7칸 한 번에!", CuteUIKit.LavenderText);
            Check(3);
        }
        yield return Wait(3f);
    }

    private IEnumerator FirstFamily()
    {
        var s = Shuttle(out var pad);
        if (s == null || pad == null) yield break;
        yield return WalkTo(pad.transform.position + Vector3.down * 1.6f);
        yield return Fov(56f);

        // 고향별 편지와 짐 목록
        ShuttleUI.Ensure();
        ShuttleUI.Instance?.Open(pad);
        Check(0);
        yield return Wait(4f);
        ShuttleUI.Instance?.Close();
        yield return Wait(0.6f);

        if (SendShuttles() > 0)
        {
            Say(pad.transform.position, "짐 싣고 고향별로 출발!", CuteUIKit.Butter);
            Check(1);
        }
        yield return Wait(3.5f);
        // 왕복은 빨리 감기
        ShuttleService.Instance?.DebugArriveAll();
        yield return Wait(2.6f);
        Say(pad.transform.position, "가족이 도착했어요!", CuteUIKit.MintText);
        Check(2);
        yield return Wait(4.5f);
    }

    private IEnumerator Friends()
    {
        yield return WaitForCat(false, 4f);
        var cat = PickCat(false);
        if (cat == null) yield break;
        yield return Visit(cat, null);
        Check(0);
        yield return Wait(3.4f);

        var fav = ResidentBond.FavoriteCrop(cat.Resident) ?? Item("BlueStar");
        yield return Visit(cat, fav);
        if (cat != null) Say(cat.transform.position + Vector3.up * 0.8f, $"좋아하는 {fav.itemName} 선물!", CuteUIKit.Strawberry, 3.4f);
        // 이 주민이 좋아하는 작물로 바꿔 보여 줌
        panel.Note(1, $"{fav.itemName} 선물 — 하트 쑥", fav.icon);
        Check(1);
        yield return Wait(4.5f);

        // 주민 캠프: 잠자리
        Hotbar(3, "Item_ResidentCamp", 1);
        Hold(3);
        var item = Item("Item_ResidentCamp");
        var at = OutpostPlan.OpenCell(Core + new Vector2Int(-8, -4), 3, 3, item != null ? item.tilePrefab : null);
        yield return WalkTo(at + new Vector2Int(1, -2));
        if (Place("Item_ResidentCamp", at) != null)
        {
            Say(at + new Vector2Int(1, 3), $"주민 캠프 · 잠자리 {ResidentCamp.Beds}칸", CuteUIKit.Butter);
            Check(2);
        }
        yield return Wait(3.5f);
    }

    private IEnumerator Kitchen()
    {
        // 다음 가족은 요리하는 동안 다녀옴
        SendShuttles();
        Hotbar(3, "Item_CatKitchen", 1);
        Hold(3);
        var item = Item("Item_CatKitchen");
        var at = OutpostPlan.OpenCell(Core + new Vector2Int(-4, -5), 2, 2, item != null ? item.tilePrefab : null);
        yield return WalkTo(Gm.GridToWorldCenter(at, 2, 2) + Vector3.down * 1.9f);
        yield return Fov(56f);
        var kitchen = Place("Item_CatKitchen", at) as CatKitchen;
        var recipe = CatKitchen.FindRecipe("Recipe_CatnipCookie");
        if (kitchen == null || recipe == null) yield break;
        EnergyManager.Instance?.RefreshEnergyNetwork();
        Check(0);
        yield return Wait(0.8f);

        kitchen.SetRecipe(recipe);
        foreach (var ing in recipe.ingredients)
            if (ing.item != null) kitchen.InsertIngredient(ing.item, ing.amount * 2);
        Say(at + new Vector2Int(1, 2), "캣닢 2 + 블루스타 1 → 캣닢 쿠키", CuteUIKit.Cocoa, 3.4f);
        yield return Wait(1f);
        CatKitchenUI.Open(kitchen);
        Check(1);
        yield return Wait(4.5f);
        CatKitchenUI.Instance?.Close();

        for (float t = 0f; t < 14f && !hurry && kitchen != null && kitchen.OutputCount <= 0; t += 0.25f)
            yield return new WaitForSeconds(0.25f);
        FarmingItemData dish = null;
        if (kitchen != null && kitchen.TakeOutput(1, out dish) > 0)
        {
            Inventory.Instance?.AddItem(dish, 1, false);
            Say(at + new Vector2Int(1, 2), $"{dish.itemName} 완성!", CuteUIKit.Strawberry);
        }
        yield return Wait(1.5f);

        // 요리는 선물로 인기
        var cat = PickCat(true);
        if (cat != null)
        {
            yield return Visit(cat, dish ?? recipe.outputItem);
            Check(2);
            yield return Wait(3.5f);
        }
        ShuttleService.Instance?.DebugArriveAll();
        yield return Wait(3f);
    }

    private IEnumerator NightSky()
    {
        var weather = WeatherManager.Instance;
        Teleport(BerryCenter + new Vector2Int(0, -4));
        yield return Fov(64f);
        yield return TimeLapse(20, 3.5f);

        // 오로라 아래 별빛 작물 → 황금 작물
        weather?.ForceWeather(WeatherType.Aurora);
        Say(BerryCenter + Vector2Int.up * 3, "오로라!", CuteUIKit.MintText, 5f);
        yield return Wait(4.5f);
        Hold(0);
        yield return WalkTo(BerryCenter + new Vector2Int(0, -3));
        if (Harvest(BerryCenter))
        {
            Say(BerryCenter, "별빛 작물 + 오로라 = 황금 작물 확률 UP", CuteUIKit.Butter, 3.6f);
            Check(0);
        }
        yield return Wait(3.5f);
        // 처음 거둔 작물 · 요리 · 황금 작물은 도감에
        CodexUI.Toggle();
        if (CodexUI.IsOpen) Check(1);
        yield return Wait(4f);
        if (CodexUI.IsOpen) CodexUI.Toggle();
        yield return Wait(0.6f);

        // 유성우: 곁에 운석이 떨어짐
        weather?.ForceWeather(WeatherType.MeteorShower);
        yield return Wait(1.5f);
        var ore = OreData("RD_Diamond") ?? OreData("RD_Titanium");
        var meteors = new List<ResourceBlock>();
        for (int i = 0; i < 2 && ore != null; i++)
        {
            if (MeteorSpot(i, out var at)) DropMeteor(ore, at, meteors);
            yield return Wait(0.9f);
        }
        yield return Wait(1.6f);
        var rock = meteors.Find(b => b != null);
        if (rock != null)
        {
            Check(2);
            Hold(6);
            yield return WalkTo(rock.transform.position + Vector3.down * 1.1f);
            if (rock != null)
            {
                rock.StartMining();
                yield return Wait(3.5f);
                if (rock != null) rock.StopMining();
            }
        }

        // 다음 날 아침 별비
        yield return TimeLapse(7, 3f);
        weather?.ForceWeather(WeatherType.StarRain);
        Say(PlayerT.position, "별비 — 모든 밭에 물이 닿아요", CuteUIKit.SkyText);
        Check(3);
        yield return Wait(5f);
        weather?.ForceWeather(WeatherType.Clear);
    }

    private static ResourceData OreData(string name)
    {
        foreach (var r in GameDataRegistry.FindAll<ResourceData>())
            if (r != null && r.name == name) return r;
        return null;
    }

    // 플레이어 곁 빈자리 (왼쪽 · 오른쪽 번갈아)
    private bool MeteorSpot(int i, out Vector2 at)
    {
        at = default;
        var from = Gm.WorldToGrid(PlayerT.position);
        int side = i % 2 == 0 ? 1 : -1;
        for (int r = 4; r <= 9; r++)
        for (int dy = -2; dy <= 3; dy++)
        {
            var cell = from + new Vector2Int(side * r, dy);
            if (!Gm.CanPlaceAt(cell, 1, 1)) continue;
            Vector2 p = Gm.GridToWorld(cell);
            if (!ResourceBlock.FitsAt(p)) continue;
            at = p;
            return true;
        }
        return false;
    }

    private void DropMeteor(ResourceData ore, Vector2 at, List<ResourceBlock> into)
    {
        MeteorFall.Play(at, () =>
        {
            into.Add(ResourceBlock.CreateSaved(ore, at, PlayerT, 24));
            NotificationUI.Instance?.Show($"운석이 떨어졌어요! {ore.resourceName} 덩어리를 캐 보세요", NotificationUI.GoodNewsColor);
        });
    }

    private IEnumerator Explore()
    {
        var map = TerrainMap.Current;
        if (map == null) yield break;
        yield return Fov(84f);
        float half = Mathf.Min(Gm.GridWidth, Gm.GridHeight) * 0.5f;
        WorldTheme last = map.StartBiome;
        for (int i = 0; i < WorldThemeConfig.Count; i++)
        {
            var b = (WorldTheme)i;
            if (b == map.StartBiome) continue;
            Teleport(Vector2Int.RoundToInt(map.BiomeDirection(b) * half * 0.5f));
            last = b;
            Check(0);
            yield return Wait(4.5f);
        }

        BiomeLandmark target = null;
        float best = float.MaxValue;
        foreach (var l in BiomeLandmark.All)
        {
            if (l == null || l.IsOpened || l.Biome != last) continue;
            float d = Vector2.Distance(l.transform.position, PlayerT.position);
            if (d < best) { best = d; target = l; }
        }
        if (target == null) target = BiomeLandmark.Nearest(PlayerT.position, true);
        if (target != null)
        {
            var dir = ((Vector2)(target.transform.position - PlayerT.position)).normalized;
            Teleport(target.transform.position - (Vector3)(dir * 7f));
            Hold(2);
            yield return Wait(1f);
            var scanner = Item("Item_DeepScanner");
            if (scanner != null && ToolActions.Scan(scanner, PlayerT.position)) Check(1);
            yield return Wait(3f);
            yield return WalkTo(target.transform.position + Vector3.down * 1.1f);
            yield return Wait(0.5f);
            if (target.TryOpen(PlayerT.position)) Check(2);
            yield return Wait(4.5f);
        }
        Teleport(Core + new Vector2Int(1, -4));
        yield return Wait(1f);
    }

    // 광맥 → 자동채굴기 → 전초기지 창고 → 화물 레일 → 본진 창고 ↔ 제련소
    private IEnumerator Industry()
    {
        var ore = FarOre(24f, 34f) ?? FarOre(18f, 48f);
        if (ore == null) yield break;
        var oreItem = ore.DropItem;
        var bar = Smelter.RecipeFor(oreItem) != null ? Smelter.RecipeFor(oreItem).outputItem : null;
        var oreCell = Gm.WorldToGrid(ore.transform.position);
        var home = Core + new Vector2Int(2, 2);
        Vector2 dir = ((Vector2)(oreCell - home)).normalized;
        var side = new Vector2(-dir.y, dir.x);

        // 드릴로 직접 캐기
        Hold(6);
        Teleport(ore.transform.position + Vector3.down * 1.1f);
        yield return Fov(64f);
        ore.StartMining();
        Say(ore.transform.position + Vector3.up * 0.5f, "드릴로 직접 캐기", CuteUIKit.Cocoa, 3.4f);
        yield return Wait(2.6f);
        if (ore != null) ore.StopMining();

        // 자동채굴기와 전초기지 창고
        var exCell = OutpostPlan.OpenCell(oreCell + Vector2Int.RoundToInt(side * 2f), 1, 1);
        var extractor = Place("T1_ResourceExtractor", exCell) as ResourceExtractor;
        var depot = PlaceNear("ResourceStorage", oreCell + Vector2Int.RoundToInt(side * 2f + dir * 2f)) as ItemStorage;
        if (extractor != null)
        {
            Say(exCell, "자동채굴기", CuteUIKit.Butter, 3.4f);
            Check(0);
        }
        if (extractor != null && depot != null) TileItem.Link(extractor, depot);
        // 화물이 바로 움직이게 미리 채움
        if (depot != null) depot.AddItem(oreItem, 120);
        yield return Wait(2.2f);

        // 본진 창고와 제련소 (레일 끝)
        var hub = PlaceNear("Item_InfiniteStorage", home + Vector2Int.RoundToInt(dir * 10f)) as ItemStorage;
        var smelter = hub != null ? PlaceNear("Item_Smelter", hub.GridPosition + Vector2Int.RoundToInt(side * 3f)) as Smelter : null;
        if (hub != null && smelter != null) TileItem.Link(smelter, hub);

        // 레일 기둥: 전초기지 → 본진
        if (depot != null && hub != null)
        {
            Vector2 a = depot.GridPosition, b = hub.GridPosition;
            float length = Vector2.Distance(a, b);
            int posts = Mathf.Max(2, Mathf.CeilToInt((length - 6f) / 9f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float k = posts == 1 ? 0.5f : Mathf.Lerp(3f / length, 1f - 3f / length, i / (float)(posts - 1));
                var cell = OutpostPlan.OpenCell(Vector2Int.RoundToInt(Vector2.Lerp(a, b, k)), 1, 1);
                yield return WalkTo(cell + Vector2Int.down * 2);
                if (Place("Item_CargoRail", cell) != null) LabFx.Burst(Gm.GridToWorld(cell), CuteUIKit.SkyText, 10, 2.2f, 0.7f);
                yield return Wait(0.5f);
            }
            Say(hub.GridPosition + new Vector2Int(1, 3), "화물 레일 연결!", CuteUIKit.SkyText);
            Check(1);
        }

        // 화물이 굴러오고 주괴가 나옴
        if (hub != null && depot != null)
        {
            yield return WalkTo(Vector2.Lerp(Gm.GridToWorld(hub.GridPosition), Gm.GridToWorld(depot.GridPosition), 0.3f));
            yield return Fov(62f);
            Say(hub.transform.position + Vector3.up * 0.6f, "본진 창고", CuteUIKit.LavenderText, 3.4f);
        }
        bool smelted = false;
        for (float t = 0f; t < 11f && !hurry; t += 0.25f)
        {
            if (!smelted && smelter != null && hub != null && bar != null && (smelter.OutputCount > 0 || hub.GetCount(bar) > 0))
            {
                smelted = true;
                Say(smelter.transform.position, $"제련소: {oreItem.itemName} → {bar.itemName}", CuteUIKit.Strawberry, 3.4f);
                Check(2);
            }
            yield return new WaitForSeconds(0.25f);
        }

        // 주괴로 기계 · 도구 제작
        var craft = CraftingTableUI.Instance;
        if (craft != null)
        {
            craft.Open();
            Check(3);
            yield return Wait(4f);
            if (craft.IsOpen) craft.Toggle();
        }
        yield return Wait(0.8f);
    }

    // 북쪽 자동화 자리를 피한, 녹일 수 있는 먼 광맥 (동쪽 우선)
    private ResourceBlock FarOre(float min, float max)
    {
        Vector2 home = Gm.GridToWorld(Core + new Vector2Int(2, 2));
        ResourceBlock best = null;
        float bestScore = float.MaxValue;
        foreach (var b in ResourceBlock.Active)
        {
            if (b == null || b.Data == null || b.Data.minToolGrade > 1 || b.RemainingResources <= 0) continue;
            if (Smelter.RecipeFor(b.DropItem) == null) continue;
            var cell = Gm.WorldToGrid(b.transform.position);
            if (cell.y > Core.y + 1) continue;
            float d = Vector2.Distance(b.transform.position, home) / Gm.CellSize;
            if (d < min || d > max) continue;
            float score = Mathf.Abs(d - 27f) + (cell.x < Core.x ? 6f : 0f);
            if (score < bestScore) { bestScore = score; best = b; }
        }
        return best;
    }

    // 통신탑마다 셔틀 한 척 더: 새 착륙장에 셔틀이 날아오면 한꺼번에 보냄
    private IEnumerator Fleet()
    {
        Shuttle(out var first);
        var row = first != null ? first.GridPosition : Core + new Vector2Int(5, -4);
        yield return WalkTo(Gm.GridToWorld(row + new Vector2Int(4, -4)));
        yield return Fov(68f);
        // 문명 Lv.2
        var core = CenterCore.Current;
        if (core != null && core.Level < 2)
        {
            core.ForceAdvance();
            yield return Wait(3f);
            LevelUpUI.HideNow();
        }

        Hotbar(3, "Item_CommTower", 2);
        Hold(3);
        for (int i = 1; i <= 2; i++)
        {
            var tower = PlaceNear("Item_CommTower", row + new Vector2Int(3 * i, 3));
            if (tower != null)
            {
                Say(tower.transform.position, "통신탑", CuteUIKit.SkyText, 3.4f);
                Check(0);
            }
            yield return Wait(0.5f);
            PlaceNear("Item_ShuttlePad", row + new Vector2Int(3 * i, 0));
            yield return Wait(0.5f);
        }
        EnergyManager.Instance?.RefreshEnergyNetwork();
        Say(Gm.GridToWorld(row + new Vector2Int(4, 2)), "새 셔틀이 날아와요", CuteUIKit.Butter);
        yield return Wait(ShuttleService.ApproachSeconds + 1.5f);
        Check(1);

        int sent = SendShuttles();
        if (sent > 0)
        {
            Say(Gm.GridToWorld(row + new Vector2Int(4, 2)), $"셔틀 {sent}척 동시 출발!", CuteUIKit.Butter);
            panel.Note(2, $"셔틀 {sent}척 동시 출발");
            Check(2);
        }
        yield return Wait(3f);
        ShuttleService.Instance?.DebugArriveAll();
        yield return Wait(6.5f);
    }

    // 장식 자리 (코어 왼쪽 아래 기준)
    private static readonly (string item, int x, int y)[] Decor =
    {
        ("Item_StarFountain", -4,  1),
        ("Item_StarLamp",     -1, -2),
        ("Item_FlowerBed",     0, -2),
        ("Item_FlowerBed",     3, -2),
        ("Item_StarLamp",      4, -2),
        ("Item_CatTower",      5,  2),
        ("Item_CatBench",      5,  0),
        ("Item_StarLamp",     -1,  5),
        ("Item_FlowerBed",     0,  5),
        ("Item_FlowerBed",     3,  5),
        ("Item_StarLamp",      4,  5),
        ("Item_YarnPark",     -4,  4),
        ("Item_GoldenCat",    -7,  2),
    };

    private IEnumerator Festival()
    {
        var core = CenterCore.Current;
        yield return WalkTo(Core + new Vector2Int(1, -4));
        yield return Fov(66f);
        // 문명 Lv.2부터 축제
        if (core != null && core.Level < TownLife.FestivalLevel)
        {
            core.ForceAdvance();
            yield return Wait(3f);
            LevelUpUI.HideNow();
        }
        SendShuttles();

        // 꾸미기 가게
        var shop = ShopUI.Instance;
        if (shop != null && ShopManager.TabOpen(ShopTab.Decor))
        {
            if (!shop.IsOpen) shop.Toggle();
            shop.ShowTab(ShopTab.Decor);
            Check(0);
            yield return Wait(3.5f);
            if (shop.IsOpen) shop.Toggle();
            yield return Wait(0.5f);
        }

        foreach (var (name, x, y) in Decor)
        {
            var placed = PlaceNear(name, Core + new Vector2Int(x, y));
            // 매력 팝업은 TownLife가 띄움
            if (placed != null)
            {
                LabFx.Burst(placed.transform.position, CuteUIKit.Butter, 10, 2.4f, 0.8f);
                Check(1);
            }
            yield return Wait(0.45f);
        }
        ShuttleService.Instance?.DebugArriveAll();
        yield return Wait(3.5f);

        TownLife.Instance?.Hold();
        Check(2);
        yield return Wait(12f);
    }

    private IEnumerator Research()
    {
        var ui = TechTreeUI.Instance;
        var tech = TechTreeManager.Instance;
        if (ui == null || tech == null) yield break;
        // 장면 배너를 먼저 보여 주고 창을 엶
        yield return Wait(2.4f);
        // 고를 연구의 재료를 먼저 챙김
        var node = tech.GetNode("crop_growth");
        if (node != null)
        {
            var (item, amount) = TechTreeManager.ItemCostOf(node);
            Give(item, amount);
        }
        if (!ui.IsOpen) ui.Toggle();
        yield return Wait(3f);
        if (node != null && tech.TryUpgrade(node))
        {
            Sfx.Play(SfxId.Upgrade);
            NotificationUI.Instance?.Show($"연구 완료: {node.displayName} Lv.{node.currentLevel}", NotificationUI.GoodNewsColor);
            panel.Note(0, $"Lv.{node.currentLevel} 완료 — 더 빨리 자라요");
            Check(0);
        }
        yield return Wait(2.5f);
        // 자동화 → 셔틀 → 대원 갈래
        for (int i = 0; i < 3; i++)
        {
            ui.CycleCategory(1);
            Check(Mathf.Min(i + 1, 2));
            yield return Wait(2.2f);
        }
        if (ui.IsOpen) ui.Toggle();
        yield return Wait(0.8f);
    }

    private IEnumerator CoreGrowth()
    {
        yield return WalkTo(Core + new Vector2Int(2, -4));
        yield return Fov(76f, 1.5f);
        var core = CenterCore.Current;
        while (core != null && core.Level < 6)
        {
            core.ForceAdvance();
            yield return new WaitForSeconds(0.4f);
            LevelUpUI.HideNow();
            // 레벨 숫자와 코어 모습을 패널에도
            panel.Note(0, $"지금 Lv.{core.Level}");
            panel.Note(1, "레벨마다 모습이 바뀌어요", DemoPanel.IconOf("core"));
            Check(0);
            if (core.Level >= 3) Check(1);
            yield return Wait(3.4f);
        }
        yield return Wait(1f);
        // 레벨마다 열리는 장
        JourneyUI.Toggle();
        Check(2);
        yield return Wait(4.5f);
        JourneyUI.CloseIfOpen();
        yield return Wait(0.6f);
    }

    // 세 단계 설비를 나란히: 파종기 · 수확기 · 무한 창고, 둘 다 닿는 칸에 밭
    private IEnumerator Automation()
    {
        Purify.SnapAll();
        yield return ClearArea(Core + new Vector2Int(-31, 3), Core + new Vector2Int(27, 35));
        // 오른쪽 설명 카드에 3단계 밭이 덜 가리게 화면을 조금 오른쪽으로
        Teleport(Core + new Vector2Int(2, 18));
        yield return Fov(104f, 1.5f);
        Place("Item_PowerRelay", Core + new Vector2Int(-26, 4));
        Place("Item_PowerRelay", Core + new Vector2Int(12, 4));
        yield return Wait(0.5f);

        var lanes = new List<(AutoSeeder s, AutoHarvester h, List<PlantTile> plots, TextMeshPro label, string name)>
        {
            Build("AutoSeeder", "AutoHarvester", Core + new Vector2Int(-27, 11), new Vector2Int(1, 0), "1단계"),
            Build("AutoSeeder_T2", "AutoHarvester_T2", Core + new Vector2Int(-14, 14), new Vector2Int(2, 0), "2단계"),
            Build("AutoSeeder_T3", "AutoHarvester_T3", Core + new Vector2Int(8, 19), new Vector2Int(3, 0), "3단계"),
        };
        EnergyManager.Instance?.RefreshEnergyNetwork();
        for (int i = 0; i < lanes.Count; i++) if (lanes[i].label != null) Check(i);

        var harvested = new int[lanes.Count];
        var points = steps[index].points;
        System.Action<AutoHarvester, PlantTile> onHarvest = (h, p) =>
        {
            for (int i = 0; i < lanes.Count; i++) if (lanes[i].h == h) harvested[i]++;
        };
        AutoHarvester.OnAnyMachineHarvest += onHarvest;
        try
        {
            for (float t = 0f; t < 42f && !hurry; t += 0.5f)
            {
                for (int i = 0; i < lanes.Count; i++)
                {
                    var lane = lanes[i];
                    if (lane.label == null) continue;
                    // 씨앗통이 비지 않게
                    if (lane.s != null && lane.s.TotalCount < lane.s.Capacity / 2)
                        lane.s.AddItem(Item("Item_Seed_BlueStar"), lane.s.Capacity - lane.s.TotalCount);
                    int planted = lane.plots.FindAll(p => p != null && p.CurrentState != PlantTile.PlantState.Empty).Count;
                    lane.label.text = $"<b>{lane.name}</b>\n심은 밭 {planted}/{lane.plots.Count} · 거둔 밭 {harvested[i]}";
                    // 패널에서도 단계별 수확 경주
                    if (i < points.Length) panel.Note(i, $"{points[i].detail} · 거둔 밭 {harvested[i]}");
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
        finally
        {
            AutoHarvester.OnAnyMachineHarvest -= onHarvest;
        }
        yield return Wait(1f);
    }

    private (AutoSeeder, AutoHarvester, List<PlantTile>, TextMeshPro, string) Build(string seederName, string harvesterName,
                                                                                    Vector2Int seederAt, Vector2Int harvesterOffset, string name)
    {
        var seeder = Place(seederName, seederAt) as AutoSeeder;
        var harvester = Place(harvesterName, seederAt + harvesterOffset) as AutoHarvester;
        var plots = new List<PlantTile>();
        if (seeder == null || harvester == null) return (seeder, harvester, plots, null, name);

        var store = Place("Item_InfiniteStorage", OutpostPlan.OpenCell(harvester.FootprintMax + new Vector2Int(1, -harvester.FootprintMax.y + harvester.FootprintMin.y), 1, 1));
        if (store != null) TileItem.Link(harvester, store);

        int r = Mathf.Min(seeder.CoverageRadiusCells, harvester.CoverageRadiusCells);
        var plotItem = Item("PlantTile");
        for (int y = seeder.FootprintMin.y - seeder.CoverageRadiusCells; y <= seeder.FootprintMax.y + seeder.CoverageRadiusCells; y++)
        for (int x = seeder.FootprintMin.x - seeder.CoverageRadiusCells; x <= seeder.FootprintMax.x + seeder.CoverageRadiusCells; x++)
        {
            if (GridManager.AreaDistance(harvester.FootprintMin, harvester.FootprintMax, x, y) > harvester.CoverageRadiusCells) continue;
            var cell = new Vector2Int(x, y);
            if (!Gm.IsEmpty(cell)) continue;
            if (Gm.PlaceTile(cell, plotItem.tilePrefab, 1, 1, 0, plotItem) is PlantTile p) plots.Add(p);
        }
        var seed = Item("Item_Seed_BlueStar");
        seeder.AddItem(seed, Mathf.Min(seeder.Capacity, plots.Count + 20));

        var top = Gm.GridToWorldCenter(new Vector2Int(seeder.FootprintMin.x - r, seeder.FootprintMax.y + seeder.CoverageRadiusCells + 1),
                                      seeder.FootprintMax.x - seeder.FootprintMin.x + 1 + r * 2, 1);
        var label = LabArt.Label(transform, $"<b>{name}</b>", 6f, CuteUIKit.Butter, top + Vector3.up * 0.9f);
        label.richText = true;
        TextPlate.Attach(label, CuteUIKit.Butter);
        return (seeder, harvester, plots, label, name);
    }

    private IEnumerator Ending()
    {
        var pad = Item("Item_LaunchPad");
        yield return ClearArea(Core + new Vector2Int(-14, -22), Core + new Vector2Int(-8, -16));
        var spot = OutpostPlan.OpenCell(Core + new Vector2Int(-12, -20), 3, 3, pad != null ? pad.tilePrefab : null);
        if (Place("Item_LaunchPad", spot) != null) Check(0);
        EnergyManager.Instance?.RefreshEnergyNetwork();
        Teleport(spot + new Vector2Int(1, -3));
        yield return Fov(80f);
        yield return Wait(1.5f);
        CenterCore.Current?.ForceAdvance();
        yield return new WaitForSeconds(0.4f);
        LevelUpUI.HideNow();

        // 이주선이 내려앉고 게임 엔딩 창이 뜰 때까지
        for (float t = 0f; t < 16f && !hurry && !EndingUI.IsShowing; t += Time.deltaTime) yield return null;
        Check(1);
        yield return Wait(4.5f);
        Check(2);
        yield return Wait(1.5f);

        // 엔딩 창을 닫고 데모 마무리 요약
        var ok = EndingUI.IsShowing && EndingUI.Root != null ? EndingUI.Root.GetComponentInChildren<Button>() : null;
        if (ok != null) ok.onClick.Invoke();
        panel.ShowOverview(true);
        yield return Wait(4f);
    }
}
