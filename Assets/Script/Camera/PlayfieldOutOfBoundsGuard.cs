using System.Collections;
using UnityEngine;

/// <summary>
/// 화면/플레이필드 밖으로 나간 Ball을 제거하고,
/// 남은 Ball이 일정 시간(noBallGrace) 이상 0개면 GameManager.ShowContinue() 호출.
/// - 카메라/Manual 영역 지원, margin/추가확장 제공
/// - 카운트다운/클리어 전환/NextStage 텍스트 노출 중에는 개입하지 않음
/// - '공을 한 번이라도 본 이후'에만 컨티뉴 판단(게임 시작 전 보호)
/// </summary>
public class PlayfieldOutOfBoundsGuard : MonoBehaviour
{
    public enum BoundsSource { FromCamera, Manual }

    [Header("Bounds 소스")]
    public BoundsSource source = BoundsSource.FromCamera;
    public Camera cam;

    [Tooltip("사방 여유(유닛). 0.5~2 권장")]
    [Min(0f)] public float margin = 1.6f;

    [Header("FromCamera 고급")]
    [Tooltip("줌해도 영역 '크기'는 시작값으로 고정(위치는 카메라 따라감)")]
    public bool lockSizeToStart = true;
    public bool recalcStartSizeOnEnable = true;

    [Tooltip("가로/세로 추가 확장(양쪽 합계)")]
    public float extraWidth = 0f, extraHeight = 0f;

    [Header("Manual 모드")]
    public Vector2 manualCenter = Vector2.zero;
    public Vector2 manualSize = new Vector2(12f, 8f);
    [Tooltip("Manual에서도 margin을 적용할지")]
    public bool applyMarginInManual = true;

    [Header("검사 대상/주기")]
    public string ballTag = "Ball";
    [Min(0.02f)] public float checkInterval = 0.15f;

    [Header("판정 안전장치")]
    [Tooltip("씬/방 진입 직후 유예(스폰 직후 오검 방지)")]
    [Min(0f)] public float startupGrace = 0.5f;

    [Tooltip("'공을 한 번이라도 본 후'에만 컨티뉴 로직 활성")]
    public bool requireBallEverSeen = true;

    [Tooltip("Ball이 0개가 된 뒤 이 시간 이상 지속될 때만 컨티뉴 실행")]
    [Min(0f)] public float noBallGrace = 1.0f;

    [Tooltip("GameManager 상태를 존중(카운트다운/전환/NextStage 중에는 개입 X)")]
    public bool respectGameManagerUI = true;

    [Header("진단")]
    public bool drawGizmos = true;
    public bool debugLog = false;

    // 내부 상태
    float _startTime;
    float _startHalfH, _startHalfW;
    bool _hasStartSize;
    Camera _cachedCam;

    bool _everSawBall = false;
    float _lastSeenBallTime = -999f;   // 마지막으로 '1개 이상'을 본 시각

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
        _lastSeenBallTime = Time.unscaledTime; // 시작 시점에는 '본 적 없다' 가정
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
        // 초기 유예
        if (Time.unscaledTime - _startTime < startupGrace) return;

        // 대상 탐색
        var balls = GameObject.FindGameObjectsWithTag(ballTag);

        bool anyLeft = false;
        Bounds b = GetPlayBounds();

        for (int i = 0; i < balls.Length; i++)
        {
            var go = balls[i];
            if (!go || !go.activeInHierarchy) continue;

            Vector3 p = go.transform.position;

            // 너무 먼 아래로 하드컷
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

        // 여기부터는 볼이 0개

        // 게임 시작 전 보호
        if (requireBallEverSeen && !_everSawBall)
        {
            if (debugLog) Debug.Log("[Guard] Skip: never saw a ball yet.");
            return;
        }

        // 최근에 볼이 사라진 직후(카운트다운/스폰 간극) 보호
        if (Time.unscaledTime - _lastSeenBallTime < noBallGrace)
        {
            if (debugLog) Debug.Log("[Guard] Skip: noBallGrace window.");
            return;
        }

        // GM UI 상태 존중(카운트다운/전환/NextStage 점멸 중 개입 금지)
        var gm = FindObjectOfType<GameManager>();
        if (respectGameManagerUI && gm)
        {
            // 전환(클리어 순간~다음 방 진입) 중
            if (gm.isTransitioning)
            {
                if (debugLog) Debug.Log("[Guard] Skip: GM isTransitioning.");
                return;
            }
            // 카운트다운 표시 중
            if (gm.countdownText && gm.countdownText.gameObject.activeInHierarchy)
            {
                if (debugLog) Debug.Log("[Guard] Skip: countdown active.");
                return;
            }
            // NextStage 텍스트가 이미 노출 중이면 건드리지 않음
            if (gm.nextStageText && gm.nextStageText.gameObject.activeInHierarchy)
            {
                if (debugLog) Debug.Log("[Guard] Skip: NextStage text active.");
                return;
            }
            // 이미 컨티뉴가 떠 있다면 중복 호출 금지
            if (gm.IsContinueShown())
            {
                if (debugLog) Debug.Log("[Guard] Skip: Continue already shown.");
                return;
            }
        }

        // 최종: 컨티뉴 표시
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
            // 카메라 없거나 비정상 → Manual로 폴백
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
