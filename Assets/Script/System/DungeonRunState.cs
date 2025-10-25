using System.Collections.Generic;
using UnityEngine; // Vector3

public static class DungeonRunState
{
    public enum ExitScheduleMode { ForceNthUnique, RandomBetween }

    public struct RoomKey
    {
        public int x, y;
        public RoomKey(int X, int Y) { x = X; y = Y; }
        public override bool Equals(object o) => o is RoomKey k && k.x == x && k.y == y;
        public override int GetHashCode() => (x * 73856093) ^ (y * 19349663);
        public override string ToString() => $"({x},{y})";
    }

    public class Snapshot
    {
        public int seed;
        public int shellIndex;
        public bool cleared;

        // ExitGate 위치 저장(재방문 재소환용)
        public bool hasExitGatePos;
        public Vector3 exitGatePos;
    }

    static bool _configured = false;
    static ExitScheduleMode _mode = ExitScheduleMode.ForceNthUnique;
    static int _forceNth = 0;
    static int _randMin = 0, _randMax = 0;
    static int _targetNth = 0;
    static bool _exitChosen = false;
    static RoomKey _exitRoomKey;

    static int _visitedCount = 0;
    static readonly Dictionary<RoomKey, Snapshot> _snapshots = new Dictionary<RoomKey, Snapshot>();
    static readonly HashSet<RoomKey> _visitedRooms = new HashSet<RoomKey>();

    public static void Reset()
    {
        _configured = false;
        _mode = ExitScheduleMode.ForceNthUnique;
        _forceNth = 0; _randMin = 0; _randMax = 0;
        _targetNth = 0; _exitChosen = false; _visitedCount = 0;
        _snapshots.Clear();
        _visitedRooms.Clear();
    }

    public static void EnsureConfigured(ExitScheduleMode mode, int forceNth, int randMin, int randMax)
    {
        if (_configured) return; // idempotent
        _configured = true;
        _mode = mode;
        _forceNth = forceNth;
        _randMin = randMin;
        _randMax = randMax;

        if (_mode == ExitScheduleMode.ForceNthUnique && _forceNth >= 1)
        {
            _targetNth = _forceNth;
        }
        else if (_mode == ExitScheduleMode.RandomBetween && _randMin >= 1 && _randMax >= _randMin)
        {
            var r = new System.Random();
            _targetNth = r.Next(_randMin, _randMax + 1);
        }
        else
        {
            _targetNth = 0;
        }
    }

    public static Snapshot GetOrCreateSnapshot(RoomKey key, System.Func<int> seedGen, System.Func<int> shellPick)
    {
        if (_snapshots.TryGetValue(key, out var s)) return s;
        s = new Snapshot
        {
            seed = (seedGen != null ? seedGen() : new System.Random().Next(1, int.MaxValue)),
            shellIndex = (shellPick != null ? shellPick() : 0),
            cleared = false,
            hasExitGatePos = false
        };
        _snapshots[key] = s;
        return s;
    }

    public static bool ReportUniqueVisit(RoomKey key)
    {
        if (_visitedRooms.Add(key))
        {
            _visitedCount++;
            if (!_exitChosen && _targetNth >= 1 && _visitedCount == _targetNth)
            {
                _exitChosen = true;
                _exitRoomKey = key; // 그 순간의 방을 Exit로 확정
            }
            return true;
        }
        return false;
    }

    public static bool IsExitRoom(RoomKey key)
    {
        return _exitChosen && key.Equals(_exitRoomKey);
    }

    public static void MarkCleared(RoomKey key)
    {
        if (_snapshots.TryGetValue(key, out var s)) s.cleared = true;
    }

    public static Snapshot PeekSnapshot(RoomKey key)
    {
        _snapshots.TryGetValue(key, out var s);
        return s;
    }

    // 게이트 위치 기억/조회
    public static void RememberExitGatePos(RoomKey key, Vector3 pos)
    {
        if (!_snapshots.TryGetValue(key, out var s)) return;
        s.exitGatePos = pos;
        s.hasExitGatePos = true;
    }

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

    // 디버그 HUD
    public static void GetDebugState(out ExitScheduleMode mode, out int targetNth, out int visited, out bool exitChosen, out RoomKey exitKey)
    {
        mode = _mode;
        targetNth = _targetNth;
        visited = _visitedCount;
        exitChosen = _exitChosen;
        exitKey = _exitRoomKey;
    }
}
