using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 퀘스트 체인 에셋 코드로 생성
public static class QuestChainBuilder
{
    private const string QuestFolder = "Assets/Scripts/Debug_field/Quest/Chain";
    private const string ChainPath   = "Assets/Resources/QuestChain.asset";
    private const string StagesPath  = "Assets/Resources/CoreStages.asset";

    private struct Obj
    {
        public QuestObjectiveType type;
        public string item;
        public string tech;
        public int amount;
    }

    private struct Rew
    {
        public string item;
        public int amount;
    }

    private class Q
    {
        public string id, name, desc, talk, hint, padHint, tip;
        public Obj[] objs;
        public Rew[] rewards = new Rew[0];
        public int coins;
    }

    private class Chapter
    {
        public string title, goal, intro;
        public List<Q> quests = new();
    }

    private static Obj Plant(string seed, int n)    => new() { type = QuestObjectiveType.PlantSeed, item = seed, amount = n };
    private static Obj Harvest(string crop, int n)  => new() { type = QuestObjectiveType.HarvestPlant, item = crop, amount = n };
    private static Obj Place(string item, int n)    => new() { type = QuestObjectiveType.PlaceTile, item = item, amount = n };
    private static Obj Craft(string item, int n)    => new() { type = QuestObjectiveType.CraftItem, item = item, amount = n };
    private static Obj Collect(string item, int n)  => new() { type = QuestObjectiveType.CollectItem, item = item, amount = n };
    private static Obj Residents(int n)             => new() { type = QuestObjectiveType.Residents, amount = n };
    private static Obj Level(int n)                 => new() { type = QuestObjectiveType.CoreLevel, amount = n };
    private static Obj Varieties(int n)             => new() { type = QuestObjectiveType.CropVarieties, amount = n };
    private static Obj AutoHarvest(int n)           => new() { type = QuestObjectiveType.AutoHarvest, item = "AutoHarvester", amount = n };
    private static Obj Tech(string node, int n)     => new() { type = QuestObjectiveType.UpgradeTech, tech = node, amount = n };
    private static Obj LinkStorage(string item)     => new() { type = QuestObjectiveType.LinkStorage, item = item, amount = 1 };
    private static Obj AutoDepart(int n)            => new() { type = QuestObjectiveType.AutoDepart, amount = n };
    private static Obj MachineCrops(int n)          => new() { type = QuestObjectiveType.MachineCrops, amount = n };
    private static Obj AutoDish(int n)              => new() { type = QuestObjectiveType.AutoDish, amount = n };
    private static Obj Stockpile(string item, int n) => new() { type = QuestObjectiveType.Stockpile, item = item, amount = n };
    private static Obj FindSoil(string seed)        => new() { type = QuestObjectiveType.FindSoil, item = seed, amount = 1 };
    private static Obj SoilCrop(string seed, int n) => new() { type = QuestObjectiveType.SoilCrop, item = seed, amount = n };
    private static Obj Outposts(int n, string kind) => new() { type = QuestObjectiveType.Outposts, item = kind, amount = n };
    private static Obj RailDelivery(string item, int n) => new() { type = QuestObjectiveType.RailDelivery, item = item, amount = n };
    private static Obj Purifiers(int n)             => new() { type = QuestObjectiveType.Purifiers, amount = n };
    private static Obj Biomes(int n)                => new() { type = QuestObjectiveType.Biomes, amount = n };
    private static Obj Landmarks(int n)             => new() { type = QuestObjectiveType.Landmarks, amount = n };
    private static Obj Charm(int n)                 => new() { type = QuestObjectiveType.Charm, amount = n };
    private static Obj Festival()                   => new() { type = QuestObjectiveType.Festival, amount = 1 };
    private static Rew R(string item, int n)        => new() { item = item, amount = n };

    private struct Req
    {
        public CoreRequirementType type;
        public string item;
        public int amount;
        public bool power;
    }

    private static Req Facility(string item, int n, bool power) => new() { type = CoreRequirementType.Facility, item = item, amount = n, power = power };
    private static Req ReqVarieties(int n) => new() { type = CoreRequirementType.CropVarieties, amount = n };
    private static Req ReqResidents(int n) => new() { type = CoreRequirementType.Residents, amount = n };
    private static Req ReqCoins(int n)     => new() { type = CoreRequirementType.Coins, amount = n };
    private static Req ReqMachineCrops(int n) => new() { type = CoreRequirementType.MachineCrops, amount = n };
    private static Req ReqOutposts(int n)  => new() { type = CoreRequirementType.Outposts, amount = n };

    // 문명 Lv.2부터 승급 조건
    private static readonly Req[][] Levels =
    {
        new[] { Facility("Item_BasicLab", 1, true), Facility("PlantTile", 10, false), ReqResidents(3), ReqCoins(300) },
        new[] { Facility("Item_Greenhouse", 1, true), Facility("Item_WaterTower", 1, true), Facility("Item_CatKitchen", 1, false),
                Facility("PlantTile", 20, true), ReqVarieties(4), ReqResidents(20), ReqCoins(2000) },
        new[] { Facility("Item_Factory", 1, true), Facility("AutoSeeder", 1, false), Facility("AutoHarvester", 1, false),
                ReqMachineCrops(1), ReqVarieties(7), ReqResidents(50), ReqCoins(8000) },
        new[] { Facility("Item_Observatory", 1, true), Facility("Item_CommTower", 1, true),
                ReqMachineCrops(2), ReqOutposts(1), ReqVarieties(10), ReqResidents(90), ReqCoins(30000) },
        new[] { Facility("Item_ControlTower", 1, true), Facility("Item_Habitat", 2, true),
                ReqMachineCrops(4), ReqOutposts(2), ReqVarieties(12), ReqResidents(150), ReqCoins(120000) },
        new[] { Facility("Item_LaunchPad", 1, true), ReqMachineCrops(6), ReqOutposts(3), ReqVarieties(14), ReqResidents(220), ReqCoins(300000) },
    };

