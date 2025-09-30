using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class ZoneVolume : MonoBehaviour
{
    [Header("스폰 가중치(확률 비율)")]
    [Range(0, 1)] public float brickWeight = 0.6f;
    [Range(0, 1)] public float monsterWeight = 0.3f;
    [Range(0, 1)] public float chestWeight = 0.1f;
    [Range(0, 1)] public float bossWeight = 0f;

    [Header("수량 범위")]
    public int minCount = 8;
    public int maxCount = 18;

    [Header("레벨 보정(옵션)")]
    [Tooltip("레벨 1 오를 때마다 수량 배율(+10% 등)")]
    public float levelDensityBonusPerStep = 0.0f;

    BoxCollider2D box;

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    public Vector3 SamplePointInside(System.Random rnd)
    {
        var c = (Vector2)transform.TransformPoint(box.offset);
        var s = box.size;
        float x = (float)(rnd.NextDouble() - 0.5) * s.x;
        float y = (float)(rnd.NextDouble() - 0.5) * s.y;
        return new Vector3(c.x + x, c.y + y, 0f);
    }

    public int ResolveCount(int level)
    {
        int baseCount = Mathf.Clamp(Random.Range(minCount, maxCount + 1), 0, 999);
        if (levelDensityBonusPerStep > 0f)
        {
            float mul = 1f + Mathf.Max(0, level) * levelDensityBonusPerStep;
            baseCount = Mathf.RoundToInt(baseCount * mul);
        }
        return baseCount;
    }

    /// <summary>0=brick, 1=monster, 2=chest, 3=boss</summary>
    public int RollKind(System.Random rnd)
    {
        float bw = brickWeight, mw = monsterWeight, cw = chestWeight, ow = bossWeight;
        float sum = bw + mw + cw + ow;
        if (sum <= 0f) return 0;
        double r = rnd.NextDouble() * sum;
        if (r < bw) return 0; r -= bw;
        if (r < mw) return 1; r -= mw;
        if (r < cw) return 2;
        return 3;
    }

    // 에디터 시각화
    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider2D>();
        if (!b) return;
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 1, 0.15f);
        Gizmos.DrawCube(b.offset, b.size);
        Gizmos.color = new Color(0, 1, 1, 1f);
        Gizmos.DrawWireCube(b.offset, b.size);
        Gizmos.matrix = m;
    }
}
