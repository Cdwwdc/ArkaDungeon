using UnityEngine;

/// <summary>
/// 브릭이 '게임플레이로' 파괴될 때 출구(ExitGate) 스폰을 요청하는 훅.
/// - RoomController가 Build/스캔 시 Init(this)로 소유자 주입
/// - RoomController가 특정 브릭을 시드로 지정하면 MarkAsExitSeed() 호출
/// - OnDestroy에서 정상 타이밍일 때만 출구 스폰(씬 언로드/리빌드 중 차단)
/// - 시드가 아니면 이 컴포넌트의 로컬 확률(localChancePercent)로 스폰 판단
/// </summary>
public class BrickExitHook : MonoBehaviour
{
    RoomController _rc;
    bool _isSeed = false;

    [Header("Exit Gate 확률(시드가 아닐 때)")]
    [Range(0f, 100f)] public float localChancePercent = 0f;

    static bool s_AppQuitting = false;
    void OnApplicationQuit() { s_AppQuitting = true; }

    // ▶ 디버그용 Getter
    public bool IsSeed => _isSeed;

    public void Init(RoomController owner) { _rc = owner; }
    public void Init(RoomController owner, float _cellW, float _halfH) { _rc = owner; }

    public void MarkAsExitSeed() { _isSeed = true; }

    void OnDestroy()
    {
        // 편집기 정지/리컴파일/씬 전환 등 비플레이 상황 → 스폰 금지
        if (!Application.isPlaying) return;
        if (s_AppQuitting) return;

        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;

        // RoomController 확보(Init이 안 왔을 경우 대비)
        if (!_rc) _rc = FindObjectOfType<RoomController>();
        if (!_rc || !_rc.isActiveAndEnabled) return;

        // 이미 출구가 존재하면 또 만들지 않음(중복 스폰 방지)
        if (FindObjectOfType<ExitGate>()) return;

        // === 스폰 여부 판단 ===
        bool shouldSpawn = false;

        // 1) 시드 브릭이면 무조건 스폰
        if (_isSeed) shouldSpawn = true;

        // 2) 시드가 아니면 로컬 확률
        if (!shouldSpawn && localChancePercent > 0f)
        {
            float p = Mathf.Clamp(localChancePercent, 0f, 100f) / 100f;
            if (Random.value < p) shouldSpawn = true;
        }
        if (!shouldSpawn) return;

        // === 실제 스폰 ===
        var eg = _rc.SpawnExitGateAt(transform.position);

        // 안전망: 위치 저장
        if (eg)
        {
            DungeonRunState.RememberExitGatePos(
                new DungeonRunState.RoomKey(_rc.roomKeyX, _rc.roomKeyY),
                transform.position
            );
        }
    }
}
