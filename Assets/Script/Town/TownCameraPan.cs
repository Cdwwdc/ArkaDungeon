using UnityEngine;

/// <summary>
/// 가로로 긴 타운 배경을 드래그/스와이프/탭으로 탐색하는 카메라 컨트롤러.
/// - 배경(root)의 SpriteRenderer/Collider/Renderer 바운즈를 합쳐 월드 경계 계산
/// - 드래그로 이동, 플링(관성) 지원, 탭하면 해당 위치로 스무스 이동
/// - 기본은 세로 고정(allowVertical=false), 필요하면 true로 바꾸면 세로도 스크롤
/// 수정사항(속도 폭주 및 끝단 두두두 방지):
/// - 드래그 중 관성(velocity) 누적 금지 → 손 뗄 때 '필터링된 드래그 속도'만 관성 시작값으로 사용
/// - 관성 속도를 '화면너비/초' 기준으로 캡(최대 속도 제한)
/// - 경계 근처에서 축별로 부드럽게 감속(에지 브레이크)
/// - 드래그는 픽셀→월드 단위 변환으로 일관된 감도 유지(해상도/Δt 무관)
/// - 경계에 닿는 프레임에는 해당 축 속도 0
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Camera))]
public class TownCameraPan : MonoBehaviour
{
    [Header("배경 루트 (필수)")]
    [Tooltip("배경/건물/NPC 등의 부모 오브젝트(자식들의 Renderer/Collider 바운즈를 합산하여 스크롤 경계를 계산)")]
    public Transform worldRoot;

    [Header("입력/이동")]
    [Tooltip("드래그 민감도(화면 픽셀 → 월드 이동 배율). 1.0에서 시작해 0.4~1.2 사이로 조절 권장")]
    public float dragSensitivity = 0.8f;
    [Tooltip("플링(관성) 감쇠 계수(초당). 값이 클수록 빨리 멈춤")]
    public float flingDamping = 10f;
    [Tooltip("탭 시 그 위치로 스무스 이동 속도(지수 보간 계수)")]
    public float tapMoveSpeed = 7f;
    [Tooltip("스크롤 허용: 가로/세로")]
    public bool allowHorizontal = true;
    public bool allowVertical = false;

    [Header("탭 판정")]
    [Tooltip("이 픽셀 이하 이동이면 '탭'으로 처리")]
    public float tapMaxPixelMove = 12f;
    [Tooltip("이 시간(초) 이하 + 이동량 제한이면 '탭'으로 처리")]
    public float tapMaxSeconds = 0.25f;

    [Header("속도 제한/보정")]
    [Tooltip("관성 최대 속도(화면 너비/초). 1.0이면 1초에 화면 1폭 이동")]
    public float maxScreensPerSecond = 0.9f;
    [Tooltip("드래그 속도 필터의 응답(Hz 느낌). 높을수록 손의 실제 속도를 더 빠르게 따라감")]
    public float dragVelocitySmoothing = 20f;
    [Tooltip("경계에서 이 값(화면 폭의 비율) 안쪽으로 들어오면 속도를 부드럽게 감속")]
    public float edgeBrakeScreens = 0.4f;

    Camera cam;
    Bounds worldBounds;           // 월드 전체 경계
    bool hasWorldBounds;

    Vector3 velocity;             // 관성(월드/초) - 드래그 중엔 갱신하지 않음
    Vector3 dragVelFiltered;      // 릴리즈 시 사용할 필터링된 드래그 속도

    // 포인터 상태
    bool dragging;
    Vector2 startScreenPos;
    float startTime;

    Vector2 prevScreenPos;
    bool havePrevScreen;

    // 탭 이동 타깃
    bool hasTapTarget;
    Vector3 tapTarget;

    void Awake()
    {
        cam = GetComponent<Camera>();
        RecalculateWorldBounds();
        ClampCameraImmediate();
    }

    void OnValidate()
    {
        if (dragSensitivity < 0.01f) dragSensitivity = 0.01f;
        if (flingDamping < 0.0f) flingDamping = 0.0f;
        if (tapMoveSpeed < 0.1f) tapMoveSpeed = 0.1f;
        if (maxScreensPerSecond < 0f) maxScreensPerSecond = 0f;
        if (dragVelocitySmoothing < 0.1f) dragVelocitySmoothing = 0.1f;
        if (edgeBrakeScreens < 0f) edgeBrakeScreens = 0f;
    }

