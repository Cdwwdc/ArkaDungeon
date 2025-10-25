using UnityEngine;

// ▣ 패들 좌우 이동 + 반사 각도 보정 + 자식 포함 폭으로 클램프
public class PaddleController : MonoBehaviour
{
    [Header("이동 범위(X 최소/최대) (벽 Transform이 없을 때만 사용)")]
    [Tooltip("좌우 벽 Transform이 지정되지 않았을 때 사용할 폴백 최소 X")]
    public float minX = -5f;
    [Tooltip("좌우 벽 Transform이 지정되지 않았을 때 사용할 폴백 최대 X")]
    public float maxX = 5f;

    [Header("좌/우 벽(옵션): 있으면 이 사이로만 이동")]
    [Tooltip("왼쪽 벽 Transform(가능하면 실제 벽/타일의 콜라이더가 포함된 오브젝트를 지정)")]
    public Transform leftWall;
    [Tooltip("오른쪽 벽 Transform(가능하면 실제 벽/타일의 콜라이더가 포함된 오브젝트를 지정)")]
    public Transform rightWall;

    [SerializeField, Tooltip("씬에서 이름으로 좌/우 벽 Transform을 자동 탐색할지 여부 (Start에서 1회만 실행됨)")]
    bool preferWallTransforms = true;
    [SerializeField, Tooltip("왼쪽 벽 자동 탐색 시 사용할 오브젝트 이름")]
    string leftWallName = "WallLeft";
    [SerializeField, Tooltip("오른쪽 벽 자동 탐색 시 사용할 오브젝트 이름")]
    string rightWallName = "WallRight";

    [Header("이동 속도(좌/우키 또는 마우스)")]
    [Tooltip("키보드 이동 시 초당 이동 속도. 마우스/스와이프 조작에서는 반응성 보간 계수로 사용")]
    public float moveSpeed = 12f;

    [Header("패들 상면 반사 각 조절 (최대 각도)")]
    [Tooltip("패들 중앙=수직, 양 끝=±이 값(도)으로 반사")]
    public float maxBounceAngle = 60f; // 도 단위

    [Header("반사 튜닝")]
    [Tooltip("패들의 수평 속도를 반사각 X성분에 얼마나 반영할지(0~1% 단위 권장)")]
    public float paddleVelInfluence = 0.20f;
    [Tooltip("반드시 이만큼은 위/아래로 튀도록 보장하는 최소 수직 비중(0.35~0.45 권장)")]
    public float minVerticalDot = 0.40f; // 0.35~0.45 권장
    [Tooltip("반사 후 속도 클램프(최소/최대)")]
    public float minSpeed = 6f, maxSpeed = 14f;

    // ==== 클램프 폭 계산 전용 옵션 ====
    [Header("클램프 폭(선택)")]
    [Tooltip("패들의 '몸통'만 가진 전용 Collider2D. 지정되면 자식 합산 대신 이 폭만 사용(무기/장식 제외)")]
    public Collider2D clampBoundsCollider;

    [Tooltip("자식 바운즈 합산 시 포함할 레이어 마스크(0이면 전체 포함)")]
    public LayerMask clampBoundsLayerMask; // 0이면 필터 없음

    // ==== 벽 '안쪽면' 접촉 계산 옵션 ====
    [Header("벽 안쪽면 접촉 보정")]
    [Tooltip("좌/우 벽의 Collider2D 바운즈로 한계선을 계산(권장). 꺼지면 Transform.x 중심선을 사용")]
    public bool useWallColliderEdges = true;

    [Tooltip("겹침/틈 방지용 아주 얇은 여유. +왼쪽, -오른쪽 방향으로 각각 적용(0.0~0.03 권장)")]
    public float wallContactSkin = 0.01f;

    // ==== 자동 스캔 수신 ====
    [Header("자동 스캔 수신(읽기 전용/디버그)")]
    [Tooltip("RoomController가 방 생성 후 호출하는 자동 스캔 결과를 사용")]
    public bool useScannedEdges = false;
    [Tooltip("스캔된 좌/우 '안쪽면' X(읽기용)")]
    public float scannedLeftInnerX, scannedRightInnerX;
    [Tooltip("스캔 Gizmo 표시")]
    public bool debugShowScannedGizmos = false;

    // ==== 스와이프 게인(상대 이동) 옵션 ====
    [Header("스와이프 게인(상대 이동)")]
    [Tooltip("ON이면 마우스/스와이프 이동량 × 게인만큼 패들이 움직여서 화면 끝까지 드래그할 필요가 줄어듭니다.")]
    [SerializeField] bool useRelativeSwipe = true;   // 기본 ON 권장
    [Tooltip("스와이프 게인 배율 (2~3부터 추천)")]
    [SerializeField, Range(0.5f, 8f)] float swipeGain = 2.0f;
    [Tooltip("해상도 독립: 화면 너비 비율로 계산(모바일/다양한 해상도에 일관)")]
    [SerializeField] bool resolutionIndependent = true;

