#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 퀘스트 자동 진행 플레이테스트 봇
public static class WishPlaytestBot
{
    public static bool Running { get; private set; }
    public static string LogText => log.ToString();
    public static string Status = "";

    private static readonly StringBuilder log = new();
    private static float speed = 8f;
    private static int   stopLevel = 99;
    private static float gameStart;
    private static float walkDistance;
    private static string lastQuest = "";
    private static int    lastKey = -1;
    private static float  lastProgressAt;
    private static float  nextAct;
    private static int    lastFrame;
    private static ResourceBlock miningBlock;
    private static FarmingItemData miningFor;
    private static int    miningTarget;
    private static float  maxGameSeconds = 60f * 60f * 6f;
    private static readonly Dictionary<string, float> chapterStart = new();

    private const float MoveSpeed = 4.5f;

    public static float PlotFactor = 1f;

    public static bool Shots;
    private static bool   introShot;
    private static float  introShotAt = -1f;
    private static string shotQuest;
    private static int    shotStage = 2, shotIndex;
    private static float  shotAt;

    private const string ScenarioKey = "WishPlaytestScenario";

    public static void RunScenario(float timeScale, int untilLevel, bool shots, int seed)
    {
        // 플레이 진입 후에도 남게 SessionState에 저장
        SessionState.SetString(ScenarioKey, $"{timeScale}|{untilLevel}|{(shots ? 1 : 0)}|{seed}");
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        else Launch();
    }

