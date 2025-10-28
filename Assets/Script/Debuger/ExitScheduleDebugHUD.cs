using System.Collections.Generic;
using UnityEngine;

public class ExitScheduleDebugHUD : MonoBehaviour
{
    RoomController rc;
    readonly HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
    Vector2Int lastKey;
    int uniqueCount;

    void Start()
    {
        // FindObjectOfType은 여러 개일 경우 위험할 수 있으니,
        // 현재 RoomController 인스턴스가 하나만 있다고 가정합니다.
        rc = FindObjectOfType<RoomController>();
        if (rc != null)
        {
            lastKey = new Vector2Int(rc.roomKeyX, rc.roomKeyY);
            seen.Add(lastKey);
            uniqueCount = 1;
        }
    }

    void Update()
    {
        if (!rc) return;
        var key = new Vector2Int(rc.roomKeyX, rc.roomKeyY);
        // RoomController가 새 방을 빌드할 때마다 Update가 불리며 키가 갱신됩니다.
        if (key != lastKey)
        {
            lastKey = key;
            if (seen.Add(key)) uniqueCount++;
        }
    }

    void OnGUI()
    {
        if (!rc) return;

        // 크기를 늘려 모든 정보를 표시할 수 있도록 수정
        var rect = new Rect(10, 10, 420, 160);
        GUILayout.BeginArea(rect, GUI.skin.box);

        // --- 섹션 1: DungeonRunState (출구 결정 상태) ---
        var dk = new DungeonRunState.RoomKey(rc.roomKeyX, rc.roomKeyY);
        bool isExitHere = DungeonRunState.IsExitRoom(dk);

        // DungeonRunState에서 현재 런의 확정된 Target N을 가져옵니다.
        DungeonRunState.GetDebugState(out var mode, out int targetNth, out int visited, out bool exitChosen, out var exitKey);

        string targetN = (targetNth >= 1) ? targetNth.ToString() : "Unset/Invalid";
        string exitStatus = isExitHere ? "<color=green>YES</color>" : "<color=red>NO</color>";

        GUILayout.Label($"Mode: {mode.ToString()}  |  Target N: {targetN}  |  Exit Chosen: {exitChosen}");
        GUILayout.Label($"Key: ({rc.roomKeyX},{rc.roomKeyY})  |  Unique Visited: {visited}  |  Is Exit Room: {exitStatus}");

        // --- 섹션 2: RoomController (스폰 성공 조건) ---
        string plannedStatus = rc.DebugHasPlannedExitGate ? "<color=lime>PLANNED</color>" : "<color=red>FAILED</color>";
        string enableStatus = rc.exitGateEnable ? "Enabled" : "Disabled";
        string prefabStatus = rc.DebugExitPrefabExists ? "Exists" : "<color=red>MISSING</color>";

        GUILayout.Space(5);
        GUILayout.Label("--- [Exit Gate Planning & Status] ---");
        GUILayout.Label($"Bricks Alive: {rc.DebugAliveBricks}  |  Gate Planned: {plannedStatus}");
        GUILayout.Label($"Exit Logic: {enableStatus}  |  Prefab: {prefabStatus}");

        GUILayout.EndArea();
    }
}
