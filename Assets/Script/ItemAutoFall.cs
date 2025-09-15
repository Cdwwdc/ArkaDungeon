using UnityEngine;

public class ItemAutoFall : MonoBehaviour
{
    [SerializeField] float minGravity = 1f;

    void OnEnable()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.simulated = true;
            rb.isKinematic = false;
            if (rb.gravityScale <= 0f) rb.gravityScale = minGravity;
        }
        // 부모(브릭) 파괴 영향 방지: 월드로 분리
        transform.SetParent(null, true);
    }
}
