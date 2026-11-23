using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 기계 작업선: 얇고 옅게 잠깐만
public class FlashBeam : MonoBehaviour
{
    public enum Kind { Mining, Seeding, Harvesting, Generic }

    private const float ShootTime = 0.07f;
    private const float HoldTime  = 0.06f;
    private const float FadeTime  = 0.17f;
    private const float TotalTime = ShootTime + HoldTime + FadeTime;
    private const float Width     = 0.028f;
    private const float Alpha     = 0.6f;
    // 고급 설비가 한 번에 6칸 쳐도 선은 둘만
    private const int   MaxBeams  = 2;

    private Kind defaultKind;
    private int  baseOrder;
    private readonly List<Beam> pool = new();

    private class Beam
    {
        public LineRenderer line;
        public Coroutine    co;
        public float        startedAt;
        public bool         busy;
    }

    public static FlashBeam Attach(Transform owner, Kind kind = Kind.Generic, int sortingOrder = 60)
    {
        var go = new GameObject("FlashBeam");
        go.transform.SetParent(owner, false);

        var beam = go.AddComponent<FlashBeam>();
        beam.defaultKind = kind;
        beam.baseOrder   = sortingOrder;
        return beam;
    }

    public static FlashBeam Attach(Transform owner, int sortingOrder) => Attach(owner, Kind.Generic, sortingOrder);

    public void Fire(Vector3 from, Vector3 to) => Fire(from, to, defaultKind);

    public void Fire(Vector3 from, Vector3 to, Kind kind) => Launch(from, to, ColorOf(kind), kind);

    public void Flash(Vector3 from, Vector3 to, Color color, float width, float duration)
        => Launch(from, to, Color.Lerp(color, Color.white, 0.3f), Kind.Generic);

    private void Launch(Vector3 from, Vector3 to, Color color, Kind kind)
    {
        if (!isActiveAndEnabled) return;
        Sfx.PlayAt(SoundOf(kind), (from + to) * 0.5f);

        var beam = AcquireBeam();
        if (beam.co != null) StopCoroutine(beam.co);
        beam.co = StartCoroutine(Run(beam, from, to, color));
    }

    private Beam AcquireBeam()
    {
        foreach (var b in pool)
            if (!b.busy) return b;

        if (pool.Count < MaxBeams)
        {
            var created = CreateBeam();
            pool.Add(created);
            return created;
        }

        // 다 쓰면 가장 오래된 것 재사용
        Beam oldest = pool[0];
        foreach (var b in pool)
            if (b.startedAt < oldest.startedAt) oldest = b;
        return oldest;
    }

    private IEnumerator Run(Beam b, Vector3 from, Vector3 to, Color color)
    {
        b.busy      = true;
        b.startedAt = Time.time;
        b.line.enabled = true;

        from.z = to.z = 0f;
        Vector3 dir  = to - from;
        float   len  = dir.magnitude;
        Vector3 unit = len > 0.0001f ? dir / len : Vector3.right;
        Vector3 start = from + unit * Mathf.Min(0.18f, len * 0.25f);

        for (float t = 0f; t < TotalTime; t += Time.deltaTime)
        {
            float headP = Mathf.Clamp01(t / ShootTime);
            headP = 1f - (1f - headP) * (1f - headP);
            float fadeP = Mathf.Clamp01((t - ShootTime - HoldTime) / FadeTime);

            b.line.SetPosition(0, Vector3.Lerp(start, to, fadeP * fadeP * 0.9f));
            b.line.SetPosition(1, Vector3.Lerp(start, to, headP));
            color.a = Alpha * (1f - fadeP);
            b.line.startColor = b.line.endColor = color;

            yield return null;
        }

        b.line.enabled = false;
        b.busy = false;
        b.co   = null;
    }

    private static SfxId SoundOf(Kind kind) => kind switch
    {
        Kind.Seeding    => SfxId.LaserSeeding,
        Kind.Harvesting => SfxId.LaserHarvest,
        _               => SfxId.LaserMining,
    };

    private static Color ColorOf(Kind kind) => kind switch
    {
        Kind.Mining     => new Color(0.68f, 0.86f, 0.96f),
        Kind.Seeding    => new Color(0.70f, 0.90f, 0.66f),
        Kind.Harvesting => new Color(0.98f, 0.83f, 0.58f),
        _               => new Color(0.87f, 0.90f, 0.96f),
    };

    private Beam CreateBeam()
    {
        var line = WorldConnectionLine.Create(Vector3.zero, Vector3.zero, Color.clear, Width, baseOrder, transform);
        line.enabled = false;
        return new Beam { line = line };
    }
}
