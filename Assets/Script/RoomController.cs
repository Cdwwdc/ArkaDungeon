using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("참조")]
    public Transform brickRoot;       // BricksContainer (이걸 옮기면 전체가 같이 움직이게)
    public GameObject brickPrefab;    // Brick 프리팹
    public ExitDoors exitDoors;       // ExitCanvas(문 UI)

    [Header("격자 생성 옵션")]
    public int cols = 10;
    public int rows = 5;
    public Vector2 cellSize = new Vector2(0.9f, 0.6f);

    [Tooltip("기본: 월드 좌표 origin 사용. 체크하면 brickRoot/ layoutPivot 기준의 로컬 배치")]
    public bool useRootAsOrigin = true;

    [Tooltip("useRootAsOrigin=false일 때 쓰는 월드 좌표 오프셋")]
    public Vector2 origin = new Vector2(-4.0f, 2.5f);

    [Tooltip("선택: 이 Transform를 원점으로 사용(있으면 최우선). 없으면 brickRoot를 원점으로 사용")]
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
                if (Random.value < 0.12f) continue; // 빈 칸

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
                    Debug.LogError("RoomController: brickPrefab에 Brick.cs가 없음");
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
            Debug.LogError("RoomController: brickRoot(BricksContainer) 미지정");
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
            else Debug.LogWarning("RoomController: exitDoors가 비어 있어 문 UI를 켤 수 없음");

            var gm = FindObjectOfType<GameManager>();
            gm?.OnStageClear();
        }
    }

    // 버튼에서 연결
    public void GoNorth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoEast() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoSouth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoWest() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }

    // === 여기부터 추가 유틸 ===

    // 셀의 "월드 위치" 계산: (layoutPivot 또는 brickRoot)을 원점으로 사용
    Vector3 GetCellWorldPos(int x, int y)
    {
        // 그리드의 (0,0)을 화면 왼쪽-위쪽처럼 쓰고 있었으니, y는 -로 내려가게 유지
        Vector3 local = new Vector3(x * cellSize.x, -y * cellSize.y, 0f);

        if (useRootAsOrigin)
        {
            Transform originT = layoutPivot != null ? layoutPivot : brickRoot;
            if (originT != null)
                return originT.TransformPoint(local);     // 원점 Transform의 로컬 기준
            // brickRoot가 없으면 월드로 폴백
        }

        // 기존 방식: 고정된 월드 좌표 origin 기준
        return new Vector3(origin.x + local.x, origin.y + local.y, 0f);
    }

    // 에디터에서 배치 미리보기
    void OnDrawGizmosSelected()
    {
        // 그릴 기준 Transform
        Transform originT = (useRootAsOrigin ? (layoutPivot != null ? layoutPivot : brickRoot) : null);
        Vector3 baseWorld = originT ? originT.position : new Vector3(origin.x, origin.y, 0f);

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f); // 노란색
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Vector3 local = new Vector3(x * cellSize.x, -y * cellSize.y, 0f);
                Vector3 p = originT ? originT.TransformPoint(local) : baseWorld + local;
                // 칸 미리보기 사각형(조금 작게)
                Gizmos.DrawWireCube(p, new Vector3(cellSize.x * 0.9f, cellSize.y * 0.9f, 0.01f));
            }
        }

        // 원점 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(baseWorld, 0.05f);
    }
}
