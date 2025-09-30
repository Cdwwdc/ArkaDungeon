using UnityEngine;

/// 좌/우 벽과의 거리 비례로 무기를 기울이되, 여유/이징/부드러운 회전을 지원.
/// - 무기 자식(PolygonCollider2D)에 붙이고 rotateTarget(부모 피벗) 지정.
/// - Physics2D Layer Collision Matrix에서 Weapon×Walls는 해제, Weapon×Ball은 체크.
[DisallowMultipleComponent]
public class WeaponTiltByPosition : MonoBehaviour
{
    [Header("대상")]
    public Transform rotateTarget;      // 회전시킬 부모 피벗
    public Transform paddleRoot;        // 비우면 자동 탐색
    [Tooltip("레이캐스트 원점(비우면 paddleRoot)")]
    public Transform probeOrigin;

    [Header("벽 거리 측정")]
    public float probeMaxDistance = 3.0f;      // 중앙~벽 대략 거리
    public float probeRadius = 0.08f;          // 패들/무기 두께만큼
    [Tooltip("비워두면 'Walls' 레이어를 기본 사용")]
    public LayerMask wallMask;                  // Walls만 보기

    [Header("각도(절대 로컬 Z)")]
    [Tooltip("가운데에서의 기본 각도(비우면 현재 회전 캡처)")]
    public float baseAngleZ = 0f;
    public float leftWallAngleZ = 0f;           // 왼쪽 벽일수록
    public float rightWallAngleZ = 0f;          // 오른쪽 벽일수록

    [Header("보간/안정화")]
    [Tooltip("벽으로부터 이 거리까지는 '아직 붙지 않은 것'으로 취급(여유)")]
    public float wallClearance = 0.20f;         // 0.15~0.35 권장
    [Tooltip("최대 기울기 비율(1=완전 수평까지). 0.9~0.98 권장")]
    [Range(0.5f, 1f)] public float maxTiltPercent = 0.92f;

    [Header("이징(근접도 → 기울임 비율)")]
    [Tooltip("가운데→벽으로 갈수록 얼마나 빠르게 눕힐지 곡선으로 제어")]
    public AnimationCurve responseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    // 기본 커브를 벽 근처에서 급해지도록 바꾸고 싶다면: new Keyframe[]{ new(0,0,0,2), new(1,1,0,0) }

    [Header("회전 방식")]
    [Tooltip("부드럽게 가속/감속하는 스무스댐프 사용(권장)")]
    public bool useSmoothDamp = true;
    [Tooltip("스무스댐프 목표에 수렴하는 시간(짧을수록 민첩)")]
    public float smoothTime = 0.10f;
    [Tooltip("스무스댐프 최대 각속도(도/초)")]
    public float maxAngularSpeed = 1080f;
    [Tooltip("스무스댐프 OFF일 때 각속도(도/초)")]
    public float rotateSpeed = 720f;

    [Header("디버그")]
    public bool debugDraw = false;

    float capturedBaseZ;
    float angularVel; // SmoothDampAngle용

    void OnEnable()
    {
        if (!rotateTarget)
            rotateTarget = transform.parent ? transform.parent : transform;

        if (!paddleRoot)
        {
            var pc = GetComponentInParent<PaddleController>();
            paddleRoot = pc ? pc.transform : (transform.parent ? transform.parent : transform);
        }

        capturedBaseZ = Mathf.Abs(baseAngleZ) > 0.0001f
            ? baseAngleZ
            : rotateTarget.localEulerAngles.z;

        // 기본 커브가 비어있으면 선형
        if (responseCurve == null || responseCurve.length == 0)
            responseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        angularVel = 0f;
    }

    void Update()
    {
        if (!rotateTarget || !paddleRoot) return;

        // 원점: 지정 없으면 패들 루트
        Vector2 origin = (probeOrigin ? probeOrigin : paddleRoot).position;

        // 좌/우 벽까지 실제 거리
        float distL = ProbeWalls(origin, Vector2.left);
        float distR = ProbeWalls(origin, Vector2.right);

        // 각쪽의 근접도(0~1): 여유(wallClearance)만큼 빼고 계산
        float tL = ToProximity(distL);
        float tR = ToProximity(distR);

        // 더 가까운 쪽의 근접도 선택 → 이징 커브 적용 → 최대 기울기 캡
        float t = Mathf.Max(tL, tR);
        t = Mathf.Clamp01(responseCurve.Evaluate(Mathf.Clamp01(t)));
        t *= Mathf.Clamp01(maxTiltPercent);

        // 목표 각도 계산
        float targetZ = (tL >= tR)
            ? Mathf.LerpAngle(capturedBaseZ, leftWallAngleZ, t)
            : Mathf.LerpAngle(capturedBaseZ, rightWallAngleZ, t);

        // 적용: 스무스댐프 or 선형
        float current = rotateTarget.localEulerAngles.z;
        float next;
        if (useSmoothDamp)
            next = Mathf.SmoothDampAngle(current, targetZ, ref angularVel, Mathf.Max(0.01f, smoothTime), maxAngularSpeed);
        else
            next = Mathf.MoveTowardsAngle(current, targetZ, rotateSpeed * Time.deltaTime);

        var e = rotateTarget.localEulerAngles;
        e.z = next;
        rotateTarget.localEulerAngles = e;

        if (debugDraw)
        {
            DebugDrawProbe(origin, Vector2.left, distL, Color.cyan);
            DebugDrawProbe(origin, Vector2.right, distR, Color.yellow);
        }
    }

    // 거리 → 근접도(0~1). clearance 안쪽으로는 1로 수렴하지 않게 완충.
    float ToProximity(float distance)
    {
        float max = Mathf.Max(0.05f, probeMaxDistance);
        float d = Mathf.Max(0f, distance - Mathf.Max(0f, wallClearance)); // 여유만큼 빼고
        float t = 1f - Mathf.Clamp01(d / max);                            // 기본 근접도
        return t;
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
            // 자기 패들/무기 계층 무시
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
}
