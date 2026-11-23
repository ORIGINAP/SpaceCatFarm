using UnityEngine;

// 주민이 들어가 쉬는 숙소
public class ResidentCamp : TileItem
{
    public const int Beds = 20;
    public const int HabitatBeds = 60;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite litSprite;
    [SerializeField] private Sprite emptySprite;

    private bool lit;

    public override string AbilityText =>
        $"주민 {Beds}마리가 들어가 쉬어요. 잠자리가 넉넉하면 밖에는 몇 마리만 번갈아 나와 놀아요";

    public string Report =>
        $"주민 캠프: 한 채에 {Beds}마리 · 마을 잠자리 {ResidentCats.Beds} · 주민 {ResidentCats.Population}마리"
        + (ResidentCats.Homeless > 0 ? $" · {ResidentCats.Homeless}마리는 잠자리가 없어요" : "");

    private void Start() => ApplyLook();

    public void SetLit(bool on)
    {
        if (lit == on) return;
        lit = on;
        ApplyLook();
    }

    private void ApplyLook()
    {
        if (spriteRenderer == null) return;
        var sprite = IsGhost || lit ? litSprite : emptySprite;
        if (sprite != null) spriteRenderer.sprite = sprite;
    }
}
