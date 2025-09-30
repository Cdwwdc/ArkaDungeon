using UnityEngine;

public class Brick : MonoBehaviour
{
    [Header("브릭 설정")]
    public int hitPoints = 1;            // 체력 (1=한 번에 파괴)
    public int score = 10;               // 점수(선택)
    public bool destroyOnAnyHit = false; // 모든 충돌에 바로 파괴

    [Header("이펙트(옵션)")]
    public ParticleSystem breakFx;       // 파괴 이펙트(없어도 됨)
    public AudioSource breakSfx;         // 사운드(없어도 됨)

    [Header("아이템 드롭")]
    public GameObject powerUpPrefab;     // PowerUpItem 프리팹
    [Range(0f, 1f)] public float dropChance = 0.2f;

    // 내부
    RoomController room;
    bool isBreaking = false;             // 중복 파괴 방지

    // 마지막 히트 연출용 캐시 (유지)
    Vector3 _lastHitPos;
    GameObject _lastBallGO;
    bool _hasLastHitInfo = false;

    // RoomController가 생성 시 호출
    public void Init(RoomController rc)
    {
        room = rc;
        isBreaking = false;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.collider.CompareTag("Ball")) return;

        // 히트 지점/공 캐시(슬로모/줌 연출에 사용)
        _lastHitPos = (col.contactCount > 0) ? (Vector3)col.GetContact(0).point : transform.position;
        _lastBallGO = col.rigidbody ? col.rigidbody.gameObject : null;
        _hasLastHitInfo = true;

        if (destroyOnAnyHit) { Break(); return; }
        Hit(1);
    }

    public void Hit(int dmg)
    {
        if (isBreaking) return;
        hitPoints -= Mathf.Max(1, dmg);

        // HP 변화 시 색 재적용(있을 때만)
        var skin = GetComponent<BrickSkin>();
        if (skin) skin.ApplyColor();

        if (hitPoints <= 0) Break();
    }

    public void Break()
    {
        if (isBreaking) return;
        isBreaking = true;

        // ⬇⬇⬇ 변경: ClearCinematic → CinematicFX (나머지 동일)
        if (IsLastBrickInScene())
        {
            var sr = GetComponentInChildren<SpriteRenderer>(true);
            var focus = _hasLastHitInfo ? _lastHitPos : transform.position;
            CinematicFX.I?.PlayClear(focus, _lastBallGO, sr); // ← 여기만 교체
        }
        // ⬆⬆⬆ 변경 끝

        // 이펙트/사운드
        if (breakFx)
        {
            var fx = Instantiate(breakFx, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.5f);
        }
        if (breakSfx) breakSfx.Play();

        // 드롭
        if (powerUpPrefab && Random.value < dropChance)
        {
            Instantiate(powerUpPrefab, transform.position, Quaternion.identity);
        }

        // 파괴 및 알림
        Destroy(gameObject);
        if (room != null) room.NotifyBrickDestroyed();
        else Debug.LogWarning("[Brick] room 참조가 없음(Init 누락?)");
    }

    // RoomController 수정 없이 '마지막 벽돌' 간단 판정
    bool IsLastBrickInScene()
    {
        var bricks = FindObjectsOfType<Brick>(false); // 비활성 제외
        return bricks != null && bricks.Length <= 1;
    }
}
