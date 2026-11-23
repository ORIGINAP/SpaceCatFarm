using System.Collections.Generic;
using UnityEngine;

public class ResidentCats : MonoBehaviour
{
    public const int MaxVisible = 16;
    private const float CheckInterval = 0.5f;
    // 잠자리 있는 주민 중 밖에 나와 노는 수
    private const int   MinOut = 4, MaxOut = 8, PopulationPerOut = 60;
    private const float OutMinSeconds = 60f, OutMaxSeconds = 120f;
    private const float ArrivalSeconds = 40f;
    private const int   MaxArrivalsShown = 10;
    private const float HomelessNoticeSeconds = 300f;
    private const int   CampLevel = 2;

    private static ResidentCats instance;

    public static int Population => ShuttleService.Population;

    // 캠프 · 거주 모듈의 잠자리 합
    public static int Beds { get; private set; }

    public static int Homeless => Mathf.Max(0, Population - Beds);

    private readonly List<ResidentCat>  cats  = new();
    private readonly List<ResidentSave> shown = new();
    private readonly List<TileItem>     homes = new();
    private readonly List<TileItem>     buffer = new();
    private readonly List<ResidentSave> visible = new();
    private readonly List<float>        leaveAt  = new();
    private readonly List<bool>         arrivals = new();
    private readonly List<TileItem>     dens = new();
    private float nextHomelessNotice;
    private float nextCheck;
    private int   seenPopulation = -1;

