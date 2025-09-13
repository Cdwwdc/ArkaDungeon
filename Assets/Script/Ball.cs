using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Ball : MonoBehaviour
{
    [Header("속도")]
    public float startSpeed = 8f;
    public float minSpeed = 6f;
    public float maxSpeed = 14f;

    [Header("각도 고착 회피 (충돌 시에만 보정)")]
    [Tooltip("수직/수평에 너무 가까우면 회피 (0~1)")]
    public float axisLockDot = 0.985f;
    [Tooltip("정대각(45°) 고착도 약하게 회피 (0~1)")]
    public float diagLockDot = 0.995f;
    [Tooltip("고착 회피 시 ±얼마나 틀지(도 단위)")]
    public float nudgeDegrees = 3.5f;

    [Header("최소 상승각 보장 (수평 루프 차단)")]
    [Tooltip("|dir.y|가 이 값보다 작으면 강제로 올려줌 (예: 0.40 ≈ 약 24°)")]
    public float minVerticalDot = 0.40f; // 0.35~0.45 추천

    private Rigidbody2D rb;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Start()
    {
        // 초기 각도: 위쪽 대각(좌/우 랜덤) + 살짝 틀기
        float sx = Random.value < 0.5f ? -1f : 1f;
        Vector2 dir = new Vector2(sx, 1f).normalized;
        dir = RotateDeg(dir, Random.Range(-10f, 10f));
        rb.velocity = dir * startSpeed;
        // 권장: GravityScale=0, CollisionDetection=Continuous, Interpolate=Interpolate
    }

    void FixedUpdate()
    {
        // 속도만 클램프 (각도는 건드리지 않음)
        float spd = Mathf.Clamp(rb.velocity.magnitude, minSpeed, maxSpeed);
        rb.velocity = rb.velocity.normalized * spd;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // 충돌 순간에만 보정
        Vector2 dir = rb.velocity.normalized;

        if (IsAxisLocked(dir, axisLockDot) || IsDiagonalLocked(dir, diagLockDot))
            dir = RotateDeg(dir, Random.Range(-nudgeDegrees, nudgeDegrees));

        // ★ 수평 루프 방지: 최소 상승각 강제
        dir = EnsureMinVertical(dir);

        rb.velocity = dir * Mathf.Clamp(rb.velocity.magnitude, minSpeed, maxSpeed);
    }

    // ===== Helpers =====
    static bool IsAxisLocked(Vector2 d, float dot)
    {
        d.Normalize();
        return Mathf.Abs(Vector2.Dot(d, Vector2.up)) > dot
            || Mathf.Abs(Vector2.Dot(d, Vector2.right)) > dot;
    }

    static bool IsDiagonalLocked(Vector2 d, float dot)
    {
        d.Normalize();
        Vector2 d1 = new Vector2(1, 1).normalized;
        Vector2 d2 = new Vector2(1, -1).normalized;
        return Mathf.Abs(Vector2.Dot(d, d1)) > dot
            || Mathf.Abs(Vector2.Dot(d, d2)) > dot;
    }

    static Vector2 RotateDeg(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    // public: 패들에서도 재사용 가능
    public Vector2 EnsureMinVertical(Vector2 d)
    {
        d.Normalize();
        float ay = Mathf.Abs(d.y);
        if (ay >= minVerticalDot) return d;

        float signY = d.y >= 0f ? 1f : -1f;
        float newY = minVerticalDot * signY;
        float newX = Mathf.Sign(d.x == 0 ? 1f : d.x) * Mathf.Sqrt(Mathf.Max(0.0001f, 1f - minVerticalDot * minVerticalDot));
        return new Vector2(newX, newY).normalized;
    }
}
