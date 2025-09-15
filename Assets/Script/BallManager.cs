using System.Collections.Generic;
using UnityEngine;

public class BallManager : MonoBehaviour
{
    [Header("참조(없으면 자동 탐색)")]
    public GameManager gm;

    [Header("멀티볼 설정")]
    [Tooltip("아이템으로 추가 생성 시 각도 오프셋(도)")]
    public float splitAngle = 12f;

    // 등록은 느슨하게: Spawn 시 Register, ClearAll 시 비움. 안전을 위해 Count는 태그 스캔.
    private readonly HashSet<GameObject> set = new HashSet<GameObject>();
    private int lastCount = 0;

    void Awake()
    {
        if (!gm) gm = FindObjectOfType<GameManager>();
    }

    // 매 프레임 라스트볼 체크(파괴 타이밍 race 방지)
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
            gm.ShowContinue(); // 마지막 공이 떨어진 시점에만 컨티뉴
        }
        lastCount = count;
    }

    public int ActiveCount()
    {
        // 태그 스캔이 레이스에 강함(수량도 많지 않은 게임)
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

        // 혹시 태그 스캔으로 남은 것이 있으면 안전 청소
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
        // 기준 위치를 못 받으면 패들 위치 기준
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
    // ===== 아이템: 현재 '가장 가까운' 공에서 1개 추가 =====
    public void AddOneFromNearestTo(Vector3 refPos)
    {
        var nearest = FindNearestBall(refPos);
        if (nearest == null || gm == null) return;

        var rb = nearest.GetComponent<Rigidbody2D>();
        Vector2 dir = rb && rb.velocity.sqrMagnitude > 0.001f ? rb.velocity.normalized : Vector2.up;
        float speed = rb ? rb.velocity.magnitude : 8f;

        // 새 공은 splitAngle만큼 틀어서 생성(겹침 방지)
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
