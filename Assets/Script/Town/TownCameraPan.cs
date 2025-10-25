using UnityEngine;

/// <summary>
/// ���η� �� Ÿ�� ����� �巡��/��������/������ Ž���ϴ� ī�޶� ��Ʈ�ѷ�.
/// - ���(root)�� SpriteRenderer/Collider/Renderer �ٿ�� ���� ���� ��� ���
/// - �巡�׷� �̵�, �ø�(����) ����, ���ϸ� �ش� ��ġ�� ������ �̵�
/// - �⺻�� ���� ����(allowVertical=false), �ʿ��ϸ� true�� �ٲٸ� ���ε� ��ũ��
/// ��������(�ӵ� ���� �� ���� �εε� ����):
/// - �巡�� �� ����(velocity) ���� ���� �� �� �� �� '���͸��� �巡�� �ӵ�'�� ���� ���۰����� ���
/// - ���� �ӵ��� 'ȭ��ʺ�/��' �������� ĸ(�ִ� �ӵ� ����)
/// - ��� ��ó���� �ະ�� �ε巴�� ����(���� �극��ũ)
/// - �巡�״� �ȼ������ ���� ��ȯ���� �ϰ��� ���� ����(�ػ�/��t ����)
/// - ��迡 ��� �����ӿ��� �ش� �� �ӵ� 0
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Camera))]
public class TownCameraPan : MonoBehaviour
{
    [Header("��� ��Ʈ (�ʼ�)")]
    [Tooltip("���/�ǹ�/NPC ���� �θ� ������Ʈ(�ڽĵ��� Renderer/Collider �ٿ�� �ջ��Ͽ� ��ũ�� ��踦 ���)")]
    public Transform worldRoot;

    [Header("�Է�/�̵�")]
    [Tooltip("�巡�� �ΰ���(ȭ�� �ȼ� �� ���� �̵� ����). 1.0���� ������ 0.4~1.2 ���̷� ���� ����")]
    public float dragSensitivity = 0.8f;
    [Tooltip("�ø�(����) ���� ���(�ʴ�). ���� Ŭ���� ���� ����")]
    public float flingDamping = 10f;
    [Tooltip("�� �� �� ��ġ�� ������ �̵� �ӵ�(���� ���� ���)")]
    public float tapMoveSpeed = 7f;
    [Tooltip("��ũ�� ���: ����/����")]
    public bool allowHorizontal = true;
    public bool allowVertical = false;

    [Header("�� ����")]
    [Tooltip("�� �ȼ� ���� �̵��̸� '��'���� ó��")]
    public float tapMaxPixelMove = 12f;
    [Tooltip("�� �ð�(��) ���� + �̵��� �����̸� '��'���� ó��")]
    public float tapMaxSeconds = 0.25f;

    [Header("�ӵ� ����/����")]
    [Tooltip("���� �ִ� �ӵ�(ȭ�� �ʺ�/��). 1.0�̸� 1�ʿ� ȭ�� 1�� �̵�")]
    public float maxScreensPerSecond = 0.9f;
    [Tooltip("�巡�� �ӵ� ������ ����(Hz ����). �������� ���� ���� �ӵ��� �� ������ ����")]
    public float dragVelocitySmoothing = 20f;
    [Tooltip("��迡�� �� ��(ȭ�� ���� ����) �������� ������ �ӵ��� �ε巴�� ����")]
    public float edgeBrakeScreens = 0.4f;

    Camera cam;
    Bounds worldBounds;           // ���� ��ü ���
    bool hasWorldBounds;

    Vector3 velocity;             // ����(����/��) - �巡�� �߿� �������� ����
    Vector3 dragVelFiltered;      // ������ �� ����� ���͸��� �巡�� �ӵ�

    // ������ ����
    bool dragging;
    Vector2 startScreenPos;
    float startTime;

    Vector2 prevScreenPos;
    bool havePrevScreen;

