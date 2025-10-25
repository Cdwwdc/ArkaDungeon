using UnityEngine;
using UnityEngine.UI;

public class NextStageButtonsBinder : MonoBehaviour
{
    [Header("참조")]
    public DungeonMapRuntime runtime;      // 자동 탐색
    public RoomController room;            // 씬의 RoomController
    public Button northBtn, eastBtn, southBtn, westBtn;

    [Header("옵션")]
    [Tooltip("true면 인스펙터에 미리 연결된 onClick 리스너들을 모두 지우고 Binder가 단일로 관리합니다.")]
    public bool replaceInspectorOnClick = true;

    bool wired;

    void Awake()
    {
        if (!runtime) runtime = DungeonMapRuntime.I;
        if (!room) room = FindObjectOfType<RoomController>();
        WireButtonsOnce();
    }

    void OnEnable()
    {
        Refresh();
        if (runtime)
        {
            runtime.OnRoomEntered.AddListener(Refresh);
            runtime.OnMoved.AddListener(Refresh);
        }
    }

    void OnDisable()
    {
        if (runtime)
        {
            runtime.OnRoomEntered.RemoveListener(Refresh);
            runtime.OnMoved.RemoveListener(Refresh);
        }
    }

    void WireButtonsOnce()
    {
        if (wired) return;
        wired = true;

        Wire(northBtn, Dir.N, () => room?.GoNorth());
        Wire(eastBtn, Dir.E, () => room?.GoEast());
        Wire(southBtn, Dir.S, () => room?.GoSouth());
        Wire(westBtn, Dir.W, () => room?.GoWest());
    }

    void Wire(Button btn, byte dir, System.Action thenBuildRoom)
    {
        if (!btn) return;

        if (replaceInspectorOnClick)
            btn.onClick.RemoveAllListeners(); // ← 기존 인스펙터 연결 제거 (중복 차단)

        btn.onClick.AddListener(() => TryMove(dir, thenBuildRoom));
    }

    void TryMove(byte dir, System.Action thenBuildRoom)
    {
        if (runtime && runtime.TryMove(dir))
        {
            thenBuildRoom?.Invoke();   // 기존 RoomController 흐름 그대로
        }
        // 이동 불가면 (문 없으면) 버튼이 비활성화라 여기 안 들어오는 게 정상
    }

    public void Refresh()
    {
        if (!runtime) runtime = DungeonMapRuntime.I;
        if (runtime == null) return;

        byte d = runtime.GetCurrentDoors();

        if (northBtn) northBtn.interactable = (d & Dir.N) != 0;
        if (eastBtn) eastBtn.interactable = (d & Dir.E) != 0;
        if (southBtn) southBtn.interactable = (d & Dir.S) != 0;
        if (westBtn) westBtn.interactable = (d & Dir.W) != 0;
    }
}
