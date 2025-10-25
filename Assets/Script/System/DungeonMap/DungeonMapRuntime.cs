using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 던전 맵의 싱글톤 런타임 컨트롤러. (씬 간 유지 옵션)
/// </summary>
public class DungeonMapRuntime : MonoBehaviour
{
    public static DungeonMapRuntime I { get; private set; }

    [Header("생성 설정")]
    public int width = 9;
    public int height = 9;
    public int seed = 0;
    [Tooltip("미로에 추가 연결 수(0이면 자동: width*height/6)")]
    public int extraLinks = 0;
    [Tooltip("씬 이동해도 유지할지")]
    public bool dontDestroyOnLoad = true;

    [Header("공개 설정(FoW)")]
    public int revealRadius = 1;

    [Header("이벤트")]
    public UnityEvent OnMapGenerated;
    public UnityEvent OnRoomEntered;      // 현재 방 들어갈 때
    public UnityEvent OnMoved;            // 이동 성공 시

    public DungeonMap Map { get; private set; }

    void Awake()
    {
        // 싱글톤 보장
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

        // DontDestroyOnLoad는 루트 오브젝트만 가능 → 부모가 있으면 떼서 승격
        if (dontDestroyOnLoad)
        {
            if (transform.parent != null)
                transform.SetParent(null);              // 루트로 승격
            DontDestroyOnLoad(gameObject);              // 이제 안전
        }

        // 첫 맵 생성(원하면 꺼도 됨)
        if (Map == null) NewRun();
    }

    public void NewRun(Vector2Int? start = null)
    {
        Map = DungeonMap.Generate(width, height, seed, extraLinks, start);
        Map.MarkVisited(Map.current);
        Map.RevealAround(Map.current, revealRadius);
        OnMapGenerated?.Invoke();
        OnRoomEntered?.Invoke();
    }

    public bool TryMove(byte dir)
    {
        if (Map == null) return false;
        if (!Map.Move(dir)) return false;

        Map.MarkVisited(Map.current);
        Map.RevealAround(Map.current, revealRadius);
        OnMoved?.Invoke();
        OnRoomEntered?.Invoke();
        return true;
    }

    // 간단 레퍼: 현재 방의 문 비트마스크
    public byte GetCurrentDoors()
    {
        if (Map == null) return 0;
        return Map[Map.current.x, Map.current.y].doors;
    }
}