    void Update()
    {
        HandlePointer();

        float dt = Time.deltaTime;

        if (hasTapTarget)
        {
            // 탭 타깃으로 스무스 이동(세로 고정 옵션 반영)
            Vector3 pos = transform.position;
            Vector3 to = tapTarget;
            if (!allowVertical) to.y = pos.y;

            pos = Vector3.Lerp(pos, to, 1f - Mathf.Exp(-tapMoveSpeed * dt));

            if ((new Vector2(pos.x, pos.y) - new Vector2(to.x, to.y)).sqrMagnitude < 0.0004f)
            {
                pos.x = to.x; pos.y = to.y;
                hasTapTarget = false;
                velocity = Vector3.zero;
            }
            transform.position = pos;
        }
        else
        {
            // 관성 이동(경계 근접 시 제동 → 이동 → 감쇠)
            if (velocity.sqrMagnitude > 0.000001f)
            {
                Vector3 v = velocity;

                // 경계 접근 시 축별 감속(브레이크)
                ApplyEdgeBrake(ref v, transform.position);

                transform.position += v * dt;

                // 지수 감쇠
                float k = Mathf.Exp(-flingDamping * dt);
                velocity *= k;
                if (velocity.sqrMagnitude < 0.00001f) velocity = Vector3.zero;
            }
        }

        // 경계 클램프 + 튕김 방지(축별로 관성 제거)
        ClampCameraImmediate();
    }

    // --- 입력 처리 ---
    void HandlePointer()
    {
        bool down = Input.GetMouseButtonDown(0);
        bool held = Input.GetMouseButton(0);
        bool up = Input.GetMouseButtonUp(0);

        if (down)
        {
            dragging = true;
            hasTapTarget = false;     // 드래그 시작하면 탭 이동 취소
            velocity = Vector3.zero;  // 새 조작에서 이전 관성 리셋

            startScreenPos = Input.mousePosition;
            startTime = Time.time;

            prevScreenPos = Input.mousePosition;
            havePrevScreen = true;

            dragVelFiltered = Vector3.zero; // 드래그 속도 필터 초기화
        }
        else if (held && dragging)
        {
            if (havePrevScreen)
            {
                Vector2 cur = Input.mousePosition;
                Vector2 ds = cur - prevScreenPos; // 픽셀 단위 변화

                // 픽셀 → 월드 변환(직교 카메라 기준: 화면 높이를 orthographicSize로 환산)
                float worldPerPixel = (2f * cam.orthographicSize) / Mathf.Max(Screen.height, 1);
                Vector3 move = new Vector3(
                    -ds.x * worldPerPixel * dragSensitivity,
                    -ds.y * worldPerPixel * dragSensitivity,
                    0f);

                if (!allowHorizontal) move.x = 0f;
                if (!allowVertical) move.y = 0f;

                Vector3 before = transform.position;
                transform.position += move;

                // 드래그 프레임에서도 즉시 경계 클램프 + 축 속도 샘플 0(에지에서 관성 누적 방지)
                bool hx, hy;
                Vector3 clamped = ClampToBounds(transform.position, out hx, out hy);
                transform.position = clamped;

                // 현재 프레임의 "실제 이동"을 기준으로 드래그 속도 샘플
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                Vector3 actualMove = clamped - before;
                if (hx) actualMove.x = 0f;
                if (hy) actualMove.y = 0f;

                Vector3 sampleVel = actualMove / dt;

                // 1차 저역통과로 필터링(릴리즈 시 이 값을 관성 시작속도로 사용)
                float a = 1f - Mathf.Exp(-dragVelocitySmoothing * dt);
                dragVelFiltered = Vector3.Lerp(dragVelFiltered, sampleVel, a);
            }
            prevScreenPos = Input.mousePosition;
            havePrevScreen = true;
        }
        else if (up && dragging)
        {
            dragging = false;

            float movePix = (Input.mousePosition - (Vector3)startScreenPos).magnitude;
            float dur = Time.time - startTime;
            bool isTap = (movePix <= tapMaxPixelMove) && (dur <= tapMaxSeconds);

            if (isTap)
            {
                Vector3 hit = ScreenToWorld(Input.mousePosition);
                tapTarget = ClampTargetPoint(new Vector3(hit.x, hit.y, transform.position.z));
                hasTapTarget = true;
                velocity = Vector3.zero;  // 탭 이동 중 관성 제거
            }
            else
            {
                // 릴리즈 시점의 필터링된 드래그 속도를 관성 시작값으로 사용(최대 속도 캡)
                float maxSpeed = MaxSpeedWorldPerSec();
                velocity = Vector3.ClampMagnitude(dragVelFiltered, maxSpeed);
            }

            havePrevScreen = false;
            dragVelFiltered = Vector3.zero;
        }
    }

    Vector3 ScreenToWorld(Vector3 screenPos)
    {
        // 직교 카메라인 경우 z는 의미 없음. 퍼스면 카메라 평면 기준.
        var wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        wp.z = transform.position.z;
        return wp;
    }

