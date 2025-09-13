using UnityEngine;

// �� �÷��̾� ����(�е�) �¿� �̵� + �е�� �� �浹 ���� ����(��ȭ)
public class PaddleController : MonoBehaviour
{
    [Header("�̵� ����(X �ּ�/�ִ�)")]
    public float minX = -5f;
    public float maxX = 5f;

    [Header("�̵� �ӵ�(��/��Ű �Ǵ� ���콺)")]
    public float moveSpeed = 12f;

    [Header("�е� ��� �ݻ� �� ���� (�ִ� ����)")]
    public float maxBounceAngle = 60f; // �� ���� (�߾��� 0��, ������ ������ ��max)

    [Header("�ݻ� Ʃ��")]
    [Tooltip("�е��� x�ӵ��� �ݻ簢�� ��ġ�� ����(����)")]
    public float paddleVelInfluence = 0.20f;
    [Tooltip("|dir.y| �ּҰ�. �۰� �������� ���� ����� ƨ�� ���")]
    public float minVerticalDot = 0.40f; // 0.35~0.45 ����
    [Tooltip("�ݻ� �� �ӵ� ����/����")]
    public float minSpeed = 6f, maxSpeed = 14f;

    // ���� ����: �е� x�ӵ� ����(Transform �̵��̹Ƿ� ���� ���)
    float lastX;
    float xVel;

    void Start()
    {
        lastX = transform.position.x;
    }

    void Update()
    {
        // �� Ű����(�¿�) �Է�
        float h = Input.GetAxisRaw("Horizontal");

        // �� ���콺 �巡�׷ε� �̵� (����)
        if (Input.GetMouseButton(0))
        {
            Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float targetX = Mathf.Clamp(m.x, minX, maxX);
            float nextX = Mathf.MoveTowards(transform.position.x, targetX, moveSpeed * Time.deltaTime);
            // x�ӵ� ���(Transform �̵��̹Ƿ� �츮�� ���� ����)
            xVel = (nextX - transform.position.x) / Mathf.Max(Time.deltaTime, 0.0001f);
            transform.position = new Vector3(nextX, transform.position.y, 0f);
        }
        else
        {
            // �� Ű���� �̵�
            if (Mathf.Abs(h) > 0.01f)
            {
                float nextX = Mathf.Clamp(transform.position.x + h * moveSpeed * Time.deltaTime, minX, maxX);
                xVel = (nextX - transform.position.x) / Mathf.Max(Time.deltaTime, 0.0001f);
                transform.position = new Vector3(nextX, transform.position.y, 0f);
            }
            else
            {
                // �Է� ������ ����
                xVel = Mathf.MoveTowards(xVel, 0f, 50f * Time.deltaTime);
            }
        }

        lastX = transform.position.x;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // ���� �ε����� �� �ݻ簢�� "���� ����"�� ���� ���� + �е� �ӵ� ���� + �ּ� ��°� ����
        if (!col.collider.CompareTag("Ball")) return;

        Rigidbody2D rb = col.rigidbody; // ���� ������ٵ�
        if (rb == null) return;

        // �е� �߾� ��� �浹 ������ ��� x ������(-1 ~ +1)
        var myCol = GetComponent<Collider2D>();
        if (myCol == null || col.contactCount == 0) return;

        float centerX = myCol.bounds.center.x;
        float half = Mathf.Max(myCol.bounds.extents.x, 0.0001f);
        float hitX = col.GetContact(0).point.x;
        float t = Mathf.Clamp((hitX - centerX) / half, -1f, 1f);

        // ���� ���: �߾� 0��, �¿� �� ��maxBounceAngle
        float angleRad = t * maxBounceAngle * Mathf.Deg2Rad;

        // �⺻ ����(���� ����): x=sin, y=cos
        Vector2 dir = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad));

        // �е��� x�ӵ� ����(��¦ ����)
        dir.x += xVel * paddleVelInfluence * 0.01f; // �ӵ��� ū ���� �� �־ 0.01 ������
        dir = dir.normalized;

        // �ּ� ��°� ���� (Ball.cs�� EnsureMinVertical ��� ���� �� �켱 ���)
        var ball = col.collider.GetComponent<Ball>();
        if (ball != null)
        {
            dir = ball.EnsureMinVertical(dir);
        }
        else
        {
            // Ball.cs�� �Լ��� ���ٸ� ���� ���� ���� ���
            dir = EnsureMinVerticalLocal(dir, minVerticalDot);
        }

        // �ӵ� �����ϸ� ���⸸ ���� (���� Ŭ����)
        float speed = rb.velocity.magnitude;
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        rb.velocity = dir * speed;
    }

    // Ball.cs�� ���� ���� ����� ���� ���� ���� (���� ����)
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
