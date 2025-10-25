using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Ball : MonoBehaviour
{
    [Header("속도(기본)")]
    [Tooltip("출발 시 속도(최초 발사/리스폰 시)")]
    public float startSpeed = 6f;

    [Tooltip("첫 충돌 이후 유지할 최소 속도(런닝 최소속도)")]
    public float minSpeed = 6f;

    [Tooltip("최대 속도(이 값 이상으론 올라가지 않음)")]
    public float maxSpeed = 14f;

    [Header("출발 단계(첫 충돌 전)")]
    [Tooltip("첫 충돌 전 최소 속도(출발 단계에서만 적용). 낮출수록 출발이 더 느려짐")]
    public float launchMinSpeed = 3f;

    [Tooltip("첫 충돌이 발생한 순간 이 값 이상으로 즉시 올림(0이면 비활성)")]
    public float speedAfterFirstHit = 0f; // ★ 권장: 0 (급가속 방지)

    [Header("충돌 가속(선택)")]
    [Tooltip("첫 충돌 이후, 매 충돌마다 추가로 붙일 속도 증분(0이면 가속 없음)")]
    public float hitSpeedGain = 0.0f; // 벽/브릭/패들 공통 증분(원하면 0)

    [Header("가속 규칙(중요)")]
    [Tooltip("특수효과가 없는 한 절대로 느려지지 않음(단, maxSpeed 초과 방지는 함)")]
    public bool monotonicSpeed = true;

    [Tooltip("한 번의 충돌에서 허용되는 최대 속도 증가량(급가속 억제)")]
    public float maxDeltaPerHit = 0.8f; // 0.5~1.2 권장

    [Tooltip("패들/무기와 접촉했을 때만 가속(벽/브릭에서 가속 안함)")]
    public bool increaseOnlyOnPaddle = true;

    [Tooltip("패들/무기를 특정 태그로 구분하려면 설정(비워두면 PaddleController 유무로 판정)")]
    public string paddleTag = ""; // 예: "Paddle"  (비워두면 컴포넌트로 판정)

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

    [Header("데드존 처리")]
    [Tooltip("바닥 트리거에 부여할 태그 이름. 태그가 프로젝트에 없어도 에러 안 남")]
    public string deadZoneTag = "DeadZone";

    [Header("디버그")]
    [Tooltip("충돌 시 콘솔에 속도 로그 출력")]
    public bool debugLogSpeed = false;

    private Rigidbody2D rb;
    private bool hasHitOnce = false;

    // ★ 내부: 지금까지의 ‘최고’ 속도(특수효과 없이는 절대 감소하지 않음)
    private float lastSpeed = 0f;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Start()
    {
        // GameManager가 이미 속도를 줬다면 존중
        if (rb != null && rb.velocity.sqrMagnitude > 0.001f)
        {
            lastSpeed = Mathf.Max(startSpeed, rb.velocity.magnitude);
            return;
        }

        // 자체 발사 벡터
        float sx = Random.value < 0.5f ? -1f : 1f;
        Vector2 dir = new Vector2(sx, 1f).normalized;
        dir = RotateDeg(dir, Random.Range(-10f, 10f));

        float v0 = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        rb.velocity = dir * v0;
        lastSpeed = v0; // 최고 속도 초기화
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // 출발 단계/첫 충돌 이후에 따라 "최소속도"만 보장
        float targetMin = hasHitOnce ? minSpeed : launchMinSpeed;

        // ★ 감속 금지(특수효과 제외): 물리가 속도를 줄여도 lastSpeed까지는 끌어올림
        float cur = rb.velocity.magnitude;
        float floor = monotonicSpeed ? Mathf.Max(targetMin, lastSpeed) : targetMin;

        float final = Mathf.Max(cur, floor);
        final = Mathf.Min(final, maxSpeed); // 절대 최대치는 지킴(넘지 않도록)

        if (rb.velocity.sqrMagnitude > 0.0001f)
            rb.velocity = rb.velocity.normalized * final;
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

        // --- 가속 규칙 계산 ---
        float measured = rb.velocity.magnitude;

        // 충돌 객체가 패들/무기인가?
        bool hitPaddle = false;
        if (!string.IsNullOrEmpty(paddleTag))
        {
            // CompareTag 대신 문자열 비교(태그가 프로젝트에 없어도 경고 안 뜸)
            hitPaddle = (col.collider != null && col.collider.tag == paddleTag);
        }
        if (!hitPaddle && col.collider != null)
        {
            // PaddleController가 부모 어딘가에 있으면 패들/무기로 간주
            hitPaddle = col.collider.GetComponentInParent<PaddleController>() != null;
        }

        // 이번 충돌로 제안되는 속도(소폭 가속 포함)
        float proposed = measured;
        if (!hasHitOnce)
        {
            hasHitOnce = true;
            // ★ 급가속 방지: speedAfterFirstHit는 사용 권장 X (0이면 무시)
            if (speedAfterFirstHit > 0f)
                proposed = Mathf.Max(proposed, speedAfterFirstHit);
        }
        else
        {
            // 매 충돌 소량 가속(옵션)
            if (!increaseOnlyOnPaddle || hitPaddle)
                proposed += Mathf.Max(0f, hitSpeedGain);
        }

        // ★ ‘한 번에’ 너무 많이 뛰지 않도록 캡
        float allowedUp = lastSpeed + Mathf.Max(0.01f, maxDeltaPerHit);

        // ★ 감속 금지: 최소 lastSpeed는 유지 (특수효과 미사용 가정)
        float s = proposed;
        s = Mathf.Min(s, allowedUp);          // 급가속 캡
        s = Mathf.Max(s, lastSpeed);          // 감속 금지
        s = Mathf.Clamp(s, minSpeed, maxSpeed); // 절대 범위

        // 최종 적용
        rb.velocity = dir * s;
        lastSpeed = s;

        if (debugLogSpeed)
            Debug.Log($"[Ball] Hit {col.collider.name}  measured={measured:0.00} → applied={s:0.00} (last={lastSpeed:0.00})");
    }

    // === DeadZone(트리거) 안전 처리 ===
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other) return;

        // CompareTag 대신 문자열 비교(프로젝트에 태그가 없어도 경고 없음)
        if (!string.IsNullOrEmpty(deadZoneTag) && other.gameObject.tag == deadZoneTag)
        {
            if (debugLogSpeed) Debug.Log("[Ball] DeadZone entered.");
            // TODO: 게임 룰에 맞게 처리
            // 1) GM 알림
            var gm = FindObjectOfType<GameManager>();
            if (gm) { /* gm.OnBallDead(); */ }

            // 2) 볼 제거/리셋
            // Destroy(gameObject); // 풀링 시스템이면 비활성/반납
            // gameObject.SetActive(false);
        }
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

    // 볼이 죽거나 리스폰시 사용
    public void ResetLaunchPhase()
    {
        hasHitOnce = false;
        lastSpeed = Mathf.Max(lastSpeed, Mathf.Max(launchMinSpeed, startSpeed));
    }
}
