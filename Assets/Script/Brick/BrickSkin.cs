using UnityEngine;

public class BrickSkin : MonoBehaviour
{
    [Header("Ÿ�� ��������Ʈ (����θ� �ڽĿ��� �ڵ� �˻�)")]
    public SpriteRenderer target;

    [Header("�� �ҽ�")]
    public ColorPaletteSO palette;   // �ȷ�Ʈ�� ������ �ֿ켱
    public bool mapByHitPoints = false; // HP���ȷ�Ʈ �ε��� ����
    public int colorIndex = -1;      // -1�̸� ����
    public bool randomizeOnAwake = true;

    public bool useHpGradient = false;
    public Gradient hpGradient;      // useHpGradient=true�� �� ���
    public int maxHpForGradient = 10;

    [Header("���� ���")]
    public bool usePropertyBlock = false; // ������ ������ ���ΰ� sr.color ���
    public bool keepOriginalAlpha = true; // ���Ĵ� ���� �� ����

    MaterialPropertyBlock _mpb;

    void Awake()
    {
        ResolveTarget();
        ApplyColor();
    }

    void OnValidate()
    {
        // �����Ϳ��� �� �ٲ� ���� ��� �ݿ�
        ResolveTarget();
        ApplyColor();
    }

    void ResolveTarget()
    {
        if (target == null)
        {
            // ���� �� �ڽ� ������ ���� ���� �����ϴ� SpriteRenderer�� ���
            target = GetComponent<SpriteRenderer>();
            if (target == null)
                target = GetComponentInChildren<SpriteRenderer>(true);
        }
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    public void ApplyColor()
    {
        if (target == null) return;

        // 1) �� ����
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

        // ���Ĵ� ���� ����(����ȭ ����)
        if (keepOriginalAlpha)
        {
            var a = target.color.a;
            tint.a = Mathf.Max(a, 1f); // ������ �ʴ� ���� ȸ��(1 ����)
        }
        else
        {
            // �ּ� 1 ����
            tint.a = 1f;
        }

        // 2) ����
        if (usePropertyBlock)
        {
            target.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", tint);
            target.SetPropertyBlock(_mpb);
        }
        else
        {
            // ���� ����: �׳� SpriteRenderer.color
            target.color = tint;
        }
    }
}
