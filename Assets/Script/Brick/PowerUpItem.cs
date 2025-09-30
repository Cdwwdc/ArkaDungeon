using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PowerUpItem : MonoBehaviour
{
    [Header("설정")]
    public string paddleTag = "Paddle"; // 패들 '루트'에 이 태그 필요

    bool consumed; // 중복 처리 방지

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

        // ★ 자식 콜라이더여도 루트(또는 붙어있는 Rigidbody)의 Transform으로 판정
        Transform hitRoot = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;

        // 1) 태그로 먼저 체크
        bool isPaddle = hitRoot.CompareTag(paddleTag);

        // 2) 보강: 패들 컴포넌트가 붙어있는지도 허용 (자식 콜라이더 대비)
        if (!isPaddle && hitRoot.GetComponentInParent<PaddleController>() != null)
            isPaddle = true;

        if (!isPaddle) return;

        consumed = true;
        FindObjectOfType<BallManager>()?.PowerUp_MultiBall();
        Destroy(gameObject);
    }
}
