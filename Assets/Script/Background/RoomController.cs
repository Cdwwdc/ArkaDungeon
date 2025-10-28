using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using TMPro;

public class RoomController : MonoBehaviour
{
    [Header("루트/브릭 프리팹")]
    public Transform brickRoot;
    public GameObject brickPrefab;

    [Header("출구(선택)")]
    public ExitDoors exitDoors;
    public ExitGate exitGatePrefab;

    [Header("격자 기본(폴백용)")]
    public int cols = 12;
    public int rows = 7;
    public Vector2 cellSize = new Vector2(0.9f, 0.6f);

    public enum GridAnchor { TopLeft, Center }
    public GridAnchor gridAnchor = GridAnchor.Center;
    public Vector2 origin = new Vector2(-4.0f, 2.5f);

    [Header("채움 컨트롤(폴백용)")]
    public bool usePerlinFill = true;
    [Range(0f, 1f)] public float fill = 0.55f;
    public float noiseScale = 1.1f;
    public float jitter = 0.03f;

    [Header("대칭 옵션(폴백용)")]
    public bool mirrorX = true;
    public bool mirrorY = false;

    [Header("레이아웃 마스크(폴백용)")]
    public bool useMask = false;
    public Texture2D layoutMask;

    [Header("타겟 개수(폴백용)")]
    public bool useTargetCount = true;
    public int minBricks = 28;
    public int maxBricks = 52;

    [Header("랜덤 시드")]
    public bool randomizeSeedEachBuild = true;
    public int seed = 0;
    System.Random rnd;

    [Header("겹침 회피")]
    public bool avoidOverlap = true;
    public Vector2 overlapBoxScale = new Vector2(0.8f, 0.8f);

    [Header("추가 요소 프리팹들(폴백/양념)")]
    public GameObject[] obstaclePrefabs;
    public GameObject[] chestPrefabs;
    public GameObject[] monsterPrefabs;

    [Header("랜덤 스파이스 확률(폴백용)")]
    [Range(0f, 0.5f)] public float obstacleChance = 0.08f;
    [Range(0f, 0.2f)] public float chestChance = 0.02f;
    [Range(0f, 0.2f)] public float monsterChance = 0.03f;

    [Header("오토 빌드/안전장치")]
    public bool autoBuildOnPlay = true;
    public bool autoScanExistingOnPlay = true;

    [Header("클리어 감시자")]
    public bool enableClearWatchdog = true;
    public float watchInterval = 0.25f;

    [Header("NEW: 셸/브릭 루트 분리")]
    public Transform shellRoot;
    public Transform brickSpawnRoot;
    public Vector2 brickGridOffset = Vector2.zero;

    [Header("NEW: 셸/Zone 기반 스폰")]
    public GameObject[] wallLayoutPrefabs;
    public bool spawnByZonesFirst = true;

    [Header("셸 정렬(선택)")]
    public bool autoCenterShell = true;
    public Vector3 shellSpawnOffset = Vector3.zero;

    public enum CenterBoundsMode { AllRenderers, TilemapOnly, LayerMask }
    [Header("NEW: 셸 센터링 기준/동작")]
    public CenterBoundsMode centerBoundsMode = CenterBoundsMode.TilemapOnly;
    public LayerMask centerBoundsMask = ~0;
    public bool centerMoveChildren = true;

    [Header("NEW: 프리팹 내부 오프셋 유지")]
    public bool keepPrefabInternalOffsets = true;

    [Header("NEW: 벽 경계 자동 스캔")]
    public bool autoScanWallsForPaddle = true;
    public Transform wallsRootForScan;
    [Range(0, 6)] public int wallScanDelayFrames = 2;

    [Header("난이도(임시)")]
    public int debugLevel = 0;

    [Header("Exit Gate Spawn (엑시트 방 내부 규칙)")]
    public bool exitGateEnable = true;
    [Min(0)] public int exitGateEdgeMarginCells = 0;
    public BoxCollider2D[] exitGateAreas;

