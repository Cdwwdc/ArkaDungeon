using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps; // TilemapRenderer 사용

public class RoomController : MonoBehaviour
{
    [Header("루트/브릭 프리팹")]
    public Transform brickRoot;           // BricksContainer (없으면 this)
    public GameObject brickPrefab;        // Brick.cs 포함 필수

    [Header("출구(선택)")]
    public ExitDoors exitDoors;           // 없어도 동작

    [Header("격자 기본(폴백용)")]
    public int cols = 12;
    public int rows = 7;
    public Vector2 cellSize = new Vector2(0.9f, 0.6f);

    public enum GridAnchor { TopLeft, Center }
    [Tooltip("Center: brickSpawnRoot.position을 격자 중앙으로 사용")]
    public GridAnchor gridAnchor = GridAnchor.Center;

    [Tooltip("TopLeft 모드에서 사용되는 좌상단 월드 좌표")]
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
    public GameObject[] obstaclePrefabs; // (Brick.cs 없어야 함)
    public GameObject[] chestPrefabs;
    public GameObject[] monsterPrefabs;

    [Header("랜덤 스파이스 확률(폴백용)")]
    [Range(0f, 0.5f)] public float obstacleChance = 0.08f;
    [Range(0f, 0.2f)] public float chestChance = 0.02f;
    [Range(0f, 0.2f)] public float monsterChance = 0.03f;

    [Header("오토 빌드/안전장치")]
    public bool autoBuildOnPlay = true;
    public bool autoScanExistingOnPlay = true;

    [Header("클리어 감시자(신호 유실 대비)")]
    public bool enableClearWatchdog = true;
    public float watchInterval = 0.25f;

    // ---------------- NEW: 루트 분리/스폰 기준 분리 ----------------
    [Header("NEW: 셸/브릭 루트 분리")]
    [Tooltip("셸(벽 레이아웃) 프리팹이 붙을 루트. 기본=RoomController 오브젝트")]
    public Transform shellRoot;
    [Tooltip("브릭 격자/Zone 스폰의 '중심' 기준점. 기본=brickRoot")]
    public Transform brickSpawnRoot;
    [Tooltip("브릭 격자 전체에 추가할 오프셋(미세조정)")]
    public Vector2 brickGridOffset = Vector2.zero;

    [Header("NEW: 셸/Zone 기반 스폰")]
    [Tooltip("테마별 벽(셸) 프리팹 후보들")]
    public GameObject[] wallLayoutPrefabs;
    [Tooltip("셸에 ZoneVolume이 있으면 그 설정을 우선 사용")]
    public bool spawnByZonesFirst = true;

    [Header("셸 정렬(선택)")]
    [Tooltip("셸 프리팹 내용물의 중심을 shellRoot.position에 자동 정렬")]
    public bool autoCenterShell = true;
    public Vector3 shellSpawnOffset = Vector3.zero;

    // === 센터링 기준/동작 (이전 그대로 유지) ===
    public enum CenterBoundsMode { AllRenderers, TilemapOnly, LayerMask }

    [Header("NEW: 셸 센터링 기준/동작")]
    [Tooltip("TilemapOnly 권장: 거대한 BG Sprite 등으로 인한 바운즈 튐 방지")]
    public CenterBoundsMode centerBoundsMode = CenterBoundsMode.TilemapOnly;

    [Tooltip("LayerMask 모드에서만 사용: 포함할 레이어")]
    public LayerMask centerBoundsMask = ~0;

    [Tooltip("true면 셸 부모는 고정, 자식만 이동하여 중앙 정렬")]
    public bool centerMoveChildren = true;

    // === ★ 이번 핀셋 추가: 내부 오프셋 보존 옵션 ===
    [Header("NEW: 프리팹 내부 오프셋 유지")]
    [Tooltip("켜면 Grid/Tilemap 등의 자식 로컬 오프셋을 그대로 둡니다.")]
    public bool keepPrefabInternalOffsets = true;

    [Header("난이도(외부 연동 전까지 임시)")]
    public int debugLevel = 0;

    // 내부 상태
    int aliveBricks = 0;
    Coroutine clearWatchCR;

