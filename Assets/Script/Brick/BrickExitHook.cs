using UnityEngine;

/// <summary>
/// �긯�� '�����÷��̷�' �ı��� �� �ⱸ(ExitGate) ������ ��û�ϴ� ��.
/// - RoomController�� Build/��ĵ �� Init(this)�� ������ ����
/// - RoomController�� Ư�� �긯�� �õ�� �����ϸ� MarkAsExitSeed() ȣ��
/// - OnDestroy���� ���� Ÿ�̹��� ���� �ⱸ ����(�� ��ε�/������ �� ����)
/// - �õ尡 �ƴϸ� �� ������Ʈ�� ���� Ȯ��(localChancePercent)�� ���� �Ǵ�
/// </summary>
public class BrickExitHook : MonoBehaviour
{
    RoomController _rc;
    bool _isSeed = false;

    [Header("Exit Gate Ȯ��(�õ尡 �ƴ� ��)")]
    [Range(0f, 100f)] public float localChancePercent = 0f;

    static bool s_AppQuitting = false;
    void OnApplicationQuit() { s_AppQuitting = true; }

    // �� ����׿� Getter
    public bool IsSeed => _isSeed;

    public void Init(RoomController owner) { _rc = owner; }
    public void Init(RoomController owner, float _cellW, float _halfH) { _rc = owner; }

    public void MarkAsExitSeed() { _isSeed = true; }

    void OnDestroy()
    {
        // ������ ����/��������/�� ��ȯ �� ���÷��� ��Ȳ �� ���� ����
        if (!Application.isPlaying) return;
        if (s_AppQuitting) return;

        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;

        // RoomController Ȯ��(Init�� �� ���� ��� ���)
        if (!_rc) _rc = FindObjectOfType<RoomController>();
        if (!_rc || !_rc.isActiveAndEnabled) return;

        // �̹� �ⱸ�� �����ϸ� �� ������ ����(�ߺ� ���� ����)
        if (FindObjectOfType<ExitGate>()) return;

        // === ���� ���� �Ǵ� ===
        bool shouldSpawn = false;

        // 1) �õ� �긯�̸� ������ ����
        if (_isSeed) shouldSpawn = true;

        // 2) �õ尡 �ƴϸ� ���� Ȯ��
        if (!shouldSpawn && localChancePercent > 0f)
        {
            float p = Mathf.Clamp(localChancePercent, 0f, 100f) / 100f;
            if (Random.value < p) shouldSpawn = true;
        }
        if (!shouldSpawn) return;

        // === ���� ���� ===
        var eg = _rc.SpawnExitGateAt(transform.position);

        // ������: ��ġ ����
        if (eg)
        {
            DungeonRunState.RememberExitGatePos(
                new DungeonRunState.RoomKey(_rc.roomKeyX, _rc.roomKeyY),
                transform.position
            );
        }
    }
}