    [Header("Exit Gate Scheduling (런 단위)")]
    public DungeonRunState.ExitScheduleMode exitScheduleMode = DungeonRunState.ExitScheduleMode.ForceNthUnique;
    [Min(1)] public int forceExitAtRoomNth = 2;
    [Min(1)] public int randomExitMinNth = 3;
    [Min(1)] public int randomExitMaxNth = 6;

    [Header("Room Identity (좌표 키)")]
    public int roomKeyX = 0;
    public int roomKeyY = 0;

    [Header("씬 진입 시 런 상태 리셋")]
    public bool resetRunOnSceneStart = true;

    // 내부 상태
    int aliveBricks = 0;
    Coroutine clearWatchCR;

    bool _lastBuildWasCleared = false;

    List<BrickExitHook> _spawnedBrickHooks = new List<BrickExitHook>();
    ExitGate _exitGateInstance;

    bool _isExitRoomThisBuild = false;
    bool _hasPlannedExitGate = false;
    Vector3 _plannedExitGatePos = Vector3.zero;

    static int _sExitHookSuppressDepth = 0;
    public static bool ExitHookSuppressed => _sExitHookSuppressDepth > 0;
    static void PushExitHookSuppress() { _sExitHookSuppressDepth++; }
    static void PopExitHookSuppress() { _sExitHookSuppressDepth = Mathf.Max(0, _sExitHookSuppressDepth - 1); }

    static int sRoomIndex = 0;
    DungeonRunState.RoomKey _currentKey;

    // 디버그 HUD (외부 HUD가 읽을 수 있게 공개 Getter만 추가)
    public System.Collections.Generic.IReadOnlyList<BrickExitHook> DebugHooks => _spawnedBrickHooks;
    public bool DebugLastBuildWasCleared => _lastBuildWasCleared;
    public int DebugAliveBricks => aliveBricks;
    public ExitGate DebugExitGate => _exitGateInstance;
    public bool DebugIsExitRoomThisBuild => _isExitRoomThisBuild; // NEW
    public bool DebugHasPlannedExitGate => _hasPlannedExitGate;   // NEW
    public bool DebugExitPrefabExists => exitGatePrefab != null;  // NEW

    void Awake()
    {
        if (!brickRoot) brickRoot = transform;
        if (!shellRoot) shellRoot = transform;
        if (!brickSpawnRoot) brickSpawnRoot = brickRoot;
    }

    void Start()
    {
        if (!Application.isPlaying) return;

        // 타운 → 던전 재진입 시 런 상태 초기화
        if (resetRunOnSceneStart) DungeonRunState.Reset();

        // ---------------------------------------------------------------------
        // 중복 호출 수정: DungeonRunState.IsInitialized를 사용하여
        // NextStageButtonsBinder 등에 의한 GoNorth 호출 시 중복 빌드를 방지하고
        // 첫 씬 로드 시에만 BuildRoomSimple()이 호출되도록 변경
        // ---------------------------------------------------------------------
        if (!DungeonRunState.IsInitialized)
        {
            BuildRoomSimple();
        }
        else if (autoScanExistingOnPlay)
        {
            ForceAttachAndRecount();
        }
        // ---------------------------------------------------------------------

        if (aliveBricks > 0) StartClearWatchdog();
    }

    void StopClearWatchdog()
    {
        if (clearWatchCR != null)
        {
            StopCoroutine(clearWatchCR);
            clearWatchCR = null;
        }
    }
    void StartClearWatchdog()
    {
        if (!enableClearWatchdog) return;
        StopClearWatchdog();
        clearWatchCR = StartCoroutine(CoClearWatch());
    }