    private void Awake() => instance = this;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Beds = 0;
    }

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + CheckInterval;
        var svc = ShuttleService.Instance;
        if (svc == null || SaveManager.IsLoading) return;

        CollectHomes();
        if (homes.Count == 0) return;

        var list = svc.Residents;

        int arrived = seenPopulation < 0 || SaveManager.RecentlyLoaded ? 0 : Mathf.Max(0, ShuttleService.Population - seenPopulation);
        if (seenPopulation == 0 && arrived > 0) NotificationUI.Instance?.Show(ResidentBond.FirstArrivalTip);
        bool firstPass = seenPopulation < 0;
        seenPopulation = ShuttleService.Population;
        if (arrived > 0) NoticeHomeless();
        arrived = Mathf.Min(arrived, MaxArrivalsShown);

        ChooseVisible(list, arrived);

        for (int i = cats.Count - 1; i >= 0; i--)
        {
            if (cats[i] == null) { cats.RemoveAt(i); shown.RemoveAt(i); continue; }
            var now = Find(visible, shown[i]);
            if (now != null) { shown[i] = now; cats[i].Resident = now; continue; }
            var den = NearestDen(cats[i].transform.position);
            if (den != null) cats[i].SetHome(den);
            cats[i].GoHome();
            cats.RemoveAt(i);
            shown.RemoveAt(i);
        }
        int hopIndex = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            if (r == null || !visible.Contains(r) || Contains(r)) continue;
            bool arrival = i >= list.Count - arrived && PadPosition(r, out _);
            var home = homes[(i + homes.Count) % homes.Count];
            // 잠자리에서 나오는 고양이는 문에서 폴짝
            var den  = arrival || firstPass || dens.Count == 0 ? null : dens[Random.Range(0, dens.Count)];
            Vector2 at = arrival && PadPosition(r, out var door) ? door : den != null ? DoorOf(den) : Near(home);
            var cat = ResidentCat.Spawn(at, home, r.name, r.color, r.accessory, arrival || den != null,
                                        arrival ? hopIndex * 0.35f : 0f, arrival ? hopIndex : 0, arrival && hopIndex == 0, quiet: !arrival);
            if (arrival) hopIndex++;
            if (cat == null) continue;
            cat.Resident = r;
            cats.Add(cat);
            shown.Add(r);
        }

        // 축제 동안은 모두 코어로, 끝나면 제자리로
        bool party = TownLife.FestivalActive && CenterCore.Current != null;
        bool partyOver = partied && !party;
        partied = party;
        for (int i = 0; i < cats.Count; i++)
        {
            var cat = cats[i];
            if (cat == null) continue;
            if (party) cat.SetHome(CenterCore.Current);
            else if (partyOver || cat.Home == null || !homes.Contains(cat.Home)) cat.SetHome(homes[i % homes.Count]);
        }
    }

    private bool partied;

    private int FieldTarget()
    {
        if (TownLife.FestivalActive) return MaxVisible;
        int pop = Population;
        int outCats = Mathf.Clamp(MinOut + pop / PopulationPerOut, MinOut, MaxOut);
        return Mathf.Min(MaxVisible, outCats + Homeless);
    }

    private void ChooseVisible(IReadOnlyList<ResidentSave> list, int arrived)
    {
        // 시간 다 됐으면 들어가고 다른 고양이가 나옴
        for (int i = visible.Count - 1; i >= 0; i--)
        {
            var now = Find(list, visible[i]);
            if (now == null || Time.time >= leaveAt[i]) RemoveVisible(i);
            else visible[i] = now;
        }
        // 막 도착한 가족은 잠깐 구경
        for (int i = Mathf.Max(0, list.Count - arrived); i < list.Count; i++)
        {
            if (list[i] == null) continue;
            int at = VisibleIndex(list[i]);
            if (at >= 0) RemoveVisible(at);
            AddVisible(list[i], ArrivalSeconds, true);
        }

        int want = FieldTarget(), regular = 0;
        foreach (bool arrival in arrivals) if (!arrival) regular++;
        // 캠프를 지으면 바로 줄어듦
        for (; regular > want; regular--) RemoveVisible(SoonestToLeave());
        for (; regular < want; regular++)
        {
            var r = PickOut(list);
            if (r == null) break;
            AddVisible(r, Random.Range(OutMinSeconds, OutMaxSeconds), false);
        }
    }

    private void AddVisible(ResidentSave r, float seconds, bool arrival)
    {
        visible.Add(r);
        leaveAt.Add(Time.time + seconds);
        arrivals.Add(arrival);
    }

    private void RemoveVisible(int i)
    {
        visible.RemoveAt(i);
        leaveAt.RemoveAt(i);
        arrivals.RemoveAt(i);
    }

    private int VisibleIndex(ResidentSave r)
    {
        for (int i = 0; i < visible.Count; i++)
            if (Same(visible[i], r)) return i;
        return -1;
    }

    private int SoonestToLeave()
    {
        int best = -1;
        for (int i = 0; i < visible.Count; i++)
            if (!arrivals[i] && (best < 0 || leaveAt[i] < leaveAt[best])) best = i;
        return best;
    }

    // 친해진 주민이 조금 더 자주 나옴
    private ResidentSave PickOut(IReadOnlyList<ResidentSave> list)
    {
        if (list.Count == 0) return null;
        for (int tries = 0; tries < 12; tries++)
        {
            var r = list[Random.Range(0, list.Count)];
            if (r == null || VisibleIndex(r) >= 0) continue;
            if (r.friendship <= 0 && tries < 4 && Random.value < 0.5f) continue;
            return r;
        }
        foreach (var r in list)
            if (r != null && VisibleIndex(r) < 0) return r;
        return null;
    }

    private void NoticeHomeless()
    {
        if (Homeless <= 0 || CenterCore.CurrentLevel < CampLevel || Time.time < nextHomelessNotice) return;
        nextHomelessNotice = Time.time + HomelessNoticeSeconds;
        NotificationUI.Instance?.Show($"잠자리가 모자라요 (주민 {Population} · 잠자리 {Beds}) — 주민 캠프를 더 지으면 안으로 들어가요");
    }

    private TileItem NearestDen(Vector3 from)
    {
        TileItem best = null;
        float bestDist = float.MaxValue;
        foreach (var d in dens)
        {
            float dist = (d.transform.position - from).sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; best = d; }
        }
        return best;
    }

    private static Vector2 DoorOf(TileItem t)
    {
        var gm = GridManager.Instance;
        Vector2 a = gm.GridToWorld(t.FootprintMin), b = gm.GridToWorld(t.FootprintMax);
        return new Vector2((a.x + b.x) * 0.5f, a.y - gm.CellSize * 0.6f);
    }

    private void CollectHomes()
    {
        homes.Clear();
        dens.Clear();
        var gm = GridManager.Instance;
        if (gm == null) return;
        int beds = 0;
        gm.CollectTiles(TileType.Civilization, buffer);
        foreach (var t in buffer)
            if (t is CivFacility f && !f.IsGhost && f.IsPowered && !homes.Contains(f))
            {
                homes.Add(f);
                if (f.Effect == CivEffect.Housing)
                {
                    homes.Add(f);
                    dens.Add(f);
                    beds += ResidentCamp.HabitatBeds;
                }
            }
        gm.CollectTiles(TileType.Camp, buffer);
        int left = Population - beds;
        foreach (var t in buffer)
        {
            if (t is not ResidentCamp camp || camp.IsGhost) continue;
            homes.Add(camp);
            homes.Add(camp);
            dens.Add(camp);
            beds += ResidentCamp.Beds;
            camp.SetLit(left > 0);
            left -= ResidentCamp.Beds;
        }
        Beds = beds;
        gm.CollectTiles(TileType.ShuttlePad, buffer);
        foreach (var t in buffer)
            if (t != null && !t.IsGhost && !homes.Contains(t)) homes.Add(t);
        if (CenterCore.Current != null) homes.Add(CenterCore.Current);
    }

    private bool Contains(ResidentSave r)
    {
        foreach (var s in shown) if (Same(s, r)) return true;
        return false;
    }

    private static ResidentSave Find(IReadOnlyList<ResidentSave> list, ResidentSave r)
    {
        foreach (var x in list) if (Same(x, r)) return x;
        return null;
    }

    // 참가자는 id로 비교
    private static bool Same(ResidentSave a, ResidentSave b)
        => a == b || (a != null && b != null && (a.id > 0 && b.id > 0
               ? a.id == b.id
               : a.name == b.name && a.color == b.color && a.accessory == b.accessory && a.padX == b.padX && a.padY == b.padY));

    private static bool PadPosition(ResidentSave r, out Vector2 door)
    {
        door = default;
        var gm = GridManager.Instance;
        if (gm == null || r.padX == ShuttleService.NoPad) return false;
        var pos = new Vector2Int(r.padX, r.padY);
        if (gm.GetTile(pos) is not ShuttlePad pad || pad.GridPosition != pos) return false;
        door = (Vector2)pad.transform.position + Vector2.up * 0.1f;
        return true;
    }

    private static Vector2 Near(TileItem home)
    {
        Vector2 p = (Vector2)home.transform.position + Random.insideUnitCircle * 2.2f + Vector2.down * 0.8f;
        return GridManager.Instance != null ? GridManager.Instance.ClampToWorldBounds(p) : p;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (var cat in cats)
            if (cat != null) Destroy(cat.gameObject);
        cats.Clear();
        shown.Clear();
    }
}
