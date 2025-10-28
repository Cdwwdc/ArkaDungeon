using System; // Func<T> 사용을 위해 추가
using System.Collections.Generic;
using System.Runtime.CompilerServices; // [CallerMemberName]을 위해 추가
using UnityEngine;

// 이 클래스는 게임의 던전 실행 상태를 관리하는 정적(Static) 클래스입니다.
public static class DungeonRunState
{
    // --- 열거형 및 구조체 정의 ---
    public enum ExitScheduleMode { ForceNthUnique, RandomBetween }

    public struct RoomKey
    {
        public int x, y;
        public RoomKey(int X, int Y) { x = X; y = Y; }

        // Equals, GetHashCode, ToString 오버라이드는 딕셔너리 키로 사용하기 위해 필수적입니다.
        public override bool Equals(object o) => o is RoomKey k && k.x == x && k.y == y;
        public override int GetHashCode() => (x * 73856093) ^ (y * 19349663);
        public override string ToString() => $"({x},{y})";
    }

    // 각 방의 상태를 저장하는 스냅샷 클래스
    public class Snapshot
    {
        public int seed;
        public int shellIndex;
        public bool cleared;
        public bool hasExitGatePos;
        public Vector3 exitGatePos;
    }

    // --- 정적 필드 정의 ---

    // CS0117 오류 해결 및 시스템 초기화 상태 확인을 위한 속성 추가
    public static bool IsInitialized { get; private set; } = false;

    static bool _configured = false;
    static ExitScheduleMode _mode = ExitScheduleMode.ForceNthUnique;
    static int _forceNth = 0;
    static int _randMin = 0, _randMax = 0;
    static int _targetNth = 0; // 던전 탈출 방으로 확정될 유니크 방문 카운트 (Nth)
    static bool _exitChosen = false;

    static RoomKey _exitRoomKey; // 탈출 방으로 지정된 RoomKey

    static int _visitedCount = 0; // 유니크하게 방문한 *방*의 개수 (통로 제외)
    static readonly Dictionary<RoomKey, Snapshot> _snapshots = new Dictionary<RoomKey, Snapshot>();
    static readonly HashSet<RoomKey> _visitedRooms = new HashSet<RoomKey>(); // 모든 고유 키(방 + 통로)를 추적

    // --- 초기화 및 리셋 메서드 ---

    // 시스템 초기화 메서드 (IsInitialized = true로 설정)
    public static void Initialize()
    {
        if (IsInitialized)
        {
            Debug.LogWarning("DungeonRunState is already initialized.");
            return;
        }

        // 이곳에 던전 상태 초기화에 필요한 로직을 추가합니다.

        IsInitialized = true;
        Debug.Log("DungeonRunState Initialized successfully.");
    }

    // 게임 시작 시 모든 상태를 초기화합니다.
    public static void Reset()
    {
        // 초기화 상태 재설정
        IsInitialized = false;

        _configured = false;
        _mode = ExitScheduleMode.ForceNthUnique;
        _forceNth = 0; _randMin = 0; _randMax = 0;
        _targetNth = 0; _exitChosen = false; _visitedCount = 0;
        _snapshots.Clear();
        _visitedRooms.Clear();
        _exitRoomKey = default; // RoomKey 기본값으로 초기화
    }

    // --- 던전 설정 메서드 ---

    // 첫 유니크 방문 이전에만 타깃 N 확정. 한 번 정해지면 고정.
    public static void EnsureConfigured(ExitScheduleMode mode, int forceNth, int randMin, int randMax)
    {
        // 이미 타깃이 유효하면 유지
        if (_targetNth >= 1) return;

        int candTarget = 0;
        if (mode == ExitScheduleMode.ForceNthUnique && forceNth >= 1)
        {
            candTarget = forceNth;
        }
        else if (mode == ExitScheduleMode.RandomBetween && randMin >= 1 && randMax >= randMin)
        {
            // UnityEngine.Random 사용
            candTarget = UnityEngine.Random.Range(randMin, randMax + 1);
            candTarget = Mathf.Clamp(candTarget, randMin, randMax);
        }

        if (candTarget < 1) return;

        _configured = true;
        _mode = mode;
        _forceNth = forceNth;
        _randMin = randMin;
        _randMax = randMax;
        _targetNth = candTarget;
    }

    // --- 메인 상태 관리 메서드 ---