    void Awake()
    {
        if (!brickRoot) brickRoot = transform;
        if (!shellRoot) shellRoot = transform;
        if (!brickSpawnRoot) brickSpawnRoot = brickRoot; // ← 브릭 스폰 기준점
    }

    void Start()
    {
        if (!Application.isPlaying) return;

        if (autoBuildOnPlay) BuildRoomSimple();
        else if (autoScanExistingOnPlay) ForceAttachAndRecount();

        if (aliveBricks == 0) OpenExitAndNotifyClear();
        StartClearWatchdog();
    }

    // === 외부에서 호출되는 간단 빌드(기존 시그니처 유지) ===
    public void BuildRoomSimple()
    {
        // 브릭과 셸을 '각자' 비움
        ClearChildren(brickRoot);
        ClearChildren(shellRoot);

        // 시드
        if (randomizeSeedEachBuild && Application.isPlaying)
            seed = UnityEngine.Random.Range(1, int.MaxValue);
        rnd = (seed == 0) ? new System.Random() : new System.Random(seed);

        // 셸(벽) 배치 → **항상 shellRoot 기준**
        GameObject shell = null;
        if (wallLayoutPrefabs != null && wallLayoutPrefabs.Length > 0)
        {
            var pick = wallLayoutPrefabs[UnityEngine.Random.Range(0, wallLayoutPrefabs.Length)];
            if (pick)
            {
                shell = Instantiate(pick,
                                    shellRoot ? shellRoot.position : Vector3.zero,
                                    Quaternion.identity,
                                    shellRoot ? shellRoot : null);

                // ▼▼▼ 이번 변경: 내부 오프셋/센터링은 '옵션이 꺼진 경우'에만 실행
                if (!keepPrefabInternalOffsets)
                {
                    ZeroLocalGridOffsets(shell);
                    if (autoCenterShell) CenterShellTo(shellRoot ? shellRoot.position : Vector3.zero, shell);
                    if (autoCenterShell) StartCoroutine(CoCenterShellNextFrame(shell));
                }
                // ▲▲▲

                // 필요 시 루트 자체 오프셋
                if (shellSpawnOffset != Vector3.zero) shell.transform.position += shellSpawnOffset;
            }
        }

        // Zone 우선 스폰(브릭은 brickSpawnRoot 기준)
        bool anyFromZones = false;
        if (spawnByZonesFirst && shell)
            anyFromZones = SpawnFromZones(shell);

        // 폴백 그리드
        if (!anyFromZones)
        {
            Vector2 topLeft = CalcTopLeft();
            if (useMask && layoutMask != null) BuildFromMask(topLeft);
            else BuildGrid(topLeft);
        }

        // HP/색 적용
        FindObjectOfType<BrickHPAssigner>()?.AssignAll();

        // room 참조 보정
        ForceAttachAndRecount();

        if (exitDoors) exitDoors.Show(false);
        StartClearWatchdog();
    }

    // === 셸 내용물 중심을 target으로 자동 정렬(보강) ===
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

        // 과도한 이동 가드
        if (delta.sqrMagnitude > 1000f * 1000f) return;

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

