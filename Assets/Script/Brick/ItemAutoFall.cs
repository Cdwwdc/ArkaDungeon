using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ItemAutoFall : MonoBehaviour
{
    [Header("�⺻ ����")]
    [SerializeField] float minGravity = 1f;
    [SerializeField] bool resetKinematic = true;

    [Header("�е� ���� �帮��Ʈ(����)")]
    [Tooltip("�Ѹ� ���� ���� �е� ������ ���� �ӵ��� ��¦ �ݴϴ�.")]
    [SerializeField] bool driftToPaddle = false;   // �⺻ OFF �� ������ ����
    [SerializeField] float driftSpeedX = 2f;
    [Tooltip("������ X�ӵ��� ���� ���� ���� ����ϴ�.")]
    [SerializeField] bool onlyIfNoXVelocity = true;
    [SerializeField] float minXVelThreshold = 0.1f;

    [Header("�߷� ����(������Ʈ �߷�=0�� ��)")]
    [Tooltip("Physics2D.gravity�� ��ǻ� 0�̸� ������ �Ʒ��� �귯������ �մϴ�.")]
    [SerializeField] bool forceFallIfNoGravity = true;
    [SerializeField] float fallbackFallSpeed = 2.0f;  // �ʴ� ���� �ӵ�(�ּ� ����)

    Rigidbody2D rb;

    void OnEnable()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.simulated = true;
            if (resetKinematic) rb.isKinematic = false;
            if (rb.gravityScale < minGravity) rb.gravityScale = minGravity;

            // (����) �е� �� ���� �帮��Ʈ �� ���� �ο�
            if (driftToPaddle && (!onlyIfNoXVelocity || Mathf.Abs(rb.velocity.x) < minXVelThreshold))
            {
                Transform paddle = null;
                var gm = FindObjectOfType<GameManager>();
                if (gm) paddle = gm.paddle;

                if (paddle)
                {
                    float dx = paddle.position.x - transform.position.x;
                    float dir = Mathf.Abs(dx) < 0.01f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(dx);
                    rb.velocity = new Vector2(dir * driftSpeedX, rb.velocity.y);
                }
            }
        }

        // �θ�(�긯) �ı� ���� ����: ����� �и�
        transform.SetParent(null, true);
    }

    void FixedUpdate()
    {
        if (!rb || !forceFallIfNoGravity) return;

        // ������Ʈ �߷��� ��ǻ� 0�̸�(�Ǵ� �ſ� ���ϸ�) �ּ� ���ϼӵ��� ����
        if (Physics2D.gravity.sqrMagnitude < 0.0001f)
        {
            if (rb.velocity.y > -fallbackFallSpeed)
                rb.velocity = new Vector2(rb.velocity.x, -fallbackFallSpeed);
        }
    }
}