    /// <summary>
    /// RoomKey에 대한 스냅샷을 가져오거나 새로 생성합니다.
    /// 또한, 좌표가 짝수인 '방(Room)'에 대해서만 유니크 방문 카운트를 증가시킵니다.
    /// </summary>
    /// <param name="key">현재 방 또는 통로의 좌표 키입니다.</param>
    /// <param name="seedGen">새 스냅샷 생성 시 호출할 시드 생성 함수입니다.</param>
    /// <param name="shellPick">새 스냅샷 생성 시 호출할 쉘 인덱스 함수입니다.</param>
    /// <param name="callerId">이 함수를 호출한 메서드의 이름입니다. 컴파일러가 자동으로 채웁니다.</param>
    /// <returns>해당 키에 대한 Snapshot 객체를 반환합니다.</returns>
    public static Snapshot GetOrCreateSnapshot(
        RoomKey key,
        System.Func<int> seedGen,
        System.Func<int> shellPick,
        [CallerMemberName] string callerId = "Unknown") // [CallerMemberName] 속성 적용
    {
        Snapshot existingSnapshot;

        // 1. 이미 스냅샷이 존재하는지 확인 (기존에 생성된 방/통로)
        if (_snapshots.TryGetValue(key, out existingSnapshot))
        {
            // 방이 이미 생성된 경우, 카운트 로직을 건너뜁니다.
            return existingSnapshot;
        }

        // --- NEW LOGIC: Room/Hallway Determination ---
        // X, Y 좌표가 모두 짝수(Even)일 때만 '방(Room)'으로 간주합니다.
        bool isRoom = (key.x % 2 == 0) && (key.y % 2 == 0);
        string type = isRoom ? "Room" : "Hallway";
        // --- END NEW LOGIC ---

        // 2. 새 키 발견: Unique Visit 처리 로직 실행
        // Add 메서드는 해당 key가 Set에 성공적으로 추가되었을 때만 true를 반환합니다.
        if (_visitedRooms.Add(key))
        {
            if (isRoom)
            {
                _visitedCount++; // Room일 경우에만 유니크 방문 카운트 증가

                // ********** 로그 출력 조정: Unique Count 증가 시에만 출력 **********
                Debug.Log($"[DungeonRunState] **Unique Visit Counted!** New Count: {_visitedCount}. Key: {key}. Caller: {callerId} (Type: {type})");

                // 타깃이 아직 없으면 현재 저장된 설정값으로 한 번 더 잠금 시도
                if (_targetNth < 1)
                {
                    EnsureConfigured(_mode, _forceNth, _randMin, _randMax);
                }

                // Nth 타깃에 도달하면 Exit Room 확정
                if (!_exitChosen && _targetNth >= 1 && _visitedCount == _targetNth)
                {
                    _exitChosen = true;
                    _exitRoomKey = key;
                    Debug.Log($"[DungeonRunState] EXIT ROOM CHOSEN! Key: {_exitRoomKey}, Target Nth: {_targetNth}. Caller: {callerId}");
                }
            }
            else
            {
                // 통로일 경우 카운트는 증가시키지 않고, 방문 로그만 남깁니다.
                Debug.Log($"[DungeonRunState] Hallway Visited (Unique). Key: {key}. Caller: {callerId} (Type: {type})");
            }
        }
        // else: 이미 _visitedRooms에 있지만 스냅샷이 없었던 경우, 카운트 증가 없이 스냅샷만 생성합니다.

        // 3. 새로운 스냅샷 생성 및 저장
        existingSnapshot = new Snapshot
        {
            seed = (seedGen != null ? seedGen() : UnityEngine.Random.Range(1, int.MaxValue)),
            shellIndex = (shellPick != null ? shellPick() : 0),
            cleared = false,
            hasExitGatePos = false
        };
        _snapshots[key] = existingSnapshot;
        return existingSnapshot;
    }
    // *************************************************

    // 해당 방이 탈출 방인지 확인합니다.
    public static bool IsExitRoom(RoomKey key) => _exitChosen && key.Equals(_exitRoomKey);

    // 해당 방이 클리어되었음을 표시합니다.
    public static void MarkCleared(RoomKey key)
    {
        if (_snapshots.TryGetValue(key, out var s)) s.cleared = true;
    }

    // 해당 방의 스냅샷을 엿봅니다 (없으면 null 반환).
    public static Snapshot PeekSnapshot(RoomKey key)
    {
        _snapshots.TryGetValue(key, out var s);
        return s;
    }

    // 탈출 게이트의 위치를 저장합니다.
    public static void RememberExitGatePos(RoomKey key, Vector3 pos)
    {
        if (!_snapshots.TryGetValue(key, out var s)) return;
        s.exitGatePos = pos;
        s.hasExitGatePos = true;
    }

    // 탈출 게이트의 위치를 가져옵니다.
    public static bool TryGetExitGatePos(RoomKey key, out Vector3 pos)
    {
        pos = default;
        if (_snapshots.TryGetValue(key, out var s) && s.hasExitGatePos)
        {
            pos = s.exitGatePos;
            return true;
        }
        return false;
    }

    // 디버깅 정보를 외부에 제공합니다.
    public static void GetDebugState(out ExitScheduleMode mode, out int targetNth, out int visited, out bool exitChosen, out RoomKey exitKey)
    {
        mode = _mode; targetNth = _targetNth; visited = _visitedCount; exitChosen = _exitChosen; exitKey = _exitRoomKey;
    }
}