    // �� �̵� Ÿ��
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
            // �� Ÿ������ ������ �̵�(���� ���� �ɼ� �ݿ�)
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
            // ���� �̵�(��� ���� �� ���� �� �̵� �� ����)
            if (velocity.sqrMagnitude > 0.000001f)
            {
                Vector3 v = velocity;

                // ��� ���� �� �ະ ����(�극��ũ)
                ApplyEdgeBrake(ref v, transform.position);

                transform.position += v * dt;

                // ���� ����
                float k = Mathf.Exp(-flingDamping * dt);
                velocity *= k;
                if (velocity.sqrMagnitude < 0.00001f) velocity = Vector3.zero;
            }
        }

        // ��� Ŭ���� + ƨ�� ����(�ະ�� ���� ����)
        ClampCameraImmediate();
    }

    // --- �Է� ó�� ---
    void HandlePointer()
    {
        bool down = Input.GetMouseButtonDown(0);
        bool held = Input.GetMouseButton(0);
        bool up = Input.GetMouseButtonUp(0);

        if (down)
        {
            dragging = true;
            hasTapTarget = false;     // �巡�� �����ϸ� �� �̵� ���
            velocity = Vector3.zero;  // �� ���ۿ��� ���� ���� ����

            startScreenPos = Input.mousePosition;
            startTime = Time.time;

            prevScreenPos = Input.mousePosition;
            havePrevScreen = true;

            dragVelFiltered = Vector3.zero; // �巡�� �ӵ� ���� �ʱ�ȭ
        }
        else if (held && dragging)
        {
            if (havePrevScreen)
            {
                Vector2 cur = Input.mousePosition;
                Vector2 ds = cur - prevScreenPos; // �ȼ� ���� ��ȭ

                // �ȼ� �� ���� ��ȯ(���� ī�޶� ����: ȭ�� ���̸� orthographicSize�� ȯ��)
                float worldPerPixel = (2f * cam.orthographicSize) / Mathf.Max(Screen.height, 1);
                Vector3 move = new Vector3(
                    -ds.x * worldPerPixel * dragSensitivity,
                    -ds.y * worldPerPixel * dragSensitivity,
                    0f);

                if (!allowHorizontal) move.x = 0f;
                if (!allowVertical) move.y = 0f;

                Vector3 before = transform.position;
                transform.position += move;

                // �巡�� �����ӿ����� ��� ��� Ŭ���� + �� �ӵ� ���� 0(�������� ���� ���� ����)
                bool hx, hy;
                Vector3 clamped = ClampToBounds(transform.position, out hx, out hy);
                transform.position = clamped;

                // ���� �������� "���� �̵�"�� �������� �巡�� �ӵ� ����
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                Vector3 actualMove = clamped - before;
                if (hx) actualMove.x = 0f;
                if (hy) actualMove.y = 0f;

                Vector3 sampleVel = actualMove / dt;

                // 1�� ��������� ���͸�(������ �� �� ���� ���� ���ۼӵ��� ���)
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
                velocity = Vector3.zero;  // �� �̵� �� ���� ����
            }
            else
            {
                // ������ ������ ���͸��� �巡�� �ӵ��� ���� ���۰����� ���(�ִ� �ӵ� ĸ)
                float maxSpeed = MaxSpeedWorldPerSec();
                velocity = Vector3.ClampMagnitude(dragVelFiltered, maxSpeed);
            }

            havePrevScreen = false;
            dragVelFiltered = Vector3.zero;
        }
    }

    Vector3 ScreenToWorld(Vector3 screenPos)
    {
        // ���� ī�޶��� ��� z�� �ǹ� ����. �۽��� ī�޶� ��� ����.
        var wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        wp.z = transform.position.z;
        return wp;
    }

    // --- �ӵ�/��� ��ƿ ---
    float MaxSpeedWorldPerSec()
    {
        // ȭ�� �ʺ�(���� ����) * maxScreensPerSecond
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
            float distEdge = Mathf.Min(pos.x - minX, maxX - pos.x); // ��/�� �� �� ����� ������ �Ÿ�
            float k = Mathf.Clamp01(distEdge / thX);               // 0~1, 0�ϼ��� ��迡 ��¦
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

    // --- ��� ���/Ŭ���� ---
    public void RecalculateWorldBounds()
    {
        hasWorldBounds = false;
        if (!worldRoot) return;

        bool any = false;
        Bounds b = new Bounds(worldRoot.position, Vector3.zero);

        // 1) Collider ���
        var cols = worldRoot.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (var c in cols)
        {
            if (!c || !c.enabled) continue;
            if (!any) { b = c.bounds; any = true; }
            else b.Encapsulate(c.bounds);
        }
        // 2) Renderer ���
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
            // ����: 0,0�� ���� �ڽ�
            worldBounds = new Bounds(worldRoot.position, new Vector3(10, 10, 0));
            hasWorldBounds = true;
        }
    }

    // ��ġ�� ���� Ŭ�����ϰ� ��� �࿡�� �ɷȴ��� ��ȯ
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

    // Ÿ�� ����Ʈ�� �̸� ��� ������
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

    // ��� Ŭ���� + �ະ �ӵ� 0 (������Ʈ ��/�ʱ� ���Կ�)
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
