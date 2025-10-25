using System.Collections;
using UnityEngine;

/// <summary>
/// ȭ��/�÷����ʵ� ������ ���� Ball�� �����ϰ�,
/// ���� Ball�� ���� �ð�(noBallGrace) �̻� 0���� GameManager.ShowContinue() ȣ��.
/// - ī�޶�/Manual ���� ����, margin/�߰�Ȯ�� ����
/// - ī��Ʈ�ٿ�/Ŭ���� ��ȯ/NextStage �ؽ�Ʈ ���� �߿��� �������� ����
/// - '���� �� ���̶� �� ����'���� ��Ƽ�� �Ǵ�(���� ���� �� ��ȣ)
/// </summary>
public class PlayfieldOutOfBoundsGuard : MonoBehaviour
{
    public enum BoundsSource { FromCamera, Manual }

    [Header("Bounds �ҽ�")]
    public BoundsSource source = BoundsSource.FromCamera;
    public Camera cam;

    [Tooltip("��� ����(����). 0.5~2 ����")]
    [Min(0f)] public float margin = 1.6f;

    [Header("FromCamera ���")]
    [Tooltip("���ص� ���� 'ũ��'�� ���۰����� ����(��ġ�� ī�޶� ����)")]
    public bool lockSizeToStart = true;
    public bool recalcStartSizeOnEnable = true;

    [Tooltip("����/���� �߰� Ȯ��(���� �հ�)")]
    public float extraWidth = 0f, extraHeight = 0f;

    [Header("Manual ���")]
    public Vector2 manualCenter = Vector2.zero;
    public Vector2 manualSize = new Vector2(12f, 8f);
    [Tooltip("Manual������ margin�� ��������")]
    public bool applyMarginInManual = true;

    [Header("�˻� ���/�ֱ�")]
    public string ballTag = "Ball";
    [Min(0.02f)] public float checkInterval = 0.15f;

    [Header("���� ������ġ")]
    [Tooltip("��/�� ���� ���� ����(���� ���� ���� ����)")]
    [Min(0f)] public float startupGrace = 0.5f;

    [Tooltip("'���� �� ���̶� �� ��'���� ��Ƽ�� ���� Ȱ��")]
    public bool requireBallEverSeen = true;

    [Tooltip("Ball�� 0���� �� �� �� �ð� �̻� ���ӵ� ���� ��Ƽ�� ����")]
    [Min(0f)] public float noBallGrace = 1.0f;

    [Tooltip("GameManager ���¸� ����(ī��Ʈ�ٿ�/��ȯ/NextStage �߿��� ���� X)")]
    public bool respectGameManagerUI = true;

    [Header("����")]
    public bool drawGizmos = true;
    public bool debugLog = false;

    // ���� ����
    float _startTime;
    float _startHalfH, _startHalfW;
    bool _hasStartSize;
    Camera _cachedCam;

    bool _everSawBall = false;
    float _lastSeenBallTime = -999f;   // ���������� '1�� �̻�'�� �� �ð�

    void OnValidate()
    {
        manualSize.x = Mathf.Max(0.01f, manualSize.x);
        manualSize.y = Mathf.Max(0.01f, manualSize.y);
        extraWidth = Mathf.Max(0f, extraWidth);
        extraHeight = Mathf.Max(0f, extraHeight);
    }

    void Awake()
    {
        if (!cam) cam = Camera.main;
        _cachedCam = cam;
        ResetArmingAndGrace();
        CacheStartSize();
    }

    void OnEnable()
    {
        if (recalcStartSizeOnEnable)
        {
            if (!cam) cam = Camera.main;
            if (cam != _cachedCam) { _cachedCam = cam; _hasStartSize = false; }
            CacheStartSize();
        }
        StartCoroutine(CoWatch());
    }

    void ResetArmingAndGrace()
    {
        _startTime = Time.unscaledTime;
        _everSawBall = false;
        _lastSeenBallTime = Time.unscaledTime; // ���� �������� '�� �� ����' ����
    }

    void CacheStartSize()
    {
        if (cam && cam.orthographic)
        {
            _startHalfH = cam.orthographicSize;
            _startHalfW = _startHalfH * cam.aspect;
            _hasStartSize = true;
        }
        else _hasStartSize = false;
    }

    IEnumerator CoWatch()
    {
        var wait = new WaitForSeconds(checkInterval);
        while (true)
        {
            TryCullAndMaybeContinue();
            yield return wait;
        }
    }

