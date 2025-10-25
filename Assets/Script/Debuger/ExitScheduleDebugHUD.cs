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
        if (key != lastKey)
        {
            lastKey = key;
            if (seen.Add(key)) uniqueCount++;
        }
    }

    void OnGUI()
    {
        if (!rc) return;

        var rect = new Rect(10, 10, 420, 80);
        GUILayout.BeginArea(rect, GUI.skin.box);

        string mode = rc.exitScheduleMode.ToString();
        string targetInfo = (rc.exitScheduleMode == DungeonRunState.ExitScheduleMode.ForceNthUnique)
            ? $"Target N={rc.forceExitAtRoomNth}"
            : $"Target N¡ô[{rc.randomExitMinNth},{rc.randomExitMaxNth}]";

        var dk = new DungeonRunState.RoomKey(rc.roomKeyX, rc.roomKeyY);
        bool isExitHere = DungeonRunState.IsExitRoom(dk);

        GUILayout.Label($"Mode: {mode}  |  {targetInfo}");
        GUILayout.Label($"Current Key: ({rc.roomKeyX},{rc.roomKeyY})");
        GUILayout.Label($"Unique Visited: {uniqueCount}  |  IsExitRoom: {isExitHere}");

        GUILayout.EndArea();
    }
}
