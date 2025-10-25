using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ���� ���� �̱��� ��Ÿ�� ��Ʈ�ѷ�. (�� �� ���� �ɼ�)
/// </summary>
public class DungeonMapRuntime : MonoBehaviour
{
    public static DungeonMapRuntime I { get; private set; }

    [Header("���� ����")]
    public int width = 9;
    public int height = 9;
    public int seed = 0;
    [Tooltip("�̷ο� �߰� ���� ��(0�̸� �ڵ�: width*height/6)")]
    public int extraLinks = 0;
    [Tooltip("�� �̵��ص� ��������")]
    public bool dontDestroyOnLoad = true;

    [Header("���� ����(FoW)")]
    public int revealRadius = 1;

    [Header("�̺�Ʈ")]
    public UnityEvent OnMapGenerated;
    public UnityEvent OnRoomEntered;      // ���� �� �� ��
    public UnityEvent OnMoved;            // �̵� ���� ��

    public DungeonMap Map { get; private set; }

    void Awake()
    {
        // �̱��� ����
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

        // DontDestroyOnLoad�� ��Ʈ ������Ʈ�� ���� �� �θ� ������ ���� �°�
        if (dontDestroyOnLoad)
        {
            if (transform.parent != null)
                transform.SetParent(null);              // ��Ʈ�� �°�
            DontDestroyOnLoad(gameObject);              // ���� ����
        }

        // ù �� ����(���ϸ� ���� ��)
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

    // ���� ����: ���� ���� �� ��Ʈ����ũ
    public byte GetCurrentDoors()
    {
        if (Map == null) return 0;
        return Map[Map.current.x, Map.current.y].doors;
    }
}
