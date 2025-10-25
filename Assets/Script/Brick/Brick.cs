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

    // ===================== ▼ 추가: 시각 연출 세팅 ▼ =====================
    [Header("깨짐 시각 연출(자식 토글 방식)")]
    [Tooltip("평소 보이는 오브젝트(일반 블록)")]
    public GameObject intactGO;          // 일반 블록 자식
    [Tooltip("깨진 상태 오브젝트(반쯤 부서진 블록)")]
    public GameObject brokenGO;          // 깨진 블록 자식

    [Tooltip("두 단계 히트 모드: 첫 타격에 깨진 상태로 전환, 다음 타격에 파괴")]
    public bool twoStageHit = false;

    [Tooltip("한 방에 파괴될 때, 제거 전에 깨진 모습을 잠깐 보여줌")]
    public bool flashBrokenOnBreak = true;

    [Tooltip("깨진 모습 노출 시간(초) - 플래시 또는 최종 파괴 직전")]
    public float brokenFlashDuration = 0.20f;

    [Header("콜라이더 전환(선택)")]
    [Tooltip("깨짐 상태에서 콜라이더가 다르면 체크")]
    public bool useColliderSwap = false;
    public Collider2D intactCollider;
    public Collider2D brokenCollider;
    // ===================== ▲ 추가: 시각 연출 세팅 ▲ =====================

    // 내부
    RoomController room;
    bool isBreaking = false;             // 중복 파괴 방지

    // 마지막 히트 연출용 캐시 (유지)
    Vector3 _lastHitPos;
    GameObject _lastBallGO;
    bool _hasLastHitInfo = false;

    // ─────────────────────────────────────────────────────────────────────
    // 초기화/세팅
    void Reset()
    {
        // 컴포넌트 붙였을 때 자식 자동 바인딩 시도
        if (!intactGO && transform.childCount > 0) intactGO = transform.GetChild(0).gameObject;
        if (!brokenGO && transform.childCount > 1) brokenGO = transform.GetChild(1).gameObject;

        if (!intactCollider) intactCollider = GetComponent<Collider2D>();
    }

    void Awake()
    {
        // 시작 상태: 일반 ON, 깨진 OFF
        SetActiveSafe(intactGO, true);
        SetActiveSafe(brokenGO, false);

        if (useColliderSwap)
        {
            if (intactCollider) intactCollider.enabled = true;
            if (brokenCollider) brokenCollider.enabled = false;
        }
    }
    // ─────────────────────────────────────────────────────────────────────

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

        // 두 단계 히트 모드: 아직 안 깨졌다면 '깨진 상태'를 보여주고 대기
        if (twoStageHit && hitPoints > 0)
        {
            ShowBrokenOnly(); // 일반→깨진
            return;
        }

        if (hitPoints <= 0) Break();
    }

    public void Break()
    {
        if (isBreaking) return;
        isBreaking = true;

        // 더 이상 충돌하지 않도록 콜라이더 정지
        if (intactCollider) intactCollider.enabled = false;
        if (brokenCollider) brokenCollider.enabled = false;

        // 마지막 벽돌 연출(기존 유지)
        if (IsLastBrickInScene())
        {
            var sr = GetComponentInChildren<SpriteRenderer>(true);
            var focus = _hasLastHitInfo ? _lastHitPos : transform.position;
            CinematicFX.I?.PlayClear(focus, _lastBallGO, sr);
        }

        // 시각 전환: 파괴 직전 '깨진 상태' 노출
        if (flashBrokenOnBreak)
            ShowBrokenOnly();

        // 이펙트/사운드
        if (breakFx)
        {
            var fx = Instantiate(breakFx, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.5f);
        }
        if (breakSfx) breakSfx.Play();

        // 아이템 드롭
        if (powerUpPrefab && Random.value < dropChance)
        {
            Instantiate(powerUpPrefab, transform.position, Quaternion.identity);
        }

        // 깜빡 노출 후 제거 / 또는 즉시 제거
        if (flashBrokenOnBreak && brokenFlashDuration > 0f)
        {
            // 코루틴으로 잠깐 보여준 뒤 삭제
            StartCoroutine(Co_BrokenFlashThenDestroy(brokenFlashDuration));
        }
        else
        {
            // 즉시 제거
            Destroy(gameObject);
            if (room != null) room.NotifyBrickDestroyed();
            else Debug.LogWarning("[Brick] room 참조가 없음(Init 누락?)");
        }
    }

    System.Collections.IEnumerator Co_BrokenFlashThenDestroy(float t)
    {
        // 콜라이더 전환 모드라면 깨진 콜라이더만 잠깐 켜둘 수도 있음(원하면 아래 한 줄 주석 해제)
        if (useColliderSwap && brokenCollider)
        {
            brokenCollider.enabled = true; // 깨진 상태에서 아주 잠깐 충돌 유지하고 싶다면
        }

        yield return new WaitForSeconds(Mathf.Max(0f, t));

        Destroy(gameObject);
        if (room != null) room.NotifyBrickDestroyed();
        else Debug.LogWarning("[Brick] room 참조가 없음(Init 누락?)");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 유틸

    // 일반 → 깨진으로 전환(시각/콜라이더)
    void ShowBrokenOnly()
    {
        SetActiveSafe(intactGO, false);
        SetActiveSafe(brokenGO, true);

        if (useColliderSwap)
        {
            if (intactCollider) intactCollider.enabled = false;
            if (brokenCollider) brokenCollider.enabled = true;
        }
    }

    static void SetActiveSafe(GameObject go, bool on)
    {
        if (go && go.activeSelf != on) go.SetActive(on);
    }

    // RoomController 수정 없이 '마지막 벽돌' 간단 판정 (기존 유지)
    bool IsLastBrickInScene()
    {
        var bricks = FindObjectsOfType<Brick>(false); // 비활성 제외
        return bricks != null && bricks.Length <= 1;
    }
}