    [InitializeOnLoadMethod]
    private static void HookScenario()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(SessionState.GetString(ScenarioKey, "")))
            Launch();
    }

    private static void Launch()
    {
        var parts = SessionState.GetString(ScenarioKey, "").Split('|');
        SessionState.EraseString(ScenarioKey);
        if (parts.Length < 4) return;
        float ts = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        int lv = int.Parse(parts[1]);
        bool shots = parts[2] == "1";
        int seed = int.Parse(parts[3]);

        Application.runInBackground = true;
        GameLaunchContext.SelectedSlot = 0;
        GameLaunchContext.IsNewGame = true;
        GameLaunchContext.PendingWorldSize = WorldSize.Middle;
        GameLaunchContext.PendingWorldTheme = WorldTheme.Wasteland;
        GameLaunchContext.PendingWorldSeed = seed;
        GameLaunchContext.LaunchedFromMenu = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
        UnityEngine.SceneManagement.SceneManager.LoadScene("InGame");

        void OnLoaded(UnityEngine.SceneManagement.Scene sc, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
            EditorApplication.delayCall += () =>
            {
                Shots = shots;
                shotIndex = 1;
                shotStage = 2;
                introShot = false;
                introShotAt = -1f;
                if (lv > 0) Start(ts, lv, 8f);
            };
        }
    }

    public static void Start(float timeScale, int untilLevel, float maxHours)
    {
        log.Clear();
        speed = timeScale;
        stopLevel = untilLevel;
        maxGameSeconds = maxHours * 3600f;
        gameStart = Time.time;
        walkDistance = 0f;
        lastQuest = "";
        lastKey = -1;
        lastProgressAt = Time.time;
        miningBlock = null;
        chapterStart.Clear();
        wantedAt.Clear();
        GrowBag();
        Running = true;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Log("봇 시작 x" + timeScale);
    }

    public static void Stop(string why)
    {
        if (!Running) return;
        Running = false;
        EditorApplication.update -= Tick;
        if (miningBlock != null) miningBlock.StopMining();
        Time.timeScale = 1f;
        Log("봇 멈춤: " + why);
    }

    // 사람의 창고 정리 대신 측정 중에만 가방 확장
    private const int BotBagSlots = 140;

    private static void GrowBag()
    {
        var inv = Inventory.Instance;
        if (inv == null || inv.SlotCount >= BotBagSlots) return;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(Inventory).GetField("slots", flags);
        var old = (InventorySlot[])field.GetValue(inv);
        var grown = new InventorySlot[BotBagSlots];
        for (int i = 0; i < grown.Length; i++) grown[i] = i < old.Length ? old[i] : new InventorySlot();
        field.SetValue(inv, grown);
        typeof(Inventory).GetField("slotCount", flags).SetValue(inv, BotBagSlots);
    }

    private static int UsedSlots()
    {
        var inv = Inventory.Instance;
        int n = 0;
        for (int i = 0; i < inv.SlotCount; i++) { var s = inv.GetSlot(i); if (s != null && !s.IsEmpty) n++; }
        return n;
    }

    private static float GameT => Time.time - gameStart;
    private static string Clock(float s) => $"{(int)(s / 60f):00}:{(int)(s % 60f):00}";

    private static void Log(string msg)
    {
        float t = Application.isPlaying ? GameT : 0f;
        log.AppendLine($"[{Clock(t)} 걷기{Clock(walkDistance / MoveSpeed)}] {msg}");
    }

    private static double levelUpSeenAt = -1;

    private static void Tick()
    {
        if (LevelUpUI.IsOpen)
        {
            if (levelUpSeenAt < 0) levelUpSeenAt = EditorApplication.timeSinceStartup;
            else if (EditorApplication.timeSinceStartup - levelUpSeenAt > 2.5) { Click("좋아요!"); levelUpSeenAt = -1; }
        }
        else levelUpSeenAt = -1;
        if (!EditorApplication.isPlaying) { Stop("플레이 종료"); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        if (Time.timeScale != speed) Time.timeScale = speed;

        var qm = QuestManager.Instance;
        if (qm == null || PlayerTileController.Instance == null) return;

        var tut = UnityEngine.Object.FindFirstObjectByType<TutorialUI>();
        if (tut != null && TutorialUI.IsShowing)
        {
            if (Shots && introShotAt < 0f) { introShotAt = Time.realtimeSinceStartup + 0.8f; return; }
            if (Shots && Time.realtimeSinceStartup < introShotAt) return;
            if (Shots && !introShot)
            {
                string dir = Application.dataPath + "/../Temp/McpScreenshots/walk/";
                System.IO.Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(dir + "00_intro.png");
                introShot = true;
                introShotAt = Time.realtimeSinceStartup + 0.4f;
                return;
            }
            var btn = tut.transform.Find("DialogPanel/BottomRow/NextBtn");
            if (btn != null) btn.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            return;
        }

        string q = qm.CurrentQuestId;
        if (q != lastQuest)
        {
            if (!string.IsNullOrEmpty(lastQuest)) Log($"완료 {lastQuest}");
            lastQuest = q;
            if (Shots && !string.IsNullOrEmpty(q))
            {
                shotQuest = q;
                shotStage = 0;
                shotAt = Time.realtimeSinceStartup + 1.3f;
            }
            var ch = qm.CurrentChapter;
            if (ch != null && !chapterStart.ContainsKey(ch.title))
            {
                chapterStart[ch.title] = GameT;
                Log($"=== {qm.CurrentChapterIndex + 1}장 {ch.title} 시작 (코인 {Coins()} · 밭 {PlotCount()} · 주민 {ShuttleService.Population} · 가방 {UsedSlots()}칸 · 전초 {CargoRails.OutpostCount} · 레일 {Tiles<CargoRail>(TileType.CargoRail).Count} · 중계탑 {Tiles<PowerRelay>(TileType.PowerRelay).Count} · 토양 밭 {Plots().FindAll(p => p.SoilKind != SoilType.None).Count}) ===");
            }
            Log($"시작 {q} {qm.CurrentQuest?.questName} | 안내: {QuestGuide.Current.action}");
        }
        if (qm.AllQuestsDone || CenterCore.CurrentLevel >= stopLevel) { Stop("목표 도달 Lv." + CenterCore.CurrentLevel); return; }
        if (GameT > maxGameSeconds) { Stop("시간 초과"); return; }

        // 진행도 해시로 막힘 감지
        int key = qm.CurrentQuestIndex * 100000;
        bool civ = false;
        foreach (var o in qm.ActiveObjectives) { key += o.currentAmount; civ |= o.type == QuestObjectiveType.CoreLevel; }
        // 주민 · 수확은 늘 늘어나서 문명 조건 퀘스트에서만 진행으로 셈
        if (civ) key += ShuttleService.Population * 7 + HarvestLog.Count * 13;
        if (key != lastKey) { lastKey = key; lastProgressAt = Time.time; }
        else if (Time.time - lastProgressAt > 600f)
        {
            Log($"!!! 10분째 진전 없음: {q} | 안내: {QuestGuide.Current.action} | 코인 {Coins()}");
            lastProgressAt = Time.time;
        }

        if (Shots && shotStage < 2 && !string.IsNullOrEmpty(shotQuest))
        {
            if (Time.realtimeSinceStartup < shotAt) return;
            string dir = Application.dataPath + "/../Temp/McpScreenshots/walk/";
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(dir + $"{shotIndex:00}_{shotQuest}_{(shotStage == 0 ? "a_card" : "b_play")}.png");
            shotStage++;
            if (shotStage == 1) shotAt = Time.realtimeSinceStartup + 5.2f;
            else { shotIndex++; nextAct = Time.realtimeSinceStartup + 0.3f; }
            return;
        }

        if (Time.realtimeSinceStartup < nextAct) return;
        nextAct = Time.realtimeSinceStartup + (Shots ? 0.25f : 0.05f);

        try
        {
            UpdateMining();
            Chores();
            QuestObjective first = null;
            foreach (var o in qm.ActiveObjectives)
            {
                if (o.IsComplete) continue;
                if (first == null)
                {
                    first = o;
                    Status = $"{q} {o.type} {(o.targetItem ? o.targetItem.itemName : "")} {o.currentAmount}/{o.targetAmount}";
                    Act(o);
                    if (o.type != QuestObjectiveType.Stockpile) break;
                    continue;
                }
                // 안내처럼 다음 비축도 같이 챙김
                if (o.type == QuestObjectiveType.Stockpile) Act(o);
            }
        }
        catch (Exception e)
        {
            Log("예외: " + e.Message + " " + e.StackTrace?.Split('\n')[0]);
            nextAct = Time.realtimeSinceStartup + 1f;
        }
    }

    private static void Act(QuestObjective o)
    {
        switch (o.type)
        {
            case QuestObjectiveType.PlantSeed:
                if (!Plant(o.targetItem))
                {
                    if (o.targetItem != null && Count(o.targetItem) <= 0) Buy(o.targetItem, 4);
                    else if (!HasEmptyPlot()) MakePlots(2);
                }
                break;
            case QuestObjectiveType.HarvestPlant:
                if (o.targetItem != null) EnsureGrowing(o.targetItem, 3);
                else if (!AnyGrowing()) PlantAny();
                Shuttles(0); // 기다리는 동안 셔틀 병행
                break;
            case QuestObjectiveType.CollectItem:
                Obtain(o.targetItem, o.targetAmount - o.currentAmount + Count(o.targetItem), 0);
                break;
            case QuestObjectiveType.CraftItem:
                Make(o.targetItem, Count(o.targetItem) + 1, 0);
                break;
            case QuestObjectiveType.PlaceTile:
                Place(o.targetItem, 0);
                break;
            case QuestObjectiveType.CompleteOrder:
            case QuestObjectiveType.OrdersTotal:
            case QuestObjectiveType.Residents:
                Shuttles(0);
                break;
            case QuestObjectiveType.CropVarieties:
                NewVariety();
                break;
            case QuestObjectiveType.CoreLevel:
                Core();
                break;
            case QuestObjectiveType.SellItem:
                if (Count(o.targetItem) > 0) ShopManager.Instance?.TrySell(o.targetItem, Count(o.targetItem));
                else Obtain(o.targetItem, 1, 0);
                break;
            case QuestObjectiveType.UpgradeTech:
                Research(o.targetTechNodeId);
                break;
            case QuestObjectiveType.LinkStorage:
                LinkToStorage(o.targetItem);
                break;
            case QuestObjectiveType.AutoDepart:
                AutoDepart();
                break;
            case QuestObjectiveType.MachineCrops:
                MachineLines(o.targetAmount);
                Shuttles(0);
                break;
            case QuestObjectiveType.AutoDish:
                AutoDish();
                break;
            case QuestObjectiveType.Stockpile:
                Stock(o.targetItem, o.targetAmount);
                Shuttles(0);
                break;
            case QuestObjectiveType.FindSoil:
                GoToSoil(o.targetItem);
                break;
            case QuestObjectiveType.SoilCrop:
                SoilCrop(o.targetItem, o.targetAmount);
                Shuttles(0);
                break;
            case QuestObjectiveType.Outposts:
                BuildOutpost(o.targetItem);
                Shuttles(0);
                break;
            case QuestObjectiveType.RailDelivery:
                RailDeliver(o.targetItem);
                Shuttles(0);
                break;
            case QuestObjectiveType.Purifiers:
                PurifyEdge();
                break;
            case QuestObjectiveType.Biomes:
                VisitBiome();
                break;
            case QuestObjectiveType.Landmarks:
                OpenLandmark();
                break;
            case QuestObjectiveType.Charm:
                Decorate(o.targetAmount);
                break;
            case QuestObjectiveType.Festival:
                if (TownLife.FestivalBlock() == null) HoldFestival();
                else if (!TownLife.HeldToday) EarnCoins();
                break;
            case QuestObjectiveType.AutoHarvest:
                AutoFarm();
                Shuttles(0); // 기다리는 동안 셔틀 병행
                break;
        }
    }

    private static bool PlaceAtCell(FarmingItemData item, Vector2Int cell)
    {
        if (item == null) return false;
        if (Count(item) <= 0) { Make(item, 1, 1); if (Count(item) <= 0) return false; }
        var gm = GridManager.Instance;
        cell = OutpostPlan.OpenCell(cell, item.tileWidth, item.tileHeight, item.tilePrefab);
        Select(item);
        GoTo(gm.GridToWorld(cell) + Vector3.down);
        tryPlace.Invoke(PlayerTileController.Instance, new object[] { cell });
        bool ok = gm.GetTile(cell) != null;
        if (ok && item.name != "PlantTile") Log($"  설치 {item.itemName} {cell} (본진에서 {(cell - CenterCore.Current.GridPosition).magnitude:0}칸)");
        return ok;
    }

    // 정화 경계 밖, 본진 오른쪽부터 돌아가며
    private static void PurifyEdge()
    {
        var core = CenterCore.Current;
        int edge = Purify.CoreRadius(core.Level) + 4 + Purify.Purifiers.Count * 2;
        var dirs = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        var d = dirs[Purify.Purifiers.Count % dirs.Length];
        PlaceAtCell(NetWorld.Item("Item_Purifier"), core.GridPosition + d * edge);
    }

    private static void VisitBiome()
    {
        var map = TerrainMap.Current;
        var gm = GridManager.Instance;
        for (int i = 0; i < WorldThemeConfig.Count; i++)
        {
            if (Exploration.HasFound((WorldTheme)i)) continue;
            float half = Mathf.Min(gm.GridWidth, gm.GridHeight) * 0.5f;
            var cell = Vector2Int.RoundToInt(map.BiomeDirection((WorldTheme)i) * half * 0.5f);
            GoTo(gm.GridToWorld(cell));
            return;
        }
    }

    private static void OpenLandmark()
    {
        var player = PlayerTileController.Instance;
        var l = BiomeLandmark.Nearest(player.transform.position, true);
        if (l == null) return;
        GoTo(l.transform.position + Vector3.down);
        l.TryOpen(player.transform.position);
    }

    private static void GoToSoil(FarmingItemData seed)
    {
        var gm = GridManager.Instance;
        if (Soil.TryNearest(Soil.Of(seed), CenterCore.Current.GridPosition, out var patch))
            GoTo(gm.GridToWorld(patch.center));
    }

    // 맞는 토양에 전기 → 밭 → 씨앗
    private static void SoilCrop(FarmingItemData seed, int want)
    {
        var soil = Soil.Of(seed);
        if (!Soil.TryNearest(soil, CenterCore.Current.GridPosition, out var patch)) return;
        var em = EnergyManager.Instance;
        if (em != null && !em.HasEnergy(patch.center))
        {
            PlaceAtCell(NetWorld.Item("Item_PowerRelay"), OutpostPlan.NextRelayCell(patch.center));
            return;
        }
        if (!Purify.IsPure(patch.center))
        {
            PlaceAtCell(NetWorld.Item(Count(NetWorld.Item("Item_Purifier_T2")) > 0 ? "Item_Purifier_T2" : "Item_Purifier"), patch.center);
            return;
        }
        var onSoil = Plots().FindAll(p => p.SoilKind == soil);
        if (onSoil.Count < want) { PlaceAtCell(NetWorld.Item("PlantTile"), patch.center); return; }
        if (Count(seed) <= 0) { Buy(seed, want); if (Count(seed) <= 0) return; }
        foreach (var p in onSoil)
        {
            if (p.CurrentState != PlantTile.PlantState.Empty || !p.IsPowered) continue;
            Select(seed);
            tryPlant.Invoke(PlayerTileController.Instance, new object[] { p.GridPosition });
            return;
        }
    }

    private static void BuildOutpost(FarmingItemData kind)
    {
        bool ore = kind != null && kind.tilePrefab != null && kind.tilePrefab.GetComponent<ResourceExtractor>() != null;
        var plan = OutpostPlan.Next(Soil.Of(kind), ore);
        switch (plan.step)
        {
            case OutpostPlan.Step.HomeStorage:
            case OutpostPlan.Step.Storage:
                PlaceAtCell(NetWorld.Item("NormalStorage"), plan.cell);
                break;
            case OutpostPlan.Step.Power:
                PlaceAtCell(NetWorld.Item("Item_PowerRelay"), plan.cell);
                break;
            case OutpostPlan.Step.Purify:
                PlaceAtCell(NetWorld.Item(CenterCore.CurrentLevel >= 5 ? "Item_Purifier_T2" : "Item_Purifier"), plan.cell);
                break;
            case OutpostPlan.Step.Rail:
                PlaceAtCell(NetWorld.Item("Item_CargoRail"), plan.cell);
                break;
            case OutpostPlan.Step.Producer:
                if (plan.soil == SoilType.None) PlaceExtractor(plan.storage);
                else if (PlaceAtCell(NetWorld.Item("AutoHarvester"), plan.cell)) LinkNearby<AutoHarvester>(TileType.AutoHarvester, plan.storage);
                break;
        }
    }

    private static void LinkNearby<T>(TileType type, ItemStorage storage) where T : TileItem
    {
        foreach (var m in Tiles<T>(type))
            if (!MachineFarm.LinkedToStorage(m) && LinkRules.DistanceCells(m, storage) <= LinkRules.MaxDistanceCells
                && TileItem.Link(storage, m)) Log($"  연결 {LinkRules.NameOf(m)} - 전초 창고");
    }

    private static void PlaceExtractor(ItemStorage storage)
    {
        var item = NetWorld.Item("T1_ResourceExtractor");
        if (Count(item) <= 0) { Make(item, 1, 1); if (Count(item) <= 0) return; }
        var gm = GridManager.Instance;
        ResourceBlock block = null;
        float best = float.MaxValue;
        foreach (var b in ResourceBlock.Active)
        {
            if (b == null || b.RemainingResources <= 0 || b.Data == null || b.Data.minToolGrade > 1) continue;
            float d = (b.transform.position - storage.transform.position).sqrMagnitude;
            if (d < best) { best = d; block = b; }
        }
        if (block == null) return;
        var c = gm.WorldToGrid(block.transform.position);
        for (int r = 1; r <= 4; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var a = c + new Vector2Int(dx, dy);
                    if (!gm.CanPlaceAt(a, item.tileWidth, item.tileHeight)) continue;
                    if (Vector2.Distance(gm.GridToWorld(a), block.transform.position) > 1.9f) continue;
                    if (PlaceAtCell(item, a)) LinkNearby<ResourceExtractor>(TileType.Extractor, storage);
                    return;
                }
    }

    private static void RailDeliver(FarmingItemData item)
    {
        if (CargoRails.OutpostCount <= 0) { BuildOutpost(null); return; }
        if (item == null || !item.isCropProduct) return;
        var seed = SeedFor(item);
        var soilBase = SoilBaseOf(seed);
        if (soilBase != null) TendSoilFarm(seed, soilBase);
        else BuildOutpost(seed);
    }

    private static ItemStorage SoilBaseOf(FarmingItemData seed)
    {
        var soil = Soil.Of(seed);
        if (soil == SoilType.None) return null;
        foreach (var net in CargoRails.Networks)
            foreach (var s in net.Storages)
                if (CargoRails.IsOutpost(net, s) && Soil.At(s.GridPosition) == soil) return s;
        return null;
    }

    private static readonly Vector2Int[] SoilLineSpots =
    {
        new(0, 3), new(-6, 0), new(6, 0), new(0, 7), new(0, -6), new(-5, 5), new(5, 5), new(-5, -5), new(5, -5),
    };
    private const int SoilPlotsPerLine = 20;

    // 토양 전초 농장: 작물마다 파종기 + 수확기 + 밭
    private static void TendSoilFarm(FarmingItemData seed, ItemStorage soilBase)
    {
        var gm = GridManager.Instance;
        var soil = Soil.Of(seed);
        AutoSeeder seeder = null;
        int lines = 0;
        foreach (var s in Tiles<AutoSeeder>(TileType.AutoSeeder))
        {
            if (Soil.At(s.GridPosition) != soil) continue;
            lines++;
            var assigned = MachineFarm.SeedOf(s);
            if (assigned == seed) seeder = s;
            else if (assigned == null && seeder == null) seeder = s;
        }
        if (seeder == null)
        {
            if (lines >= SoilLineSpots.Length) return;
            PlaceAtCell(NetWorld.Item("AutoSeeder"), soilBase.GridPosition + SoilLineSpots[lines]);
            return;
        }
        if (MachineFarm.SeedOf(seeder) == null || seeder.IsStarved)
        {
            if (Count(seed) <= 0) { Buy(seed, 12); if (Count(seed) <= 0) return; }
            Select(seed);
            int put = seeder.AddItem(seed, Mathf.Min(Count(seed), seeder.Capacity));
            if (put > 0) { Inventory.Instance.ConsumeItem(seed, put); Log($"  토양 파종기에 {seed.itemName} ×{put}"); }
            return;
        }
        AutoHarvester partner = null;
        foreach (var h in Tiles<AutoHarvester>(TileType.AutoHarvester))
            if (LinkRules.DistanceCells(h, seeder) <= h.CoverageRadiusCells / 2f) { partner = h; break; }
        if (partner == null)
        {
            if (PlaceAtCell(NetWorld.Item("AutoHarvester"), seeder.GridPosition + Vector2Int.right)) LinkNearby<AutoHarvester>(TileType.AutoHarvester, soilBase);
            return;
        }
        MachineFarm.CountPlots(seeder, out _, out int covered);
        if (covered >= SoilPlotsPerLine) return;
        int reach = Mathf.Min(seeder.CoverageRadiusCells, partner.CoverageRadiusCells);
        for (int r = 1; r <= reach; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var a = seeder.GridPosition + new Vector2Int(dx, dy);
                    if (Soil.At(a) != soil || !gm.CanPlaceAt(a, 1, 1)) continue;
                    if (GridManager.AreaDistance(partner.FootprintMin, partner.FootprintMax, a.x, a.y) > partner.CoverageRadiusCells) continue;
                    PlaceAtCell(NetWorld.Item("PlantTile"), a);
                    return;
                }
    }

    private static void Research(string id)
    {
        var tt = TechTreeManager.Instance;
        var node = string.IsNullOrEmpty(id) ? null : tt.GetNode(id);
        if (node == null) { foreach (var n in tt.Nodes) if (tt.TryUpgrade(n)) break; return; }
        if (node.IsMaxed) return;
        int lv = node.currentLevel;
        if (node.itemCosts != null && lv < node.itemCosts.Length && !string.IsNullOrEmpty(node.itemCosts[lv]))
        {
            var it = NetWorld.Item(node.itemCosts[lv]);
            int need = node.itemAmounts[lv];
            if (Count(it) < need) { Obtain(it, need, 0); return; }
        }
        if (tt.TryUpgrade(node)) Log($"  연구 {node.displayName}");
    }

    private static TileItem MachineOf(FarmingItemData item, bool unlinkedOnly)
    {
        foreach (var t in GridManager.Instance.GetDynamicTiles())
            if (MachineFarm.SameMachine(t, item) && (!unlinkedOnly || !MachineFarm.LinkedToStorage(t))) return t;
        return null;
    }

    private static bool LinkToStorage(FarmingItemData item)
    {
        var machine = MachineOf(item, true);
        if (machine == null)
        {
            if (MachineOf(item, false) != null) return true;
            Place(item, 0);
            return false;
        }
        var storage = MachineFarm.NearestStorage(machine) ?? PlaceStorageNear(machine);
        if (storage == null) return false;
        GoTo(machine.transform.position + Vector3.down);
        if (TileItem.Link(storage, machine)) Log($"  연결 {LinkRules.NameOf(machine)} - 창고");
        return true;
    }

    private static ItemStorage PlaceStorageNear(TileItem to)
    {
        var item = NetWorld.Item("NormalStorage");
        if (Count(item) <= 0) { Make(item, 1, 1); if (Count(item) <= 0) return null; }
        var gm = GridManager.Instance;
        var c = to.FootprintMin;
        for (int r = 1; r < 12; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var a = c + new Vector2Int(dx, dy);
                    if (!gm.CanPlaceAt(a, item.tileWidth, item.tileHeight)) continue;
                    Select(item);
                    GoTo(gm.GridToWorld(a) + Vector3.down);
                    tryPlace.Invoke(PlayerTileController.Instance, new object[] { a });
                    if (gm.GetTile(a) is ItemStorage s) { Log("  설치 창고 (설비 곁)"); return s; }
                }
        return null;
    }

    private static bool autoLoading;

    // 창고 짐으로 셔틀 혼자 보내기
    private static void AutoDepart()
    {
        var svc = ShuttleService.Instance;
        if (svc == null) return;
        var padItem = ShuttleService.PadItem;
        if (MachineFarm.LinkedCount(padItem) <= 0) { LinkToStorage(padItem); return; }
        foreach (var s in svc.Shuttles)
        {
            var pad = ShuttleService.PadOf(s);
            if (s.away || !MachineFarm.LinkedToStorage(pad)) continue;
            var storage = MachineFarm.LinkedStorage(pad);
            foreach (var l in s.cargo)
            {
                if (l.Missing <= 0) continue;
                var it = NetWorld.Item(l.item);
                if (it == null) continue;
                int have = Count(it);
                if (have < l.Missing) { Obtain(it, l.Missing, 0); return; }
                int put = storage.AddItem(it, l.Missing);
                if (put > 0) { Inventory.Instance.ConsumeItem(it, put); Log($"  창고로 {it.itemName} ×{put} (셔틀 짐)"); }
            }
            return;
        }
    }

    // 작물 종류만큼 파종기 + 씨앗
    private static void MachineLines(int want)
    {
        if (Tiles<AutoHarvester>(TileType.AutoHarvester).Count == 0) { Place(NetWorld.Item("AutoHarvester"), 0); return; }
        var seeders = Tiles<AutoSeeder>(TileType.AutoSeeder);
        if (seeders.Count < want && MachineFarm.AssignedKinds() >= seeders.Count) { Place(NetWorld.Item("AutoSeeder"), 0); return; }
        foreach (var s in seeders)
        {
            if (MachineFarm.SeedOf(s) != null) continue;
            var seed = NextLineSeed();
            if (seed == null) return;
            if (Count(seed) <= 0) { Buy(seed, 6); if (Count(seed) <= 0) return; }
            Select(seed);
            int n = Mathf.Min(Count(seed), s.Capacity);
            int put = s.AddItem(seed, n);
            if (put > 0) { Inventory.Instance.ConsumeItem(seed, put); Log($"  파종기에 {seed.itemName} ×{put}"); }
            return;
        }
        if (MachineFarm.CropKinds < want) EnsurePlotsForSeeders();
    }

    private const int MaxLines = 14;

    // 사람처럼 필요한 작물마다 파종기 하나 더
    private static bool AddLine(FarmingItemData seed)
    {
        var soilBase = SoilBaseOf(seed);
        if (soilBase != null) { TendSoilFarm(seed, soilBase); return true; }
        var seeders = Tiles<AutoSeeder>(TileType.AutoSeeder);
        if (seeders.Count == 0) return false;
        foreach (var a in seeders)
        {
            if (MachineFarm.SeedOf(a) != seed) continue;
            if (a.IsStarved && Count(seed) > 0)
            {
                int back = a.AddItem(seed, Mathf.Min(Count(seed), a.Capacity));
                if (back > 0) { Inventory.Instance.ConsumeItem(seed, back); Log($"  멈춘 줄에 씨앗 {seed.itemName} ×{back}"); }
            }
            return true;
        }
        var idle = seeders.Find(a => MachineFarm.SeedOf(a) == null);
        if (idle == null)
        {
            if (seeders.Count >= MaxLines) return false;
            Place(NetWorld.Item("AutoSeeder"), 0);
            return true;
        }
        if (Count(seed) <= 0) return true;
        Select(seed);
        int put = idle.AddItem(seed, Mathf.Min(Count(seed), idle.Capacity));
        if (put > 0) { Inventory.Instance.ConsumeItem(seed, put); Log($"  작물 줄 추가 {seed.itemName} ×{put}"); }
        return true;
    }

    private static float nextHarvesterAt;

    // 새 기계 연결, 수확이 밀리면 수확기 추가
    private static void TendMachines()
    {
        if (Tiles<ItemStorage>(TileType.Storage).Count > 0)
        {
            foreach (var h in Tiles<AutoHarvester>(TileType.AutoHarvester)) if (!MachineFarm.LinkedToStorage(h)) { LinkToStorage(NetWorld.Item("AutoHarvester")); break; }
        }
        var harvesters = Tiles<AutoHarvester>(TileType.AutoHarvester);
        if (harvesters.Count == 0 || harvesters.Count >= 8 || Time.time < nextHarvesterAt) return;
        int backlog = 0;
        foreach (var p in Plots()) if (p.SoilKind == SoilType.None && p.CurrentState == PlantTile.PlantState.ReadyToHarvest && CoveredByHarvester(p)) backlog++;
        if (backlog < 12) return;
        nextHarvesterAt = Time.time + 60f;
        Place(NetWorld.Item("AutoHarvester"), 0);
    }

    private static FarmingItemData NextLineSeed()
    {
        var used = new HashSet<FarmingItemData>();
        foreach (var s in Tiles<AutoSeeder>(TileType.AutoSeeder)) { var a = MachineFarm.SeedOf(s); if (a != null) used.Add(a); }
        foreach (var it in GameDataRegistry.FindAll<FarmingItemData>())
        {
            if (it == null || it.itemType != FarmingItemType.Seed || it.seedData == null || used.Contains(it)) continue;
            if (it.unlockCoreLevel > CenterCore.CurrentLevel || !ShopManager.Instance.IsPurchasable(it)) continue;
            return it;
        }
        return null;
    }

    private static void EnsurePlotsForSeeders()
    {
        foreach (var s in Tiles<AutoSeeder>(TileType.AutoSeeder))
        {
            MachineFarm.CountPlots(s, out int near, out int covered);
            if (covered > 0) continue;
            if (near > 0) { Place(NetWorld.Item("AutoHarvester"), 0); return; }
            MakePlots(2);
            return;
        }
    }

    // 창고에 재료 넣어 주방 혼자 요리
    private static void AutoDish()
    {
        var kitchenItem = NetWorld.Item("Item_CatKitchen");
        var kitchens = Tiles<CatKitchen>(TileType.Kitchen);
        var linked = kitchens.Find(k => MachineFarm.LinkedToStorage(k));
        if (linked == null) { LinkToStorage(kitchenItem); return; }
        if (linked.Recipe == null)
        {
            var r = CatKitchen.FindRecipe("Item_StarJam") ?? CatKitchen.Menu()[0];
            if (!linked.SetRecipe(r)) return;
            Log($"  주방 메뉴 {r.outputItem.itemName}");
        }
        var storage = MachineFarm.LinkedStorage(linked);
        foreach (var ing in linked.Recipe.ingredients)
        {
            if (ing.item == null || storage.GetCount(ing.item) + linked.GetInput(ing.item) >= ing.amount * 2) continue;
            int need = ing.amount * 4;
            if (Count(ing.item) < need) { Obtain(ing.item, need, 0); return; }
            int put = storage.AddItem(ing.item, need);
            if (put > 0) Inventory.Instance.ConsumeItem(ing.item, put);
        }
    }

    private static void Stock(FarmingItemData item, int want)
    {
        if (item == null) return;
        Obtain(item, want - MachineFarm.InStorages(item), 0);
    }

    // 가방과 달리 창고엔 봇이 안 닿으니 씨앗 빼고 가져옴
    private static void SweepStorages()
    {
        var qm = QuestManager.Instance;
        autoLoading = false;
        var keep = new HashSet<FarmingItemData>();
        if (qm?.ActiveObjectives != null)
            foreach (var o in qm.ActiveObjectives)
            {
                if (o.IsComplete) continue;
                if (o.type == QuestObjectiveType.AutoDepart)
                {
                    autoLoading = true;
                    foreach (var s in ShuttleService.Instance.Shuttles)
                        foreach (var l in s.cargo) keep.Add(NetWorld.Item(l.item));
                }
                if (o.type == QuestObjectiveType.AutoDish)
                    foreach (var k in Tiles<CatKitchen>(TileType.Kitchen))
                        if (k.Recipe != null && MachineFarm.LinkedToStorage(k))
                            foreach (var ing in k.Recipe.ingredients) keep.Add(ing.item);
            }
        var buf = new List<TileItem>();
        GridManager.Instance.CollectTiles(TileType.Storage, buf);
        foreach (var t in buf)
        {
            if (t is not ItemStorage st || st.IsGhost) continue;
            // 전초 창고는 레일이 실어 옴
            if (CargoRails.CellsFromCore(st) >= CargoRails.OutpostCells) continue;
            foreach (var it in new List<FarmingItemData>(st.StockedItems))
            {
                if (it == null || it.itemType == FarmingItemType.Seed || keep.Contains(it)) continue;
                int n = st.RemoveItem(it, 9999);
                if (n > 0) Inventory.Instance.AddItem(it, n);
            }
        }
    }

    // 셔틀 짐을 3분 안에 채울 만큼의 밭
    private static int DemandPlots()
    {
        var svc = ShuttleService.Instance;
        if (svc == null) return 0;
        float plots = 0f;
        foreach (var s in svc.Shuttles)
        {
            if (s.away) continue;
            foreach (var l in s.cargo)
            {
                var it = NetWorld.Item(l.item);
                var seed = it != null ? SeedFor(it) : null;
                if (seed == null) continue;
                plots += Mathf.Max(0, l.Missing - Count(it)) * seed.seedData.TotalGrowthSeconds / 180f;
            }
        }
        return Mathf.Min(MaxBotPlots, Mathf.CeilToInt(plots));
    }

    private const int MaxBotPlots = 400;
    private const int PlotsPerLine = 12;
    private const int CropBuffer = 200;

    private static bool SeederCovers(PlantTile p)
    {
        if (!CoveredByHarvester(p)) return false;
        foreach (var s in Tiles<AutoSeeder>(TileType.AutoSeeder))
            if (MachineFarm.SeedOf(s) != null
                && GridManager.AreaDistance(s.FootprintMin, s.FootprintMax, p.GridPosition.x, p.GridPosition.y) <= s.CoverageRadiusCells)
                return true;
        return false;
    }

    private static void AutoFarm()
    {
        if (Tiles<AutoSeeder>(TileType.AutoSeeder).Count > 0) MachineLines(1);
        bool near = false;
        foreach (var h in Tiles<AutoHarvester>(TileType.AutoHarvester))
            foreach (var p in Plots())
                if (Covers(h, p)) { near = true; break; }
        if (!near) Place(NetWorld.Item("AutoHarvester"), 0);
        if (!AnyGrowing()) PlantAny();
    }

    private static bool Covers(AutoHarvester h, PlantTile p)
        => GridManager.AreaDistance(h.FootprintMin, h.FootprintMax, p.GridPosition.x, p.GridPosition.y) <= h.CoverageRadiusCells;

    private static bool CoveredByHarvester(PlantTile p)
    {
        foreach (var h in Tiles<AutoHarvester>(TileType.AutoHarvester))
            if (Covers(h, p)) return true;
        return false;
    }

    private static int NextPlotRequirement()
    {
        var next = CenterCore.Current != null ? CenterCore.Current.NextStage : null;
        if (next == null) return 0;
        foreach (var r in next.requirements)
            if (r.type == CoreRequirementType.Facility && r.item != null && r.item.name == "PlantTile") return r.amount;
        return 0;
    }

    private static void Core()
    {
        var core = CenterCore.Current;
        var next = core != null ? core.NextStage : null;
        if (next == null) return;
        foreach (var r in next.requirements)
        {
            if (CoreProgress.IsMet(r)) continue;
            switch (r.type)
            {
                case CoreRequirementType.Facility:      Place(r.item, 0); return;
                case CoreRequirementType.CropVarieties: NewVariety(); return;
                case CoreRequirementType.Orders:
                case CoreRequirementType.Residents:     Shuttles(0); return;
                case CoreRequirementType.Coins:         EarnCoins(); return;
                case CoreRequirementType.MachineCrops:  MachineLines(r.amount); Shuttles(0); return;
                case CoreRequirementType.Outposts:      BuildOutpost(null); Shuttles(0); return;
            }
        }
    }

    private static float nextChores;

    private static void Chores()
    {
        if (Time.time < nextChores) return;
        nextChores = Time.time + 2f;

        HarvestAll();
        SweepStorages();
        TendMachines();
        foreach (var s in Tiles<Smelter>(TileType.Smelter))
            if (s.OutputCount > 0) { int n = s.TakeOutput(999, out var it); if (n > 0) Inventory.Instance.AddItem(it, n); }
        foreach (var k in Tiles<CatKitchen>(TileType.Kitchen))
            if (k.OutputCount > 0 && !MachineFarm.LinkedToStorage(k)) { int n = k.TakeOutput(999, out var it); if (n > 0) Inventory.Instance.AddItem(it, n); }
        foreach (var c in Tiles<Composter>(TileType.Composter))
            if (c.OutputCount > 0) { int n = c.TakeOutput(999); if (n > 0) Inventory.Instance.AddItem(NetWorld.Item("Item_Compost"), n); }
        var svc = ShuttleService.Instance;
        if (svc != null && !autoLoading)
            foreach (var s in new List<ShuttleSave>(svc.Shuttles))
                if (ShuttleService.CanComplete(s)) LoadShuttle(s);
        int wantPlots = Mathf.Max(10 + 5 * (CenterCore.CurrentLevel - 1), Mathf.Max(Mathf.CeilToInt(NextPlotRequirement() * PlotFactor), DemandPlots()));
        wantPlots = Mathf.Min(MaxBotPlots, Mathf.Max(wantPlots, Tiles<AutoSeeder>(TileType.AutoSeeder).Count * PlotsPerLine));
        if (PlotCount() < wantPlots) MakePlots(Mathf.Min(4, wantPlots - PlotCount()));
        PlantSmart();
        SellSurplus();
        SpendSurplus();
    }

    // 사람처럼 남는 코인을 마을에 씀: 축제, 장식
    public static bool SpendCoins = true;
    private static readonly int[] CharmByLevel = { 0, 0, 6, 16, 30, 60, 110, 160 };

    private static void SpendSurplus()
    {
        if (!SpendCoins) return;
        long reserve = NextFunds() + 2000 * CenterCore.CurrentLevel;
        if (Coins() > reserve + TownLife.FestivalCost * 2 && TownLife.FestivalBlock() == null) HoldFestival();
        int want = CharmByLevel[Mathf.Clamp(CenterCore.CurrentLevel, 0, CharmByLevel.Length - 1)];
        if (TownLife.Charm < want && Coins() > reserve) Decorate(want, reserve);
        // 남는 코인이 셔틀 여러 번 좌석값을 넘으면 좌석을 늘림
        if (TownLife.SeatsUnlocked && TownLife.Instance != null)
        {
            long spare = Coins() - reserve;
            int seats = spare > TownLife.SeatCost * 3 * 6 ? 3 : spare > TownLife.SeatCost * 4 ? 1 : 0;
            if (seats != TownLife.ExtraSeats) { TownLife.Instance.SetSeats(seats); Log($"  이주 지원 좌석 +{seats}"); }
        }
    }

    private static long NextFunds()
    {
        var next = CenterCore.Current != null ? CenterCore.Current.NextStage : null;
        if (next == null) return 0;
        foreach (var r in next.requirements)
            if (r.type == CoreRequirementType.Coins) return CoreProgress.Target(r);
        return 0;
    }

    private static void HoldFestival()
    {
        long cost = TownLife.FestivalCost;
        TownLife.Instance?.Hold();
        if (TownLife.FestivalActive) Log($"  마을 축제 ({cost:N0}코인)");
    }

    // 살 수 있는 가장 매력 좋은 장식부터 사서 놓기
    private static void Decorate(int targetCharm, long keep = 0)
    {
        var inv = Inventory.Instance;
        for (int i = 0; i < inv.SlotCount; i++)
        {
            var s = inv.GetSlot(i);
            if (s == null || s.IsEmpty || s.Item.charm <= 0) continue;
            var deco = s.Item;
            if (Place(deco, 1)) Log($"  장식 {deco.itemName} (매력 ★{TownLife.Charm})");
            return;
        }
        if (TownLife.Charm >= targetCharm) return;
        var shop = ShopManager.Instance;
        FarmingItemData best = null;
        foreach (var it in shop.CurrentStock)
        {
            if (it == null || it.charm <= 0 || !shop.IsPurchasable(it) || Coins() - shop.BuyPrice(it) < keep) continue;
            if (best == null || it.charm > best.charm) best = it;
        }
        if (best != null && shop.TryBuy(best, 1)) Log($"  구매 {best.itemName} ({shop.BuyPrice(best):N0}코인)");
    }

    private static readonly MethodInfo tryPlant   = typeof(PlayerTileController).GetMethod("TryPlantSeed", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo tryHarvest = typeof(PlayerTileController).GetMethod("TryHarvest",   BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo tryPlace   = typeof(PlayerTileController).GetMethod("TryPlaceTile", BindingFlags.Instance | BindingFlags.NonPublic);

    private static List<PlantTile> Plots()
    {
        var list = new List<PlantTile>();
        foreach (var t in Tiles<PlantTile>(TileType.Plant)) list.Add(t);
        return list;
    }

    private static int PlotCount() => Plots().Count;

    private static void HarvestAll()
    {
        var ptc = PlayerTileController.Instance;
        bool any = false;
        foreach (var p in Plots())
            if (p.CurrentState == PlantTile.PlantState.ReadyToHarvest && !CoveredByHarvester(p)) { tryHarvest.Invoke(ptc, new object[] { p.GridPosition }); any = true; }
        if (any && miningBlock == null) GoTo(FarmCenter());
    }

    private static bool HasEmptyPlot()
    {
        foreach (var p in Plots()) if (p.CurrentState == PlantTile.PlantState.Empty && p.IsPowered) return true;
        return false;
    }

    private static bool AnyGrowing()
    {
        foreach (var p in Plots()) if (p.CurrentState != PlantTile.PlantState.Empty) return true;
        return false;
    }

    private static bool Plant(FarmingItemData seedItem)
    {
        var seed = seedItem ?? FirstSeed();
        if (seed == null || Count(seed) <= 0) return false;
        foreach (var p in Plots())
        {
            if (p.CurrentState != PlantTile.PlantState.Empty || (!p.IsPowered && seed.seedData.trait != CropTrait.Shade)) continue;
            if (p.SoilKind != SoilType.None && p.SoilKind != Soil.Of(seed)) continue;
            if (seedItem == null && SeederCovers(p)) continue; // 콕 집은 씨앗은 어디든 손으로
            Select(seed);
            tryPlant.Invoke(PlayerTileController.Instance, new object[] { p.GridPosition });
            return true;
        }
        return false;
    }

    private static void PlantAny()
    {
        if (!Plant(null)) Buy(NetWorld.Item("Item_Seed_BlueStar"), 6);
    }

    private static void PlantSmart()
    {
        foreach (var p in Plots())
        {
            if (p.CurrentState != PlantTile.PlantState.Empty || SeederCovers(p) || p.SoilKind != SoilType.None) continue;
            var seed = WantedSeed();
            if (seed == null) return;
            if (!p.IsPowered && seed.seedData.trait != CropTrait.Shade) continue;
            Select(seed);
            tryPlant.Invoke(PlayerTileController.Instance, new object[] { p.GridPosition });
        }
    }

    private static readonly Dictionary<FarmingItemData, int> wanted = new();

    private static FarmingItemData WantedSeed()
    {
        wanted.Clear();
        var svc = ShuttleService.Instance;
        if (svc != null)
            foreach (var s in svc.Shuttles)
            {
                if (s.away) continue;
                foreach (var l in s.cargo)
                {
                    var it = NetWorld.Item(l.item);
                    if (it == null || l.Missing <= 0) continue;
                    var r = Recipe(it);
                    if (r != null && r.IsKitchenRecipe) { foreach (var ing in r.ingredients) Want(ing.item, ing.amount * l.Missing); }
                    else Want(it, l.Missing);
                }
            }
        var qm = QuestManager.Instance;
        if (qm?.ActiveObjectives != null)
            foreach (var o in qm.ActiveObjectives)
            {
                if (o.IsComplete) continue;
                if (o.type == QuestObjectiveType.UpgradeTech)
                {
                    var n = TechTreeManager.Instance.GetNode(o.targetTechNodeId);
                    if (n != null && !n.IsMaxed && n.itemCosts != null && n.currentLevel < n.itemCosts.Length)
                        Want(NetWorld.Item(n.itemCosts[n.currentLevel]), n.itemAmounts[n.currentLevel]);
                }
                if (o.type == QuestObjectiveType.AutoDish)
                {
                    var linked = Tiles<CatKitchen>(TileType.Kitchen).Find(k => MachineFarm.LinkedToStorage(k));
                    if (linked != null && linked.Recipe != null)
                        foreach (var ing in linked.Recipe.ingredients) Want(ing.item, ing.amount * 4);
                }
                if (o.targetItem == null) continue;
                if (o.targetItem.isCropProduct) Want(o.targetItem, o.targetAmount);
                var r = Recipe(o.targetItem);
                if (r != null) foreach (var ing in r.ingredients) if (ing.item != null && ing.item.isCropProduct) Want(ing.item, ing.amount * 2);
            }
        foreach (var kv in wanted)
        {
            if (Count(kv.Key) + Growing(kv.Key) >= kv.Value) continue;
            var s = SeedFor(kv.Key);
            if (s == null) continue;
            if (Count(s) <= 0) Buy(s, 6);
            if (Count(s) > 0) return s;
        }
        foreach (var it in GameDataRegistry.FindAll<FarmingItemData>())
        {
            if (it == null || it.itemType != FarmingItemType.Seed || it.seedData == null) continue;
            var crop = CropOf(it);
            if (crop != null && !HarvestLog.Contains(crop) && it.unlockCoreLevel <= CenterCore.CurrentLevel && Growing(crop) == 0)
            {
                if (Count(it) <= 0) Buy(it, 3);
                if (Count(it) > 0) return it;
            }
        }
        FarmingItemData best = null;
        int bestPrice = -1;
        var inv = Inventory.Instance;
        for (int i = 0; i < inv.SlotCount; i++)
        {
            var s = inv.GetSlot(i);
            if (s == null || s.IsEmpty || s.Item.itemType != FarmingItemType.Seed) continue;
            var crop = CropOf(s.Item);
            int price = crop != null ? crop.basePrice : 0;
            if (price > bestPrice) { bestPrice = price; best = s.Item; }
        }
        return best;
    }

    private static void Want(FarmingItemData it, int n)
    {
        if (it == null || !it.isCropProduct) return;
        wanted[it] = (wanted.TryGetValue(it, out var v) ? v : 0) + n;
    }

    private static int Growing(FarmingItemData crop)
    {
        int n = 0;
        foreach (var p in Plots()) if (p.CurrentSeed != null && CropOf(p.CurrentSeed) == crop) n++;
        return n;
    }

    private static void EnsureGrowing(FarmingItemData crop, int plots)
    {
        if (Growing(crop) >= plots) return;
        var s = SeedFor(crop);
        if (s == null) return;
        if (Count(s) <= 0) Buy(s, plots);
        if (AddLine(s)) return;
        if (!Plant(s) && Count(s) > 0 && !HasEmptyPlot()) MakePlots(2);
    }

    private static void NewVariety()
    {
        foreach (var it in GameDataRegistry.FindAll<FarmingItemData>())
        {
            if (it == null || it.itemType != FarmingItemType.Seed || it.seedData == null) continue;
            var crop = CropOf(it);
            if (crop == null || HarvestLog.Contains(crop) || it.unlockCoreLevel > CenterCore.CurrentLevel) continue;
            if (Growing(crop) > 0) continue;
            if (Count(it) <= 0) Buy(it, 2);
            if (Count(it) > 0) { if (!Plant(it)) MakePlots(2); return; }
        }
    }

    private static void MakePlots(int n)
    {
        var plot = NetWorld.Item("PlantTile");
        for (int i = 0; i < n; i++)
        {
            if (Count(plot) <= 0 && !Make(plot, 1, 1)) return;
            if (!Place(plot, 1)) return;
        }
    }

    // 메가 기계: 메가 ← 고급 ← 기본 ← 합금 ← 주괴 ← 광석
    private const int MaxDepth = 10;

    private static bool Obtain(FarmingItemData item, int need, int depth)
    {
        if (item == null || depth > MaxDepth) return false;
        if (Count(item) >= need) return true;
        var r = Recipe(item);
        if (r != null && r.IsKitchenRecipe) { Cook(r, need, depth + 1); return false; }
        if (item.isCropProduct) { EnsureGrowing(item, 4); return false; }
        if (Ore(item) != null) { Mine(item, need); return false; }
        var smelt = Furnace(item);
        if (smelt != null) { Smelt(smelt, need, depth + 1); return false; }
        if (item.itemType == FarmingItemType.Seed) { Buy(item, need - Count(item)); return Count(item) >= need; }
        if (r != null) return Make(item, need, depth + 1);
        return false;
    }

    private static bool Make(FarmingItemData item, int need, int depth)
    {
        if (item == null || depth > MaxDepth) return false;
        if (Count(item) >= need) return true;
        if (item.name == "Item_Compost") { Compost(need - Count(item), depth + 1); return false; }
        var r = Recipe(item);
        if (r == null) return false;
        if (r.IsKitchenRecipe) { Cook(r, need, depth + 1); return false; }
        if (r.IsFurnaceRecipe) { Smelt(r, need, depth + 1); return false; }
        var cm = CraftingManager.Instance;
        if (!cm.IsRecipeUnlocked(r)) return false;
        if (!Inventory.Instance.CanAddItem(item)) { SellSurplus(force: true); return false; }
        foreach (var ing in r.ingredients)
            if (ing.item != null && Count(ing.item) < ing.amount)
            {
                Obtain(ing.item, ing.amount, depth + 1);
                if (Count(ing.item) < ing.amount) return false;
            }
        if (r.station == CraftingStation.Smelter && !cm.IsStationAvailable(CraftingStation.Smelter))
        {
            var s = Nearest(Tiles<Smelter>(TileType.Smelter));
            if (s == null) { Place(NetWorld.Item("Item_Smelter"), depth + 1); return false; }
            GoTo(s.transform.position + Vector3.down * 1.2f);
        }
        bool ok = cm.Craft(r);
        if (ok) Log($"  제작 {item.itemName}");
        return ok && Count(item) >= need;
    }

    private static void Compost(int need, int depth)
    {
        var bin = Nearest(Tiles<Composter>(TileType.Composter));
        if (bin == null) { Place(NetWorld.Item("Item_Composter"), depth + 1); return; }
        if (bin.Points >= bin.PointsPerCompost * Mathf.Max(1, need) || bin.IsWorking) return;
        var inv = Inventory.Instance;
        for (int i = 0; i < inv.SlotCount && bin.Points < bin.PointCapacity; i++)
        {
            var s = inv.GetSlot(i);
            if (s == null || s.IsEmpty || s.Item.itemType != FarmingItemType.Seed || s.Count <= 4) continue;
            int n = Mathf.Min(s.Count - 4, bin.RoomFor(s.Item));
            if (n <= 0) continue;
            var seed = s.Item;
            GoTo(bin.transform.position + Vector3.down * 1.2f);
            if (!inv.ConsumeItem(seed, n)) continue;
            int put = bin.Insert(seed, n);
            if (put < n) inv.AddItem(seed, n - put);
            Log($"  퇴비통에 {seed.itemName} {put}개");
        }
    }

    private static bool Place(FarmingItemData item, int depth)
    {
        if (item == null || depth > MaxDepth) return false;
        if (Count(item) <= 0) { Make(item, 1, depth + 1); if (Count(item) <= 0) return false; }
        var anchor = FindSpot(item);
        if (anchor == null) { Log("  자리 없음: " + item.itemName); return false; }
        Select(item);
        var ptc = PlayerTileController.Instance;
        GoTo(GridManager.Instance.GridToWorld(anchor.Value) + Vector3.down);
        tryPlace.Invoke(ptc, new object[] { anchor.Value });
        bool ok = GridManager.Instance.GetTile(anchor.Value) != null;
        if (ok && item.name != "PlantTile") Log($"  설치 {item.itemName}");
        return ok;
    }

    private static Vector2Int? FindSpot(FarmingItemData item)
    {
        var gm = GridManager.Instance;
        var em = EnergyManager.Instance;
        var core = CenterCore.Current;
        int w = item.tileWidth, h = item.tileHeight;
        bool plot = item.name == "PlantTile";
        bool farmMachine = item.tilePrefab != null
                           && (item.tilePrefab.GetComponent<AutoHarvester>() != null || item.tilePrefab.GetComponent<AutoSeeder>() != null);
        Vector2Int center = core.GridPosition + new Vector2Int(1, 1);
        if (plot) center += new Vector2Int(-6, -6);
        if (farmMachine && PlotCount() > 0) center = gm.WorldToGrid(FarmCenter());
        for (int r = plot || farmMachine ? 0 : 4; r < 24; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var a = center + new Vector2Int(dx, dy);
                    if (!plot && !farmMachine && !gm.CanPlaceAt(a - Vector2Int.one, w + 2, h + 2)) continue;
                    if (!gm.CanPlaceAt(a, w, h, item.tilePrefab)) continue;
                    if (em != null && !em.HasEnergy(a, a + new Vector2Int(w - 1, h - 1))) continue;
                    return a;
                }
        return null;
    }

    private static void Mine(FarmingItemData ore, int need)
    {
        if (miningBlock != null) return;
        var data = Ore(ore);
        var drill = BestDrill();
        if (drill == null || drill.toolGrade < data.minToolGrade)
        {
            foreach (var it in GameDataRegistry.FindAll<FarmingItemData>())
                if (it != null && it.IsDrill && it.toolGrade == data.minToolGrade) { Make(it, 1, 1); break; }
            return;
        }
        ResourceBlock best = null;
        float bd = float.MaxValue;
        Vector2 me = PlayerTileController.Instance.transform.position;
        foreach (var b in ResourceBlock.Active)
        {
            if (b == null || b.DropItem != ore || b.RemainingResources <= 0) continue;
            float d = Vector2.Distance(me, b.transform.position);
            if (d < bd) { bd = d; best = b; }
        }
        if (best == null) { Log("  광석 없음: " + ore.itemName); return; }
        Select(drill);
        GoTo(best.transform.position + Vector3.down * 1.1f);
        miningBlock = best;
        miningFor = ore;
        miningTarget = need;
        miningSince = Time.time;
        best.StartMining();
    }

    private static float miningSince;

    private static void UpdateMining()
    {
        if (ReferenceEquals(miningBlock, null)) return;
        if (miningBlock == null || miningBlock.RemainingResources <= 0 || Count(miningFor) >= miningTarget || Time.time - miningSince > 120f)
        {
            if (miningBlock != null) miningBlock.StopMining();
            miningBlock = null;
            return;
        }
        var me = PlayerTileController.Instance.transform.position;
        if (Vector2.Distance(me, miningBlock.transform.position) > 2f) GoTo(miningBlock.transform.position + Vector3.down * 1.1f);
        var drill = BestDrill();
        if (drill != null && PlayerTileController.Instance.SelectedItem != drill) { Select(drill); miningBlock.StopMining(); miningBlock.StartMining(); }
    }

    private static void Smelt(CraftingRecipe r, int need, int depth)
    {
        var smelter = Nearest(Tiles<Smelter>(TileType.Smelter));
        if (smelter == null) { Place(NetWorld.Item("Item_Smelter"), depth + 1); return; }
        var ore = r.ingredients[0].item;
        int per = r.ingredients[0].amount;
        if (smelter.OutputCount > 0) { int n = smelter.TakeOutput(999, out var it); if (n > 0) Inventory.Instance.AddItem(it, n); }
        if (smelter.HasLeftover && smelter.InputItem != ore) { int n = smelter.TakeInput(999, out var left); if (n > 0) Inventory.Instance.AddItem(left, n); }
        if (smelter.InputCount > 0 && smelter.InputItem != ore)
        {
            foreach (var s in Tiles<Smelter>(TileType.Smelter))
                if (s.InputCount == 0 || s.InputItem == ore) { smelter = s; break; }
            if (smelter.InputCount > 0 && smelter.InputItem != ore) { if (Tiles<Smelter>(TileType.Smelter).Count < 3) Place(NetWorld.Item("Item_Smelter"), depth + 1); return; }
        }
        int queued = smelter.InputItem == ore ? smelter.InputCount / per : 0;
        int still = need - Count(r.outputItem) - queued;
        if (still <= 0) return;
        int oreNeed = still * per;
        if (Count(ore) < per) { Mine(ore, oreNeed); return; }
        int ins = smelter.InsertOre(ore, Mathf.Min(Count(ore), oreNeed));
        if (ins > 0) Inventory.Instance.ConsumeItem(ore, ins);
    }

    private static readonly HashSet<CraftingRecipe> cooking = new();
    private static readonly Dictionary<CraftingRecipe, float> wantedAt = new();

    private static bool Wanted(CraftingRecipe r) => r != null && wantedAt.TryGetValue(r, out float t) && Time.time - t < 30f;

    private static void Cook(CraftingRecipe r, int need, int depth)
    {
        if (!cooking.Add(r)) return;
        try { CookInner(r, need, depth); }
        finally { cooking.Remove(r); }
    }

    private static void CookInner(CraftingRecipe r, int need, int depth)
    {
        var kitchens = Tiles<CatKitchen>(TileType.Kitchen);
        if (kitchens.Count == 0) { Place(NetWorld.Item("Item_CatKitchen"), 0); return; }
        foreach (var k in kitchens)
            if (k.OutputCount > 0 && !MachineFarm.LinkedToStorage(k)) { int n = k.TakeOutput(999, out var it); if (n > 0) Inventory.Instance.AddItem(it, n); }
        wantedAt[r] = Time.time;
        CatKitchen kitchen = kitchens.Find(k => k.Recipe == r) ?? kitchens.Find(k => k.Recipe == null)
                             ?? kitchens.Find(k => k.CanChangeRecipe && !Wanted(k.Recipe) && !cooking.Contains(k.Recipe));
        if (kitchen == null)
        {
            if (kitchens.Count < 6) { Place(NetWorld.Item("Item_CatKitchen"), 0); return; }
            kitchen = kitchens.Find(k => !cooking.Contains(k.Recipe) && !Wanted(k.Recipe));
            if (kitchen == null) return;
            foreach (var it in new List<FarmingItemData>(kitchen.InputItems)) { int n = kitchen.TakeInput(it, 999); if (n > 0) Inventory.Instance.AddItem(it, n); }
            Log($"  주방 메뉴 바꿈: {(kitchen.Recipe != null ? kitchen.Recipe.outputItem.itemName : "-")} → {r.outputItem.itemName}");
        }
        if (kitchen.Recipe != r && !kitchen.SetRecipe(r)) return;
        foreach (var ing in r.ingredients)
        {
            if (ing.item == null) continue;
            int have = Count(ing.item);
            if (have + kitchen.GetInput(ing.item) < ing.amount) { Obtain(ing.item, ing.amount - kitchen.GetInput(ing.item), depth + 1); continue; }
            int put = kitchen.InsertIngredient(ing.item, Mathf.Min(have, ing.amount * 3));
            if (put > 0) Inventory.Instance.ConsumeItem(ing.item, put);
        }
    }

    private static int   focusShuttle = -1;
    private static float focusSince;

    private static void LoadShuttle(ShuttleSave s)
    {
        var svc = ShuttleService.Instance;
        if (svc == null || s == null || s.away) return;
        string fam = s.family;
        int n = svc.LoadFromBag(s);
        if (n > 0 && s.away) Log($"  셔틀 출발 ({fam} 가족 {s.cats.Count}마리) · 주민 {ShuttleService.Population}");
    }

    private static void Shuttles(int depth)
    {
        var svc = ShuttleService.Instance;
        if (svc == null) return;
        if (svc.Shuttles.Count == 0)
        {
            if (Tiles<ShuttlePad>(TileType.ShuttlePad).Count == 0 && ShuttleService.PadItem != null) Place(ShuttleService.PadItem, depth + 1);
            return;
        }
        foreach (var s in new List<ShuttleSave>(svc.Shuttles))
            if (ShuttleService.CanComplete(s)) { LoadShuttle(s); return; }

        ShuttleSave best = null;
        float bestScore = float.MaxValue;
        foreach (var s in svc.Shuttles)
        {
            if (s.away) continue;
            float score = 0f;
            bool feasible = true;
            foreach (var l in s.cargo)
            {
                var it = NetWorld.Item(l.item);
                if (it == null) { feasible = false; break; }
                int lack = l.Missing - Count(it);
                if (lack <= 0) continue;
                var r = Recipe(it);
                if (it.isGolden) feasible = false;
                else if (r != null && !CraftingManager.Instance.IsRecipeUnlocked(r)) feasible = false;
                score += lack * Mathf.Max(1, it.basePrice) * (r != null && r.IsKitchenRecipe ? 1.5f : 1f);
            }
            if (!feasible) { svc.Reroll(s); Log("  다른 짐 받기 (어려움)"); return; }
            if (score < bestScore) { bestScore = score; best = s; }
        }
        if (best == null) { SellSurplus(); return; }
        var focus = svc.Find(focusShuttle);
        if (focus == null || focus.away) { focusShuttle = best.id; focusSince = Time.time; focus = best; }
        else if (Time.time - focusSince > 240f)
        {
            Log("  다른 짐 받기 (4분째 못 채움): " + ShuttleUI.MissingText(focus));
            svc.Reroll(focus);
            focusShuttle = -1;
            return;
        }
        best = focus;
        if (ShuttleService.Loadable(best) > 0) LoadShuttle(best);
        foreach (var l in best.cargo)
        {
            var it = NetWorld.Item(l.item);
            if (l.Missing > 0 && Count(it) < l.Missing) { Obtain(it, l.Missing, depth + 1); return; }
        }
    }

    private static void EarnCoins()
    {
        Shuttles(0);
        SellSurplus(force: true);
    }

    private static void SellSurplus(bool force = false)
    {
        var shop = ShopManager.Instance;
        var inv = Inventory.Instance;
        if (shop == null) return;
        bool full = !HasFreeSlot();
        for (int i = 0; i < inv.SlotCount; i++)
        {
            var s = inv.GetSlot(i);
            if (s == null || s.IsEmpty) continue;
            if (s.Item.isCropProduct)
            {
                // 기계 농장 시기엔 다음 셔틀 짐으로 쌓아 둠
                int keep = force || CenterCore.CurrentLevel < 4 ? KeepFor(s.Item) : Mathf.Max(KeepFor(s.Item), CropBuffer);
                int extra = s.Count - keep;
                if (extra > (force || full ? 0 : 10)) shop.TrySell(s.Item, extra);
            }
            else if (s.Item.itemType == FarmingItemType.Seed && s.Count > (full ? 4 : 15) && ShopManager.IsSellable(s.Item))
                shop.TrySell(s.Item, s.Count - (full ? 4 : 15));
            if (full && s.Item != null && s.Item.isGolden && ShopManager.IsSellable(s.Item))
                shop.TrySell(s.Item, s.Count);
        }
    }

    private static bool HasFreeSlot()
    {
        var inv = Inventory.Instance;
        for (int i = 0; i < inv.SlotCount; i++) { var s = inv.GetSlot(i); if (s == null || s.IsEmpty) return true; }
        return false;
    }

    private static int KeepFor(FarmingItemData item)
    {
        WantedSeed();
        return wanted.TryGetValue(item, out var n) ? n + 5 : 0;
    }

    private static void Buy(FarmingItemData seed, int n)
    {
        var shop = ShopManager.Instance;
        if (shop == null || seed == null || n <= 0 || !shop.IsPurchasable(seed)) return;
        int price = shop.BuyPrice(seed) * n;
        if (Coins() < price + 50) n = (int)Mathf.Max(0, (Coins() - 50) / Mathf.Max(1, shop.BuyPrice(seed)));
        if (n > 0 && shop.TryBuy(seed, n)) Log($"  구매 {seed.itemName} ×{n}");
    }

    private static void Select(FarmingItemData item) => PlayerTileController.Instance.SelectItem(item);

    private static void GoTo(Vector3 p)
    {
        var t = PlayerTileController.Instance.transform;
        walkDistance += Vector2.Distance(t.position, p);
        // 걷기 대신 순간이동, 거리만 기록
        t.position = new Vector3(p.x, p.y, t.position.z);
    }

    private static Vector3 FarmCenter()
    {
        Vector3 c = Vector3.zero;
        int n = 0;
        foreach (var p in Plots())
        {
            if (p.SoilKind != SoilType.None) continue;
            c += p.transform.position;
            n++;
        }
        return n > 0 ? c / n : PlayerTileController.Instance.transform.position;
    }

    private static long Coins()
    {
        var f = typeof(CurrencyManager).GetField("_currency", BindingFlags.Instance | BindingFlags.NonPublic);
        return f != null ? (long)f.GetValue(CurrencyManager.Instance) : 0;
    }

    private static int Count(FarmingItemData it) => it != null && Inventory.Instance != null ? Inventory.Instance.GetCount(it) : 0;

    private static FarmingItemData FirstSeed()
    {
        var inv = Inventory.Instance;
        for (int i = 0; i < inv.SlotCount; i++)
        {
            var s = inv.GetSlot(i);
            if (s != null && !s.IsEmpty && s.Item.itemType == FarmingItemType.Seed) return s.Item;
        }
        return null;
    }

    private static FarmingItemData BestDrill()
    {
        FarmingItemData best = null;
        var inv = Inventory.Instance;
        for (int i = 0; i < inv.SlotCount; i++)
        {
            var s = inv.GetSlot(i);
            if (s == null || s.IsEmpty || !s.Item.IsDrill) continue;
            if (best == null || s.Item.toolGrade > best.toolGrade) best = s.Item;
        }
        return best;
    }

    private static FarmingItemData CropOf(FarmingItemData seed) => seed != null ? CropOf(seed.seedData) : null;
    private static FarmingItemData CropOf(SeedData sd)
    {
        if (sd?.drops == null) return null;
        foreach (var d in sd.drops) if (d.item != null && d.item.isCropProduct) return d.item;
        return null;
    }

    private static FarmingItemData SeedFor(FarmingItemData crop)
    {
        foreach (var it in GameDataRegistry.FindAll<FarmingItemData>())
            if (it != null && it.itemType == FarmingItemType.Seed && it.seedData != null && CropOf(it.seedData) == crop) return it;
        return null;
    }

    private static CraftingRecipe Recipe(FarmingItemData item)
    {
        var cm = CraftingManager.Instance;
        if (cm == null || item == null) return null;
        CraftingRecipe found = null;
        foreach (var r in cm.Recipes)
            if (r != null && r.outputItem == item && (found == null || (cm.IsRecipeUnlocked(r) && !cm.IsRecipeUnlocked(found)))) found = r;
        return found;
    }

    private static CraftingRecipe Furnace(FarmingItemData item)
    {
        foreach (var r in CraftingManager.Instance.Recipes)
            if (r != null && r.IsFurnaceRecipe && r.outputItem == item) return r;
        return null;
    }

    private static ResourceData Ore(FarmingItemData item)
    {
        foreach (var d in GameDataRegistry.FindAll<ResourceData>()) if (d != null && d.dropItem == item) return d;
        return null;
    }

    private static List<T> Tiles<T>(TileType type) where T : TileItem
    {
        var buf = new List<TileItem>();
        GridManager.Instance.CollectTiles(type, buf);
        var list = new List<T>();
        foreach (var t in buf) if (t is T x && !x.IsGhost) list.Add(x);
        return list;
    }

    private static System.Action[] seqSteps;
    private static float[]         seqDelays;
    private static int             seqIndex, seqCalm;
    private static double          seqAt;

    public static void Sequence(float[] delays, System.Action[] steps)
    {
        seqDelays = delays;
        seqSteps  = steps;
        seqIndex  = 0;
        seqCalm   = 0;
        seqAt     = -1;
        EditorApplication.update -= SeqTick;
        EditorApplication.update += SeqTick;
    }

    private static int seqFrame = -1;

    private static void SeqTick()
    {
        if (seqSteps == null || !EditorApplication.isPlaying) { EditorApplication.update -= SeqTick; return; }
        if (Time.frameCount == seqFrame) return;
        seqFrame = Time.frameCount;
        if (seqAt < 0)
        {
            // 프레임 안정될 때까지 대기
            seqCalm = Time.unscaledDeltaTime < 0.1f ? seqCalm + 1 : 0;
            if (seqCalm < 8) return;
            seqAt = EditorApplication.timeSinceStartup + (seqDelays.Length > 0 ? seqDelays[0] : 0f);
        }
        if (EditorApplication.timeSinceStartup < seqAt) return;
        try { seqSteps[seqIndex]?.Invoke(); }
        catch (System.Exception e) { Debug.LogException(e); }
        seqIndex++;
        if (seqIndex >= seqSteps.Length) { seqSteps = null; EditorApplication.update -= SeqTick; return; }
        seqAt = EditorApplication.timeSinceStartup + (seqIndex < seqDelays.Length ? seqDelays[seqIndex] : 0f);
    }

    public static void Shot(string name)
        => ScreenCapture.CaptureScreenshot(Application.dataPath + "/../Temp/McpScreenshots/" + name + ".png");

    public static bool Click(string label)
    {
        foreach (var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
        {
            var t = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (t != null && t.text == label && b.isActiveAndEnabled && b.interactable) { b.onClick.Invoke(); return true; }
        }
        return false;
    }

    private static T Nearest<T>(List<T> list) where T : TileItem
    {
        Vector2 me = PlayerTileController.Instance.transform.position;
        T best = null;
        float bd = float.MaxValue;
        foreach (var t in list) { float d = t.DistanceFrom(me); if (d < bd) { bd = d; best = t; } }
        return best;
    }
}
#endif
