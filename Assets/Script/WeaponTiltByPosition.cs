using UnityEngine;

/// ��/�� ������ �Ÿ� ��ʷ� ���⸦ ����̵�, ����/��¡/�ε巯�� ȸ���� ����.
/// - ���� �ڽ�(PolygonCollider2D)�� ���̰� rotateTarget(�θ� �ǹ�) ����.
/// - Physics2D Layer Collision Matrix���� Weapon��Walls�� ����, Weapon��Ball�� üũ.
[DisallowMultipleComponent]
public class WeaponTiltByPosition : MonoBehaviour
{
    [Header("���")]
    public Transform rotateTarget;      // ȸ����ų �θ� �ǹ�
    public Transform paddleRoot;        // ���� �ڵ� Ž��
    [Tooltip("����ĳ��Ʈ ����(���� paddleRoot)")]
    public Transform probeOrigin;

    [Header("�� �Ÿ� ����")]
    public float probeMaxDistance = 3.0f;      // �߾�~�� �뷫 �Ÿ�
    public float probeRadius = 0.08f;          // �е�/���� �β���ŭ
    [Tooltip("����θ� 'Walls' ���̾ �⺻ ���")]
    public LayerMask wallMask;                  // Walls�� ����

    [Header("����(���� ���� Z)")]
    [Tooltip("��������� �⺻ ����(���� ���� ȸ�� ĸó)")]
    public float baseAngleZ = 0f;
    public float leftWallAngleZ = 0f;           // ���� ���ϼ���
    public float rightWallAngleZ = 0f;          // ������ ���ϼ���

    [Header("����/����ȭ")]
    [Tooltip("�����κ��� �� �Ÿ������� '���� ���� ���� ��'���� ���(����)")]
    public float wallClearance = 0.20f;         // 0.15~0.35 ����
    [Tooltip("�ִ� ���� ����(1=���� �������). 0.9~0.98 ����")]
    [Range(0.5f, 1f)] public float maxTiltPercent = 0.92f;

    [Header("��¡(������ �� ����� ����)")]
    [Tooltip("����溮���� ������ �󸶳� ������ ������ ����� ����")]
    public AnimationCurve responseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    // �⺻ Ŀ�긦 �� ��ó���� ���������� �ٲٰ� �ʹٸ�: new Keyframe[]{ new(0,0,0,2), new(1,1,0,0) }

    [Header("ȸ�� ���")]
    [Tooltip("�ε巴�� ����/�����ϴ� ���������� ���(����)")]
    public bool useSmoothDamp = true;
    [Tooltip("���������� ��ǥ�� �����ϴ� �ð�(ª������ ��ø)")]
    public float smoothTime = 0.10f;
    [Tooltip("���������� �ִ� ���ӵ�(��/��)")]
    public float maxAngularSpeed = 1080f;
    [Tooltip("���������� OFF�� �� ���ӵ�(��/��)")]
    public float rotateSpeed = 720f;

    [Header("�����")]
    public bool debugDraw = false;

    float capturedBaseZ;
    float angularVel; // SmoothDampAngle��

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

        // �⺻ Ŀ�갡 ��������� ����
        if (responseCurve == null || responseCurve.length == 0)
            responseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        angularVel = 0f;
    }

    void Update()
    {
        if (!rotateTarget || !paddleRoot) return;

        // ����: ���� ������ �е� ��Ʈ
        Vector2 origin = (probeOrigin ? probeOrigin : paddleRoot).position;

        // ��/�� ������ ���� �Ÿ�
        float distL = ProbeWalls(origin, Vector2.left);
        float distR = ProbeWalls(origin, Vector2.right);

        // ������ ������(0~1): ����(wallClearance)��ŭ ���� ���
        float tL = ToProximity(distL);
        float tR = ToProximity(distR);

        // �� ����� ���� ������ ���� �� ��¡ Ŀ�� ���� �� �ִ� ���� ĸ
        float t = Mathf.Max(tL, tR);
        t = Mathf.Clamp01(responseCurve.Evaluate(Mathf.Clamp01(t)));
        t *= Mathf.Clamp01(maxTiltPercent);

        // ��ǥ ���� ���
        float targetZ = (tL >= tR)
            ? Mathf.LerpAngle(capturedBaseZ, leftWallAngleZ, t)
            : Mathf.LerpAngle(capturedBaseZ, rightWallAngleZ, t);

        // ����: ���������� or ����
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

    // �Ÿ� �� ������(0~1). clearance �������δ� 1�� �������� �ʰ� ����.
    float ToProximity(float distance)
    {
        float max = Mathf.Max(0.05f, probeMaxDistance);
        float d = Mathf.Max(0f, distance - Mathf.Max(0f, wallClearance)); // ������ŭ ����
        float t = 1f - Mathf.Clamp01(d / max);                            // �⺻ ������
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
            // �ڱ� �е�/���� ���� ����
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
