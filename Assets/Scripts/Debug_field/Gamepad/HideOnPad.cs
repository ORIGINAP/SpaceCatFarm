using UnityEngine;

// 키보드 전용 표시를 패드에선 숨김
public class HideOnPad : MonoBehaviour
{
    public GameObject target;

    private void Update()
    {
        bool show = !Pad.Active;
        if (target != null && target.activeSelf != show) target.SetActive(show);
    }
}
