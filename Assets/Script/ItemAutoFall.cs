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
        // �θ�(�긯) �ı� ���� ����: ����� �и�
        transform.SetParent(null, true);
    }
}
