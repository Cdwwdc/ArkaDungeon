using UnityEngine;

[DisallowMultipleComponent]
public class WeaponTiltByPosition : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("회전시킬 피벗(보통 무기 부모). 비우면 부모/자신을 자동 사용")]
    public Transform rotateTarget;
    [Tooltip("패들의 Transform. 비우면 PaddleController를 찾아 자동 설정")]
    public Transform paddleRoot;
    [Tooltip("좌우 벽 거리 측정의 원점. 비우면 paddleRoot 사용")]
    public Transform probeOrigin;

    [Header("벽 거리 측정")]
    [Tooltip("가운데에서 한쪽 벽까지 대략 거리(씬 스케일에 맞게)")]
    public float probeMaxDistance = 3.0f;
    [Tooltip("서클 캐스트 반경(무기/패들의 두께만큼)")]
    public float probeRadius = 0.08f;
    [Tooltip("벽 레이어만 체크하도록 설정. 비우면 'Walls' 레이어 자동 사용")]
    public LayerMask wallMask;

    [Header("각도(절대 로컬 Z)")]
    [Tooltip("가운데(벽과 멀리) 있을 때 기준 각도")]
    public float baseAngleZ = 0f;
    [Tooltip("왼쪽 벽에 가까워질수록 보간될 각도")]
    public float leftWallAngleZ = 0f;
    [Tooltip("오른쪽 벽에 가까워질수록 보간될 각도")]
    public float rightWallAngleZ = 0f;

    [Header("보간/안정화")]
    [Tooltip("벽에서 이 거리만큼은 아직 '붙지 않은' 여유로 간주(너무 일찍 눕는 것 방지)")]
    public float wallClearance = 0.20f;
    [Range(0.5f, 1f), Tooltip("최대 눕힘 비중(1=정의한 벽 각도까지, 0.9~0.98 권장)")]
    public float maxTiltPercent = 0.92f;

    [Header("이징(근접도 → 기울기 비율)")]
    [Tooltip("벽에 가까워질수록 얼마나 빠르게 눕힐지 곡선으로 제어(초반 완만, 끝 급격 권장)")]
    public AnimationCurve responseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ★ 근접도 스무딩 추가 (바운스 제거 핵심)
    [Header("근접도 입력 스무딩")]
    [Range(0f, 1f), Tooltip("tL/tR(벽 근접도) 자체를 저역통과로 안정화(바운스 제거). 0=즉각, 1=아주 느림")]
    public float proximitySmoothing = 0.25f; // 0=즉각, 1=아주 느림

    [Header("회전 방식")]
    [Tooltip("부드럽게 수렴(SmoothDampAngle) 사용할지 여부")]
    public bool useSmoothDamp = true;
    [Tooltip("목표 각도로 수렴하는데 걸리는 시간(짧을수록 민첩)")]
    public float smoothTime = 0.10f;
    [Tooltip("SmoothDampAngle의 최대 각속도(도/초)")]
    public float maxAngularSpeed = 1080f;
    [Tooltip("SmoothDamp 미사용 시 초당 회전 속도(도/초)")]
    public float rotateSpeed = 720f;

    // ===== 아날로그 스윙 =====
    [Header("연속(아날로그) 스윙")]
    [Tooltip("수평 이동 속도가 임계값을 넘을 때만 스윙을 적용")]
    public bool useAnalogSwing = true;
    [Tooltip("스윙이 시작되는 수평속도 임계값(무게감)")]
    public float speedDeadzone = 0.5f;
    [Tooltip("스윙에 반영되는 최대 수평속도(이 이상은 동일 취급)")]
    public float speedMax = 12f;
    [Tooltip("스윙으로 추가되는 최대 각도(±)")]
    public float maxSwingAngle = 55f;
    [Tooltip("스윙 오버레이가 목표값에 수렴하는 시간(길수록 묵직)")]
    public float analogSmoothTime = 0.06f;
    [Range(0f, 1f), Tooltip("수평속도 측정 자체의 필터링(0=즉각, 1=아주 느리게 바뀜)")]
    public float velocitySmoothing = 0.15f;
    [Tooltip("스윙 방향 부호(특수 케이스 아니면 -1 유지)")]
    public int analogSign = -1;

    [Header("공 히트 임팩트(선택)")]
    [Tooltip("공과 부딪칠 때 순간 스윙 임팩트를 줄지 여부")]
    public bool analogImpactOnBallHit = true;
    [Tooltip("공 태그 이름")]
    public string ballTag = "Ball";
    [Tooltip("히트 시 추가되는 임팩트 각(부호는 충돌 방향으로 자동)")]
    public float impactAngle = 28f;
    [Tooltip("임팩트 각이 0으로 감쇠되는 시간")]
    public float impactDampingTime = 0.10f;

    // ===== 벽측 고정 / 스윙 약화 / 각도 클램프 =====
    [Header("벽측 고정(요동 방지)")]
    [Range(0.5f, 1f), Tooltip("벽으로 붙었다고 간주하는 임계 근접도(들어갈 때)")]
    public float latchEnter = 0.88f;
    [Range(0.3f, 0.9f), Tooltip("붙은 상태에서 떨어졌다고 보는 임계 근접도(나올 때)")]
    public float latchExit = 0.72f;

    [Header("벽 근접 시 스윙 약화")]
    [Range(0f, 1f), Tooltip("벽에 거의 붙었을 때 스윙 기여를 얼마나 줄일지 비율")]
    public float swingNearWallScale = 0.15f;

    [Header("최종 각도 클램프 (기준각 대비 ‘상대’ 범위)")]
    [Tooltip("최종 각도를 기준각 대비 상대 범위로 제한할지")]
    public bool clampFinalAngle = true;
    [Tooltip("기준각으로부터 허용되는 음수 방향(아래로) 각도")]
    public float minClampZ = -75f;  // ← 기준각에서 -75°까지 허용 (예: -45 - 75 = -120)
    [Tooltip("기준각으로부터 허용되는 양수 방향(위로) 각도")]
    public float maxClampZ = 45f;   // ← 기준각에서 +45°까지 허용 (예: -45 + 45 = 0)

    // ★ 기준각 아래 금지(상대 클램프)
    [Header("기준각 아래 금지(상대)")]
    [Tooltip("기준각(capturedBaseZ)보다 아래(음수 delta)로 내려가면 위로 반사합니다.")]
    public bool preventBelowBase = false; // ← 기본값 false(아래쪽도 허용)

    [Header("디버그")]
    [Tooltip("씬 뷰에 레이/마커 디버그 선을 그립니다")]
    public bool debugDraw = false;

    // 내부
    float capturedBaseZ;
    float tiltAngularVel;
    float swingOverlayZ = 0f;
    float analogVelZ = 0f;
    float vxFiltered = 0f;
    float impactZ = 0f, impactVel = 0f;
    float lastPaddleX;

    // ★ 근접도 필터링 내부 변수
    float _tLFiltered = 0f, _tRFiltered = 0f;
    bool _tFilterInit = false;

    enum WallSide { None, Left, Right }
    WallSide latchedSide = WallSide.None;

    // ★ 이중 임팩트 방지
    float _lastImpactTime = -999f;
    const float _impactCooldown = 0.05f; // 50ms

    void OnEnable()
    {
        if (!rotateTarget) rotateTarget = transform.parent ? transform.parent : transform;

        if (!paddleRoot)
        {
            var pc = GetComponentInParent<PaddleController>();
            paddleRoot = pc ? pc.transform : (transform.parent ? transform.parent : transform);
        }

        capturedBaseZ = Mathf.Abs(baseAngleZ) > 0.0001f ? baseAngleZ : rotateTarget.localEulerAngles.z;
        if (responseCurve == null || responseCurve.length == 0)
            responseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        tiltAngularVel = 0f;
        analogVelZ = 0f;
        vxFiltered = 0f;
        impactZ = 0f; impactVel = 0f;
        lastPaddleX = paddleRoot ? paddleRoot.position.x : transform.position.x;
        swingOverlayZ = 0f;

        // ★ OnEnable 시 필터 초기화
        _tFilterInit = false;

        analogSign = (analogSign >= 0) ? 1 : -1;

        // 너무 굵은 탐침으로 벽으로 판정 튀는 것 완화
        if (probeRadius > wallClearance * 0.9f) probeRadius = wallClearance * 0.5f;
    }

    // ★ 핵심 변경: Update → LateUpdate (패들 위치 확정 후 적용)
    void LateUpdate()
    {
        if (!rotateTarget || !paddleRoot) return;

        // 1) 벽 근접도
        Vector2 origin = (probeOrigin ? probeOrigin : paddleRoot).position;
        float distL = ProbeWalls(origin, Vector2.left);
        float distR = ProbeWalls(origin, Vector2.right);
        float tL = ToProximity(distL);
        float tR = ToProximity(distR);

        // ======== ★ 근접도 필터링 로직 (바운스 제거) ★ ========
        // (1) 필터 초기화 (첫 프레임 튀지 않도록)
        if (!_tFilterInit)
        {
            _tLFiltered = tL;
            _tRFiltered = tR;
            _tFilterInit = true;
        }

        // (2) 가벼운 저역통과: tFiltered = lerp(tFiltered, tRaw, alpha)
        float baseAlpha = Mathf.Clamp01(proximitySmoothing);
        // 근접할수록 alpha를 약간 낮춰 더 부드럽게 (가변형 LPF)
        float alphaL = Mathf.Clamp01(baseAlpha * (1f - 0.35f * tL));
        float alphaR = Mathf.Clamp01(baseAlpha * (1f - 0.35f * tR));

        _tLFiltered = Mathf.Lerp(_tLFiltered, tL, alphaL);
        _tRFiltered = Mathf.Lerp(_tRFiltered, tR, alphaR);
        // ======== ★ 필터링 로직 끝 ★ ========

        // (3) 래치 판정은 '원본 t' (tL/tR)로 유지 → 스냅 타이밍 유지
        float t = Mathf.Clamp01(responseCurve.Evaluate(Mathf.Max(tL, tR))) * Mathf.Clamp01(maxTiltPercent);

        // 벽측 고정(히스테리시스)
        if (latchedSide == WallSide.None)
        {
            if (tL >= latchEnter && tL >= tR) latchedSide = WallSide.Left;
            else if (tR >= latchEnter && tR > tL) latchedSide = WallSide.Right;
        }
        else
        {
            if (latchedSide == WallSide.Left && tL <= latchExit) latchedSide = WallSide.None;
            if (latchedSide == WallSide.Right && tR <= latchExit) latchedSide = WallSide.None;
        }

        // (4) 목표 각도 계산 시 '필터된 t' 사용
        float tiltTargetZ =
          (latchedSide == WallSide.Left) ? Mathf.LerpAngle(capturedBaseZ, leftWallAngleZ, _tLFiltered) :
          (latchedSide == WallSide.Right) ? Mathf.LerpAngle(capturedBaseZ, rightWallAngleZ, _tRFiltered) :
          (_tLFiltered >= _tRFiltered) ? Mathf.LerpAngle(capturedBaseZ, leftWallAngleZ, _tLFiltered)
                 : Mathf.LerpAngle(capturedBaseZ, rightWallAngleZ, _tRFiltered);

        // === 기준각 대비 서명 델타 공간에서 연속스윙/임팩트 합성 ===
        // 2) 아날로그 스윙
        float nowX = paddleRoot.position.x;
        float vx = (nowX - lastPaddleX) / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        lastPaddleX = nowX;

        vxFiltered = Mathf.Lerp(vxFiltered, vx, 1f - Mathf.Clamp01(velocitySmoothing));

        float desiredAnalog = 0f;
        if (useAnalogSwing)
        {
            float vMag = Mathf.Abs(vxFiltered);
            if (vMag > speedDeadzone)
            {
                float v = Mathf.Clamp(vMag, speedDeadzone, speedMax);
                float k = (v - speedDeadzone) / Mathf.Max(0.0001f, (speedMax - speedDeadzone));
                float baseSwing = analogSign * Mathf.Sign(vxFiltered) * (k * Mathf.Abs(maxSwingAngle));
                if (latchedSide != WallSide.None) baseSwing *= swingNearWallScale;
                desiredAnalog = baseSwing;
            }
        }

        // 임팩트 감쇠
        if (Mathf.Abs(impactZ) > 0.001f)
            impactZ = Mathf.SmoothDamp(impactZ, 0f, ref impactVel, Mathf.Max(0.01f, impactDampingTime), Mathf.Infinity, Time.unscaledDeltaTime);
        else
            impactZ = 0f;

        // (절대각 → 기준각 대비 서명 델타)
        float tiltDelta = Mathf.DeltaAngle(capturedBaseZ, tiltTargetZ); // -180..+180
        float targetSwingOverlay = desiredAnalog + impactZ;
        swingOverlayZ = Mathf.SmoothDamp(swingOverlayZ, targetSwingOverlay, ref analogVelZ, Mathf.Max(0.01f, analogSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);

        // 합성도 '서명 델타'에서
        float finalDelta = tiltDelta + swingOverlayZ;

        // 기준각 아래 금지(원하면)
        if (preventBelowBase && finalDelta < 0f)
            finalDelta = Mathf.Abs(finalDelta);

        // 최종 클램프: 기준각 대비 상대 범위
        if (clampFinalAngle)
            finalDelta = Mathf.Clamp(finalDelta, minClampZ, maxClampZ);

        // 델타 → 절대각 복귀
        float finalZ = capturedBaseZ + finalDelta;

        // 스무딩 적용
        float cur = rotateTarget.localEulerAngles.z;
        float next = useSmoothDamp
            ? Mathf.SmoothDampAngle(cur, finalZ, ref tiltAngularVel, Mathf.Max(0.01f, smoothTime), maxAngularSpeed)
            : Mathf.MoveTowardsAngle(cur, finalZ, rotateSpeed * Time.deltaTime);

        var e = rotateTarget.localEulerAngles; e.z = next; rotateTarget.localEulerAngles = e;

        if (debugDraw)
        {
            DebugDrawProbe(origin, Vector2.left, distL, Color.cyan);
            DebugDrawProbe(origin, Vector2.right, distR, Color.yellow);
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!analogImpactOnBallHit) return;
        if (!col.collider || !col.collider.CompareTag(ballTag)) return;
        if (Time.unscaledTime - _lastImpactTime < _impactCooldown) return; // ★ 이중 방지

        float dirSign = (col.contactCount > 0) ? Mathf.Sign(col.GetContact(0).relativeVelocity.x) : Mathf.Sign(vxFiltered);
        if (Mathf.Approximately(dirSign, 0f)) dirSign = 1f;

        impactZ += Mathf.Sign(dirSign) * impactAngle;
        impactZ = Mathf.Clamp(impactZ, -Mathf.Abs(maxSwingAngle), Mathf.Abs(maxSwingAngle));
        _lastImpactTime = Time.unscaledTime; // ★ 갱신
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!analogImpactOnBallHit) return;
        if (!other || !other.CompareTag(ballTag)) return;
        if (Time.unscaledTime - _lastImpactTime < _impactCooldown) return; // ★ 이중 방지

        float dirSign = Mathf.Sign(vxFiltered);
        if (Mathf.Approximately(dirSign, 0f)) dirSign = 1f;

        impactZ += dirSign * impactAngle;
        impactZ = Mathf.Clamp(impactZ, -Mathf.Abs(maxSwingAngle), Mathf.Abs(maxSwingAngle));
        _lastImpactTime = Time.unscaledTime; // ★ 갱신
    }

    // ===== 유틸 =====
    float ToProximity(float distance)
    {
        float max = Mathf.Max(0.05f, probeMaxDistance);
        float d = Mathf.Max(0f, distance - Mathf.Max(0f, wallClearance));
        return 1f - Mathf.Clamp01(d / max);
    }

    float ProbeWalls(Vector2 origin, Vector2 dir)
    {
        float max = Mathf.Max(0.05f, probeMaxDistance);
        int mask = (wallMask.value != 0) ? wallMask.value : LayerMask.GetMask("Walls");

        var hits = Physics2D.CircleCastAll(origin, probeRadius, dir, max, mask);
        float nearest = max;

        foreach (var h in hits)
        {
            if (!h.collider || h.collider.isTrigger) continue;
            var tr = h.collider.transform;
            if (paddleRoot && (tr == paddleRoot || tr.IsChildOf(paddleRoot))) continue;
            if (h.distance < nearest) nearest = h.distance;
        }
        return nearest;
    }

    void DebugDrawProbe(Vector2 origin, Vector2 dir, float dist, Color c)
    {
        Vector2 end = origin + dir.normalized * dist;
        Debug.DrawLine(origin, end, c, 0f);
        Debug.DrawLine(end + Vector2.up * probeRadius, end - Vector2.up * probeRadius, c, 0f);
        Debug.DrawLine(end + Vector2.right * probeRadius, end - Vector2.right * probeRadius, c, 0f);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        analogSign = (analogSign >= 0) ? 1 : -1;
        if (latchExit > latchEnter) latchExit = latchEnter - 0.02f;
    }
#endif
}
