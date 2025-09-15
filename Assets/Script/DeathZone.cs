using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [Header("옵션")]
    [SerializeField] bool enableOutOfBoundsCatch = true;
    [SerializeField] float extraMargin = 0.05f;
    [SerializeField] float checkInterval = 0.2f;

    static float ignoreUntilTime = 0f;
    float killY;
    float checkTimer = 0f;

    public float GetKillY() => killY;

    public static void IgnoreFor(float seconds)
    {
        ignoreUntilTime = Mathf.Max(ignoreUntilTime, Time.time + seconds);
    }

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        killY = col ? col.bounds.max.y : transform.position.y;
    }

    void OnTriggerEnter2D(Collider2D other) => TryKill(other);
    void OnTriggerStay2D(Collider2D other) => TryKill(other);

    void Update()
    {
        if (!enableOutOfBoundsCatch) return;
        checkTimer += Time.unscaledDeltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        if (Time.time < ignoreUntilTime) return;

        var gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.isTransitioning) return;

        var balls = GameObject.FindGameObjectsWithTag("Ball");
        foreach (var b in balls)
        {
            if (!b) continue;
            if (b.transform.position.y < (killY - extraMargin))
                Object.Destroy(b); // 파괴만, 컨티뉴 판단은 BallManager가 함
        }
    }

    void TryKill(Collider2D other)
    {
        var root = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!root || !root.CompareTag("Ball")) return;

        if (Time.time < ignoreUntilTime) return;
        var gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.isTransitioning) return;

        Object.Destroy(root); // 파괴만
        // 컨티뉴/라스트볼 체크는 BallManager.LateUpdate에서 수행
    }
}
