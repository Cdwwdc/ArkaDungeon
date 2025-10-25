using UnityEngine;

public class ExitDebugHUD : MonoBehaviour
{
    public RoomController room;          // Inspector에서 할당(없으면 자동검색)
    public Vector2 guiPos = new Vector2(10, 10);
    public bool show = true;             // 필요시 토글

    void Awake()
    {
        if (!room) room = FindObjectOfType<RoomController>();
    }

    void OnGUI()
    {
        if (!show || !Application.isPlaying || !room) return;

        var key = new DungeonRunState.RoomKey(room.roomKeyX, room.roomKeyY);
        var snap = DungeonRunState.PeekSnapshot(key);
        bool isExit = DungeonRunState.IsExitRoom(key);

        int hooks = room.DebugHooks != null ? room.DebugHooks.Count : 0;
        int seedCount = 0;
        if (room.DebugHooks != null)
        {
            for (int i = 0; i < room.DebugHooks.Count; i++)
                if (room.DebugHooks[i] && room.DebugHooks[i].IsSeed) seedCount++;
        }

        // (선택) 스케줄 내부 상태 표시
        DungeonRunState.ExitScheduleMode mode; int targetNth, visited;
        bool exitChosen; DungeonRunState.RoomKey exitKey;
        DungeonRunState.GetDebugState(out mode, out targetNth, out visited, out exitChosen, out exitKey);

        string text =
            $"[EXIT DEBUG]\n" +
            $"Key: ({key.x},{key.y})\n" +
            $"Schedule: {mode} targetNth={targetNth} visited={visited}\n" +
            $"ExitChosen={exitChosen} ExitKey={exitKey}\n" +
            $"IsExitRoom: {isExit}\n" +
            $"Cleared: {(snap != null && snap.cleared)} (LastBuildCleared={room.DebugLastBuildWasCleared})\n" +
            $"AliveBricks={room.DebugAliveBricks} Hooks={hooks} SeedMarked={seedCount}\n" +
            $"ExitGatePresent={(room.DebugExitGate != null)}";

        var rect = new Rect(guiPos.x, guiPos.y, 520, 170);
        GUI.color = new Color(0, 0, 0, 0.65f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16), text);
    }
}