    // === 위치 계산/도우미 ===
    Vector2 CalcTopLeft()
    {
        if (gridAnchor == GridAnchor.Center)
        {
            Vector2 center = brickSpawnRoot ? (Vector2)brickSpawnRoot.position : (Vector2)transform.position;
            center += brickGridOffset; // 미세 오프셋
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

    // === 빌드: 일반 그리드(폴백) ===
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

    // === 빌드: 마스크(폴백) ===
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

    // === Zone 우선 스폰 ===
    bool SpawnFromZones(GameObject shell)
    {
        var zones = shell.GetComponentsInChildren<ZoneVolume>(true);
        if (zones == null || zones.Length == 0) return false;

        var zrnd = new System.Random(UnityEngine.Random.Range(1, int.MaxValue));
        int level = Mathf.Max(0, debugLevel);

        int spawned = 0;
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
                    case 0: SpawnBrick(p); break;
                    case 1:
                        if (monsterPrefabs != null && monsterPrefabs.Length > 0)
                            Instantiate(Pick(monsterPrefabs), p, Quaternion.identity, brickRoot);
                        break;
                    case 2:
                        if (chestPrefabs != null && chestPrefabs.Length > 0)
                            Instantiate(Pick(chestPrefabs), p, Quaternion.identity, brickRoot);
                        break;
                    case 3:
                        if (monsterPrefabs != null && monsterPrefabs.Length > 0)
                            Instantiate(Pick(monsterPrefabs), p, Quaternion.identity, brickRoot);
                        break;
                }
                spawned++;
            }
        }
        return spawned > 0;
    }

    // === 스폰/유틸 ===
    void SpawnBrick(Vector3 pos)
    {
        if (!brickPrefab) return;
        var go = Instantiate(brickPrefab, pos, Quaternion.identity, brickRoot);
        var br = go.GetComponent<Brick>();
        if (br)
        {
            br.Init(this);
            aliveBricks++;
        }
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

    // === 정리/보정 ===
    void ClearChildren(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        if (root == brickRoot) aliveBricks = 0;
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

    // === Brick -> Room 알림 ===
    public void NotifyBrickDestroyed()
    {
        aliveBricks = Mathf.Max(0, aliveBricks - 1);
        if (aliveBricks > 0) return;

        int real = RecountAliveBricks();
        if (real > 0) { aliveBricks = real; return; }

        OpenExitAndNotifyClear();
    }

    void OpenExitAndNotifyClear()
    {
        if (exitDoors) exitDoors.Show(true);
        var gm = FindObjectOfType<GameManager>();
        gm?.OnStageClear();
    }

    // === 출구 버튼(N/E/S/W) ===
    public void GoNorth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoEast() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoSouth() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }
    public void GoWest() { BuildRoomSimple(); FindObjectOfType<GameManager>()?.OnNextRoomEntered(); FindObjectOfType<BackgroundManager>()?.NextRoom(); }

    // === 클리어 감시자 ===
    void StartClearWatchdog()
    {
        if (!enableClearWatchdog) return;
        if (clearWatchCR != null) StopCoroutine(clearWatchCR);
        clearWatchCR = StartCoroutine(CoClearWatch());
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

    [ContextMenu("Rebuild (Simple)")]
    void EditorRebuild() { BuildRoomSimple(); }

    // =====================[ 진단/보정 유틸 ]=====================

    // 내부 오프셋 0화(옵션 OFF일 때만 사용)
    void ZeroLocalGridOffsets(GameObject shellGO)
    {
        var tfs = shellGO.GetComponentsInChildren<Transform>(true);
        foreach (var tf in tfs)
        {
            if (tf.GetComponent<Grid>() || tf.GetComponent<Tilemap>())
            {
                tf.localPosition = Vector3.zero;
                tf.localRotation = Quaternion.identity;
                // tf.localScale = Vector3.one;
            }
        }
        var trs = shellGO.GetComponentsInChildren<TilemapRenderer>(true);
        foreach (var tr in trs) tr.enabled = true;
    }

    // 다음 프레임 재센터링(옵션 OFF일 때만 사용)
    IEnumerator CoCenterShellNextFrame(GameObject shell)
    {
        yield return null;
        if (!shell) yield break;
        Vector3 target = shellRoot ? shellRoot.position : Vector3.zero;
        CenterShellTo(target, shell);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (shellRoot)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(shellRoot.position, 0.12f);
        }
        if (brickSpawnRoot)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(brickSpawnRoot.position, 0.12f);
        }
    }

    [ContextMenu("DEBUG/Dump Renderers Under Shell")]
    void DebugDumpShellInfo()
    {
        if (!shellRoot || shellRoot.childCount == 0)
        {
            Debug.Log("[RoomController] shellRoot 밑에 셸 인스턴스가 없습니다.");
            return;
        }

        var shell = shellRoot.GetChild(shellRoot.childCount - 1).gameObject;
        var rs = shell.GetComponentsInChildren<Renderer>(true);
        System.Array.Sort(rs, (a, b) => b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));

        Debug.Log($"[DumpRenderers] total={rs.Length} (큰 순서)");
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            var sz = r.bounds.size;
            var ct = r.bounds.center;
            Debug.Log($"  #{i:00} {r.GetType().Name} path={GetHierarchyPath(r.transform)} size={sz} center={ct}");
        }
    }

    string GetHierarchyPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack);
    }
#endif
}
