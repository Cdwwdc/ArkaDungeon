// Assets/Script/Brick/BrickHPAssigner.cs
using UnityEngine;

public class BrickHPAssigner : MonoBehaviour
{
    [Header("대상(BricksContainer 권장)")]
    [Tooltip("브릭들이 담긴 부모(이 컴포넌트를 그 오브젝트에 붙이는 걸 권장)")]
    public Transform brickRoot;

    [Header("HP 기본 범위(방 전체 기본값)")]
    public int roomHpMin = 1;
    public int roomHpMax = 5;
    public int maxHpClamp = 10;

    [Header("동작 옵션")]
    [Tooltip("기본 HP(=1)인 브릭에만 할당. 이미 2 이상이면 건드리지 않음")]
    public bool onlyAssignIfHpIsOne = true;
    [Tooltip("Start에서 한 번 자동 적용")]
    public bool applyOnStart = true;
    [Tooltip("brickRoot의 자식 구성이 바뀌면 자동 재적용")]
    public bool reapplyWhenChildrenChange = true;

    [Header("레벨→HP 매핑")]
    [Tooltip("레벨을 그대로 HP로 사용(상한 클램프)")]
    public bool useDirectLevelAsHP = true;
    [Tooltip("useDirectLevelAsHP를 끄면 이 커브로 레벨을 HP로 변환")]
    public AnimationCurve levelToHpCurve = AnimationCurve.Linear(1, 1, 10, 10);

    [Header("적용 범위")]
    [Tooltip("ON이면 '레벨존 안'에만 HP를 배분하고, 존 밖은 건드리지 않음")]
    public bool zonesOnly = false; // ★ 추가된 옵션

    LevelZone[] _zones;

    void Reset()
    {
        if (!brickRoot) brickRoot = transform;
    }

    void Awake()
    {
        if (!brickRoot) brickRoot = transform;
    }

    void OnEnable()
    {
        CacheZones();
        if (applyOnStart) AssignAll();
    }

    void Start()
    {
        // 스포너가 Start 이후에 브릭을 깔 때 대비
        if (applyOnStart) AssignAll();
    }

    void OnTransformChildrenChanged()
    {
        if (!reapplyWhenChildrenChange) return;
        // 이 컴포넌트를 brickRoot에 붙였다는 전제(권장)
        AssignAll();
    }

    void CacheZones()
    {
        _zones = FindObjectsOfType<LevelZone>();
    }

    public void AssignAll()
    {
        if (!brickRoot)
        {
            Debug.LogWarning("[BrickHPAssigner] brickRoot가 비어 있어 자동 탐색 시도");
            var rc = FindObjectOfType<RoomController>();
            brickRoot = rc ? rc.brickRoot : brickRoot;
            if (!brickRoot) brickRoot = transform;
        }

        var bricks = brickRoot.GetComponentsInChildren<Brick>(true);
        foreach (var b in bricks)
        {
            if (!b) continue;

            if (onlyAssignIfHpIsOne && b.hitPoints > 1)
                continue;

            int level = PickLevelForPosition(b.transform.position);

            // ★ 추가: zonesOnly가 켜져 있고 구역 밖이면 스킵
            if (level < 0) continue;

            int hp = useDirectLevelAsHP
                ? level
                : Mathf.RoundToInt(levelToHpCurve.Evaluate(level));

            b.hitPoints = Mathf.Clamp(hp, 1, maxHpClamp);

            // HP→색 매핑(BrickSkin: palette 연결 + mapByHitPoints=ON이어야 적용됨)
            var skin = b.GetComponent<BrickSkin>();
            if (skin) skin.ApplyColor();
        }
    }

    int PickLevelForPosition(Vector2 p)
    {
        if (_zones == null || _zones.Length == 0)
            _zones = FindObjectsOfType<LevelZone>();

        // 구역 우선: 포함되는 첫 구역의 범위를 사용
        foreach (var z in _zones)
        {
            if (z && z.Contains(p))
                return Random.Range(z.levelRange.x, z.levelRange.y + 1);
        }

        // ★ 추가: 구역 밖은 스킵(존 밖은 손대지 않음)
        if (zonesOnly) return -1;

        // 폴백: 방 전체 기본 범위
        return Random.Range(roomHpMin, roomHpMax + 1);
    }

    // 에디터에서 우클릭(⋯)로 즉시 재적용
    [ContextMenu("Assign Now")]
    public void AssignNow() => AssignAll();
}
