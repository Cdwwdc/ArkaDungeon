using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ExitGate : MonoBehaviour
{
    [Header("스냅(선택)")]
    public bool snapToGrid = true;
    public float cellWidth = 1.0f;       // 128px = 1유닛 가정(네 그리드면 0.9)
    public float halfBrickHeight = 0.5f; // 64px = 0.5유닛(네 그리드면 0.6)

    [Header("동작")]
    [Tooltip("이 태그를 가진 오브젝트만 트리거 허용(비우면 모두 허용). 보통 Ball 또는 Player")]
    public string requiredTag = "Ball";
    [Tooltip("씬 전환 대상")]
    public string exitSceneName = "Town";

    [Header("UI")]
    [Tooltip("포탈 활성화(잠금 해제)되는 순간 자동으로 UI를 띄움")]
    public bool showUIWhenActivated = true;
    [Tooltip("UI를 띄울 때 NextStage 텍스트/출구 버튼 숨김")]
    public bool hideNextStageAndDoors = true;

    [Header("트리거로 UI 열기 허용 (클리어 후)")]
    [Tooltip("클리어(잠금 해제) 이후 공/플레이어가 닿았을 때도 UI를 열지 여부")]
    public bool openByTriggerAfterActivated = true; // false면 트리거로는 안 열림(우리가 띄운 패널만 사용)

    // 내부
    bool used;
    bool locked = true; // 클리어 전 잠금

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
        if (locked) return;                                   // 클리어 전엔 잠금
        if (!openByTriggerAfterActivated) return;             // 옵션으로 트리거 진입 열기 금지
        if (used) return;

        used = true;
        ShowUI();
    }

    public void SetLocked(bool v) => locked = v;

    /// <summary>클리어 시 RoomController가 호출. 잠금 해제 + NextStage 억제 + (옵션) UI 오픈.</summary>
    public void OnGateActivated()
    {
        locked = false;

        // NextStage 텍스트 깜빡임 완전 정지 + 숨김
        var gm = FindObjectOfType<GameManager>();
        gm?.HideNextStageUIAndStopBlink();

        // 출구 버튼은 옵션에 따라 숨김
        if (hideNextStageAndDoors)
            gm?.SetExitDoorsVisible(false);

        if (showUIWhenActivated)
            ShowUI();
    }

    void ShowUI()
    {
        // title=null → ExitGateUI 프리팹의 기존 타이틀 텍스트를 유지
        ExitGateUI.Show(
            title: null,
            onContinue: () =>
            {
                // 계속 탐험: 다시 들어오면 또 뜨도록 플래그 해제
                used = false;

                var gm = FindObjectOfType<GameManager>();

                // 문(ExitDoors) 다시 노출
                gm?.SetExitDoorsVisible(true);

                // Next Stage 텍스트 재표시 + 깜빡임 재시작
                gm?.ShowNextStageUIAndBlink();
            },
            onExit: () => ExitGateUI.LoadScene(exitSceneName)
        );
    }
}
