using System.Collections.Generic;
using UnityEngine;

public class BallManager : MonoBehaviour
{
    [Header("����(������ �ڵ� Ž��)")]
    public GameManager gm;

    [Header("��Ƽ�� ����")]
    [Tooltip("���������� �߰� ���� �� ���� ������(��)")]
    public float splitAngle = 12f;

    // ����� �����ϰ�: Spawn �� Register, ClearAll �� ���. ������ ���� Count�� �±� ��ĵ.
    private readonly HashSet<GameObject> set = new HashSet<GameObject>();
    private int lastCount = 0;

    void Awake()
    {
        if (!gm) gm = FindObjectOfType<GameManager>();
    }

    // �� ������ ��Ʈ�� üũ(�ı� Ÿ�̹� race ����)
    void LateUpdate()
    {
        if (gm == null || gm.isTransitioning || gm.IsContinueShown())
        {
            lastCount = ActiveCount();
            return;
        }

        int count = ActiveCount();
        if (lastCount > 0 && count == 0)
        {
            gm.ShowContinue(); // ������ ���� ������ �������� ��Ƽ��
        }
        lastCount = count;
    }

    public int ActiveCount()
    {
        // �±� ��ĵ�� ���̽��� ����(������ ���� ���� ����)
        return GameObject.FindGameObjectsWithTag("Ball").Length;
    }

    public void Register(GameObject ball)
    {
        if (ball) set.Add(ball);
    }

    public void ClearAll()
    {
        foreach (var b in set) if (b) Object.Destroy(b);
        set.Clear();

        // Ȥ�� �±� ��ĵ���� ���� ���� ������ ���� û��
        var leftovers = GameObject.FindGameObjectsWithTag("Ball");
        foreach (var b in leftovers) if (b) Object.Destroy(b);
    }

    public void ResetForNewRoom()
    {
        set.Clear();
        lastCount = 0;
    }
    public void PowerUp_MultiBall()
    {
        // ���� ��ġ�� �� ������ �е� ��ġ ����
        Vector3 refPos = gm && gm.paddle ? gm.paddle.position : Vector3.zero;
        AddOneFromNearestTo(refPos);
    }

    public void PowerUp_MultiBall(Vector3 refPos)
    {
        AddOneFromNearestTo(refPos);
    }

    public void PowerUp_MultiBall(Transform t)
    {
        if (t) AddOneFromNearestTo(t.position);
        else PowerUp_MultiBall();
    }

    public void PowerUp_MultiBall(GameObject go)
    {
        if (go) AddOneFromNearestTo(go.transform.position);
        else PowerUp_MultiBall();
    }
    // ===== ������: ���� '���� �����' ������ 1�� �߰� =====
    public void AddOneFromNearestTo(Vector3 refPos)
    {
        var nearest = FindNearestBall(refPos);
        if (nearest == null || gm == null) return;

        var rb = nearest.GetComponent<Rigidbody2D>();
        Vector2 dir = rb && rb.velocity.sqrMagnitude > 0.001f ? rb.velocity.normalized : Vector2.up;
        float speed = rb ? rb.velocity.magnitude : 8f;

        // �� ���� splitAngle��ŭ Ʋ� ����(��ħ ����)
        Vector2 dir2 = RotateDeg(dir, splitAngle);
        gm.SpawnBallAt(nearest.transform.position, dir2, speed);
    }

    GameObject FindNearestBall(Vector3 p)
    {
        GameObject best = null;
        float bestD = float.MaxValue;
        var balls = GameObject.FindGameObjectsWithTag("Ball");
        foreach (var b in balls)
        {
            if (!b) continue;
            float d = (b.transform.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = b; }
        }
        return best;
    }

    static Vector2 RotateDeg(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }
}
