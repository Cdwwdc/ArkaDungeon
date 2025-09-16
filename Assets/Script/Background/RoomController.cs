using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("����")]
    public Transform brickRoot;       // BricksContainer
    public GameObject brickPrefab;    // Brick ������
    public ExitDoors exitDoors;       // ExitCanvas(�� UI)

    [Header("���� ���� �ɼ�")]
    public int cols = 10;
    public int rows = 5;
    public Vector2 cellSize = new Vector2(0.9f, 0.6f);
    public Vector2 origin = new Vector2(-4.0f, 2.5f);

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

                Vector3 pos = new Vector3(
                    origin.x + x * cellSize.x,
                    origin.y - y * cellSize.y,
                    0f
                );

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
            Vector3 pos = new Vector3(origin.x + (cols / 2) * cellSize.x, origin.y, 0f);
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
        // 1) �긯 ����
        for (int i = brickRoot.childCount - 1; i >= 0; i--)
            Destroy(brickRoot.GetChild(i).gameObject);
        aliveBricks = 0;

        // 2) ���� �ִ� ������ ���� ����(���� ������ �Ѿ�� �ʵ���)
        var items = FindObjectsOfType<PowerUpItem>();
        foreach (var it in items) Destroy(it.gameObject);
    }

    public void NotifyBrickDestroyed()
    {
        aliveBricks--;
        if (aliveBricks <= 0)
        {
            if (exitDoors) exitDoors.Show(true);
            else Debug.LogWarning("RoomController: exitDoors�� ��� �־� �� UI�� �� �� ����");

            // �������� Ŭ���� �˸� �� �� ������ ���� Kill�� GameManager�� �˾Ƽ�
            var gm = FindObjectOfType<GameManager>();
            gm?.OnStageClear();
        }
    }

    // ��ư���� ����
    public void GoNorth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoEast() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoSouth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoWest() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
}
