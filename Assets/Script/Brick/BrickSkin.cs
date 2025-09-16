using UnityEngine;

public class BrickSkin : MonoBehaviour
{
    [Header("타깃 스프라이트 (비워두면 자식에서 자동 검색)")]
    public SpriteRenderer target;

    [Header("색 소스")]
    public ColorPaletteSO palette;   // 팔레트가 있으면 최우선
    public bool mapByHitPoints = false; // HP→팔레트 인덱스 매핑
    public int colorIndex = -1;      // -1이면 랜덤
    public bool randomizeOnAwake = true;

    public bool useHpGradient = false;
    public Gradient hpGradient;      // useHpGradient=true일 때 사용
    public int maxHpForGradient = 10;

    [Header("적용 방식")]
    public bool usePropertyBlock = false; // 문제가 있으면 꺼두고 sr.color 사용
    public bool keepOriginalAlpha = true; // 알파는 원래 값 유지

    MaterialPropertyBlock _mpb;

    void Awake()
    {
        ResolveTarget();
        ApplyColor();
    }

    void OnValidate()
    {
        // 에디터에서 값 바꿀 때도 즉시 반영
        ResolveTarget();
        ApplyColor();
    }

    void ResolveTarget()
    {
        if (target == null)
        {
            // 본인 → 자식 순으로 가장 먼저 등장하는 SpriteRenderer를 사용
            target = GetComponent<SpriteRenderer>();
            if (target == null)
                target = GetComponentInChildren<SpriteRenderer>(true);
        }
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    public void ApplyColor()
    {
        if (target == null) return;

        // 1) 색 결정
        Color tint = Color.white;

        if (useHpGradient && hpGradient != null && hpGradient.colorKeys != null && hpGradient.colorKeys.Length > 0)
        {
            int hp = 1;
            var brick = GetComponent<Brick>();
            if (brick) hp = Mathf.Max(1, brick.hitPoints);
            float t = Mathf.InverseLerp(1, Mathf.Max(1, maxHpForGradient), hp);
            tint = hpGradient.Evaluate(t);
        }
        else if (palette != null && palette.colors != null && palette.colors.Length > 0)
        {
            int idx = colorIndex;
            if (mapByHitPoints)
            {
                var brick = GetComponent<Brick>();
                int hp = brick ? Mathf.Max(1, brick.hitPoints) : 1;
                idx = Mathf.Clamp(hp - 1, 0, palette.colors.Length - 1);
            }
            if (idx < 0 || idx >= palette.colors.Length)
                idx = randomizeOnAwake ? Random.Range(0, palette.colors.Length) : 0;
            tint = palette.colors[idx];
        }

        // 알파는 원래 유지(투명화 방지)
        if (keepOriginalAlpha)
        {
            var a = target.color.a;
            tint.a = Mathf.Max(a, 1f); // 보이지 않는 문제 회피(1 보장)
        }
        else
        {
            // 최소 1 보장
            tint.a = 1f;
        }

        // 2) 적용
        if (usePropertyBlock)
        {
            target.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", tint);
            target.SetPropertyBlock(_mpb);
        }
        else
        {
            // 가장 안전: 그냥 SpriteRenderer.color
            target.color = tint;
        }
    }
}
