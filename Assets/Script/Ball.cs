using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Ball : MonoBehaviour
{
    [Header("속도(기본)")]
    [Tooltip("출발 시 속도(최초 발사/리스폰 시)")]
    public float startSpeed = 6f;
    [Tooltip("첫 충돌 이후 유지할 최소 속도(런닝 최소속도)")]
    public float minSpeed = 6f;
    [Tooltip("최대 속도")]
    public float maxSpeed = 14f;

    [Header("출발 단계(첫 충돌 전)")]
    [Tooltip("첫 충돌 전 최소 속도(출발 단계에서만 적용). 낮출수록 출발이 더 느려짐")]
    public float launchMinSpeed = 3f;
    [Tooltip("첫 충돌이 발생한 순간 이 값 이상으로 즉시 올림(0이면 비활성)")]
    public float speedAfterFirstHit = 8f;

    [Header("충돌 가속(선택)")]
    [Tooltip("첫 충돌 이후, 매 충돌마다 추가로 붙일 속도 증분(0이면 가속 없음)")]
    public float hitSpeedGain = 0f;

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
    private bool hasHitOnce = false;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Start()
    {
        // GameManager가 이미 속도를 줬다면 존중
        if (rb != null && rb.velocity.sqrMagnitude > 0.001f) return;

        // 자체 발사 벡터
        float sx = Random.value < 0.5f ? -1f : 1f;
        Vector2 dir = new Vector2(sx, 1f).normalized;
        dir = RotateDeg(dir, Random.Range(-10f, 10f));
        rb.velocity = dir * Mathf.Clamp(startSpeed, 0f, maxSpeed);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // 출발 단계/첫 충돌 이후에 따라 "최소속도"를 다르게 적용
        float targetMin = hasHitOnce ? minSpeed : launchMinSpeed;
        float spd = Mathf.Clamp(rb.velocity.magnitude, targetMin, maxSpeed);
        rb.velocity = rb.velocity.sqrMagnitude > 0.0001f ? rb.velocity.normalized * spd : rb.velocity;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (rb == null) return;

        // 각도 보정
        Vector2 dir = rb.velocity.sqrMagnitude > 0.0001f ? rb.velocity.normalized : Vector2.up;

        if (IsAxisLocked(dir, axisLockDot) || IsDiagonalLocked(dir, diagLockDot))
            dir = RotateDeg(dir, Random.Range(-nudgeDegrees, nudgeDegrees));

        // 수평 루프 방지
        dir = EnsureMinVertical(dir);

        // 속도 가변: 첫 충돌 시 점프업, 이후엔 매 충돌마다 소폭 가속(옵션)
        float s = rb.velocity.magnitude;
        if (!hasHitOnce)
        {
            hasHitOnce = true;
            if (speedAfterFirstHit > 0f) s = Mathf.Max(s, speedAfterFirstHit);
        }
        else if (hitSpeedGain > 0f)
        {
            s += hitSpeedGain;
        }

        s = Mathf.Clamp(s, minSpeed, maxSpeed);
        rb.velocity = dir * s;
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

    public void ResetLaunchPhase()
    {
        hasHitOnce = false;  // 출발 단계로 되돌림 → launchMinSpeed 규칙 적용
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
