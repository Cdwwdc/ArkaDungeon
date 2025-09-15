using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PowerUpItem : MonoBehaviour
{
    [Header("설정")]
    public string paddleTag = "Paddle"; // 패들에 이 태그 필요

    void Awake()
    {
        // 프리팹 세팅이 누락돼도 안전하게 보정
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1.2f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.sharedMaterial = null; // 불필요한 바운스 방지(있다면)
        rb.simulated = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(paddleTag)) return;

        FindObjectOfType<BallManager>()?.PowerUp_MultiBall();
        Destroy(gameObject);
    }
}
