using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("����")]
    public Transform brickRoot;       // BricksContainer (�̰� �ű�� ��ü�� ���� �����̰�)
    public GameObject brickPrefab;    // Brick ������
    public ExitDoors exitDoors;       // ExitCanvas(�� UI)

    [Header("���� ���� �ɼ�")]
    public int cols = 10;
    public int rows = 5;
    public Vector2 cellSize = new Vector2(0.9f, 0.6f);

    [Tooltip("�⺻: ���� ��ǥ origin ���. üũ�ϸ� brickRoot/ layoutPivot ������ ���� ��ġ")]
    public bool useRootAsOrigin = true;

    [Tooltip("useRootAsOrigin=false�� �� ���� ���� ��ǥ ������")]
    public Vector2 origin = new Vector2(-4.0f, 2.5f);

    [Tooltip("����: �� Transform�� �������� ���(������ �ֿ켱). ������ brickRoot�� �������� ���")]
    public Transform layoutPivot;

    int aliveBricks = 0;

    void Start()
    {
        if (exitDoors) exitDoors.Show(false);
        BuildRoomSimple();
    }

    public void BuildRoomSimple()
    {
        ClearRoom();

        int created = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                if (Random.value < 0.12f) continue; // �� ĭ

                Vector3 pos = GetCellWorldPos(x, y);

                var go = Instantiate(brickPrefab, pos, Quaternion.identity, brickRoot);
                var brick = go.GetComponent<Brick>();
                if (brick != null)
                {
                    brick.Init(this);
                    aliveBricks++;
                    created++;
                }
                else
                {
                    Debug.LogError("RoomController: brickPrefab�� Brick.cs�� ����");
                }
            }
        }

        if (created == 0)
        {
            Vector3 pos = GetCellWorldPos(cols / 2, 0);
            var go = Instantiate(brickPrefab, pos, Quaternion.identity, brickRoot);
            var brick = go.GetComponent<Brick>();
            if (brick != null) { brick.Init(this); aliveBricks++; }
        }

        if (exitDoors) exitDoors.Show(false);
    }

    public void ClearRoom()
    {
        if (brickRoot == null)
        {
            Debug.LogError("RoomController: brickRoot(BricksContainer) ������");
            return;
        }
        for (int i = brickRoot.childCount - 1; i >= 0; i--)
            Destroy(brickRoot.GetChild(i).gameObject);
        aliveBricks = 0;
    }

    public void NotifyBrickDestroyed()
    {
        aliveBricks--;
        if (aliveBricks <= 0)
        {
            if (exitDoors) exitDoors.Show(true);
            else Debug.LogWarning("RoomController: exitDoors�� ��� �־� �� UI�� �� �� ����");

            var gm = FindObjectOfType<GameManager>();
            gm?.OnStageClear();
        }
    }

    // ��ư���� ����
    public void GoNorth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoEast() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoSouth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoWest() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }

    // === ������� �߰� ��ƿ ===

    // ���� "���� ��ġ" ���: (layoutPivot �Ǵ� brickRoot)�� �������� ���
    Vector3 GetCellWorldPos(int x, int y)
    {
        // �׸����� (0,0)�� ȭ�� ����-����ó�� ���� �־�����, y�� -�� �������� ����
        Vector3 local = new Vector3(x * cellSize.x, -y * cellSize.y, 0f);

        if (useRootAsOrigin)
        {
            Transform originT = layoutPivot != null ? layoutPivot : brickRoot;
            if (originT != null)
                return originT.TransformPoint(local);     // ���� Transform�� ���� ����
            // brickRoot�� ������ ����� ����
        }

        // ���� ���: ������ ���� ��ǥ origin ����
        return new Vector3(origin.x + local.x, origin.y + local.y, 0f);
    }

    // �����Ϳ��� ��ġ �̸�����
    void OnDrawGizmosSelected()
    {
        // �׸� ���� Transform
        Transform originT = (useRootAsOrigin ? (layoutPivot != null ? layoutPivot : brickRoot) : null);
        Vector3 baseWorld = originT ? originT.position : new Vector3(origin.x, origin.y, 0f);

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f); // �����
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Vector3 local = new Vector3(x * cellSize.x, -y * cellSize.y, 0f);
                Vector3 p = originT ? originT.TransformPoint(local) : baseWorld + local;
                // ĭ �̸����� �簢��(���� �۰�)
                Gizmos.DrawWireCube(p, new Vector3(cellSize.x * 0.9f, cellSize.y * 0.9f, 0.01f));
            }
        }

        // ���� ǥ��
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(baseWorld, 0.05f);
    }
}