    public void BuildRoomSimple()
    {
        StopClearWatchdog();

        // 빌드 직전 UI/모달/카운트다운 정리
        var gm0 = FindObjectOfType<GameManager>();
        gm0?.DismissAllModals();
        gm0?.CancelStartCountdown();
        gm0?.SetStartUIVisible(false);

        // 인스펙터 값으로 스케줄 확정(첫 방에서만 실효, 이후 고정)
        DungeonRunState.EnsureConfigured(exitScheduleMode, forceExitAtRoomNth, randomExitMinNth, randomExitMaxNth);

        _currentKey = new DungeonRunState.RoomKey(roomKeyX, roomKeyY);

        var snap = DungeonRunState.GetOrCreateSnapshot(
            _currentKey,
            seedGen: () =>
            {
                if (randomizeSeedEachBuild && Application.isPlaying)
                    return UnityEngine.Random.Range(1, int.MaxValue);
                return (seed != 0) ? seed : UnityEngine.Random.Range(1, int.MaxValue);
            },
            shellPick: () =>
            {
                if (wallLayoutPrefabs != null && wallLayoutPrefabs.Length > 0)
                    return UnityEngine.Random.Range(0, wallLayoutPrefabs.Length);
                return -1;
            }
        );

        // 유니크 방문 보고 (여기서 중복 카운팅이 발생했었으므로 제거)
        // DungeonRunState.ReportUniqueVisit(_currentKey); // ★★★ 제거된 줄 ★★★
        rnd = (snap.seed == 0) ? new System.Random(1234567) : new System.Random(snap.seed);

        // 이전 잔재 정리
        ClearChildren(brickRoot);
        ClearChildren(shellRoot);

        _spawnedBrickHooks.Clear();
        _exitGateInstance = null;
        _isExitRoomThisBuild = false;
        _hasPlannedExitGate = false;
        _plannedExitGatePos = Vector3.zero;

        sRoomIndex++;

        GameObject shell = null;
        if (wallLayoutPrefabs != null && wallLayoutPrefabs.Length > 0 && snap.shellIndex >= 0)
        {
            var pick = wallLayoutPrefabs[Mathf.Clamp(snap.shellIndex, 0, wallLayoutPrefabs.Length - 1)];
            if (pick)
            {
                shell = Instantiate(pick,
                                         shellRoot ? shellRoot.position : Vector3.zero,
                                         Quaternion.identity,
                                         shellRoot ? shellRoot : null);

                if (!keepPrefabInternalOffsets)
                {
                    ZeroLocalGridOffsets(shell);
                    if (autoCenterShell) CenterShellTo(shellRoot ? shellRoot.position : Vector3.zero, shell);
                    if (autoCenterShell) StartCoroutine(CoCenterShellNextFrame(shell));
                }
                if (shellSpawnOffset != Vector3.zero) shell.transform.position += shellSpawnOffset;

                if (autoScanWallsForPaddle)
                    StartCoroutine(CoScanWallsAfterShellSettles(shell));
            }
        }
        else
        {
            if (autoScanWallsForPaddle)
                StartCoroutine(CoScanWallsAfterShellSettles(null));
        }

        bool wasCleared = snap.cleared;
        _lastBuildWasCleared = wasCleared;

        _isExitRoomThisBuild = DungeonRunState.IsExitRoom(_currentKey);

        var gm = FindObjectOfType<GameManager>();

        if (!wasCleared)
        {
            bool anyFromZones = false;
            if (spawnByZonesFirst && shell) anyFromZones = SpawnFromZones(shell, snap.seed);
            if (!anyFromZones)
            {
                Vector2 topLeft = CalcTopLeft();
                if (useMask && layoutMask != null) BuildFromMask(topLeft);
                else BuildGrid(topLeft);
            }

            FindObjectOfType<BrickHPAssigner>()?.AssignAll();
            ForceAttachAndRecount();

            // 출구방이면 이 시점에 '한 번만' 위치 계획
            if (_isExitRoomThisBuild && exitGateEnable)
                _hasPlannedExitGate = PlanExitGateSeed();

            if (exitDoors) exitDoors.Show(false);

            if (aliveBricks > 0) StartClearWatchdog();

            // 전투 방 준비(다음 방에서는 GameManager가 자동 카운트다운)
            gm?.PrepareCombatRoomEntry();
        }
        else
        {
            // 재방문(빈방): 탐험 모드
            aliveBricks = 0;

            // gm?.SendMessage("SetAllUIVisible", true, SendMessageOptions.DontRequireReceiver); // ★★★ 변경됨: Direct Call
            gm?.SetAllUIVisible(true);

            gm?.DismissAllModals();
            gm?.CancelStartCountdown();
            gm?.SetStartUIVisible(false);

            // 탐험 모드 컨티뉴 금지
            gm?.SetAllowContinue(false);

            if (exitDoors) exitDoors.Show(true);

            // 이미 출구방으로 선정된 방이라면, 기억된 위치로 재소환 + UI 자동
            if (_isExitRoomThisBuild && exitGateEnable)
            {
                if (DungeonRunState.TryGetExitGatePos(_currentKey, out var remembered))
                {
                    if (_exitGateInstance == null)
                        SpawnExitGateAt(remembered);

                    if (_exitGateInstance != null)
                        _exitGateInstance.OnGateActivated(); // 질문 패널 자동
                }
            }
        }
    }

