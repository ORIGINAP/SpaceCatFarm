using UnityEngine;

// 토양 위 반짝임 + 처음 들어갈 때 알림
public class SoilFx : MonoBehaviour
{
    private const int   Pool     = 6;
    private const float Life     = 1.2f;
    private const float Rise     = 0.35f;
    private const float Gap      = 0.4f;
    private const float NoticeCooldown = 180f;

    private static readonly string[] SparkRows =
    {
        "..#..",
        ".#w#.",
        "#www#",
        ".#w#.",
        "..#..",
    };
    private static readonly string[] PuffRows =
    {
        ".###.",
        "#www#",
        "#www#",
        ".###.",
    };
    private static readonly string[] EmberRows =
    {
        ".#.",
        "#w#",
        ".#.",
    };

    private readonly SpriteRenderer[] items = new SpriteRenderer[Pool];
    private readonly float[]   born = new float[Pool];
    private readonly Vector3[] from = new Vector3[Pool];
    private readonly float[] noticeAt = new float[4];
    private Sprite spark, puff, emberSprite;
    private float next, nextCheck;
    private SoilType standing;

    public static void Ensure()
    {
        if (FindFirstObjectByType<SoilFx>() == null) new GameObject("SoilFx").AddComponent<SoilFx>();
    }

    private void Awake()
    {
        spark       = LabArt.Pixel("soil_spark", SparkRows, "##E8DEFF w#FFF6C2", new Vector2(0.5f, 0.5f));
        puff        = LabArt.Pixel("soil_puff", PuffRows, "##CFEFE8 w#FFFFFF", new Vector2(0.5f, 0.5f));
        emberSprite = LabArt.Pixel("soil_ember", EmberRows, "##FF8C50 w#FFD27A", new Vector2(0.5f, 0.5f));
        for (int i = 0; i < Pool; i++)
        {
            items[i] = LabArt.Make("SoilFx", spark, Vector3.zero, 2, Color.white, transform);
            items[i].enabled = false;
            born[i] = -10f;
        }
    }

    private void Update()
    {
        float now = Time.time;
        for (int i = 0; i < Pool; i++)
        {
            var s = items[i];
            if (!s.enabled) continue;
            float t = (now - born[i]) / Life;
            if (t >= 1f) { s.enabled = false; continue; }
            s.transform.position = from[i] + Vector3.up * (Rise * t);
            s.color = new Color(1f, 1f, 1f, Mathf.Sin(t * Mathf.PI));
        }

        if (now >= next)
        {
            next = now + Gap;
            Spawn(now);
        }
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            CheckStanding();
        }
    }

    private void Spawn(float now)
    {
        var gm  = GridManager.Instance;
        var cam = Camera.main;
        if (gm == null || cam == null || TerrainMap.Current == null) return;
        float dist = -cam.transform.position.z;
        if (dist <= 0f) return;
        Vector3 a = cam.ViewportToWorldPoint(new Vector3(0f, 0f, dist));
        Vector3 b = cam.ViewportToWorldPoint(new Vector3(1f, 1f, dist));
        var p = new Vector3(Random.Range(a.x, b.x), Random.Range(a.y, b.y), 0f);
        var soil = Soil.At(gm.WorldToGrid(p));
        if (soil == SoilType.None) return;

        for (int i = 0; i < Pool; i++)
        {
            var s = items[i];
            if (s.enabled) continue;
            s.sprite = soil == SoilType.Starlight ? spark : soil == SoilType.Spring ? puff : emberSprite;
            s.color  = new Color(1f, 1f, 1f, 0f);
            s.transform.position = from[i] = p;
            born[i]   = now;
            s.enabled = true;
            return;
        }
    }

    private void CheckStanding()
    {
        var gm  = GridManager.Instance;
        var ptc = PlayerTileController.Instance;
        if (gm == null || ptc == null) return;
        var soil = Soil.At(gm.WorldToGrid(ptc.transform.position));
        if (soil == standing) return;
        standing = soil;
        if (soil == SoilType.None || Time.unscaledTime < noticeAt[(int)soil]) return;
        noticeAt[(int)soil] = Time.unscaledTime + NoticeCooldown;
        NotificationUI.Instance?.Show($"{Soil.Name(soil)}! 여기서 {Soil.GrowthMult:0}배로 자라고 하나 더 열려요: {Soil.CropsText(soil)}");
    }
}
