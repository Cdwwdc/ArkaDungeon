using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PowerUpAddBall : MonoBehaviour
{
    [Tooltip("패들을 식별할 태그")]
    public string paddleTag = "Paddle";

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(paddleTag)) return;

        var bm = FindObjectOfType<BallManager>();
        if (bm) bm.AddOneFromNearestTo(other.transform.position);

        // 아이템 소비
        Destroy(gameObject);
    }
}