    // === 최적화/상태 변수 ===
    float paddleHalfWidth; // 패들 폭의 절반 캐시
    float xVel;            // 반사 계산용 패들 x속도
    bool _inputEnabled = true;

    // 드래그 상태(상대 스와이프 모드 전용)
    float _dragStartPaddleX;
    float _dragStartMouseX;     // 화면 픽셀 X
    float _dragPrevWorldMouseX; // 월드 X (해상도 의존 모드에서 사용)
    bool _dragging;             // ← 경고 제거: 실제 분기에서 사용

    public void SetInputEnabled(bool enabled) { _inputEnabled = enabled; }

    void Start()
    {
        // 1) 패들 폭 캐시
        paddleHalfWidth = ComputeHalfWidthFromChildrenInternal();

        // 2) 벽 Transform 자동 탐색(옵션)
        if (preferWallTransforms)
        {
            if (!leftWall) leftWall = FindActiveByName(leftWallName);
            if (!rightWall) rightWall = FindActiveByName(rightWallName);
        }

        // 3) 시작 시 안전 클램프
        ResolveLimits(paddleHalfWidth, out float leftLimit, out float rightLimit);
        float clampedX = Mathf.Clamp(transform.position.x, leftLimit, rightLimit);
        if (!Mathf.Approximately(clampedX, transform.position.x))
            transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
    }

    // Update 대신 LateUpdate: 다른 업데이트(물리 포함) 이후 최종 확정
    void LateUpdate()
    {
        if (!_inputEnabled) return;

        // 한계 계산
        ResolveLimits(paddleHalfWidth, out float leftLimit, out float rightLimit);
        if (leftLimit > rightLimit)
        {
            // 잘못 스캔/설정 시 폴백
            leftLimit = Mathf.Min(minX + paddleHalfWidth, maxX - paddleHalfWidth);
            rightLimit = Mathf.Max(minX + paddleHalfWidth, maxX - paddleHalfWidth);
        }

        // --- 입력 처리: targetX 산출 ---
        float targetX = transform.position.x;

        // 드래그 시작/종료 감지 (마우스 왼쪽 버튼 기준)
        if (Input.GetMouseButtonDown(0))
        {
            _dragging = true;
            _dragStartPaddleX = transform.position.x;
            _dragStartMouseX = Input.mousePosition.x;
            _dragPrevWorldMouseX = Camera.main.ScreenToWorldPoint(Input.mousePosition).x;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _dragging = false;
        }

        if (_dragging) // ← 여기서 실제 사용 (이전: Input.GetMouseButton(0))
        {
            if (useRelativeSwipe)
            {
                // ■ 상대 스와이프: "이동량 × 게인" → 짧은 손동작으로 큰 이동
                if (resolutionIndependent)
                {
                    // 해상도 독립: 화면 너비 비율로 델타 계산
                    float dxPixels = Input.mousePosition.x - _dragStartMouseX;
                    float dxNorm = dxPixels / Mathf.Max(1f, (float)Screen.width); // -1..+1
                    float range = (rightLimit - leftLimit);
                    targetX = _dragStartPaddleX + dxNorm * range * swipeGain;
                }
                else
                {
                    // 해상도 의존: 월드 X 델타 사용(직관적, PC에선 충분히 자연스러움)
                    float curWorldX = Camera.main.ScreenToWorldPoint(Input.mousePosition).x;
                    float dWorld = curWorldX - _dragPrevWorldMouseX;
                    targetX = transform.position.x + dWorld * swipeGain;
                    _dragPrevWorldMouseX = curWorldX; // 프레임 누적 기준점 갱신
                }
            }
            else
            {
                // ■ 기존 절대 추적(보존): 마우스 월드 X로 따라가기 (moveSpeed는 반응성)
                Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                float mouseTargetX = m.x;
                float distanceToMouse = mouseTargetX - transform.position.x;
                targetX = transform.position.x + distanceToMouse * moveSpeed * Time.deltaTime;
            }
        }
        else
        {
            // ■ 키보드 입력(보존)
            float h = Input.GetAxisRaw("Horizontal");
            targetX = transform.position.x + h * moveSpeed * Time.deltaTime;
        }

        // 부드럽게 보간(반응성 튜닝: moveSpeed 사용)
        float responsiveX = Mathf.Lerp(transform.position.x, targetX, moveSpeed * Time.deltaTime);

        // 한계 클램프 + 경계면 떨림 제거
        float nextX = Mathf.Clamp(responsiveX, leftLimit, rightLimit);
        const float TOL = 0.00001f;
        if (Mathf.Abs(nextX - rightLimit) < TOL) nextX = rightLimit;
        else if (Mathf.Abs(nextX - leftLimit) < TOL) nextX = leftLimit;

        // 이동 적용 + xVel 계산(반사 시 사용)
        xVel = (nextX - transform.position.x) / Mathf.Max(Time.deltaTime, 0.0001f);
        transform.position = new Vector3(nextX, transform.position.y, 0f);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.collider.CompareTag("Ball")) return;

