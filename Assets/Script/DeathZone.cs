using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [Header("옵션")]
    [Tooltip("트리거를 스쳐 지나가도 Y선 아래로 내려간 공을 주기적으로 강제 처리")]
    [SerializeField] bool enableOutOfBoundsCatch = true;
    [Tooltip("추가 마진(Y). killY - margin 아래로 내려가면 강제 처리")]
    [SerializeField] float extraMargin = 0.05f;
    [Tooltip("바운더리 체크 주기(초)")]
    [SerializeField] float checkInterval = 0.2f;

    static float ignoreUntilTime = 0f;
    float killY;         // 이 Y보다 아래면 죽은 것으로 처리
    float checkTimer = 0f;

    /// <summary>외부에서 기준선 조회용</summary>
    public float GetKillY() => killY;

    /// <summary>앞으로 seconds초 동안 데스존 무시(재출발 유예)</summary>
    public static void IgnoreFor(float seconds)
    {
        ignoreUntilTime = Mathf.Max(ignoreUntilTime, Time.time + seconds);
    }

    void Awake()
    {
        // 트리거 콜라이더의 상단 Y를 기준선으로 사용
        var col = GetComponent<Collider2D>();
        killY = col ? col.bounds.max.y : transform.position.y;
    }

    void OnTriggerEnter2D(Collider2D other) => TryKill(other);
    void OnTriggerStay2D(Collider2D other) => TryKill(other); // 빠르게 스쳐도 안전빵

    void Update()
    {
        if (!enableOutOfBoundsCatch) return;
        checkTimer += Time.unscaledDeltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        // 유예 중이면 스킵(유예 끝나고 아래에 머물러 있거나 더 내려가 있으면 잡힘)
        if (Time.time < ignoreUntilTime) return;

        var gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.isTransitioning) return;

        // 트리거를 "놓쳐서" killY 아래로 빠져버린 볼도 잡아낸다
        var balls = GameObject.FindGameObjectsWithTag("Ball");
        int killed = 0;
        foreach (var b in balls)
        {
            if (!b) continue;
            if (b.transform.position.y < (killY - extraMargin))
            {
                Object.Destroy(b);
                killed++;
            }
        }
        if (killed > 0 && gm != null)
        {
            gm.OnBallDeath(); // 한 번만 호출해도 충분
        }
    }

    void TryKill(Collider2D other)
    {
        // 자식 콜라이더여도 붙은 Rigidbody의 루트를 기준으로 판정
        var root = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!root || !root.CompareTag("Ball")) return;

        // 유예시간 동안은 트리거 이벤트를 무시(무적 중)
        if (Time.time < ignoreUntilTime) return;

        var gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.isTransitioning) return;

        Object.Destroy(root);       // 잔존/스텔스 방지: 즉시 제거
        if (gm != null) gm.OnBallDeath();
    }
}
