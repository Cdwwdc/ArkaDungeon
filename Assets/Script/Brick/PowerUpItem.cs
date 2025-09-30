using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PowerUpItem : MonoBehaviour
{
    [Header("����")]
    public string paddleTag = "Paddle"; // �е� '��Ʈ'�� �� �±� �ʿ�

    bool consumed; // �ߺ� ó�� ����

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1.2f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.sharedMaterial = null;
        rb.simulated = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;

        // �� �ڽ� �ݶ��̴����� ��Ʈ(�Ǵ� �پ��ִ� Rigidbody)�� Transform���� ����
        Transform hitRoot = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;

        // 1) �±׷� ���� üũ
        bool isPaddle = hitRoot.CompareTag(paddleTag);

        // 2) ����: �е� ������Ʈ�� �پ��ִ����� ��� (�ڽ� �ݶ��̴� ���)
        if (!isPaddle && hitRoot.GetComponentInParent<PaddleController>() != null)
            isPaddle = true;

        if (!isPaddle) return;

        consumed = true;
        FindObjectOfType<BallManager>()?.PowerUp_MultiBall();
        Destroy(gameObject);
    }
}