    private static List<Chapter> Define()
    {
        var chapters = new List<Chapter>();

        var c1 = new Chapter { title = "착륙 기지", goal = "땅을 정화해 첫 농장을 일구고, 첫 셔틀로 고양이들을 맞아요!", intro = "선발대 무사히 착륙! 이 별은 보랏빛 오염 안개투성이 — 에너지 코어가 정화한 땅에서 고양이들이 먹고 살 밭부터 일궈요." };
        c1.quests.Add(new Q
        {
            id = "C1_01_Plant", name = "첫 씨앗 심기",
            talk = "고양이들을 맞으려면 먹을 것부터! 우리 첫 밭에 블루스타를 심어 볼까요?",
            desc = "고양이들이 건너오려면 먹을 것이 있어야 해요. 착륙 기지 옆 밭에 블루스타 씨앗을 심어요.",
            hint = "1번 칸 씨앗을 고르고 빈 밭을 클릭 (누른 채 쓸면 여러 칸)",
            padHint = "LB/RB로 씨앗을 고르고 밭 앞에서 RT",
            tip = "블루스타는 20초면 자라요. 밭에 마우스를 잠시 올려 두면 남은 시간이 보여요.\n하늘색 테두리 안이 에너지 코어가 정화한 땅이에요 — 밭과 건물은 그 안에만 세울 수 있어요.",
            objs = new[] { Plant("Item_Seed_BlueStar", 6) },
            rewards = new[] { R("Item_Seed_BlueStar", 4) },
        });
        c1.quests.Add(new Q
        {
            id = "C1_02_Expand", name = "밭 넓히기",
            talk = "밭이 넓을수록 많이 거둬요. 가져온 플랜트를 깔아서 밭을 넓혀 봐요!",
            desc = "밭이 많을수록 더 많이 거둬요. 가방에 있는 플랜트(밭)를 깔고 씨앗을 심어요.",
            hint = "3번 칸 플랜트를 고르고 빈 땅 클릭 → 씨앗 심기",
            padHint = "LB/RB로 플랜트를 고르고 RT로 설치 → 씨앗 심기",
            tip = "설치물은 커서를 따라다니는 초록 그림자 자리에 놓여요 (빨강 = 못 놓는 자리).\n잘못 놓았으면 우클릭을 꾹 누르면 해체돼 돌려받아요.",
            objs = new[] { Place("PlantTile", 10), Plant(null, 4) },
            rewards = new[] { R("Item_WateringCan", 1) },
            coins = 50,
        });
        c1.quests.Add(new Q
        {
            id = "C1_03_Harvest", name = "첫 수확",
            talk = "와, 반짝반짝 다 자랐어요! 얼른 거둬들여요.",
            desc = "다 자란 작물은 반짝반짝! 거둬들여요.",
            hint = "다 자란 작물을 클릭 (무엇을 들고 있어도 돼요)",
            padHint = "다 자란 밭 앞에서 RT",
            tip = "수확하면 작물과 씨앗(가끔 2개)이 나와요. 빈 밭엔 바로 다시 심을 수 있어요.",
            objs = new[] { Harvest(null, 10) },
            rewards = new[] { R("Item_Seed_RedStar", 4), R("Item_Seed_YellowStar", 4) },
        });
        c1.quests.Add(new Q
        {
            id = "C1_04_Order", name = "첫 셔틀",
            talk = "착륙장의 셔틀에 블루스타를 실어 보내면, 고향별 고양이들을 태워 와요!",
            desc = "셔틀에 블루스타 5개를 실으면 고향별에 다녀오며 첫 고양이 가족을 태워 와요.",
            hint = "착륙장의 셔틀을 클릭하면 짐이 실려요 · 다 실으면 출발!",
            padHint = "셔틀 앞에서 A로 짐 싣기 · 다 실으면 출발!",
            tip = "셔틀 머리 위 말풍선이 실을 짐이에요. 다 실으면 짐값을 받고, 잠시 뒤 셔틀이 고양이들을 태우고 돌아와요.\n[셔틀](J) 창에서 짐과 태워 올 가족을 볼 수 있어요.",
            objs = new[] { Residents(3) },
            coins = 100,
        });
        c1.quests.Add(new Q
        {
            id = "C1_05_Stars", name = "별 삼총사",
            talk = "별 작물은 서로 붙어 있으면 더 많이 열린대요. 레드스타랑 옐로스타도 심어 봐요!",
            desc = "블루 · 레드 · 옐로스타를 서로 붙여 심으면 [별자리] 성향 덕분에 작물이 더 열려요!",
            hint = "레드스타 · 옐로스타 씨앗을 블루스타 옆에 심어요",
            padHint = "LB/RB로 레드 · 옐로스타 씨앗을 골라 블루스타 옆에 RT",
            tip = "작물마다 성향이 있어요. 밭에 마우스를 잠시 올려 두면 지금 받는 보너스가 보여요.",
            objs = new[] { Plant("Item_Seed_RedStar", 3), Plant("Item_Seed_YellowStar", 3) },
            coins = 50,
        });
        c1.quests.Add(new Q
        {
            id = "C1_06_Mine", name = "광석 캐기",
            talk = "건물을 지으려면 금속이 필요해요. 드릴로 구리랑 철 광석을 캐 와요!",
            desc = "건물을 지으려면 금속이 필요해요. 드릴로 구리와 철 광석을 캐요.",
            hint = "2번 칸 드릴을 들고 광석을 꾹 클릭 (가까이 가야 해요)",
            padHint = "LB/RB로 드릴을 고르고 광석 앞에서 RT를 꾹",
            tip = "구리는 주황빛, 철은 회색 바위예요. 화살표가 가까운 광석을 가리켜요.\n바위 하나에 광석이 넉넉히 들어 있으니 마음껏 캐도 돼요.",
            objs = new[] { Collect("Item_Copper", 20), Collect("Item_iron", 20) },
        });
        c1.quests.Add(new Q
        {
            id = "C1_06b_Purify", name = "땅 정화하기",
            talk = "보랏빛 안개 밖은 오염된 땅이라 아무것도 못 지어요. 캔 광석으로 정화탑을 세워서 땅을 되살려요!",
            desc = "정화탑은 둘레 땅의 오염을 걷어 내요. 오염된 땅에도 세울 수 있어서, 정화된 땅을 넓혀 가며 개척해요.",
            hint = "[제작](C) → 정화탑 만들기 → 하늘색 테두리 바로 밖에 설치",
            padHint = "십자키 ↑ 제작 → 정화탑 → 테두리 밖에서 RT로 설치",
            tip = "정화탑을 세우면 사각형으로 땅이 퍼지듯 정화돼요.\n센터코어가 발전할 때마다 코어가 정화하는 땅도 넓어져요.",
            objs = new[] { Purifiers(1) },
            rewards = new[] { R("Item_Scanner", 1), R("Item_iron", 6), R("Item_Copper", 4) },
            coins = 50,
        });
        c1.quests.Add(new Q
        {
            id = "C1_07_Smelter", name = "제련소 세우기",
            talk = "광석은 그대로는 못 써요. 제련소를 세워서 녹여 봐요!",
            desc = "광석은 그대로 못 써요. 제련소에서 녹여 주괴로 만들어요.",
            hint = "[제작](C) → 설비 탭 → 제련소 만들기 → 센터코어 곁에 설치",
            padHint = "십자키 ↑ 제작 → 설비 탭 → 제련소 → RT로 설치",
            tip = "제련소는 전기가 있어야 돌아가요 (센터코어 둘레 25칸). 설치물을 들면 전기 범위가 테두리로 보여요.",
            objs = new[] { Place("Item_Smelter", 1) },
        });
        c1.quests.Add(new Q
        {
            id = "C1_08_Ingot", name = "주괴 녹이기",
            talk = "제련소에 광석을 넣으면 반짝이는 주괴가 돼요!",
            desc = "제련소에 광석을 넣으면 3초마다 광석 2개가 주괴 1개로 녹아요.",
            hint = "광석을 든 채 제련소를 클릭하면 들어가요 · 다 녹으면 다시 클릭해 받기",
            padHint = "광석을 든 채 제련소 앞에서 A · 다 녹으면 A로 받기",
            tip = "한 번에 한 종류만 녹여요. 구리를 다 녹인 뒤 철을 넣어요.\n그냥 클릭하면 제련소 창이 열려 넣을 광석을 고를 수 있어요.",
            objs = new[] { Collect("Item_CopperIngot", 7), Collect("Item_IronIngot", 7) },
            coins = 50,
        });
        c1.quests.Add(new Q
        {
            id = "C1_09_Lab", name = "기초 연구소",
            talk = "연구소가 서면 우리 착륙 기지가 개척 캠프로 발전할 수 있어요!",
            desc = "연구소를 세우면 착륙 기지가 개척 캠프로 발전할 수 있어요.",
            hint = "[제작](C) → 재료 탭 → 합금, 문명 탭 → 기초 연구소 → 설치",
            padHint = "십자키 ↑ 제작 → 재료 탭 합금 · 문명 탭 기초 연구소 → RT로 설치",
            tip = "합금은 제련소 곁(5칸)에서 만들어요. 문명 시설은 전기가 들어와야 문명 조건에 세어져요.",
            objs = new[] { Craft("item_Alloy", 1), Place("Item_BasicLab", 1) },
        });
        c1.quests.Add(new Q
        {
            id = "C1_10_Civ", name = "개척 캠프로!",
            talk = "조건만 채우면 개척 캠프가 돼요. 위쪽 문명 알림판을 봐요!",
            desc = "센터코어의 조건을 모두 채우면 문명이 한 단계 발전해요.",
            hint = "위쪽 문명 알림판의 조건을 채워요 (밭 10칸 · 주민 3마리 · 300코인)",
            tip = "문명이 발전하면 새 씨앗 · 새 건물 · 새 요리가 열려요.",
            objs = new[] { Level(2) },
        });
        chapters.Add(c1);

        var c2 = new Chapter { title = "개척 캠프", goal = "요리와 새 작물로 주민을 늘려 농업 마을로!", intro = "첫 주민들이 왔어요! 요리와 새 작물로 더 많은 고양이를 맞을 준비를 해요." };
        c2.quests.Add(new Q
        {
            id = "C2_01_Kitchen", name = "고양이 주방",
            talk = "작물을 요리하면 훨씬 비싸게 팔려요. 우리도 주방을 지어 봐요!",
            desc = "작물로 요리하면 훨씬 비싸게 팔려요. 고양이 주방을 지어요.",
            hint = "[제작](C) → 농사 탭 → 고양이 주방 → 전기 범위 안에 설치",
            padHint = "십자키 ↑ 제작 → 농사 탭 → 고양이 주방 → RT로 설치",
            tip = "주방(2×2)엔 철 주괴 4 · 구리 주괴 3 · 합금 1이 들어가요.",
            objs = new[] { Place("Item_CatKitchen", 1) },
            rewards = new[] { R("Item_Seed_Catnip", 4) },
        });
        c2.quests.Add(new Q
        {
            id = "C2_02_Cook", name = "첫 요리: 별빛 잼",
            talk = "별 작물 세 가지면 별빛 잼을 만들 수 있어요. 셔틀 짐으로 보내면 고향 고양이들이 엄청 좋아할 거예요!",
            desc = "블루 · 레드 · 옐로스타를 하나씩 넣으면 별빛 잼이 돼요.",
            hint = "주방 클릭 → 별빛 잼 → [재료 넣기]",
            padHint = "주방 앞에서 A → 별빛 잼 → [재료 넣기]",
            tip = "요리는 잠시 걸려요. 다 되면 주방을 클릭해 받아요! 주방을 창고와 연결하면 재료를 알아서 가져가요.",
            objs = new[] { Craft("Item_StarJam", 1) },
            coins = 100,
        });
        c2.quests.Add(new Q
        {
            id = "C2_03_Radish", name = "달빛 무",
            talk = "새 씨앗이 도착했어요! 달빛 무는 밤에 쑥쑥 자란대요.",
            desc = "개척 캠프가 되면서 새 씨앗을 받았어요. 달빛 무는 밤에 두 배로 빨리 자라요.",
            hint = "가방(Tab)의 달빛 무 씨앗을 핫바로 옮겨 심고 거둬요",
            padHint = "Y로 가방을 열어 달빛 무 씨앗을 핫바로 → 심고 거두기",
            tip = "씨앗이 모자라면 [상점](B)에서 사요. 온실을 지을 때 달빛 무 3개가 필요해요.",
            objs = new[] { Plant("Item_Seed_MoonRadish", 3), Harvest("MoonRadish", 3) },
            rewards = new[] { R("Item_Seed_MoonRadish", 3) },
        });
        c2.quests.Add(new Q
        {
            id = "C2_03b_Explore", name = "주변 탐사",
            talk = "이 별엔 모래 언덕 말고도 눈벌판, 수정 들판, 이끼 늪이 있대요! 스캐너를 들고 가까운 지역부터 둘러봐요.",
            desc = "착륙지 바깥엔 저마다 다른 광맥과 토양이 있는 지역(바이옴)이 넷 있어요. 새 지역을 찾고 탐사 유적을 조사해요.",
            hint = "화살표를 따라 새 지역으로 · 탐사 스캐너(클릭)로 유적 방향 찾기 → 유적 클릭",
            padHint = "화살표를 따라 새 지역으로 · 스캐너를 들고 RT → 유적 곁에서 A",
            tip = "새 지역에 처음 들어가면 이름이 크게 떠요. 유적을 조사하면 그 지역 광물과 코인을 얻어요.\n바이옴마다 나는 광석이 달라요: 혹한지 알루미늄 · 리튬, 수정 지대 티타늄 · 다이아몬드, 이끼 습지 우라늄.",
            objs = new[] { Biomes(2), Landmarks(1) },
            coins = 300,
        });
        // id는 세이브 호환용으로 유지
        c2.quests.Add(new Q
        {
            id = "C3_03b_Storage", name = "창고 짓기",
            talk = "가방이 점점 꽉 차요! 창고를 하나 지어 두면 든든해요.",
            desc = "창고에는 무엇이든 넣어 둘 수 있어요. 창고를 열면 가방의 물건을 눌러 옮길 수 있어요.",
            hint = "[제작](C) → 창고 탭 → 일반창고 → 기지 근처에 설치",
            padHint = "십자키 ↑ 제작 → 창고 탭 → 일반창고 → RT로 설치",
            tip = "창고 창의 [모두 넣기]는 핫바(1~7번 칸)는 그대로 두고 나머지를 넣어요.\n주방 · 제련소를 창고와 연결하면(설비를 클릭해 [연결하기]) 재료를 알아서 꺼내 써요.",
            objs = new[] { Place("NormalStorage", 1) },
            coins = 150,
        });
        c2.quests.Add(new Q
        {
            id = "C2_04_Water", name = "급수탑",
            talk = "물을 주면 작물이 더 빨리 자라요. 밭 옆에 급수탑을 세워요!",
            desc = "급수탑 둘레 5칸의 밭은 물을 받아 1.25배 빨리 자라요 (츄르 옥수수는 2배!).",
            hint = "[제작](C) → 문명 탭 → 급수탑 → 밭 곁에 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 급수탑 → RT로 설치",
            tip = "물을 받은 밭은 흙이 파랗게 보여요.",
            objs = new[] { Place("Item_WaterTower", 1) },
            coins = 100,
        });
        c2.quests.Add(new Q
        {
            id = "C2_05_Greenhouse", name = "온실",
            talk = "온실은 작물을 빨리 키우고 폭풍에서도 지켜 줘요.",
            desc = "온실은 곁의 작물을 1.2배 빨리 키우고, 폭풍에서도 지켜 줘요.",
            hint = "[제작](C) → 문명 탭 → 온실 (달빛 무 3개 필요) → 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 온실 → RT로 설치",
            tip = "온실 재료: 플랜트 4 · 합금 2 · 구리 주괴 4 · 달빛 무 3.",
            objs = new[] { Place("Item_Greenhouse", 1) },
            coins = 150,
        });
        c2.quests.Add(new Q
        {
            id = "C2_05b_Fields", name = "밭 20칸",
            talk = "고양이가 늘수록 셔틀 짐도 커져요. 밭을 20칸까지 넓혀 둬요!",
            desc = "농업 마을이 되려면 전기가 들어오는 밭이 20칸 있어야 해요. 플랜트를 더 만들어 센터코어 둘레에 깔아요.",
            hint = "[제작](C) → 농사 탭 → 플랜트 → 누른 채 끌면 여러 칸이 깔려요",
            padHint = "십자키 ↑ 제작 → 농사 탭 → 플랜트 → RT를 누른 채 움직이면 여러 칸",
            tip = "플랜트 하나 = 구리 주괴 1개. 밭을 한곳에 모아 깔면 다음 장에서 자동파종기 · 자동수확기 한 대가 한꺼번에 돌봐요.",
            objs = new[] { Place("PlantTile", 20) },
            coins = 150,
        });
        c2.quests.Add(new Q
        {
            id = "C2_05c_Research", name = "첫 연구",
            talk = "연구소가 놀고 있어요! 작물이 더 빨리 자라게 연구해 볼까요?",
            desc = "연구는 코인과 재료를 써서 농장을 영영 좋아지게 해요. 작물 성장을 연구하면 모든 작물이 더 빨리 자라요.",
            hint = "[연구](T) → 농사 → 작물 성장 → [연구하기]",
            padHint = "십자키 ← 연구 → 농사 → 작물 성장 → [연구하기]",
            tip = "연구마다 필요한 재료는 가방에 있어야 해요. 셔틀이 오래 기다린다 싶으면 연구 창을 들러 봐요.",
            objs = new[] { Tech("crop_growth", 1) },
            coins = 150,
        });
        c2.quests.Add(new Q
        {
            id = "C2_05d_Camp", name = "고양이들의 잠자리",
            talk = "고양이들이 밭 사이에서 자고 있어요! 주민 캠프를 지어 주면 대부분 안에서 쉬고, 몇 마리만 번갈아 나와 놀아요.",
            desc = "캠프 한 채에 20마리가 들어가요. 주민이 늘어 잠자리가 모자라면 남는 고양이가 밖을 돌아다니니, 그때 더 지어요.",
            hint = "[제작](C) → 문명 탭 → 주민 캠프 → 원하는 곳에 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 주민 캠프 → RT로 설치",
            tip = "전기가 없어도 돼요. 캠프를 클릭하면 마을의 잠자리 수가 보여요.",
            objs = new[] { Place("Item_ResidentCamp", 1) },
            coins = 100,
        });
        c2.quests.Add(new Q
        {
            id = "C2_05e_Decor", name = "마을 꾸미기",
            talk = "주민들이 상점가에 꾸미기 가게를 열었어요! 장식을 놓아 마을을 예쁘게 하면 소문이 나서 셔틀 짐값도 올라요.",
            desc = "꾸미기 가게에서 산 장식을 마을에 놓으면 마을 매력 ★이 올라요. 매력이 높을수록 셔틀이 떠날 때 짐값을 더 받아요.",
            hint = "[상점](B) → 꾸미기 탭 → 장식 사기 → 들고 마을에 놓기",
            padHint = "십자키 → 상점 → LT/RT로 꾸미기 → 장식 사기 → 들고 RT로 놓기",
            tip = "매력 보너스는 셔틀 창 위쪽에 보여요. 비싼 장식일수록 매력이 커요.\n장식은 정화된 땅 어디든 놓을 수 있고, 해체하면 돌려받아요.",
            objs = new[] { Charm(3) },
            coins = 300,
        });
        c2.quests.Add(new Q
        {
            id = "C2_06_Orders", name = "이웃 늘리기",
            talk = "주민이 늘수록 마을이 커져요. 셔틀로 고양이들을 더 데려와요!",
            desc = "셔틀이 원하는 짐을 실어 보낼 때마다 고양이 가족이 건너와요. 주민 20마리를 모아요.",
            hint = "셔틀 말풍선의 짐을 모아 셔틀 클릭 — 어려운 짐은 [셔틀](J)에서 [다른 짐 받기]",
            padHint = "셔틀 앞에서 A로 짐 싣기 — 어려운 짐은 X 셔틀 창에서 [다른 짐 받기]",
            tip = "요리를 실어 보내면 짐값이 커요. 별빛 잼 · 캣닢 쿠키를 만들어 두면 좋아요.",
            objs = new[] { Residents(20) },
            coins = 200,
        });
        c2.quests.Add(new Q
        {
            id = "C2_07_Civ", name = "농업 마을로!",
            talk = "농업 마을까지 한 걸음이에요! 알림판 조건을 채워요.",
            desc = "온실 · 급수탑 · 주방이 선 개척 캠프는 이제 농업 마을이 될 수 있어요.",
            hint = "문명 알림판 조건: 밭 20칸 · 작물 4종 · 주민 20마리 · 2,000코인",
            tip = "작물 종류는 서로 다른 작물을 한 번씩 수확하면 늘어요. 코인은 셔틀 짐값 · 판매로 모아요.",
            objs = new[] { Level(3) },
        });
        chapters.Add(c2);

        var c3 = new Chapter { title = "농업 마을", goal = "도구를 강화하고 농사를 자동으로! 산업 도시로!", intro = "어엿한 농업 마을이에요! 더 많은 고양이를 먹이려면 기계에게 농사를 맡겨 봐요." };
        c3.quests.Add(new Q
        {
            id = "C3_01_Compost", name = "퇴비 만들기",
            talk = "남는 씨앗은 버리지 말고 퇴비로! 퇴비를 준 밭은 더 많이 열려요.",
            desc = "남는 씨앗을 퇴비통에 넣으면 퇴비가 돼요. 퇴비를 준 밭은 작물이 더 열려요.",
            hint = "[제작](C) → 농사 탭 → 퇴비통 설치 → 클릭해 씨앗 넣기",
            padHint = "십자키 ↑ 제작 → 농사 탭 → 퇴비통 → A로 열어 씨앗 넣기",
            tip = "퇴비를 들고 밭을 클릭하면 수확 3번 동안 작물이 하나씩 더 열리고, 황금 작물 확률도 두 배!",
            objs = new[] { Place("Item_Composter", 1), Craft("Item_Compost", 2) },
            coins = 150,
        });
        c3.quests.Add(new Q
        {
            id = "C3_02_Drill2", name = "티어2 드릴",
            talk = "더 단단한 광석을 캐려면 드릴을 강화해야 해요.",
            desc = "리튬 · 알루미늄은 단단해서 티어2 드릴이 있어야 캐요.",
            hint = "[제작](C) → 도구 탭 → 티어2 드릴 (티어1 드릴을 강화)",
            padHint = "십자키 ↑ 제작 → 도구 탭 → 티어2 드릴",
            tip = "티어2 드릴은 초당 채굴량이 2배예요.",
            objs = new[] { Craft("Item_Tier2_Drill", 1) },
        });
        c3.quests.Add(new Q
        {
            id = "C3_03_Mine2", name = "리튬 · 알루미늄",
            talk = "가벼운 금속은 눈 덮인 혹한지에서만 나요. 스캐너를 챙겨서 캐러 가요!",
            desc = "자동화 설비와 공장엔 혹한지의 가벼운 금속이 필요해요.",
            hint = "혹한지로 가서 티어2 드릴로 알루미늄 · 리튬 광석을 꾹 클릭",
            padHint = "혹한지에서 티어2 드릴을 들고 광석 앞에서 RT를 꾹",
            tip = "탐사 스캐너를 쓰면 근처 광맥에 이름표가 떠요. 미니맵(M)으로 크게 볼 수도 있어요.\n캐는 것은 오염된 땅에서도 할 수 있어요.",
            objs = new[] { Collect("Item_Aluminum", 20), Collect("Item_Lithium", 10) },
            coins = 150,
        });
        c3.quests.Add(new Q
        {
            id = "C3_03c_Tools", name = "도구 강화",
            talk = "혹한지 알루미늄으로 물뿌리개를 스프링클 건으로 바꿔요! 한 번에 5×5칸이 촉촉해져요.",
            desc = "도구도 단계가 있어요: 물뿌리개 → 스프링클 건, 수확 낫 → 큰 낫 → 별빛 낫, 탐사 스캐너 → 심우주 스캐너.",
            hint = "[제작](C) → 도구 탭 → 스프링클 건 만들기 → 들고 밭 클릭",
            padHint = "십자키 ↑ 제작 → 도구 탭 → 스프링클 건 → 들고 밭 앞에서 RT",
            tip = "물을 준 밭은 진한 색으로 바뀌고 한동안 빨리 자라요.",
            objs = new[] { Craft("Item_Sprinkler", 1), Purifiers(2) },
            rewards = new[] { R("Item_BigSickle", 1) },
            coins = 200,
        });
        c3.quests.Add(new Q
        {
            id = "C3_04_Auto", name = "자동 농사",
            talk = "심고 거두느라 바쁘죠? 기계에게 맡기면 그동안 다른 일을 할 수 있어요!",
            desc = "자동파종기는 곁의 빈 밭에 씨앗을 심고, 자동수확기는 다 자란 작물을 거둬요.",
            hint = "[제작](C) → 농사 탭 → 자동파종기 · 자동수확기 → 밭 곁에 설치",
            padHint = "십자키 ↑ 제작 → 농사 탭 → 자동파종기 · 자동수확기 → RT로 설치",
            tip = "설비에 마우스를 올리면 일하는 범위가 보여요. 밭 곁에 세워요.",
            objs = new[] { Place("AutoSeeder", 1), Place("AutoHarvester", 1) },
            coins = 300,
        });
        c3.quests.Add(new Q
        {
            id = "C3_04b_MachineFarm", name = "기계 농장",
            talk = "씨앗을 넣어 주면 파종기가 심고, 다 자라면 수확기가 거둬요. 한번 지켜봐요!",
            desc = "파종기에 씨앗을 넣으면 곁의 빈 밭에 심고, 수확기는 다 자란 작물을 거둬요. 수확기가 20번 거두는 걸 지켜봐요.",
            hint = "씨앗을 든 채 파종기 클릭 → 곁의 빈 밭에 심겨요",
            padHint = "씨앗을 든 채 파종기 앞에서 A → 곁의 빈 밭에 심겨요",
            tip = "수확기가 닿는 밭이 있으면 파종기는 그 밭에만 심어요. 범위 밖 밭은 손으로 심는 자리예요. 거둔 작물은 가방으로 들어와요.",
            objs = new[] { AutoHarvest(20) },
            coins = 300,
        });
        c3.quests.Add(new Q
        {
            id = "C3_04c_Linked", name = "창고로 모아요",
            talk = "수확물이 가방으로 쏟아지면 금방 꽉 차요. 수확기를 창고에 이어 주면 거둔 작물이 창고로 모여요!",
            desc = "수확기를 창고와 이으면 거둔 작물이 창고로 들어가요. 거둔 씨앗은 곁의 파종기로 돌아가니 밭이 혼자 돌아가요.",
            hint = "수확기 클릭 → [연결하기] → 창고 클릭",
            padHint = "수확기 앞에서 A → [연결하기] → 창고 조준 후 A",
            tip = "창고가 가득 차면 수확기가 쉬어요 — 창고를 비우거나 더 지어 이어요. 작물 판매기를 이으면 남는 작물을 알아서 팔아요.",
            objs = new[] { LinkStorage("AutoHarvester") },
            coins = 300,
        });
        c3.quests.Add(new Q
        {
            id = "C3_05_Churu", name = "츄르 요리",
            talk = "츄르는 고양이들이 제일 좋아하는 간식이에요! 셔틀 짐으로도 자주 나와요.",
            desc = "츄르는 고양이들이 제일 좋아하는 간식! 셔틀 짐으로 자주 나와요.",
            hint = "주방에서 츄르 (츄르 옥수수 2 + 우유 버섯 1)",
            padHint = "주방 앞에서 A → 츄르",
            tip = "우유 버섯은 [그늘 작물]이라 전기 없이도 자라요. 츄르 옥수수는 급수탑 곁에서 두 배로 자라요.",
            objs = new[] { Craft("Item_Churu", 2) },
            coins = 300,
        });
        c3.quests.Add(new Q
        {
            id = "C3_05c_Milk", name = "새 메뉴: 버섯 우유",
            talk = "버섯 우유는 나중에 타르트에도 들어가요! 미리 만들어 볼까요?",
            desc = "셔틀 짐에 요리가 자주 나와요. 메뉴를 하나씩 익혀 두면 짐이 와도 당황하지 않아요.",
            hint = "주방 클릭 → 메뉴판에서 버섯 우유",
            padHint = "주방 앞에서 A → 버섯 우유",
            objs = new[] { Craft("Item_ShroomMilk", 1) },
            coins = 300,
        });
        c3.quests.Add(new Q
        {
            id = "C3_05d_ProcessSpeed", name = "가공 속도 연구",
            talk = "제련소랑 주방이 조금만 더 빠르면 좋겠죠? 연구로 빠르게 만들어요!",
            desc = "가공 속도를 연구하면 제련소 · 고양이 주방 · 퇴비통이 더 빨리 일해요.",
            hint = "[연구](T) → 자동화 → 가공 속도 → [연구하기]",
            padHint = "십자키 ← 연구 → 자동화 → 가공 속도 → [연구하기]",
            objs = new[] { Tech("process_speed", 1) },
            coins = 300,
        });
        c3.quests.Add(new Q
        {
            id = "C3_05b_Neighbors", name = "이웃 마을",
            talk = "마을이 북적일수록 좋아요! 셔틀로 고양이 가족을 더 데려와요.",
            desc = "셔틀이 원하는 짐을 실어 보내 주민을 35마리까지 늘려요. 요리를 실으면 짐값이 커요.",
            hint = "셔틀 말풍선의 짐을 모아 셔틀 클릭 — 어려운 짐은 [셔틀](J)에서 [다른 짐 받기]",
            padHint = "셔틀 앞에서 A로 짐 싣기 — 어려운 짐은 X 셔틀 창에서 [다른 짐 받기]",
            tip = "셔틀은 짐을 다 실으면 바로 떠나요. 다녀오는 동안 다음 짐을 미리 모아 두면 빨라요.\n코인이 넉넉하면 셔틀 창의 [이주 지원 좌석]으로 셔틀마다 고양이를 더 데려올 수 있어요.",
            objs = new[] { Residents(35) },
            coins = 300,
        });
        c3.quests.Add(new Q
        {
            id = "C3_05e_Festival", name = "첫 마을 축제",
            talk = "고양이들이 이렇게 많아졌으니 축제를 열어요! 축제 동안은 작물이 쑥쑥 크고 셔틀 짐값도 더 받아요.",
            desc = "셔틀 창에서 마을 축제를 열어요. 하루에 한 번, 게임 시간 4시간 동안 작물 · 요리 · 제련이 빨라지고 짐값이 올라요.",
            hint = "[셔틀](J) → [마을 축제 열기]",
            padHint = "X 셔틀 창 → [마을 축제 열기]",
            tip = "축제는 코인이 들지만 그동안 셔틀을 많이 보내면 금방 돌아와요.\n축제가 열리면 주민들이 센터코어에 모여 신나게 놀아요.",
            objs = new[] { Festival() },
            coins = 1000,
        });
        c3.quests.Add(new Q
        {
            id = "C3_06_Factory", name = "조립 공장",
            talk = "공장이 있으면 제련소랑 주방이 더 빨리 일해요!",
            desc = "조립 공장 곁(8칸)의 제련소 · 주방 · 퇴비통은 1.3배 빨리 일해요.",
            hint = "[제작](C) → 문명 탭 → 조립 공장 (회로 기판 · 알루미늄 주괴) → 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 조립 공장 → RT로 설치",
            tip = "회로 기판 = 구리 주괴 + 리튬 주괴 (재료 탭). 알루미늄 주괴는 제련소에서.\n주괴 · 합금이 모자라면 상점의 고양이 공방에서 코인으로 살 수도 있어요.",
            objs = new[] { Place("Item_Factory", 1) },
        });
        c3.quests.Add(new Q
        {
            id = "C3_07_Civ", name = "산업 도시로!",
            talk = "산업 도시가 코앞이에요! 조건을 마저 채워요.",
            desc = "공장이 돌아가면 마을은 산업 도시가 돼요.",
            hint = "문명 알림판 조건: 기계 작물 1종 · 작물 7종 · 주민 50마리 · 8,000코인",
            tip = "Lv.4가 되면 고급 제작(티어3 드릴 · 고급 설비 · 침입방어막)이 열려요.",
            objs = new[] { Level(4) },
        });
        chapters.Add(c3);

        var c4 = new Chapter { title = "산업 도시", goal = "광맥 옆 기지를 레일로 잇고, 통신탑 · 천문대를 세워 과학 도시로!", intro = "공장 귀 환기구에서 김이 폴폴 나요! 이제 하늘을 연구할 차례예요." };
        c4.quests.Add(new Q
        {
            id = "C4_01_Drill3", name = "티어3 드릴",
            talk = "티타늄이랑 다이아몬드는 아주 단단해요. 드릴을 한 번 더 강화해요!",
            desc = "티타늄 · 다이아몬드는 티어3 드릴로만 캘 수 있어요.",
            hint = "[제작](C) → 도구 탭 → 티어3 드릴 (티어2 드릴을 강화)",
            padHint = "십자키 ↑ 제작 → 도구 탭 → 티어3 드릴",
            objs = new[] { Craft("Item_Tier3_Drill", 1) },
        });
        c4.quests.Add(new Q
        {
            id = "C4_02_Mine3", name = "희귀 광석",
            talk = "천문대엔 다이아몬드 렌즈가 필요해요. 라일락빛 수정 지대로 탐험을 떠나요!",
            desc = "천문대 렌즈엔 다이아몬드, 튼튼한 탑엔 티타늄이 들어가요. 둘 다 수정 지대에서만 나요.",
            hint = "수정 지대에서 티어3 드릴로 티타늄 · 다이아몬드 광석을 캐요",
            padHint = "티어3 드릴을 들고 광석 앞에서 RT를 꾹",
            tip = "희귀 광석이 바닥나면 밤의 유성우가 곁에 새 광석을 떨어뜨려요.",
            objs = new[] { Collect("Item_Titanium", 12), Collect("Item_Diamond", 2) },
            coins = 500,
        });
        c4.quests.Add(new Q
        {
            id = "C4_02b_DeepScan", name = "네 지역 탐사",
            talk = "심우주 스캐너면 먼 유적 신호도 잡혀요! 네 지역을 모두 밟고 유적을 더 조사해 봐요.",
            desc = "심우주 스캐너는 탐지 범위가 두 배예요. 바이옴 네 곳을 모두 찾고 유적 넷을 조사해요.",
            hint = "[제작](C) → 심우주 스캐너 → 들고 클릭해 유적 방향 찾기",
            padHint = "십자키 ↑ 제작 → 심우주 스캐너 → 들고 RT",
            tip = "멀리 떨어진 유적일수록 보상이 커요.",
            objs = new[] { Craft("Item_DeepScanner", 1), Biomes(4), Landmarks(4) },
            coins = 1500,
        });
        c4.quests.Add(new Q
        {
            id = "C4_03_Shield", name = "폭풍 대비",
            talk = "가끔 큰 폭풍이 와요. 방어막으로 소중한 밭을 지켜요!",
            desc = "폭풍은 지켜 주는 것이 없는 작물을 날려 버려요. 침입방어막(반경 10)을 세워요.",
            hint = "[제작](C) → 농사 탭 → 침입방어막 → 밭 한가운데 설치",
            padHint = "십자키 ↑ 제작 → 농사 탭 → 침입방어막 → RT로 설치",
            tip = "시계 아래 날씨 예보에 폭풍이 보이면 미리 대비해요. 혜성 고추는 폭풍에도 끄떡없어요.",
            objs = new[] { Place("StormShield", 1) },
            coins = 500,
        });
        c4.quests.Add(new Q
        {
            id = "C4_04_Comm", name = "통신탑",
            talk = "통신탑이 서면 고향별과 연락이 잘 닿아요. 셔틀이 한 척 더 올 수 있어요!",
            desc = "통신탑을 세우면 셔틀이 한 척 더 와요. 셔틀이 내려앉을 착륙장도 하나 더 지어요.",
            hint = "[제작](C) → 문명 탭 → 통신탑 · 셔틀 착륙장 → 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 통신탑 · 셔틀 착륙장 → RT로 설치",
            tip = "셔틀은 착륙장마다 한 척, 통신탑 하나마다 한 척 더 (최대 3척) 다녀요.",
            objs = new[] { Place("Item_CommTower", 1), Place("Item_ShuttlePad", 2) },
            coins = 500,
        });
        c4.quests.Add(new Q
        {
            id = "C4_04b_TwoShuttles", name = "셔틀 두 척",
            talk = "셔틀이 두 척이 됐어요! 두 배로 고양이들을 데려올 수 있어요.",
            desc = "두 셔틀에 짐을 실어 보내 주민을 65마리까지 늘려요.",
            hint = "착륙장마다 셔틀이 한 척 — 말풍선의 짐을 모아 셔틀 클릭",
            padHint = "셔틀 앞에서 A로 짐 싣기",
            tip = "셔틀이 다녀오는 동안 다음 짐을 미리 모아 두면 빨라요.\n코인이 넉넉하면 셔틀 창의 [이주 지원 좌석]으로 셔틀마다 고양이를 더 데려올 수 있어요.",
            objs = new[] { Residents(65) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_04c_AutoShuttle", name = "셔틀도 혼자서",
            talk = "짐을 매번 손으로 싣기 번거롭죠? 착륙장을 창고에 이어 두면, 창고에 짐이 모이는 대로 셔틀이 알아서 싣고 떠나요!",
            desc = "착륙장을 창고와 이으면 셔틀이 창고의 짐을 알아서 실어요. 짐이 다 차면 혼자 떠나요 — 한 번 지켜봐요.",
            hint = "[셔틀](J) → [창고 연결] → 창고 클릭, 그다음 짐을 창고에 모아요",
            padHint = "X 셔틀 창 → [창고 연결] → 창고 조준 후 A, 그다음 짐을 창고에 모아요",
            tip = "가방에 있는 짐은 창고 창에서 옮겨 넣을 수 있어요. 구하기 힘든 짐이면 [다른 짐 받기]로 바꿔도 돼요.",
            objs = new[] { LinkStorage("Item_ShuttlePad"), AutoDepart(1) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_04d_SecondLine", name = "두 번째 작물 줄",
            talk = "셔틀마다 원하는 짐이 달라요. 기계로 키우는 작물을 하나 더 늘려 둘까요?",
            desc = "파종기를 하나 더 세우고 다른 씨앗을 넣어 줘요. 파종기 · 수확기가 함께 돌보는 밭에서 두 가지 작물이 자라면 돼요.",
            hint = "파종기를 하나 더 세우고 다른 씨앗을 든 채 클릭",
            padHint = "파종기를 하나 더 세우고 다른 씨앗을 든 채 앞에서 A",
            tip = "파종기는 넣어 준 씨앗을 심고, 수확기가 거둔 씨앗을 돌려받아 계속 같은 작물을 심어요.",
            objs = new[] { MachineCrops(2) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_04e_FastHarvest", name = "더 빠른 수확기",
            talk = "다 자란 작물이 줄을 서 있네요! 고급 수확기는 한 번에 두 배로 거둬요.",
            desc = "고급 자동수확기는 더 넓은 범위에서 한 번에 여러 칸을 거둬요. 수확이 밀리는 밭 곁에 세워요.",
            hint = "[제작](C) → 농사 탭 → 고급 자동수확기 → 밭 곁에 설치",
            padHint = "십자키 ↑ 제작 → 농사 탭 → 고급 자동수확기 → RT로 설치",
            tip = "고급 자동수확기 = 자동수확기 + 티타늄 합금 2 + 합금 2. 창고와 잇는 것도 잊지 말아요.",
            objs = new[] { Place("AutoHarvester_T2", 1) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_04f_AutoSmelt", name = "광석도 기계로",
            talk = "기계를 지으려니 주괴가 자꾸 모자라죠? 광석 곁에 채굴기를 세워 창고에 잇고, 제련소도 같은 창고에 이으면 주괴가 저절로 모여요!",
            desc = "자동채굴기는 곁의 광석을 알아서 캐요. 채굴기 · 제련소를 같은 창고에 이으면 캔 광석을 녹여 주괴를 창고에 돌려놔요.",
            hint = "[제작](C) → 설비 탭 → 자동채굴기 → 광석 곁에 설치 → [연결하기] → 창고",
            padHint = "십자키 ↑ 제작 → 설비 탭 → 자동채굴기 → 광석 곁에 RT → A → [연결하기] → 창고",
            tip = "창고가 광석에서 멀면 광석 곁에 창고와 제련소를 하나 더 지어요. 채굴기를 클릭해 드릴을 끼우면 더 빨리 캐요.",
            objs = new[] { LinkStorage("T1_ResourceExtractor"), LinkStorage("Item_Smelter") },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_04g_OreBase", name = "광맥 옆 기지",
            talk = "광석 캐러 왔다 갔다 하느라 다리 아프죠? 먼 광맥 곁에 기지를 세우고 화물 레일로 본진 창고와 이으면, 캔 것이 저절로 집에 와요!",
            desc = "본진에서 먼 광맥 곁에 창고와 채굴기를 세우고, 화물 레일 기둥으로 본진 창고까지 이어요. 맵을 넓게 쓸수록 공장 같아져요.",
            hint = $"[제작](C) → 설비 탭 → 화물 레일. 기둥은 {LinkRules.MaxDistanceCells:0}칸 안의 기둥과 저절로 이어져요",
            padHint = $"십자키 ↑ 제작 → 설비 탭 → 화물 레일. 기둥은 {LinkRules.MaxDistanceCells:0}칸 안의 기둥과 저절로 이어져요",
            tip = "제련소도 같은 창고에 이으면 광석은 남겨 녹이고 주괴만 본진으로 보내요. 채굴기가 여럿이면 더 빨리 모여요.",
            objs = new[] { Outposts(1, "T1_ResourceExtractor"), RailDelivery(null, 10) },
            coins = 1500,
        });
        c4.quests.Add(new Q
        {
            id = "C4_05_Tart", name = "크리스탈 베리 타르트",
            talk = "고급 요리는 짐값이 아주 커요. 타르트에 도전해 봐요!",
            desc = "고급 요리는 셔틀 짐값이 아주 커요. 크리스탈 베리는 밤에 잘 자라요.",
            hint = "주방에서 크리스탈 베리 타르트 (베리 2 · 버섯 우유 · 별빛 잼)",
            padHint = "주방 앞에서 A → 크리스탈 베리 타르트",
            objs = new[] { Craft("Item_BerryTart", 1) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_05b_MachineSpeed", name = "기계 속도 연구",
            talk = "기계가 조금만 더 빠르면 좋겠죠? 연구로 파종기 · 수확기를 빠르게 만들어요!",
            desc = "기계 속도를 연구하면 자동파종기 · 자동수확기 · 자동채굴기가 더 빨리 일해요.",
            hint = "[연구](T) → 자동화 → 기계 속도 → [연구하기]",
            padHint = "십자키 ← 연구 → 자동화 → 기계 속도 → [연구하기]",
            tip = "재료 회로 기판은 구리 주괴 + 리튬 주괴로 만들어요.",
            objs = new[] { Tech("machine_speed", 1) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_05c_Salad", name = "새 메뉴: 무지개 샐러드",
            talk = "큰 잔치에는 알록달록한 샐러드가 빠질 수 없어요!",
            desc = "무지개 샐러드는 마지막 만찬에도 들어가요. 미리 익혀 둬요.",
            hint = "주방 클릭 → 메뉴판에서 무지개 샐러드",
            padHint = "주방 앞에서 A → 무지개 샐러드",
            objs = new[] { Craft("Item_RainbowSalad", 1) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_05d_Bountiful", name = "풍성한 수확 연구",
            talk = "한 번 심어서 하나 더 거둘 수 있다면 좋겠죠?",
            desc = "풍성한 수확을 연구하면 작물이 하나 더 열릴 때가 있어요. 밭을 안 늘려도 수확이 늘어요.",
            hint = "[연구](T) → 농사 → 풍성한 수확 → [연구하기]",
            padHint = "십자키 ← 연구 → 농사 → 풍성한 수확 → [연구하기]",
            objs = new[] { Tech("bountiful", 1) },
            coins = 800,
        });
        c4.quests.Add(new Q
        {
            id = "C4_06_Obs", name = "천문대",
            talk = "천문대로 고향별까지 가는 가장 안전한 하늘길을 찾아요!",
            desc = "천문대가 큰 이주선이 다닐 하늘길을 찾아요. 날씨 예보도 한 칸 더 보여요.",
            hint = "[제작](C) → 문명 탭 → 천문대 → 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 천문대 → RT로 설치",
            tip = "천문대 재료: 티타늄 합금 2 · 회로 기판 2 · 다이아몬드 2 · 크리스탈 베리 2.",
            objs = new[] { Place("Item_Observatory", 1) },
        });
        c4.quests.Add(new Q
        {
            id = "C4_07_Civ", name = "과학 도시로!",
            talk = "과학 도시까지 조금만 더 힘내요!",
            desc = "하늘을 연구하는 과학 도시가 눈앞이에요.",
            hint = "문명 알림판 조건: 기계 작물 2종 · 전초기지 1곳 · 작물 10종 · 주민 90마리 · 30,000코인",
            objs = new[] { Level(5) },
        });
        chapters.Add(c4);

        var c5 = new Chapter { title = "과학 도시", goal = "별빛 토양에 전초 농장을 세우고, 거주 모듈과 우주 관제탑으로 우주 항구를!", intro = "천문대가 큰 이주선의 하늘길을 찾았어요! 더 많은 고양이를 맞을 준비를 해요." };
        c5.quests.Add(new Q
        {
            id = "C5_01_Habitat", name = "거주 모듈",
            talk = "주민이 많아져 집이 모자라요! 거주 모듈을 지어 맞이해요.",
            desc = "거주 모듈마다 셔틀이 고양이를 한 마리 더 태워 와요 (최대 4채).",
            hint = "[제작](C) → 문명 탭 → 거주 모듈 2채 → 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 거주 모듈 → RT로 설치",
            objs = new[] { Place("Item_Habitat", 2) },
            coins = 2000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_01b_BigFamilies", name = "대가족 맞이",
            talk = "거주 모듈 덕분에 셔틀마다 고양이가 더 타요! 대가족을 맞아요.",
            desc = "셔틀로 주민을 110마리까지 늘려요. 거주 모듈마다 한 번에 한 마리씩 더 타요.",
            hint = "셔틀 말풍선의 짐을 모아 셔틀 클릭",
            padHint = "셔틀 앞에서 A로 짐 싣기",
            objs = new[] { Residents(110) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_02_Booster", name = "성장 촉진기",
            talk = "주민이 늘면 먹을 것도 더 필요해요. 작물을 더 빨리 키워 봐요!",
            desc = "성장 촉진기 둘레 3칸의 작물은 1.5배 빨리 자라요.",
            hint = "[제작](C) → 농사 탭 → 성장 촉진기 → 밭 사이에 설치",
            padHint = "십자키 ↑ 제작 → 농사 탭 → 성장 촉진기 → RT로 설치",
            objs = new[] { Place("Item_GrowthBooster", 1) },
            coins = 2000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_02b_AutoKitchen", name = "요리도 자동으로",
            talk = "짐에 요리가 점점 많아져요. 주방을 창고에 이으면 재료를 꺼내 혼자 요리하고 창고에 돌려놔요!",
            desc = "주방 메뉴를 고르고 창고와 이어 줘요. 창고에 재료가 있으면 주방이 혼자 요리해요. 요리 5개가 창고로 돌아오는 걸 지켜봐요.",
            hint = "주방 클릭 → 메뉴 고르기 → [연결하기] → 창고 클릭",
            padHint = "주방 앞에서 A → 메뉴 고르기 → [연결하기] → 창고 조준 후 A",
            tip = "재료 작물을 기계로 키워 같은 창고에 모으면 밭에서 주방까지 한 줄로 이어져요.",
            objs = new[] { LinkStorage("Item_CatKitchen"), AutoDish(5) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_02c_FourLines", name = "네 가지 작물 줄",
            talk = "셔틀 짐이 세 가지나 돼요! 기계로 키우는 작물을 네 종류로 늘리면 셔틀이 훨씬 덜 기다려요.",
            desc = "셔틀이 자주 찾는 작물부터 파종기를 하나씩 더 세워요. 셔틀 창에 지금 속도로 얼마나 걸릴지 나와요.",
            hint = "파종기를 더 세우고 작물마다 씨앗을 넣어요",
            padHint = "파종기를 더 세우고 작물마다 씨앗을 넣어요",
            tip = "급수탑 · 온실 · 성장 촉진기 곁에 두면 더 빨리 자라요. 물을 좋아하는 작물은 급수탑 곁이 제일이에요.",
            objs = new[] { MachineCrops(4) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_02d_BigStorage", name = "대형 창고",
            talk = "창고가 넘쳐서 수확물이 가방으로 쏟아지지 않나요? 대형 창고는 1,000개까지 담아요!",
            desc = "대형 창고는 무엇이든 1,000개까지 담아요. 큰 이주선을 맞으려면 넉넉한 창고가 있어야 해요.",
            hint = "[제작](C) → 창고 탭 → 대형 창고 → 설치 후 설비와 이어요",
            padHint = "십자키 ↑ 제작 → 창고 탭 → 대형 창고 → RT로 설치 후 설비와 이어요",
            tip = "설비는 먼저 이은 창고부터 채워요. 수확기 · 주방 · 착륙장을 같은 창고에 이으면 한 줄로 돌아가요.",
            objs = new[] { Place("Item_LargeStorage", 1) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_02e_Growth", name = "작물 성장 연구",
            talk = "큰 셔틀 짐을 채우려면 작물이 더 빨리 자라야 해요. 연구를 이어 가요!",
            desc = "작물 성장 3단계까지 연구하면 모든 작물이 훨씬 빨리 자라요. 밭을 늘리지 않아도 수확이 빨라져요.",
            hint = "[연구](T) → 농사 → 작물 성장 → [연구하기]",
            padHint = "십자키 ← 연구 → 농사 → 작물 성장 → [연구하기]",
            objs = new[] { Tech("crop_growth", 3) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_02f_FindSoil", name = "별빛 토양을 찾아서",
            talk = "탐사 셔틀이 보랏빛으로 반짝이는 땅을 찾았대요! 은하 수박이 거기서 훨씬 잘 자란다는데, 가 볼까요?",
            desc = "맵 곳곳에 특산 토양이 있어요. 맞는 작물을 심으면 2배 빨리 자라고 하나 더 열려요.",
            hint = "화살표를 따라 별빛 토양까지 걸어가요",
            padHint = "화살표를 따라 별빛 토양까지 걸어가요",
            tip = "별빛 토양: 은하 수박 · 크리스탈 베리 · 달빛 무 / 온천 지대: 우유 버섯 · 구름 목화 · 무지개 당근 / 화산재 땅: 태양 해바라기 · 혜성 고추 · 츄르 옥수수",
            objs = new[] { FindSoil("Item_Seed_GalaxyMelon") },
            coins = 1500,
        });
        c5.quests.Add(new Q
        {
            id = "C5_02g_BigPurifier", name = "대형 정화탑",
            talk = "별빛 토양은 수정 지대 한가운데라 아직 오염돼 있어요. 대형 정화탑이면 넓게 한 번에 되살릴 수 있어요!",
            desc = "대형 정화탑은 둘레 13칸(29×29칸)을 정화해요. 먼 바이옴에 농장 기지를 세울 때 써요.",
            hint = "[제작](C) → 대형 정화탑 → 별빛 토양 곁에 설치",
            padHint = "십자키 ↑ 제작 → 대형 정화탑 → 별빛 토양 곁에서 RT",
            tip = "정화탑은 전기 없이도 일해요. 그 땅에 밭을 깔려면 중계탑으로 전기도 이어요.",
            objs = new[] { Place("Item_Purifier_T2", 1) },
            coins = 2000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_03_Melon", name = "은하 수박",
            talk = "은하 수박은 크고 달콤해서 비싸게 팔려요! 별빛 토양에 밭을 깔고 심어 봐요. 전기가 안 닿으면 전력 중계탑부터요.",
            desc = "별빛 토양의 은하 수박은 2배 빨리 자라고 하나 더 열려요. 멀리까지 전기를 보내려면 전력 중계탑을 이어요.",
            hint = "전력 중계탑 → 플랜트 → 은하 수박 씨앗 (상점(B)에서도 팔아요)",
            padHint = "전력 중계탑 → 플랜트 → 은하 수박 씨앗 (상점: 십자키 →)",
            objs = new[] { SoilCrop("Item_Seed_GalaxyMelon", 6), Harvest("GalaxyMelon", 6) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_03c_StarFarm", name = "별빛 전초 농장",
            talk = "수박을 거두러 매번 오긴 힘들죠? 여기에도 수확기 · 파종기 · 창고를 세우고 레일로 본진까지 이으면, 수박이 저절로 집에 와요!",
            desc = "전초기지 = 본진에서 먼 창고 + 수확기(채굴기) + 본진 창고까지 이어진 레일. 맞는 토양의 농장은 본진보다 몇 배나 많이 거둬요.",
            hint = "별빛 토양에 창고 · 수확기 · 파종기를 세우고 레일을 이어요",
            padHint = "별빛 토양에 창고 · 수확기 · 파종기를 세우고 레일을 이어요",
            tip = "씨앗은 레일에 실리지 않아요. 수확기가 거둔 씨앗은 파종기로 돌아가서 농장이 혼자 돌아가요.",
            objs = new[] { Outposts(2, "Item_Seed_GalaxyMelon"), RailDelivery("GalaxyMelon", 30) },
            coins = 4000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_03a_Juice", name = "새 메뉴: 은하 수박 주스",
            talk = "수박을 거뒀으니 시원한 주스를 만들어 봐요! 마지막 만찬에도 들어가요.",
            desc = "은하 수박 주스는 비싸게 팔리고, 마지막 만찬의 재료예요.",
            hint = "주방 클릭 → 메뉴판에서 은하 수박 주스",
            padHint = "주방 앞에서 A → 은하 수박 주스",
            objs = new[] { Craft("Item_MelonJuice", 1) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_03b_FastShuttle", name = "빠른 셔틀 연구",
            talk = "셔틀이 더 빨리 다녀오면 가족들을 더 일찍 만날 수 있어요!",
            desc = "빠른 셔틀을 연구하면 셔틀이 고향별에 다녀오는 시간이 짧아져요.",
            hint = "[연구](T) → 셔틀 → 빠른 셔틀 → [연구하기]",
            padHint = "십자키 ← 연구 → 셔틀 → 빠른 셔틀 → [연구하기]",
            objs = new[] { Tech("shuttle_speed", 1) },
            coins = 3000,
        });
        c5.quests.Add(new Q
        {
            id = "C5_04_Control", name = "우주 관제탑",
            talk = "관제탑이 서면 셔틀이 더 빨리 오가요. 큰 이주선도 관제탑이 있어야 안전하게 내려앉아요!",
            desc = "관제탑이 서면 셔틀 왕복이 빨라지고 짐값도 20% 늘어요.",
            hint = "[제작](C) → 문명 탭 → 우주 관제탑 → 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 우주 관제탑 → RT로 설치",
            objs = new[] { Place("Item_ControlTower", 1) },
        });
        c5.quests.Add(new Q
        {
            id = "C5_05_Civ", name = "우주 항구로!",
            talk = "우주 항구가 되면 이제 큰 이주선을 맞을 일만 남아요!",
            desc = "우주 항구가 되면 이제 큰 이주선을 맞을 일만 남아요.",
            hint = "문명 알림판 조건: 기계 작물 4종 · 전초기지 2곳 · 작물 12종 · 주민 150마리 · 120,000코인",
            objs = new[] { Level(6) },
        });
        chapters.Add(c5);

        var c6 = new Chapter { title = "우주 항구", goal = "모든 기지의 물자를 모아, 큰 이주선이 내려앉을 착륙장을 세워요!", intro = "마지막 장이에요! 셔틀로는 다 못 태워요 — 고향별의 큰 이주선이 내려앉을 자리를 만들어 남은 고양이들을 모두 맞이해요!" };
        c6.quests.Add(new Q
        {
            id = "C6_01_StarAlloy", name = "별빛 합금",
            talk = "큰 이주선을 맞으려면 별의 힘이 깃든 합금이 있어야 해요.",
            desc = "최종 이주 계획(행성 이주 프로토콜)에 별의 힘이 깃든 합금이 들어가요.",
            hint = "[제작](C) → 재료 탭 → 별빛 합금 (티타늄 합금 + 별 작물 3종) ×5",
            padHint = "십자키 ↑ 제작 → 재료 탭 → 별빛 합금",
            objs = new[] { Craft("Item_StarAlloy", 5) },
            coins = 5000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_01a_MachineSpeed", name = "기계 속도 연구",
            talk = "기계들이 쉬지 않고 일하고 있어요. 조금만 더 빠르게 해 줄까요?",
            desc = "기계 속도를 3단계까지 연구하면 파종기 · 수확기 · 채굴기가 훨씬 빨리 일해요.",
            hint = "[연구](T) → 자동화 → 기계 속도 → [연구하기]",
            padHint = "십자키 ← 연구 → 자동화 → 기계 속도 → [연구하기]",
            objs = new[] { Tech("machine_speed", 3) },
            coins = 5000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_01b_LastShuttles", name = "마지막 셔틀들",
            talk = "큰 이주선이 오기 전에, 셔틀로 한 가족이라도 더 데려와요!",
            desc = "셔틀로 주민을 180마리까지 늘려요.",
            hint = "셔틀 말풍선의 짐을 모아 셔틀 클릭",
            padHint = "셔틀 앞에서 A로 짐 싣기",
            objs = new[] { Residents(180) },
            coins = 8000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_01c_SixLines", name = "여섯 가지 작물 줄",
            talk = "남은 고양이들을 먹이려면 농장이 쉬지 않고 돌아가야 해요. 기계로 키우는 작물을 여섯 종류로!",
            desc = "파종기 · 수확기가 돌보는 밭에서 여섯 가지 작물이 자라면 돼요. 이제 셔틀 짐 대부분이 창고에서 알아서 채워져요.",
            hint = "파종기를 더 세우고 작물마다 씨앗을 넣어요",
            padHint = "파종기를 더 세우고 작물마다 씨앗을 넣어요",
            objs = new[] { MachineCrops(6) },
            coins = 8000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_01c1_Mega", name = "메가 자동화",
            talk = "레이더 달린 메가 자동수확기면 27×27칸 들판을 1초마다 훑어요! 수확기 하나가 마을 하나 몫이에요.",
            desc = "자동화 3단계: 1단계 7×7칸 · 초당 0.8칸 → 2단계 14×14칸 · 초당 4칸 → 3단계 27×27칸 · 초당 24칸.",
            hint = "[제작](C) → 메가 자동수확기 → 넓은 밭 한가운데 설치",
            padHint = "십자키 ↑ 제작 → 메가 자동수확기 → RT로 설치",
            tip = "수정 지대 티타늄으로 만든 티타늄 합금이 들어가요.",
            objs = new[] { Place("AutoHarvester_T3", 1), AutoHarvest(200) },
            coins = 10000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_01c2_ThirdBase", name = "세 번째 기지",
            talk = "이주선 식량으로 츄르 옥수수가 잔뜩 필요해요! 화산재 땅에서 키우면 훨씬 빨리 모여요. 세 번째 기지를 세워 봐요.",
            desc = "화산재 땅: 츄르 옥수수 · 혜성 고추 · 태양 해바라기. 온천 지대: 우유 버섯 · 구름 목화 · 무지개 당근. 어느 쪽이든 레일로 본진에 이으면 돼요.",
            hint = "화살표를 따라가 창고 · 수확기를 세우고 레일을 이어요",
            padHint = "화살표를 따라가 창고 · 수확기를 세우고 레일을 이어요",
            objs = new[] { Outposts(3, "Item_Seed_ChuruCorn") },
            coins = 8000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_01d_Provisions", name = "이주선 식량",
            talk = "큰 이주선이 내리면 고양이 수천 마리가 한꺼번에 배고파져요! 모든 기지의 물자를 본진 창고에 모아요.",
            desc = "가방과 창고에 있는 걸 모두 세어요. 맞는 토양의 전초 농장을 돌릴수록 빨리 모여요.",
            hint = "츄르 옥수수 · 우유 버섯 · 은하 수박을 기계로 키워 창고에 모아요",
            padHint = "츄르 옥수수 · 우유 버섯 · 은하 수박을 기계로 키워 창고에 모아요",
            tip = "모으는 동안엔 셔틀이 창고에서 이 작물들을 꺼내 가지 않아요.\n창고가 가득 차면 수확기가 쉬어요. 대형 창고를 더 지어 두면 좋아요.",
            objs = new[] { Stockpile("ChuruCorn", 300), Stockpile("MilkShroom", 300), Stockpile("GalaxyMelon", 100) },
            coins = 8000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_01e_Treats", name = "이주선 간식",
            talk = "도착 잔치에 쓸 간식도 모아요! 주방이 창고 재료로 알아서 만들어 줘요.",
            desc = "가방과 창고에 있는 걸 모두 세어요. 주방을 여러 채 창고에 이어 두면 더 빨라요.",
            hint = "주방 메뉴를 츄르 · 별빛 잼으로 고르고 창고와 이어요",
            padHint = "주방 메뉴를 츄르 · 별빛 잼으로 고르고 창고와 이어요",
            tip = "모으는 동안엔 셔틀이 창고에서 츄르 · 별빛 잼을 꺼내 가지 않아요.\n조립 공장 곁의 주방은 1.3배 빨리 요리해요.",
            objs = new[] { Stockpile("Item_Churu", 100), Stockpile("Item_StarJam", 100) },
            coins = 8000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_02_Protocol", name = "행성 이주 프로토콜",
            talk = "프로토콜의 마지막 단계예요. 남은 고양이들을 한 번에 옮길 계획을 세워요!",
            desc = "남은 고양이들을 한 번에 안전하게 옮길 최종 이주 계획을 완성해요.",
            hint = "[제작](C) → 재료 탭 → 행성 이주 프로토콜",
            padHint = "십자키 ↑ 제작 → 재료 탭 → 행성 이주 프로토콜",
            tip = "재료: 별빛 합금 3 · 자동수확기 · 자동파종기 · 우라늄 주괴 3 · 다이아몬드 3 · 에너지 셀 4.",
            objs = new[] { Craft("Item_MigrationProtocol", 1) },
        });
        c6.quests.Add(new Q
        {
            id = "C6_03_Feast", name = "이주 만찬",
            talk = "새 행성에 도착할 고양이들을 위한 잔치를 준비해요!",
            desc = "새 행성에 도착할 고양이들을 위한 잔치를 준비해요.",
            hint = "주방에서 고양이 만찬 (츄르 2 · 타르트 · 수박 주스 · 무지개 샐러드)",
            padHint = "주방 앞에서 A → 고양이 만찬",
            objs = new[] { Craft("Item_CatFeast", 1) },
            coins = 10000,
        });
        c6.quests.Add(new Q
        {
            id = "C6_04_LaunchPad", name = "이주선 착륙장",
            talk = "드디어 큰 이주선이 내려앉을 착륙장을 세울 차례예요!",
            desc = "고향별의 큰 이주선이 내려앉을 커다란 착륙장이에요!",
            hint = "[제작](C) → 문명 탭 → 이주선 착륙장 (3×3) → 설치",
            padHint = "십자키 ↑ 제작 → 문명 탭 → 이주선 착륙장 → RT로 설치",
            objs = new[] { Place("Item_LaunchPad", 1) },
        });
        c6.quests.Add(new Q
        {
            id = "C6_05_Civ", name = "새 고향!",
            talk = "마지막 조건만 채우면 큰 이주선이 고향별을 출발해 이리로 와요. 대원님, 정말 최고예요!",
            desc = "센터코어가 마지막 단계에 오르면 큰 이주선이 고향별에 남은 고양이들을 모두 태우고 착륙장에 내려앉아요!",
            hint = "문명 알림판 조건: 기계 작물 6종 · 전초기지 3곳 · 작물 14종 · 주민 220마리 · 300,000코인",
            objs = new[] { Level(7) },
        });
        chapters.Add(c6);

        return chapters;
    }

    [MenuItem("Tools/WISH/진행 구조 다시 만들기")]
    public static void Build()
    {
        var items = new Dictionary<string, FarmingItemData>();
        foreach (var guid in AssetDatabase.FindAssets("t:FarmingItemData"))
        {
            var a = AssetDatabase.LoadAssetAtPath<FarmingItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null && !items.ContainsKey(a.name)) items[a.name] = a;
        }

        FarmingItemData Item(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (items.TryGetValue(name, out var it)) return it;
            Debug.LogError($"[QuestChainBuilder] 아이템을 찾을 수 없어요: {name}");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(QuestFolder))
            AssetDatabase.CreateFolder("Assets/Scripts/Debug_field/Quest", "Chain");

        var chain = AssetDatabase.LoadAssetAtPath<QuestChain>(ChainPath);
        if (chain == null)
        {
            chain = ScriptableObject.CreateInstance<QuestChain>();
            AssetDatabase.CreateAsset(chain, ChainPath);
        }
        chain.chapters.Clear();

        int questCount = 0;
        foreach (var ch in Define())
        {
            var chapter = new QuestChain.Chapter { title = ch.title, goal = ch.goal, intro = ch.intro ?? "" };
            foreach (var q in ch.quests)
            {
                string path = $"{QuestFolder}/{q.id}.asset";
                // 기존 에셋 재사용해서 GUID 유지
                var def = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<QuestDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }
                def.questName   = q.name;
                def.description = q.desc ?? "";
                def.talk        = q.talk ?? "";
                def.hint        = q.hint ?? "";
                def.padHint     = q.padHint ?? "";
                def.tutorialTip = q.tip ?? "";
                def.rewardCoins = q.coins;
                def.objectives  = new List<QuestObjective>();
                foreach (var o in q.objs)
                    def.objectives.Add(new QuestObjective
                    {
                        type             = o.type,
                        targetItem       = Item(o.item),
                        targetTechNodeId = o.tech ?? "",
                        targetAmount     = Mathf.Max(1, o.amount),
                    });
                def.rewards = new List<QuestReward>();
                foreach (var r in q.rewards)
                    def.rewards.Add(new QuestReward { item = Item(r.item), amount = r.amount });
                EditorUtility.SetDirty(def);
                chapter.quests.Add(def);
                questCount++;
            }
            chain.chapters.Add(chapter);
        }
        EditorUtility.SetDirty(chain);

        var stages = AssetDatabase.LoadAssetAtPath<CoreStageTable>(StagesPath);
        if (stages != null)
        {
            // stages[0]은 시작 단계
            for (int i = 0; i < Levels.Length && i + 1 < stages.stages.Count; i++)
            {
                var reqs = new List<CoreRequirement>();
                foreach (var r in Levels[i])
                    reqs.Add(new CoreRequirement
                    {
                        type       = r.type,
                        item       = Item(r.item),
                        amount     = Mathf.Max(1, r.amount),
                        needsPower = r.power,
                        techNodeId = "",
                    });
                stages.stages[i + 1].requirements = reqs;
            }
            EditorUtility.SetDirty(stages);
        }
        else Debug.LogError($"[QuestChainBuilder] {StagesPath}이 없어요.");

        AssetDatabase.SaveAssets();
        Debug.Log($"[QuestChainBuilder] 퀘스트 {questCount}개 · 장 {chain.chapters.Count}개 · 문명 조건 {Levels.Length}단계를 만들었어요.");
    }
}
