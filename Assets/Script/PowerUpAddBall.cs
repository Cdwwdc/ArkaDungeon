using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PowerUpAddBall : MonoBehaviour
{
    [Tooltip("�е��� �ĺ��� �±�")]
    public string paddleTag = "Paddle";

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(paddleTag)) return;

        var bm = FindObjectOfType<BallManager>();
        if (bm) bm.AddOneFromNearestTo(other.transform.position);

        // ������ �Һ�
        Destroy(gameObject);
    }
}