    void TryCullAndMaybeContinue()
    {
        // �ʱ� ����
        if (Time.unscaledTime - _startTime < startupGrace) return;

        // ��� Ž��
        var balls = GameObject.FindGameObjectsWithTag(ballTag);

        bool anyLeft = false;
        Bounds b = GetPlayBounds();

        for (int i = 0; i < balls.Length; i++)
        {
            var go = balls[i];
            if (!go || !go.activeInHierarchy) continue;

            Vector3 p = go.transform.position;

            // �ʹ� �� �Ʒ��� �ϵ���
            if (p.y < -50f) { Destroy(go); continue; }

            if (!IsInside2D(b, p))
            {
                Destroy(go);
                continue;
            }

            anyLeft = true;
        }

        if (anyLeft)
        {
            _everSawBall = true;
            _lastSeenBallTime = Time.unscaledTime;
            return;
        }

        // ������ʹ� ���� 0��

        // ���� ���� �� ��ȣ
        if (requireBallEverSeen && !_everSawBall)
        {
            if (debugLog) Debug.Log("[Guard] Skip: never saw a ball yet.");
            return;
        }

        // �ֱٿ� ���� ����� ����(ī��Ʈ�ٿ�/���� ����) ��ȣ
        if (Time.unscaledTime - _lastSeenBallTime < noBallGrace)
        {
            if (debugLog) Debug.Log("[Guard] Skip: noBallGrace window.");
            return;
        }

        // GM UI ���� ����(ī��Ʈ�ٿ�/��ȯ/NextStage ���� �� ���� ����)
        var gm = FindObjectOfType<GameManager>();
        if (respectGameManagerUI && gm)
        {
            // ��ȯ(Ŭ���� ����~���� �� ����) ��
            if (gm.isTransitioning)
            {
                if (debugLog) Debug.Log("[Guard] Skip: GM isTransitioning.");
                return;
            }
            // ī��Ʈ�ٿ� ǥ�� ��
            if (gm.countdownText && gm.countdownText.gameObject.activeInHierarchy)
            {
                if (debugLog) Debug.Log("[Guard] Skip: countdown active.");
                return;
            }
            // NextStage �ؽ�Ʈ�� �̹� ���� ���̸� �ǵ帮�� ����
            if (gm.nextStageText && gm.nextStageText.gameObject.activeInHierarchy)
            {
                if (debugLog) Debug.Log("[Guard] Skip: NextStage text active.");
                return;
            }
            // �̹� ��Ƽ���� �� �ִٸ� �ߺ� ȣ�� ����
            if (gm.IsContinueShown())
            {
                if (debugLog) Debug.Log("[Guard] Skip: Continue already shown.");
                return;
            }
        }

        // ����: ��Ƽ�� ǥ��
        if (gm != null)
        {
            if (debugLog) Debug.Log("[Guard] ShowContinue()");
            gm.ShowContinue();
        }
    }

    Bounds GetPlayBounds()
    {
        if (source == BoundsSource.FromCamera)
        {
            Camera c = cam ? cam : Camera.main;
            if (c && c.orthographic)
            {
                float halfH = (lockSizeToStart && _hasStartSize) ? _startHalfH : c.orthographicSize;
                float halfW = (lockSizeToStart && _hasStartSize) ? _startHalfW : halfH * c.aspect;

                halfW += margin + (extraWidth * 0.5f);
                halfH += margin + (extraHeight * 0.5f);

                Vector3 cc = c.transform.position;
                return new Bounds(new Vector3(cc.x, cc.y, 0f),
                                  new Vector3(halfW * 2f, halfH * 2f, 0.1f));
            }
            // ī�޶� ���ų� ������ �� Manual�� ����
            return BuildManualBounds();
        }
        return BuildManualBounds();
    }

    Bounds BuildManualBounds()
    {
        float w = manualSize.x + (applyMarginInManual ? margin * 2f : 0f) + extraWidth;
        float h = manualSize.y + (applyMarginInManual ? margin * 2f : 0f) + extraHeight;
        return new Bounds(new Vector3(manualCenter.x, manualCenter.y, 0f),
                          new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, h), 0.1f));
    }

    static bool IsInside2D(Bounds b, Vector3 p)
    {
        return (p.x >= b.min.x && p.x <= b.max.x &&
                p.y >= b.min.y && p.y <= b.max.y);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var old = Gizmos.color;
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.24f);
        Bounds b = Application.isPlaying ? GetPlayBounds() : PreviewBoundsInEditor();
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = old;
    }

    Bounds PreviewBoundsInEditor()
    {
        if (source == BoundsSource.FromCamera)
        {
            Camera c = cam ? cam : Camera.main;
            if (c && c.orthographic)
            {
                float halfH = (lockSizeToStart && _hasStartSize) ? _startHalfH : c.orthographicSize;
                float halfW = (lockSizeToStart && _hasStartSize) ? _startHalfW : halfH * c.aspect;

                halfW += margin + (extraWidth * 0.5f);
                halfH += margin + (extraHeight * 0.5f);

                Vector3 cc = c.transform.position;
                return new Bounds(new Vector3(cc.x, cc.y, 0f),
                                  new Vector3(halfW * 2f, halfH * 2f, 0.1f));
            }
        }
        return BuildManualBounds();
    }
#endif
}