    // --- 속도/경계 유틸 ---
    float MaxSpeedWorldPerSec()
    {
        // 화면 너비(월드 단위) * maxScreensPerSecond
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float screenW = halfW * 2f;
        return screenW * Mathf.Max(0f, maxScreensPerSecond);
    }

    void ApplyEdgeBrake(ref Vector3 vel, Vector3 pos)
    {
        if (!hasWorldBounds) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float screenW = halfW * 2f;
        float screenH = halfH * 2f;

        float thX = Mathf.Max(0.0001f, screenW * edgeBrakeScreens);
        float thY = Mathf.Max(0.0001f, screenH * edgeBrakeScreens);

        if (allowHorizontal)
        {
            float minX = worldBounds.min.x + halfW;
            float maxX = worldBounds.max.x - halfW;
            float distEdge = Mathf.Min(pos.x - minX, maxX - pos.x); // 왼/오 중 더 가까운 경계까지 거리
            float k = Mathf.Clamp01(distEdge / thX);               // 0~1, 0일수록 경계에 바짝
            vel.x *= k;
        }
        else vel.x = 0f;

        if (allowVertical)
        {
            float minY = worldBounds.min.y + halfH;
            float maxY = worldBounds.max.y - halfH;
            float distEdge = Mathf.Min(pos.y - minY, maxY - pos.y);
            float k = Mathf.Clamp01(distEdge / thY);
            vel.y *= k;
        }
        else vel.y = 0f;
    }

    // --- 경계 계산/클램프 ---
    public void RecalculateWorldBounds()
    {
        hasWorldBounds = false;
        if (!worldRoot) return;

        bool any = false;
        Bounds b = new Bounds(worldRoot.position, Vector3.zero);

        // 1) Collider 기반
        var cols = worldRoot.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (var c in cols)
        {
            if (!c || !c.enabled) continue;
            if (!any) { b = c.bounds; any = true; }
            else b.Encapsulate(c.bounds);
        }
        // 2) Renderer 기반
        var r2s = worldRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var r in r2s)
        {
            if (!r || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        if (any)
        {
            worldBounds = b;
            hasWorldBounds = true;
        }
        else
        {
            // 폴백: 0,0에 작은 박스
            worldBounds = new Bounds(worldRoot.position, new Vector3(10, 10, 0));
            hasWorldBounds = true;
        }
    }

    // 위치를 경계로 클램프하고 어느 축에서 걸렸는지 반환
    Vector3 ClampToBounds(Vector3 pos, out bool hitX, out bool hitY)
    {
        hitX = hitY = false;
        if (!hasWorldBounds) return pos;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = worldBounds.min.x + halfW;
        float maxX = worldBounds.max.x - halfW;
        float minY = worldBounds.min.y + halfH;
        float maxY = worldBounds.max.y - halfH;

        Vector3 res = pos;

        if (allowHorizontal)
        {
            float clampedX = Mathf.Clamp(res.x, minX, maxX);
            if (!Mathf.Approximately(clampedX, res.x)) hitX = true;
            res.x = clampedX;
        }
        else
        {
            res.x = Mathf.Clamp(worldBounds.center.x, minX, maxX);
        }

        if (allowVertical)
        {
            float clampedY = Mathf.Clamp(res.y, minY, maxY);
            if (!Mathf.Approximately(clampedY, res.y)) hitY = true;
            res.y = clampedY;
        }
        else
        {
            res.y = Mathf.Clamp(worldBounds.center.y, minY, maxY);
        }

        res.z = pos.z;
        return res;
    }

    // 타깃 포인트를 미리 경계 안으로
    Vector3 ClampTargetPoint(Vector3 p)
    {
        if (!hasWorldBounds) return p;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = worldBounds.min.x + halfW;
        float maxX = worldBounds.max.x - halfW;
        float minY = worldBounds.min.y + halfH;
        float maxY = worldBounds.max.y - halfH;

        if (allowHorizontal) p.x = Mathf.Clamp(p.x, minX, maxX);
        else p.x = Mathf.Clamp(worldBounds.center.x, minX, maxX);

        if (allowVertical) p.y = Mathf.Clamp(p.y, minY, maxY);
        else p.y = Mathf.Clamp(worldBounds.center.y, minY, maxY);

        p.z = transform.position.z;
        return p;
    }

    // 경계 클램프 + 축별 속도 0 (업데이트 끝/초기 진입용)
    void ClampCameraImmediate()
    {
        if (!hasWorldBounds) return;
        bool hitX, hitY;
        Vector3 clamped = ClampToBounds(transform.position, out hitX, out hitY);
        transform.position = clamped;
        if (hitX) velocity.x = 0f;
        if (hitY) velocity.y = 0f;
    }
}
