using UnityEngine;

// ▣ 플레이어 막대(패들) 좌우 이동 + 반사 각도 보정(강화) + 벽 기준 클램프
public class PaddleController : MonoBehaviour
{
    [Header("이동 범위(X 최소/최대) (벽 Transform이 없을 때만 사용)")]
    public float minX = -5f;
    public float maxX = 5f;

    [Header("좌/우 벽(옵션): 있으면 이 사이로만 이동")]
    public Transform leftWall;
    public Transform rightWall;

    [Header("이동 속도(좌/우키 또는 마우스)")]
    public float moveSpeed = 12f;

    [Header("패들 상면 반사 각 조절 (최대 각도)")]
    public float maxBounceAngle = 60f; // 도 단위

    [Header("반사 튜닝")]
    public float paddleVelInfluence = 0.20f;
    public float minVerticalDot = 0.40f; // 0.35~0.45 권장
    public float minSpeed = 6f, maxSpeed = 14f;

    float xVel;

    void Update()
    {
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

        // 클램프 기준 계산(벽 Transform 우선)
        float half = GetComponent<Collider2D>() ? GetComponent<Collider2D>().bounds.extents.x : 0.5f;
        float leftLimit = leftWall ? leftWall.position.x + half : minX;
        float rightLimit = rightWall ? rightWall.position.x - half : maxX;

        float nextX = Mathf.Clamp(targetX, leftLimit, rightLimit);
        xVel = (nextX - transform.position.x) / Mathf.Max(Time.deltaTime, 0.0001f);
        transform.position = new Vector3(nextX, transform.position.y, 0f);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.collider.CompareTag("Ball")) return;

        Rigidbody2D rb = col.rigidbody;
        if (rb == null) return;

        var myCol = GetComponent<Collider2D>();
        if (myCol == null || col.contactCount == 0) return;

        float centerX = myCol.bounds.center.x;
        float half = Mathf.Max(myCol.bounds.extents.x, 0.0001f);
        float hitX = col.GetContact(0).point.x;
        float t = Mathf.Clamp((hitX - centerX) / half, -1f, 1f);

        float angleRad = t * maxBounceAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad));

        // 패들 x속도 영향
        dir.x += xVel * paddleVelInfluence * 0.01f;
        dir = dir.normalized;

        // 최소 상승각 보장(있으면 Ball의 함수 사용)
        var ball = col.collider.GetComponent<Ball>();
        if (ball != null) dir = ball.EnsureMinVertical(dir);
        else dir = EnsureMinVerticalLocal(dir, minVerticalDot);

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
}
