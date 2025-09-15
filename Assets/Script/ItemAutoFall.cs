using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ItemAutoFall : MonoBehaviour
{
    [Header("기본 낙하")]
    [SerializeField] float minGravity = 1f;
    [SerializeField] bool resetKinematic = true;

    [Header("패들 방향 드리프트(선택)")]
    [Tooltip("켜면 스폰 순간 패들 쪽으로 가로 속도를 살짝 줍니다.")]
    [SerializeField] bool driftToPaddle = false;   // 기본 OFF → 예전과 동일
    [SerializeField] float driftSpeedX = 2f;
    [Tooltip("기존에 X속도가 거의 없을 때만 덮어씁니다.")]
    [SerializeField] bool onlyIfNoXVelocity = true;
    [SerializeField] float minXVelThreshold = 0.1f;

    [Header("중력 폴백(프로젝트 중력=0일 때)")]
    [Tooltip("Physics2D.gravity가 사실상 0이면 강제로 아래로 흘러내리게 합니다.")]
    [SerializeField] bool forceFallIfNoGravity = true;
    [SerializeField] float fallbackFallSpeed = 2.0f;  // 초당 낙하 속도(최소 보장)

    Rigidbody2D rb;

    void OnEnable()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.simulated = true;
            if (resetKinematic) rb.isKinematic = false;
            if (rb.gravityScale < minGravity) rb.gravityScale = minGravity;

            // (선택) 패들 쪽 가로 드리프트 한 번만 부여
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

        // 부모(브릭) 파괴 영향 방지: 월드로 분리
        transform.SetParent(null, true);
    }

    void FixedUpdate()
    {
        if (!rb || !forceFallIfNoGravity) return;

        // 프로젝트 중력이 사실상 0이면(또는 매우 약하면) 최소 낙하속도를 강제
        if (Physics2D.gravity.sqrMagnitude < 0.0001f)
        {
            if (rb.velocity.y > -fallbackFallSpeed)
                rb.velocity = new Vector2(rb.velocity.x, -fallbackFallSpeed);
        }
    }
}
