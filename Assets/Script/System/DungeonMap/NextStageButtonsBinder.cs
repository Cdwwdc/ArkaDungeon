using UnityEngine;
using UnityEngine.UI;

public class NextStageButtonsBinder : MonoBehaviour
{
    [Header("����")]
    public DungeonMapRuntime runtime;      // �ڵ� Ž��
    public RoomController room;            // ���� RoomController
    public Button northBtn, eastBtn, southBtn, westBtn;

    [Header("�ɼ�")]
    [Tooltip("true�� �ν����Ϳ� �̸� ����� onClick �����ʵ��� ��� ����� Binder�� ���Ϸ� �����մϴ�.")]
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
            btn.onClick.RemoveAllListeners(); // �� ���� �ν����� ���� ���� (�ߺ� ����)

        btn.onClick.AddListener(() => TryMove(dir, thenBuildRoom));
    }

    void TryMove(byte dir, System.Action thenBuildRoom)
    {
        if (runtime && runtime.TryMove(dir))
        {
            thenBuildRoom?.Invoke();   // ���� RoomController �帧 �״��
        }
        // �̵� �Ұ��� (�� ������) ��ư�� ��Ȱ��ȭ�� ���� �� ������ �� ����
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
