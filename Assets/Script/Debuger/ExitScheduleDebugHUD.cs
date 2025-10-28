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
        // FindObjectOfType�� ���� ���� ��� ������ �� ������,
        // ���� RoomController �ν��Ͻ��� �ϳ��� �ִٰ� �����մϴ�.
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
        // RoomController�� �� ���� ������ ������ Update�� �Ҹ��� Ű�� ���ŵ˴ϴ�.
        if (key != lastKey)
        {
            lastKey = key;
            if (seen.Add(key)) uniqueCount++;
        }
    }

    void OnGUI()
    {
        if (!rc) return;

        // ũ�⸦ �÷� ��� ������ ǥ���� �� �ֵ��� ����
        var rect = new Rect(10, 10, 420, 160);
        GUILayout.BeginArea(rect, GUI.skin.box);

        // --- ���� 1: DungeonRunState (�ⱸ ���� ����) ---
        var dk = new DungeonRunState.RoomKey(rc.roomKeyX, rc.roomKeyY);
        bool isExitHere = DungeonRunState.IsExitRoom(dk);

        // DungeonRunState���� ���� ���� Ȯ���� Target N�� �����ɴϴ�.
        DungeonRunState.GetDebugState(out var mode, out int targetNth, out int visited, out bool exitChosen, out var exitKey);

        string targetN = (targetNth >= 1) ? targetNth.ToString() : "Unset/Invalid";
        string exitStatus = isExitHere ? "<color=green>YES</color>" : "<color=red>NO</color>";

        GUILayout.Label($"Mode: {mode.ToString()}  |  Target N: {targetN}  |  Exit Chosen: {exitChosen}");
        GUILayout.Label($"Key: ({rc.roomKeyX},{rc.roomKeyY})  |  Unique Visited: {visited}  |  Is Exit Room: {exitStatus}");

        // --- ���� 2: RoomController (���� ���� ����) ---
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
