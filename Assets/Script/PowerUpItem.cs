using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PowerUpItem : MonoBehaviour
{
    [Header("����")]
    public string paddleTag = "Paddle"; // �е鿡 �� �±� �ʿ�

    void Awake()
    {
        // ������ ������ �����ŵ� �����ϰ� ����
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1.2f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.sharedMaterial = null; // ���ʿ��� �ٿ ����(�ִٸ�)
        rb.simulated = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(paddleTag)) return;

        FindObjectOfType<BallManager>()?.PowerUp_MultiBall();
        Destroy(gameObject);
    }
}