        Rigidbody2D rb = col.rigidbody;
        if (rb == null) return;

        // 패들 전체(자식 포함) 바운즈로 반사 각 계산
        Bounds b;
        if (!TryComputeBoundsFromChildrenInternal(out b) || col.contactCount == 0) return;

        float centerX = b.center.x;
        float half = paddleHalfWidth; // 캐시된 폭 사용(일관성/성능)

        float hitX = col.GetContact(0).point.x;
        float t = Mathf.Clamp((hitX - centerX) / half, -1f, 1f);

        float angleRad = t * maxBounceAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad));

        // 패들 x속도 영향(미세 가감)
        dir.x += xVel * paddleVelInfluence * 0.01f;
        dir = dir.normalized;

        // 최소 상승각 보장
        dir = EnsureMinVerticalLocal(dir, minVerticalDot);

        float speed = Mathf.Clamp(rb.velocity.magnitude, minSpeed, maxSpeed);
        rb.velocity = dir * speed;
    }

    Vector2 EnsureMinVerticalLocal(Vector2 d, float minDot)
    {
        d.Normalize();
        if (Mathf.Abs(d.y) >= minDot) return d;
        float signY = d.y >= 0f ? 1f : -1f;
        float newY = minDot * signY;
        float newX = Mathf.Sign(d.x == 0 ? 1f : d.x) * Mathf.Sqrt(Mathf.Max(0.0001f, 1f - minDot * minDot));
        return new Vector2(newX, newY).normalized;
    }

    // ==== 유틸: 자식까지 포함해 폭/바운즈 계산 ====

    /// <summary>자식 포함 전체 폭의 절반(half extents X) 반환. 아무것도 없으면 0.5f</summary>
    float ComputeHalfWidthFromChildrenInternal()
    {
        if (clampBoundsCollider)
            return clampBoundsCollider.bounds.extents.x;

        Bounds b;
        if (TryComputeBoundsFromChildrenInternal(out b)) return b.extents.x;
        return 0.5f;
    }

    /// <summary>자식 포함 활성 Collider2D(우선) → 없으면 활성 Renderer 기준으로 Bounds 계산</summary>
    bool TryComputeBoundsFromChildrenInternal(out Bounds bounds)
    {
        var cols = GetComponentsInChildren<Collider2D>(includeInactive: false);
        bool any = false;
        bounds = new Bounds(transform.position, Vector3.zero);

        int mask = clampBoundsLayerMask.value;

        foreach (var c in cols)
        {
            if (!c || !c.enabled) continue;
            if (mask != 0 && ((1 << c.gameObject.layer) & mask) == 0) continue;
            any = true;
            bounds.Encapsulate(c.bounds);
        }

        if (!any)
        {
            var rs = GetComponentsInChildren<Renderer>(includeInactive: false);
            foreach (var r in rs)
            {
                if (!r || !r.enabled) continue;
                if (mask != 0 && ((1 << r.gameObject.layer) & mask) == 0) continue;
                any = true;
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    /// <summary>
    /// 1) 스캔 결과가 있으면: 스캔된 안쪽면 X 사용
    /// 2) (피벗 방식) 유효한 벽 Transform이 있으면: Collider Edge 또는 Transform.x 사용
    /// 3) 폴백: minX/maxX 사용
    /// </summary>
    void ResolveLimits(float half, out float leftLimit, out float rightLimit)
    {
        if (useScannedEdges)
        {
            leftLimit = scannedLeftInnerX + half + Mathf.Max(0f, wallContactSkin);
            rightLimit = scannedRightInnerX - half - Mathf.Max(0f, wallContactSkin);
            return;
        }

        if (preferWallTransforms && leftWall && rightWall)
        {
            if (useWallColliderEdges)
            {
                bool haveL = TryGetWallInnerEdge(leftWall, isLeftWall: true, out float leftInnerX);
                bool haveR = TryGetWallInnerEdge(rightWall, isLeftWall: false, out float rightInnerX);
                if (haveL && haveR)
                {
                    leftLimit = leftInnerX + half + Mathf.Max(0f, wallContactSkin);
                    rightLimit = rightInnerX - half - Mathf.Max(0f, wallContactSkin);
                    return;
                }
            }
            // 콜라이더가 없으면 Transform.x 중심선 사용(기존 로직)
            leftLimit = leftWall.position.x + half;
            rightLimit = rightWall.position.x - half;
            return;
        }

        // 폴백: 수치 범위(half를 더해 끝단 클램프)
        leftLimit = minX + half;
        rightLimit = maxX - half;
    }

    /// <summary>
    /// 벽 트랜스폼 아래의 Collider2D들을 합쳐 월드 바운즈를 만든 뒤,
    /// 왼쪽벽이면 bounds.max.x(우측 엣지), 오른쪽벽이면 bounds.min.x(좌측 엣지)를 반환.
    /// </summary>
    bool TryGetWallInnerEdge(Transform wall, bool isLeftWall, out float innerX)
    {
        innerX = 0f;
        if (!wall) return false;

        var cols = wall.GetComponentsInChildren<Collider2D>(includeInactive: false);
        if (cols == null || cols.Length == 0) return false;

        bool any = false;
        Bounds b = new Bounds(wall.position, Vector3.zero);
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (!c || !c.enabled) continue;
            if (!any) { b = c.bounds; any = true; }
            else b.Encapsulate(c.bounds);
        }
        if (!any) return false;

        innerX = isLeftWall ? b.max.x : b.min.x;
        return true;
    }

    Transform FindActiveByName(string objName)
    {
        if (string.IsNullOrEmpty(objName)) return null;
        var go = GameObject.Find(objName);
        if (go && go.activeInHierarchy) return go.transform;
        return null;
    }

    // ================== ▼ 자동 스캔 수신 구현 ▼ ==================
    public void RecalcWallEdges(Transform wallsRoot)
    {
        useScannedEdges = false;

        if (!wallsRoot) return;

        if (!TryComputeBoundsUnderRoot(wallsRoot, out Bounds wb)) return;

        float centerX = wb.center.x;
        float sampleY = transform.position.y;
        Vector2 origin = new Vector2(centerX, sampleY);

        float maxDist = Mathf.Max(5f, wb.extents.x * 3f + 5f);

        RaycastHit2D[] hitsL = Physics2D.RaycastAll(origin, Vector2.left, maxDist);
        bool gotL = TryPickFirstHitOnRoot(hitsL, wallsRoot, out RaycastHit2D leftHit);

        RaycastHit2D[] hitsR = Physics2D.RaycastAll(origin, Vector2.right, maxDist);
        bool gotR = TryPickFirstHitOnRoot(hitsR, wallsRoot, out RaycastHit2D rightHit);

        if (gotL && gotR)
        {
            scannedLeftInnerX = leftHit.point.x;
            scannedRightInnerX = rightHit.point.x;

            if (scannedLeftInnerX > scannedRightInnerX)
            {
                float tmp = scannedLeftInnerX;
                scannedLeftInnerX = scannedRightInnerX;
                scannedRightInnerX = tmp;
            }

            useScannedEdges = true;
        }
    }

    bool TryComputeBoundsUnderRoot(Transform root, out Bounds b)
    {
        b = new Bounds(root.position, Vector3.zero);
        bool any = false;

        var cols = root.GetComponentsInChildren<Collider2D>(includeInactive: false);
        foreach (var c in cols)
        {
            if (!c || !c.enabled) continue;
            if (!any) { b = c.bounds; any = true; }
            else b.Encapsulate(c.bounds);
        }
        if (any) return true;

        var rs = root.GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var r in rs)
        {
            if (!r || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any;
    }

    bool TryPickFirstHitOnRoot(RaycastHit2D[] hits, Transform root, out RaycastHit2D picked)
    {
        picked = default;
        float bestDist = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;
            var t = h.collider.transform;
            if (!t || !t.IsChildOf(root)) continue;
            if (h.collider.isTrigger) continue;

            if (h.distance >= 0f && h.distance < bestDist)
            {
                bestDist = h.distance;
                picked = h;
                found = true;
            }
        }
        return found;
    }
    // ================== ▲ 자동 스캔 수신 구현 ▲ ==================

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (debugShowScannedGizmos && useScannedEdges)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(scannedLeftInnerX, transform.position.y - 10f, 0f),
                            new Vector3(scannedLeftInnerX, transform.position.y + 10f, 0f));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(scannedRightInnerX, transform.position.y - 10f, 0f),
                            new Vector3(scannedRightInnerX, transform.position.y + 10f, 0f));
        }
    }
#endif
}