    void CenterShellTo(Vector3 target, GameObject shell)
    {
        bool any = false;
        Bounds b = new Bounds(target, Vector3.zero);

        if (centerBoundsMode == CenterBoundsMode.TilemapOnly)
        {
            var tms = shell.GetComponentsInChildren<TilemapRenderer>(true);
            for (int i = 0; i < tms.Length; i++)
            {
                var tm = tms[i];
                if (!tm || !tm.enabled) continue;
                b.Encapsulate(tm.bounds);
                any = true;
            }
        }
        else
        {
            var rs = shell.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (!r || !r.enabled) continue;
                if (centerBoundsMode == CenterBoundsMode.LayerMask)
                {
                    if (((1 << r.gameObject.layer) & centerBoundsMask.value) == 0) continue;
                }
                b.Encapsulate(r.bounds);
                any = true;
            }
        }

        if (!any) return;

        Vector3 delta = target - b.center;
        if (delta.sqrMagnitude > 1_000_000f) return;

        if (centerMoveChildren)
        {
            var children = shell.transform.GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < children.Length; i++)
            {
                var t = children[i];
                if (t == shell.transform) continue;
                t.Translate(delta, Space.World);
            }
        }
        else
        {
            shell.transform.position += delta;
        }
    }

    Vector2 CalcTopLeft()
    {
        if (gridAnchor == GridAnchor.Center)
        {
            Vector2 center = brickSpawnRoot ? (Vector2)brickSpawnRoot.position : (Vector2)transform.position;
            center += brickGridOffset;
            return center + new Vector2(
                -(cols - 1) * 0.5f * cellSize.x,
                +(rows - 1) * 0.5f * cellSize.y
            );
        }
        return origin + brickGridOffset;
    }

    Vector3 GridToWorld(Vector2 topLeft, int r, int c)
    {
        return new Vector3(
            topLeft.x + c * cellSize.x,
            topLeft.y - r * cellSize.y,
            0f
        );
    }

    Vector2 CellOverlapSize()
    {
        return new Vector2(cellSize.x * Mathf.Clamp01(overlapBoxScale.x),
                             cellSize.y * Mathf.Clamp01(overlapBoxScale.y));
    }

    Vector3 Jitter(Vector3 pos)
    {
        if (jitter <= 0f || rnd == null) return pos;
        float jx = ((float)rnd.NextDouble() * 2f - 1f) * jitter;
        float jy = ((float)rnd.NextDouble() * 2f - 1f) * jitter;
        return pos + new Vector3(jx, jy, 0f);
    }

    void BuildGrid(Vector2 topLeft)
    {
        var candidates = new List<Vector3>();
        int colEnd = mirrorX ? (cols + 1) / 2 : cols;

        float ox = (float)rnd.NextDouble() * 1000f;
        float oy = (float)rnd.NextDouble() * 1000f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < colEnd; c++)
            {
                Vector3 pos = GridToWorld(topLeft, r, c);

                if (usePerlinFill)
                {
                    float u = (c + 0.5f) / Mathf.Max(1, cols);
                    float v = (r + 0.5f) / Mathf.Max(1, rows);
                    float n = Mathf.PerlinNoise(u * noiseScale + ox, v * noiseScale + oy);
                    if (n > fill) continue;
                }

                if (avoidOverlap && Physics2D.OverlapBox(pos, CellOverlapSize(), 0f)) continue;

                candidates.Add(pos);

                if (mirrorX)
                {
                    int mc = cols - 1 - c;
                    if (mc != c)
                    {
                        Vector3 mpos = GridToWorld(topLeft, r, mc);
                        if (!avoidOverlap || !Physics2D.OverlapBox(mpos, CellOverlapSize(), 0f))
                            candidates.Add(mpos);
                    }
                }
            }
        }

        if (mirrorY && candidates.Count > 0)
        {
            float top = topLeft.y;
            float bottom = topLeft.y - (rows - 1) * cellSize.y;
            float centerY = (top + bottom) * 0.5f;

            int initial = candidates.Count;
            for (int i = 0; i < initial; i++)
            {
                var p = candidates[i];
                float my = centerY - (p.y - centerY);
                var mp = new Vector3(p.x, my, p.z);
                if (!avoidOverlap || !Physics2D.OverlapBox(mp, CellOverlapSize(), 0f))
                    candidates.Add(mp);
            }
        }

        Shuffle(candidates);

        int want = candidates.Count;
        if (useTargetCount)
        {
            int min = Mathf.Clamp(minBricks, 0, candidates.Count);
            int max = Mathf.Clamp(maxBricks, min, candidates.Count);
            want = rnd.Next(min, max + 1);
        }

        aliveBricks = 0;
        for (int i = 0; i < want; i++)
        {
            Vector3 p = Jitter(candidates[i]);
            if (TrySpawnRandomExtra(p)) continue;
            SpawnBrick(p);
        }
    }

    void BuildFromMask(Vector2 topLeft)
    {
        if (!layoutMask) { BuildGrid(topLeft); return; }

        var brickCand = new List<Vector3>();
        var extras = new List<(Vector3 pos, int kind)>();

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                Vector3 pos = GridToWorld(topLeft, r, c);

                float u = (c + 0.5f) / Mathf.Max(1, cols);
                float v = (r + 0.5f) / Mathf.Max(1, rows);
                Color m = layoutMask.GetPixelBilinear(u, v);
                if (m.maxColorComponent < 0.05f) continue;

                if (avoidOverlap && Physics2D.OverlapBox(pos, CellOverlapSize(), 0f)) continue;

                if (m.r > 0.7f && m.g < 0.3f) { extras.Add((Jitter(pos), 0)); continue; }
                if (m.r > 0.7f && m.g > 0.7f && m.b < 0.3f) { extras.Add((Jitter(pos), 1)); continue; }
                if (m.b > 0.7f && m.r < 0.3f) { extras.Add((Jitter(pos), 2)); continue; }
                if (m.grayscale >= 0.8f) brickCand.Add(Jitter(pos));
            }

        foreach (var e in extras) SpawnExtraByKind(e.pos, e.kind);

        Shuffle(brickCand);
        int want = brickCand.Count;
        if (useTargetCount)
        {
            int min = Mathf.Clamp(minBricks, 0, brickCand.Count);
            int max = Mathf.Clamp(maxBricks, min, brickCand.Count);
            want = rnd.Next(min, max + 1);
        }

        aliveBricks = 0;
        for (int i = 0; i < want; i++)
            SpawnBrick(brickCand[i]);
    }

    bool SpawnFromZones(GameObject shell, int seedForZones)
    {
        var zones = shell.GetComponentsInChildren<ZoneVolume>(true);
        if (zones == null || zones.Length == 0) return false;

        var zrnd = new System.Random(seedForZones ^ 0x5A5A5A5A);
        int level = Mathf.Max(0, debugLevel);

        int spawnedBricks = 0;

        foreach (var z in zones)
        {
            int count = z.ResolveCount(level);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = z.SamplePointInside(zrnd);
                if (avoidOverlap && Physics2D.OverlapBox(p, CellOverlapSize(), 0f))
                    continue;

                int kind = z.RollKind(zrnd);
                switch (kind)
                {
                    case 0: SpawnBrick(p); spawnedBricks++; break;
                    case 1: if (monsterPrefabs != null && monsterPrefabs.Length > 0) Instantiate(Pick(monsterPrefabs), p, Quaternion.identity, brickRoot); break;
                    case 2: if (chestPrefabs != null && chestPrefabs.Length > 0) Instantiate(Pick(chestPrefabs), p, Quaternion.identity, brickRoot); break;
                    case 3: if (monsterPrefabs != null && monsterPrefabs.Length > 0) Instantiate(Pick(monsterPrefabs), p, Quaternion.identity, brickRoot); break;
                }
            }
        }
        return spawnedBricks > 0;
    }

    void SpawnBrick(Vector3 pos)
    {
        if (!brickPrefab) return;
        var go = Instantiate(brickPrefab, pos, Quaternion.identity, brickRoot);
        var br = go.GetComponent<Brick>();
        if (br) { br.Init(this); aliveBricks++; }

        var hook = go.GetComponent<BrickExitHook>();
        if (!hook) hook = go.AddComponent<BrickExitHook>();
        hook.Init(this);
        _spawnedBrickHooks.Add(hook);
    }

    bool TrySpawnRandomExtra(Vector3 pos)
    {
        double roll = rnd.NextDouble();
        if (obstaclePrefabs != null && obstaclePrefabs.Length > 0 && roll < obstacleChance)
        {
            Instantiate(Pick(obstaclePrefabs), pos, Quaternion.identity, brickRoot);
            return true;
        }
        roll -= obstacleChance;
        if (chestPrefabs != null && chestPrefabs.Length > 0 && roll < chestChance)
        {
            Instantiate(Pick(chestPrefabs), pos, Quaternion.identity, brickRoot);
            return true;
        }
        roll -= chestChance;
        if (monsterPrefabs != null && monsterPrefabs.Length > 0 && roll < monsterChance)
        {
            Instantiate(Pick(monsterPrefabs), pos, Quaternion.identity, brickRoot);
            return true;
        }
        return false;
    }

    void SpawnExtraByKind(Vector3 pos, int kind)
    {
        if (kind == 0 && obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            Instantiate(Pick(obstaclePrefabs), pos, Quaternion.identity, brickRoot);
        else if (kind == 1 && chestPrefabs != null && chestPrefabs.Length > 0)
            Instantiate(Pick(chestPrefabs), pos, Quaternion.identity, brickRoot);
        else if (kind == 2 && monsterPrefabs != null && monsterPrefabs.Length > 0)
            Instantiate(Pick(monsterPrefabs), pos, Quaternion.identity, brickRoot);
    }

    GameObject Pick(GameObject[] arr)
    {
        if (arr == null || arr.Length == 0) return null;
        int i = rnd.Next(0, arr.Length);
        return arr[i];
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; --i)
        {
            int j = rnd.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void ClearChildren(Transform root)
    {
        if (!root) return;

        bool suppressNow = (root == brickRoot);
        if (suppressNow) PushExitHookSuppress();

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        if (root == brickRoot) aliveBricks = 0;

        if (suppressNow) PopExitHookSuppress();
    }

    void ForceAttachAndRecount()
    {
        if (!brickRoot) brickRoot = transform;
        int cnt = 0;
        var bricks = brickRoot.GetComponentsInChildren<Brick>(true);
        foreach (var b in bricks)
        {
            if (!b || !b.gameObject.activeInHierarchy) continue;
            b.Init(this);
            cnt++;

            var h = b.GetComponent<BrickExitHook>();
            if (!h) h = b.gameObject.AddComponent<BrickExitHook>();
            h.Init(this);
            if (!_spawnedBrickHooks.Contains(h)) _spawnedBrickHooks.Add(h);
        }
        aliveBricks = cnt;
    }

    int RecountAliveBricks()
    {
        if (!brickRoot) brickRoot = transform;
        int cnt = 0;
        var bricks = brickRoot.GetComponentsInChildren<Brick>(false);
        foreach (var b in bricks) if (b && b.gameObject.activeInHierarchy) cnt++;
        return cnt;
    }

    public void NotifyBrickDestroyed()
    {
        aliveBricks = Mathf.Max(0, aliveBricks - 1);
        if (aliveBricks > 0) return;

        int real = RecountAliveBricks();
        if (real > 0) { aliveBricks = real; return; }

        OpenExitAndNotifyClear();
    }

    Coroutine _coOpenExit;
    void OpenExitAndNotifyClear()
    {
        if (_coOpenExit != null) StopCoroutine(_coOpenExit);
        _coOpenExit = StartCoroutine(CoOpenExitAfterCinematic());
    }

    IEnumerator CoOpenExitAfterCinematic()
    {
        var gm = FindObjectOfType<GameManager>();

        // gm?.SendMessage("SetAllUIVisible", false, SendMessageOptions.DontRequireReceiver); // ★★★ 변경됨: Direct Call
        gm?.SetAllUIVisible(false);

        yield return new WaitForEndOfFrame();
        while (CinematicFX.I != null && CinematicFX.IsPlaying) yield return null;

        // gm?.SendMessage("SetAllUIVisible", true, SendMessageOptions.DontRequireReceiver); // ★★★ 변경됨: Direct Call
        gm?.SetAllUIVisible(true);

        TMP_Text savedNext = null;
        bool gatePlanned = (_isExitRoomThisBuild && exitGateEnable && _hasPlannedExitGate);
        if (gatePlanned && gm)
        {
            savedNext = gm.nextStageText;
            gm.nextStageText = null;
        }

        DungeonRunState.MarkCleared(_currentKey);

        gm?.OnStageClear();

        if (gatePlanned && exitDoors) exitDoors.Show(false);

        if (gatePlanned)
        {
            // 계획된 좌표만 사용 (클리어 시 재계획 없음)
            if (_exitGateInstance == null && exitGatePrefab)
                SpawnExitGateAt(_plannedExitGatePos);

            if (_exitGateInstance != null)
            {
                DungeonRunState.RememberExitGatePos(_currentKey, _plannedExitGatePos);
                _exitGateInstance.OnGateActivated();
            }

            if (gm) gm.nextStageText = savedNext;
        }

        _coOpenExit = null;
    }

    // 방향 버튼
    public void GoNorth() { roomKeyY += 1; MoveAndMaybeAutoStart(); }
    public void GoEast() { roomKeyX += 1; MoveAndMaybeAutoStart(); }
    public void GoSouth() { roomKeyY -= 1; MoveAndMaybeAutoStart(); }
    public void GoWest() { roomKeyX -= 1; MoveAndMaybeAutoStart(); }

    void MoveAndMaybeAutoStart()
    {
        var gm = FindObjectOfType<GameManager>();
        var bg = FindObjectOfType<BackgroundManager>();

        BuildRoomSimple();     // 새 방 빌드
        bg?.NextRoom();

        // 방이 전투방이면 즉시 카운트다운 시작(스타트 버튼 없이)
        if (!DebugLastBuildWasCleared)
            gm?.OnNextRoomEntered();   // 자동 카운트다운
    }

    IEnumerator CoClearWatch()
    {
        var wait = new WaitForSeconds(watchInterval);
        while (true)
        {
            int real = RecountAliveBricks();
            if (real != aliveBricks) aliveBricks = real;
            if (real == 0)
            {
                OpenExitAndNotifyClear();
                clearWatchCR = null;
                yield break;
            }
            yield return wait;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (shellRoot) { Gizmos.color = Color.cyan; Gizmos.DrawSphere(shellRoot.position, 0.12f); }
        if (brickSpawnRoot) { Gizmos.color = Color.yellow; Gizmos.DrawSphere(brickSpawnRoot.position, 0.12f); }
    }
#endif

    // 내부 오프셋 0화
    void ZeroLocalGridOffsets(GameObject shellGO)
    {
        if (!shellGO) return;

        var tfs = shellGO.GetComponentsInChildren<Transform>(true);
        foreach (var tf in tfs)
        {
            if (!tf) continue;
            if (tf.GetComponent<Grid>() || tf.GetComponent<Tilemap>())
            {
                tf.localPosition = Vector3.zero;
                tf.localRotation = Quaternion.identity;
            }
        }
        var trs = shellGO.GetComponentsInChildren<TilemapRenderer>(true);
        foreach (var tr in trs) if (tr) tr.enabled = true;
    }

    // 다음 프레임 재센터링
    IEnumerator CoCenterShellNextFrame(GameObject shell)
    {
        yield return null;
        if (!shell) yield break;
        Vector3 target = shellRoot ? shellRoot.position : Vector3.zero;
        CenterShellTo(target, shell);
    }

    // 셸 자리 잡은 뒤 벽 경계 지연 스캔
    IEnumerator CoScanWallsAfterShellSettles(GameObject shell)
    {
        int wait = Mathf.Max(1, wallScanDelayFrames);
        for (int i = 0; i < wait; i++) yield return null;

        Transform targetRoot = wallsRootForScan ? wallsRootForScan
                             : (shell ? shell.transform : null);

        if (!targetRoot)
        {
            var anyTileCol = FindObjectOfType<TilemapCollider2D>();
            if (anyTileCol) targetRoot = anyTileCol.transform.root;
        }

        var pc = FindObjectOfType<PaddleController>();
        if (!pc || !targetRoot) yield break;

        pc.RecalcWallEdges(targetRoot);
    }

    bool PlanExitGateSeed()
    {
        if (_spawnedBrickHooks.Count == 0) return false;

        var cand = new List<BrickExitHook>(_spawnedBrickHooks);

        if (exitGateEdgeMarginCells > 0)
        {
            Vector2 topLeft = CalcTopLeft();
            float left = topLeft.x + exitGateEdgeMarginCells * cellSize.x;
            float right = topLeft.x + (cols - 1 - exitGateEdgeMarginCells) * cellSize.x;
            float top = topLeft.y - exitGateEdgeMarginCells * cellSize.y;
            float bottom = topLeft.y - (rows - 1 - exitGateEdgeMarginCells) * cellSize.y;

            cand.RemoveAll(h =>
            {
                var p = h.transform.position;
                return !(p.x >= left && p.x <= right && p.y <= top && p.y >= bottom);
            });
        }

        if (exitGateAreas != null && exitGateAreas.Length > 0)
        {
            cand.RemoveAll(h =>
            {
                var p = (Vector2)h.transform.position;
                bool inside = false;
                for (int i = 0; i < exitGateAreas.Length; i++)
                {
                    var bc = exitGateAreas[i];
                    if (!bc) continue;
                    var b = bc.bounds;
                    if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y) { inside = true; break; }
                }
                return !inside;
            });
        }

        if (cand.Count == 0) cand = _spawnedBrickHooks;

        // ★★★ 변경됨: UnityEngine.Random -> System.Random (rnd)를 사용하여 시드 기반 결정론적 위치 선택
        var pick = cand[rnd.Next(0, cand.Count)];

        _plannedExitGatePos = pick.transform.position; // 위치만 기억
        return true;
    }

    public ExitGate SpawnExitGateAt(Vector3 pos)
    {
        if (!exitGatePrefab) return null;
        var eg = Instantiate(exitGatePrefab, pos, Quaternion.identity, brickRoot);
        _exitGateInstance = eg;
        DungeonRunState.RememberExitGatePos(_currentKey, pos);
        return eg;
    }
}
