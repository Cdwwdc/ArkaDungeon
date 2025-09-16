using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("참조")]
    public Transform brickRoot;       // BricksContainer
    public GameObject brickPrefab;    // Brick 프리팹
    public ExitDoors exitDoors;       // ExitCanvas(문 UI)

    [Header("격자 생성 옵션")]
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
                if (Random.value < 0.12f) continue; // 빈 칸

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
                    Debug.LogError("RoomController: brickPrefab에 Brick.cs가 없음");
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
            Debug.LogError("RoomController: brickRoot(BricksContainer) 미지정");
            return;
        }
        // 1) 브릭 제거
        for (int i = brickRoot.childCount - 1; i >= 0; i--)
            Destroy(brickRoot.GetChild(i).gameObject);
        aliveBricks = 0;

        // 2) 남아 있는 아이템 전부 제거(다음 방으로 넘어가지 않도록)
        var items = FindObjectsOfType<PowerUpItem>();
        foreach (var it in items) Destroy(it.gameObject);
    }

    public void NotifyBrickDestroyed()
    {
        aliveBricks--;
        if (aliveBricks <= 0)
        {
            if (exitDoors) exitDoors.Show(true);
            else Debug.LogWarning("RoomController: exitDoors가 비어 있어 문 UI를 켤 수 없음");

            // 스테이지 클리어 알림 → 공 숨기지 말고 Kill은 GameManager가 알아서
            var gm = FindObjectOfType<GameManager>();
            gm?.OnStageClear();
        }
    }

    // 버튼에서 연결
    public void GoNorth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoEast() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoSouth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoWest() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
}
