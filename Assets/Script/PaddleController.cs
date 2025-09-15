using UnityEngine;

// �� �÷��̾� ����(�е�) �¿� �̵� + �ݻ� ���� ����(��ȭ) + �� ���� Ŭ����
public class PaddleController : MonoBehaviour
{
    [Header("�̵� ����(X �ּ�/�ִ�) (�� Transform�� ���� ���� ���)")]
    public float minX = -5f;
    public float maxX = 5f;

    [Header("��/�� ��(�ɼ�): ������ �� ���̷θ� �̵�")]
    public Transform leftWall;
    public Transform rightWall;

    [Header("�̵� �ӵ�(��/��Ű �Ǵ� ���콺)")]
    public float moveSpeed = 12f;

    [Header("�е� ��� �ݻ� �� ���� (�ִ� ����)")]
    public float maxBounceAngle = 60f; // �� ����

    [Header("�ݻ� Ʃ��")]
    public float paddleVelInfluence = 0.20f;
    public float minVerticalDot = 0.40f; // 0.35~0.45 ����
    public float minSpeed = 6f, maxSpeed = 14f;

    float xVel;

    void Update()
    {
        // ��ǥ X ���
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

        // Ŭ���� ���� ���(�� Transform �켱)
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

        // �е� x�ӵ� ����
        dir.x += xVel * paddleVelInfluence * 0.01f;
        dir = dir.normalized;

        // �ּ� ��°� ����(������ Ball�� �Լ� ���)
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
