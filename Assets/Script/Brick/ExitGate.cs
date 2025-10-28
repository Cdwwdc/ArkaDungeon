using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ExitGate : MonoBehaviour
{
    [Header("스냅(선택)")]
    public bool snapToGrid = true;
    public float cellWidth = 1.0f;
    public float halfBrickHeight = 0.5f;

    [Header("동작")]
    public string requiredTag = "Ball";
    public string exitSceneName = "Town";

    [Header("UI")]
    public bool showUIWhenActivated = true;
    public bool hideNextStageAndDoors = true;

    [Header("트리거로 UI 열기 허용 (클리어 후)")]
    public bool openByTriggerAfterActivated = true;

    bool used;
    bool locked = true;

    void Reset()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 1f);
        col.offset = Vector2.zero;
    }

    void Awake()
    {
        if (snapToGrid)
        {
            var p = transform.position;
            p.x = Mathf.Round(p.x / Mathf.Max(0.0001f, cellWidth)) * cellWidth;
            p.y = Mathf.Round(p.y / Mathf.Max(0.0001f, halfBrickHeight)) * halfBrickHeight;
            transform.position = p;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        if (locked) return;
        if (!openByTriggerAfterActivated) return;
        if (used) return;

        used = true;
        ShowUI();
    }

    public void SetLocked(bool v) => locked = v;

    public void OnGateActivated()
    {
        locked = false;

        var gm = FindObjectOfType<GameManager>();
        gm?.HideNextStageUIAndStopBlink();
        if (hideNextStageAndDoors) gm?.SetExitDoorsVisible(false);
        if (showUIWhenActivated) ShowUI();

        // ★ 안전망: 좌표 저장(재입장 자동 리스폰용)
        var rc = FindObjectOfType<RoomController>();
        if (rc != null)
            DungeonRunState.RememberExitGatePos(
                new DungeonRunState.RoomKey(rc.roomKeyX, rc.roomKeyY),
                transform.position
            );
    }

    void ShowUI()
    {
        ExitGateUI.Show(
            title: null,
            onContinue: () =>
            {
                used = false;
                var gm = FindObjectOfType<GameManager>();
                gm?.SetExitDoorsVisible(true);
                gm?.ShowNextStageUIAndBlink();
            },
            onExit: () => ExitGateUI.LoadScene(exitSceneName)
        );
    }
}
