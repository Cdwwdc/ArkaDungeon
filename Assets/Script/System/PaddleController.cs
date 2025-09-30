using UnityEngine;

// ▣ 패들 좌우 이동 + 반사 각도 보정 + 자식 포함 폭으로 클램프
public class PaddleController : MonoBehaviour
{
    [Header("이동 범위(X 최소/최대) (벽 Transform이 없을 때만 사용)")]
    public float minX = -5f;
    public float maxX = 5f;

    [Header("좌/우 벽(옵션): 있으면 이 사이로만 이동")]
    public Transform leftWall;
    public Transform rightWall;

    [SerializeField] bool preferWallTransforms = true;
    [SerializeField] string leftWallName = "WallLeft";
    [SerializeField] string rightWallName = "WallRight";
    float wallRefRefreshTimer = 0f;

    [Header("이동 속도(좌/우키 또는 마우스)")]
    public float moveSpeed = 12f;

    [Header("패들 상면 반사 각 조절 (최대 각도)")]
    public float maxBounceAngle = 60f; // 도 단위

    [Header("반사 튜닝")]
    public float paddleVelInfluence = 0.20f;
    public float minVerticalDot = 0.40f; // 0.35~0.45 권장
    public float minSpeed = 6f, maxSpeed = 14f;

    float xVel;

    // 입력 잠금(컨티뉴 등에서 패들 멈춤)
    bool _inputEnabled = true;
    public void SetInputEnabled(bool enabled) { _inputEnabled = enabled; }

    void Start()
    {
        // 시작 시 한 번 안전 클램프 (자식 포함 폭)
        float half = ComputeHalfWidthFromChildren();
        ResolveLimits(half, out float leftLimit, out float rightLimit);
        float clampedX = Mathf.Clamp(transform.position.x, leftLimit, rightLimit);
        if (!Mathf.Approximately(clampedX, transform.position.x))
            transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
    }

    void Update()
    {
        if (!_inputEnabled) return;

        // 벽 레퍼런스가 끊겼다면 주기적으로 재탐색(프리팹 재생성 대응)
        if (preferWallTransforms)
        {
            wallRefRefreshTimer -= Time.deltaTime;
            if (wallRefRefreshTimer <= 0f)
            {
                wallRefRefreshTimer = 0.5f;
                if (!leftWall) leftWall = FindActiveByName(leftWallName);
                if (!rightWall) rightWall = FindActiveByName(rightWallName);
            }
        }

        // 목표 X 계산
        float targetX;
        if (Input.GetMouseButton(0))
        {
            Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            targetX = m.x;
        }
        else
        {
            float h = Input.GetAxisRaw("Horizontal");
            targetX = transform.position.x + h * moveSpeed * Time.deltaTime;
        }

        // 클램프 기준 계산(자식 포함 폭 사용)
        float half = ComputeHalfWidthFromChildren();
        ResolveLimits(half, out float leftLimit, out float rightLimit);

        float nextX = Mathf.Clamp(targetX, leftLimit, rightLimit);
        xVel = (nextX - transform.position.x) / Mathf.Max(Time.deltaTime, 0.0001f);
        transform.position = new Vector3(nextX, transform.position.y, 0f);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.collider.CompareTag("Ball")) return;

        Rigidbody2D rb = col.rigidbody;
        if (rb == null) return;

        // 패들 전체(자식 포함) 바운즈로 반사 각 계산
        Bounds b;
        if (!TryComputeBoundsFromChildren(out b) || col.contactCount == 0) return;

        float centerX = b.center.x;
        float half = Mathf.Max(b.extents.x, 0.0001f);
        float hitX = col.GetContact(0).point.x;
        float t = Mathf.Clamp((hitX - centerX) / half, -1f, 1f);

        float angleRad = t * maxBounceAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad));

        // 패들 x속도 영향
        dir.x += xVel * paddleVelInfluence * 0.01f;
        dir = dir.normalized;

        // 최소 상승각 보장
        dir = EnsureMinVerticalLocal(dir, minVerticalDot);

        float speed = Mathf.Clamp(rb.velocity.magnitude, minSpeed, maxSpeed);
        rb.velocity = dir * speed;
    }

    Vector2 EnsureMinVerticalLocal(Vector2 d, float minDot)
    {
        d.Normalize();
        if (Mathf.Abs(d.y) >= minDot) return d;
        float signY = d.y >= 0f ? 1f : -1f;
        float newY = minDot * signY;
        float newX = Mathf.Sign(d.x == 0 ? 1f : d.x) * Mathf.Sqrt(Mathf.Max(0.0001f, 1f - minDot * minDot));
        return new Vector2(newX, newY).normalized;
    }

    // ==== 유틸: 자식까지 포함해 폭/바운즈 계산 ====

    /// <summary>자식 포함 전체 폭의 절반(half extents X) 반환. 아무것도 없으면 0.5f</summary>
    float ComputeHalfWidthFromChildren()
    {
        Bounds b;
        if (TryComputeBoundsFromChildren(out b)) return b.extents.x;
        return 0.5f;
    }

    /// <summary>자식 포함 활성 Collider2D(우선) → 없으면 활성 Renderer 기준으로 Bounds 계산</summary>
    bool TryComputeBoundsFromChildren(out Bounds bounds)
    {
        var cols = GetComponentsInChildren<Collider2D>(includeInactive: false);
        bool any = false;
        bounds = new Bounds(transform.position, Vector3.zero);

        foreach (var c in cols)
        {
            if (!c || !c.enabled) continue;
            any = true;
            bounds.Encapsulate(c.bounds);
        }

        if (!any)
        {
            var rs = GetComponentsInChildren<Renderer>(includeInactive: false);
            foreach (var r in rs)
            {
                if (!r || !r.enabled) continue;
                any = true;
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    /// <summary>벽 트랜스폼이 둘 다 유효하면 그걸 사용, 아니면 min/max 폴백</summary>
    void ResolveLimits(float half, out float leftLimit, out float rightLimit)
    {
        if (preferWallTransforms && leftWall && rightWall)
        {
            leftLimit = leftWall.position.x + half;
            rightLimit = rightWall.position.x - half;
        }
        else
        {
            leftLimit = minX;
            rightLimit = maxX;
        }
    }

    Transform FindActiveByName(string objName)
    {
        if (string.IsNullOrEmpty(objName)) return null;
        var go = GameObject.Find(objName);
        if (go && go.activeInHierarchy) return go.transform;
        return null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // 에디터에서 현재 한계선을 보여줌
        float half = ComputeHalfWidthFromChildren();
        ResolveLimits(half, out float leftLimit, out float rightLimit);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(leftLimit, transform.position.y - 10f, 0f),
                        new Vector3(leftLimit, transform.position.y + 10f, 0f));
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(rightLimit, transform.position.y - 10f, 0f),
                        new Vector3(rightLimit, transform.position.y + 10f, 0f));
    }
#endif
}
