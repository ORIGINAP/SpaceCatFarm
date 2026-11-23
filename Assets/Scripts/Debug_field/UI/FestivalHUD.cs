using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 축제 중에만 날씨 아래 남은 시간
public class FestivalHUD : MonoBehaviour
{
    private TextMeshProUGUI label;
    private CanvasGroup     group;
    private float           nextRefresh;

    public static FestivalHUD Create()
    {
        var weather = WeatherHUD.Instance;
        if (weather == null) return null;

        // 날씨 칸에 붙여서 폰 화면 배치 · 겹침 순서를 같이 따름
        var go = new GameObject("FestivalHUD", typeof(RectTransform));
        go.transform.SetParent(weather.transform, false);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(0f, -6f);
        rt.sizeDelta = new Vector2(150f, 26f);

        CuteUIKit.DecorateWindow(rt, catEars: false, radius: 13, border: 3);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(10, 10, 3, 3);
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        go.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var hit = go.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => NotificationUI.Instance?.Show("마을 축제 중: " + TownLife.EffectText));

        var hud = go.AddComponent<FestivalHUD>();
        hud.label = CuteUIKit.Text(go.transform, "", 12f, CuteUIKit.CoralText, FontStyles.Bold);
        hud.label.raycastTarget = false;
        hud.group = go.AddComponent<CanvasGroup>();
        hud.Refresh();
        return hud;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        Refresh();
    }

    private void Refresh()
    {
        bool on = TownLife.FestivalActive;
        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = on;
        if (!on) return;
        int left = Mathf.CeilToInt(TownLife.FestivalSecondsLeft);
        label.text = $"마을 축제 {left / 60}:{left % 60:00}";
    }
}
