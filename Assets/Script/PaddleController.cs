using UnityEngine;

// ▣ 플레이어 막대(패들) 좌우 이동 + 패들과 공 충돌 각도 보정(강화)
public class PaddleController : MonoBehaviour
{
    [Header("이동 범위(X 최소/최대)")]
    public float minX = -5f;
    public float maxX = 5f;

    [Header("이동 속도(좌/우키 또는 마우스)")]
    public float moveSpeed = 12f;

    [Header("패들 상면 반사 각 조절 (최대 각도)")]
    public float maxBounceAngle = 60f; // 도 단위 (중앙은 0°, 끝으로 갈수록 ±max)

    [Header("반사 튜닝")]
    [Tooltip("패들의 x속도가 반사각에 미치는 영향(가산)")]
    public float paddleVelInfluence = 0.20f;
    [Tooltip("|dir.y| 최소값. 작게 잡을수록 수평에 가까운 튕김 허용")]
    public float minVerticalDot = 0.40f; // 0.35~0.45 권장
    [Tooltip("반사 후 속도 하한/상한")]
    public float minSpeed = 6f, maxSpeed = 14f;

    // 내부 상태: 패들 x속도 추정(Transform 이동이므로 직접 계산)
    float lastX;
    float xVel;

    void Start()
    {
        lastX = transform.position.x;
    }

    void Update()
    {
        // ① 키보드(좌우) 입력
        float h = Input.GetAxisRaw("Horizontal");

        // ② 마우스 드래그로도 이동 (편의)
        if (Input.GetMouseButton(0))
        {
            Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float targetX = Mathf.Clamp(m.x, minX, maxX);
            float nextX = Mathf.MoveTowards(transform.position.x, targetX, moveSpeed * Time.deltaTime);
            // x속도 계산(Transform 이동이므로 우리가 직접 추정)
            xVel = (nextX - transform.position.x) / Mathf.Max(Time.deltaTime, 0.0001f);
            transform.position = new Vector3(nextX, transform.position.y, 0f);
        }
        else
        {
            // ③ 키보드 이동
            if (Mathf.Abs(h) > 0.01f)
            {
                float nextX = Mathf.Clamp(transform.position.x + h * moveSpeed * Time.deltaTime, minX, maxX);
                xVel = (nextX - transform.position.x) / Mathf.Max(Time.deltaTime, 0.0001f);
                transform.position = new Vector3(nextX, transform.position.y, 0f);
            }
            else
            {
                // 입력 없으면 감쇠
                xVel = Mathf.MoveTowards(xVel, 0f, 50f * Time.deltaTime);
            }
        }

        lastX = transform.position.x;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // 공과 부딪혔을 때 반사각을 "맞은 지점"에 따라 조정 + 패들 속도 영향 + 최소 상승각 보장
        if (!col.collider.CompareTag("Ball")) return;

        Rigidbody2D rb = col.rigidbody; // 공의 리지드바디
        if (rb == null) return;

        // 패들 중앙 대비 충돌 지점의 상대 x 오프셋(-1 ~ +1)
        var myCol = GetComponent<Collider2D>();
        if (myCol == null || col.contactCount == 0) return;

        float centerX = myCol.bounds.center.x;
        float half = Mathf.Max(myCol.bounds.extents.x, 0.0001f);
        float hitX = col.GetContact(0).point.x;
        float t = Mathf.Clamp((hitX - centerX) / half, -1f, 1f);

        // 각도 계산: 중앙 0°, 좌우 끝 ±maxBounceAngle
        float angleRad = t * maxBounceAngle * Mathf.Deg2Rad;

        // 기본 방향(상향 기준): x=sin, y=cos
        Vector2 dir = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad));

        // 패들의 x속도 영향(살짝 가산)
        dir.x += xVel * paddleVelInfluence * 0.01f; // 속도가 큰 편일 수 있어서 0.01 스케일
        dir = dir.normalized;

        // 최소 상승각 보장 (Ball.cs의 EnsureMinVertical 사용 가능 시 우선 사용)
        var ball = col.collider.GetComponent<Ball>();
        if (ball != null)
        {
            dir = ball.EnsureMinVertical(dir);
        }
        else
        {
            // Ball.cs에 함수가 없다면 로컬 보장 로직 사용
            dir = EnsureMinVerticalLocal(dir, minVerticalDot);
        }

        // 속도 유지하며 방향만 변경 (안전 클램프)
        float speed = rb.velocity.magnitude;
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        rb.velocity = dir * speed;
    }

    // Ball.cs가 없을 때를 대비한 로컬 보장 로직 (같은 동작)
    Vector2 EnsureMinVerticalLocal(Vector2 d, float minDot)
    {
        d.Normalize();
        if (Mathf.Abs(d.y) >= minDot) return d;
        float signY = d.y >= 0f ? 1f : -1f;
        float newY = minDot * signY;
        float newX = Mathf.Sign(d.x == 0 ? 1f : d.x) * Mathf.Sqrt(Mathf.Max(0.0001f, 1f - minDot * minDot));
        return new Vector2(newX, newY).normalized;
    }
}
