using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// 게임 전역 FX 컨트롤러(단일):
/// - Death: 슬로모 + 카메라 쉐이크
/// - Clear:  슬로모 + 오쏘 줌/팬 + 쉐이크 + 브릭 고스트 페이드 + 카메라 원점복귀(0,0,-10)
[DefaultExecutionOrder(9000)]
public class CinematicFX : MonoBehaviour
{
    public static CinematicFX I { get; private set; }
    public static bool IsPlaying { get; private set; }

    [Header("공통")]
    public bool overrideIfRunning = true;
    public bool debugLog = false;

    // ───────────── 사망(Dead) ─────────────
    [Header("Death FX (슬로모/쉐이크)")]
    public float deadSlowDuration = 1.5f;
    [Range(0.01f, 1f)] public float deadTimeScale = 0.214f;
    public float deadShakeDuration = 0.4f;
    public float deadShakeAmplitude = 0.3f;
    [Tooltip("비워두면 targetCam → Camera.main 순으로 자동 선택")]
    public Transform deathCameraOverride;
    public bool disableBehavioursOnDeath = true;

    [Header("Death Camera Lock")] // ★추가
    [Tooltip("죽음 연출 중 카메라 오쏘 사이즈를 원래 값으로 하드락(줌 튐 방지)")]
    public bool lockOrthoOnDeath = true; // ★추가
    [Tooltip("연출 종료 후 몇 프레임 더 하드락할지")]
    public int deathPostLockFrames = 3;  // ★추가

    // ───────────── 클리어(Clear) ─────────────
    [Header("Clear FX (슬로모/줌/팬/쉐이크/고스트)")]
    public float clearSlowDuration = 3f;
    [Range(0.05f, 1f)] public float clearTimeScale = 0.2f;

    [Header("카메라(메인)")]
    public Camera targetCam;
    public bool forceOrthographic = true;
    public bool disableAllBehaviours = true;

    [Header("줌 (통합)")]
    public bool useUnifiedZoom = true;
    [Range(0.1f, 0.95f)] public float zoomFactor = 0.15f;
    public AnimationCurve zoomUnifiedCurve = AnimationCurve.Linear(0, 0, 1, 0);

    [Header("팬")]
    public bool enablePan = true;
    [Range(0f, 1f)] public float panStrength = 0.838f;

    [Header("쉐이크")]
    public bool enableShake = true;
    public float shakeAmplitude = 0.05f;
    public float shakeFrequency = 25f;
    public bool shakeUseScaledTime = true;

    [Header("복귀 위치")]
    public bool returnToOriginAfter = true;
    public Vector3 originPosition = new Vector3(0f, 0f, -10f);

    [Header("브릭 고스트 페이드")]
    public bool alwaysGhostBrick = true;
    public float ghostLinger = 1.0f;
    public float ghostFadeStart = 0.15f;
    public float ghostSortOffset = 1000f;
    public bool forceGhostSortingLayer = false;
    public string ghostSortingLayerName = "Default";

    // 내부 상태(클리어)
    float baseOrtho;
    bool prevOrthographic;
    Vector3 baseCamPos;
    Behaviour[] disabledBehaviours;
    Camera tempCam;

    float baseFixedDelta;

    // ───── 안전 보장 ─────
    public static CinematicFX EnsureInstance()
    {
        if (I) return I;
        var found = FindObjectOfType<CinematicFX>();
        if (found) { I = found; return I; }
        var go = new GameObject("CinematicFX(Auto)");
        I = go.AddComponent<CinematicFX>();
        if (I.debugLog) Debug.Log("[CinematicFX] Auto-created instance.");
        return I;
    }
    public static void TryPlayDeath()
    {
        var inst = EnsureInstance();
        inst.PlayDeath();
    }

    void Reset()
    {
        deadSlowDuration = 1.5f; deadTimeScale = 0.214f; deadShakeDuration = 0.4f; deadShakeAmplitude = 0.3f;
        clearSlowDuration = 3f; clearTimeScale = 0.2f; zoomFactor = 0.15f; panStrength = 0.838f;
        shakeAmplitude = 0.05f; shakeFrequency = 25f; originPosition = new Vector3(0, 0, -10);
        alwaysGhostBrick = true; ghostLinger = 1f; ghostFadeStart = 0.15f; ghostSortOffset = 1000f;
        lockOrthoOnDeath = true; deathPostLockFrames = 3; // ★추가 디폴트

        zoomUnifiedCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f),
            new Keyframe(0.25f, 0.85f, 0f, 0f),
            new Keyframe(0.75f, 0.85f, 0f, 0f),
            new Keyframe(1f, 0f, -3f, 0f)
        );
    }

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        baseFixedDelta = Time.fixedDeltaTime;
        if (zoomUnifiedCurve == null || zoomUnifiedCurve.length < 2) Reset();
        if (debugLog) Debug.Log("[CinematicFX] Awake & ready.");
    }

    void OnDisable() { StopAllCoroutines(); }
    void OnDestroy()
    {
        StopAllCoroutines();
        if (tempCam) Destroy(tempCam.gameObject);
    }

    // ───────── 공개 API ─────────
    public void PlayDeath()
    {
        if (debugLog) Debug.Log($"[CinematicFX] PlayDeath called. IsPlaying={IsPlaying}, override={overrideIfRunning}");
        if (IsPlaying && !overrideIfRunning) return;
        StopAllCoroutines();
        StartCoroutine(CoDeath());
    }

    public void PlayClear(Vector3 focusPos, GameObject ballRoot = null, SpriteRenderer brickRenderer = null)
    {
        if (debugLog) Debug.Log($"[CinematicFX] PlayClear called. focus={focusPos}");
        if (IsPlaying && !overrideIfRunning) return;
        StopAllCoroutines();
        StartCoroutine(CoClear(focusPos, ballRoot, brickRenderer));
    }

    public static void ForceHomeNow()
    {
        if (I == null || I.targetCam == null) return;
        I.targetCam.transform.position = I.originPosition;
    }

    // ───────── Death 구현 ─────────
    IEnumerator CoDeath()
    {
        IsPlaying = true;
        if (debugLog) Debug.Log("[CinematicFX] CoDeath start.");

        // 1) 슬로모
        float dur = Mathf.Max(0.01f, deadSlowDuration);
        float ts = Mathf.Clamp(deadTimeScale, 0.01f, 1f);
        StartCoroutine(CoForceTimescale(ts, dur));

        // 2) 카메라 선택
        Transform camT = null;
        Camera camComp = null; // ★추가
        if (deathCameraOverride) { camT = deathCameraOverride; camComp = camT.GetComponent<Camera>(); }
        else if (targetCam) { camT = targetCam.transform; camComp = targetCam; }
        else if (Camera.main) { camT = Camera.main.transform; camComp = Camera.main; }

        if (debugLog) Debug.Log($"[CinematicFX] DeathFX camera = {(camT ? camT.name : "NULL")}");

        // 2-1) 오쏘 사이즈 백업 + 하드락 스타트(옵션) // ★추가
        float deathOrtho = 0f;
        if (lockOrthoOnDeath && camComp && camComp.orthographic)
        {
            deathOrtho = camComp.orthographicSize;
            StartCoroutine(CoLockOrtho(camComp, deathOrtho, dur, deathPostLockFrames));
        }

        // 2-2) Behaviour 잠시 OFF
        List<Behaviour> disabled = null;
        if (disableBehavioursOnDeath && camComp)
        {
            disabled = new List<Behaviour>();
            foreach (var b in camComp.GetComponents<Behaviour>())
            {
                if (!b || !b.enabled) continue;
                if (b is Camera || b is AudioListener) continue;
                b.enabled = false; disabled.Add(b);
            }
            if (debugLog && disabled.Count > 0) Debug.Log($"[CinematicFX] Disabled {disabled.Count} behaviours on death.");
        }

        // 3) 쉐이크(EndOfFrame)
        if (!camT)
        {
            Debug.LogWarning("[CinematicFX] Death shake skipped: no camera");
            float t0 = 0f; while (t0 < dur) { t0 += Time.unscaledDeltaTime; yield return null; }
        }
        else if (deadShakeDuration > 0f && deadShakeAmplitude > 0f)
        {
            yield return StartCoroutine(CoShakeOnceEndOfFrame(camT, deadShakeDuration, deadShakeAmplitude));
        }
        else
        {
            float t0 = 0f; while (t0 < dur) { t0 += Time.unscaledDeltaTime; yield return null; }
        }

        // 4) Behaviour 복구
        if (disabled != null) foreach (var b in disabled) if (b) b.enabled = true;

        if (debugLog) Debug.Log("[CinematicFX] CoDeath end.");
        IsPlaying = false;
    }

    // ───────── Clear 구현 (변경 없음) ─────────
    IEnumerator CoClear(Vector3 focusPos, GameObject ballRoot, SpriteRenderer brickRenderer)
    {
        IsPlaying = true;

        var cam = targetCam ? targetCam : Camera.main;
        if (!cam) { Debug.LogWarning("[CinematicFX] targetCam 없음"); IsPlaying = false; yield break; }

        prevOrthographic = cam.orthographic;
        baseOrtho = cam.orthographic ? cam.orthographicSize : 5f;
        baseCamPos = cam.transform.position;
        if (forceOrthographic && !cam.orthographic)
        {
            cam.orthographic = true;
            if (Mathf.Approximately(baseOrtho, 5f)) baseOrtho = Mathf.Max(5f, baseOrtho);
        }

        if (disableAllBehaviours)
        {
            var list = new List<Behaviour>();
            foreach (var b in cam.GetComponents<Behaviour>())
            {
                if (!b || !b.enabled) continue;
                if (b is Camera || b is AudioListener) continue;
                b.enabled = false; list.Add(b);
            }
            disabledBehaviours = list.ToArray();
        }

        var ghosts = new List<GameObject>();
        if (alwaysGhostBrick && brickRenderer)
        {
            ghosts.AddRange(MakeGhostsFromHierarchy(brickRenderer.transform, ghostLinger, ghostFadeStart));
        }

        StartCoroutine(CoForceTimescale(clearTimeScale, clearSlowDuration));

        bool blocked = false;
        yield return StartCoroutine(CoZoomPanUnified_Main(cam, focusPos, v => blocked = v));
        if (blocked) { /* 필요 시 플랜B */ }

        foreach (var g in ghosts) if (g) Destroy(g);

        cam.orthographicSize = baseOrtho;
        cam.transform.position = returnToOriginAfter ? originPosition : baseCamPos;
        if (forceOrthographic) cam.orthographic = prevOrthographic;
        if (disabledBehaviours != null) foreach (var b in disabledBehaviours) if (b) b.enabled = true;
        if (returnToOriginAfter) StartCoroutine(CoHardHome(cam, originPosition, 3));

        IsPlaying = false;
    }

    // === 통합 줌/팬 (메인 카메라) ===
    IEnumerator CoZoomPanUnified_Main(Camera cam, Vector3 focusPos, System.Action<bool> setBlocked)
    {
        setBlocked?.Invoke(false);

        float from = baseOrtho;
        float to = baseOrtho * Mathf.Clamp(zoomFactor, 0.1f, 0.95f);
        Vector3 panTarget = ComputePanTarget(focusPos);

        float t = 0f, dur = Mathf.Max(0.0001f, clearSlowDuration);
        while (t < dur)
        {
            float u = Mathf.Clamp01(t / dur);
            float k = Mathf.Clamp01(zoomUnifiedCurve.Evaluate(u));
            cam.orthographicSize = Mathf.Lerp(from, to, k);

            Vector3 wantPos = enablePan ? Vector3.Lerp(baseCamPos, panTarget, k) : baseCamPos;
            wantPos += ComputeShakeOffset(u);
            cam.transform.position = wantPos;

            yield return new WaitForEndOfFrame();
            if (Mathf.Abs(cam.orthographicSize - Mathf.Lerp(from, to, k)) > 0.0001f ||
                (cam.transform.position - wantPos).sqrMagnitude > 1e-6f)
            { setBlocked?.Invoke(true); yield break; }

            t += Time.unscaledDeltaTime;
        }

        cam.orthographicSize = from;
        cam.transform.position = baseCamPos;
    }

    // === 유틸 ===
    Vector3 ComputePanTarget(Vector3 focusPos)
    {
        if (!enablePan || panStrength <= 0f) return baseCamPos;
        Vector3 target = new Vector3(focusPos.x, focusPos.y, baseCamPos.z);
        return Vector3.Lerp(baseCamPos, target, Mathf.Clamp01(panStrength));
    }

    Vector3 ComputeShakeOffset(float u)
    {
        if (!enableShake || shakeAmplitude <= 0f) return Vector3.zero;
        float t = shakeUseScaledTime ? Time.time : Time.unscaledTime;
        float envelope = Mathf.Sin(u * Mathf.PI);
        float nx = (Mathf.PerlinNoise(0f, t * shakeFrequency) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(1f, t * shakeFrequency) - 0.5f) * 2f;
        return new Vector3(nx, ny, 0f) * (shakeAmplitude * envelope);
    }

    IEnumerator CoForceTimescale(float target, float duration)
    {
        float step = baseFixedDelta * Mathf.Clamp(target, 0.001f, 1f);
        float remain = Mathf.Max(0.0001f, duration);
        while (remain > 0f)
        {
            if (Time.timeScale != target) Time.timeScale = target;
            if (!Mathf.Approximately(Time.fixedDeltaTime, step)) Time.fixedDeltaTime = step;
            remain -= Time.unscaledDeltaTime;
            yield return null;
        }
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
    }

    IEnumerator CoShakeOnceEndOfFrame(Transform cam, float duration, float amplitude)
    {
        if (!cam || amplitude <= 0f || duration <= 0f) yield break;
        Vector3 origin = cam.position;
        float t = 0f;
        if (debugLog) Debug.Log("[CinematicFX] Death shake start.");

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);
            float offX = (Random.value * 2f - 1f) * amplitude * k;
            float offY = (Random.value * 2f - 1f) * amplitude * k;

            yield return new WaitForEndOfFrame();
            cam.position = origin + new Vector3(offX, offY, 0f);
        }
        yield return new WaitForEndOfFrame();
        cam.position = origin;
        if (debugLog) Debug.Log("[CinematicFX] Death shake end.");
    }

    // ★추가: 오쏘 사이즈 하드락(죽음 연출 동안 + 종료 후 N프레임)
    IEnumerator CoLockOrtho(Camera cam, float size, float duration, int extraFrames)
    {
        float t = 0f;
        while (t < duration)
        {
            if (cam) cam.orthographicSize = size;
            yield return new WaitForEndOfFrame();
            t += Time.unscaledDeltaTime;
        }
        for (int i = 0; i < Mathf.Max(0, extraFrames); i++)
        {
            if (cam) cam.orthographicSize = size;
            yield return new WaitForEndOfFrame();
        }
    }

    // 고스트/페이드/하드락(클리어) — 변동 없음
    List<GameObject> MakeGhostsFromHierarchy(Transform root, float linger, float fadeStart)
    {
        var list = new List<GameObject>();
        var srcs = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var s in srcs)
        {
            var go = new GameObject("Ghost_" + s.gameObject.name);
            go.layer = s.gameObject.layer;

            var gr = go.AddComponent<SpriteRenderer>();
            gr.sprite = s.sprite;
            gr.color = s.color;
            gr.flipX = s.flipX; gr.flipY = s.flipY;
            if (forceGhostSortingLayer && !string.IsNullOrEmpty(ghostSortingLayerName))
                gr.sortingLayerName = ghostSortingLayerName;
            else
                gr.sortingLayerID = s.sortingLayerID;
            gr.sortingOrder = s.sortingOrder + (int)ghostSortOffset;

            go.transform.position = s.transform.position;
            go.transform.rotation = s.transform.rotation;
            go.transform.localScale = s.transform.lossyScale;

            StartCoroutine(CoFadeAndKill(gr, linger, fadeStart));
            list.Add(go);
        }
        return list;
    }

    IEnumerator CoFadeAndKill(SpriteRenderer gr, float linger, float fadeStart)
    {
        if (!gr) yield break;
        Color start = gr.color;
        float t = 0f;
        float fadeBeg = Mathf.Max(0f, fadeStart);
        float fadeEnd = Mathf.Max(fadeBeg + 0.0001f, linger);

        while (t < linger)
        {
            if (!gr) yield break;
            float a = 1f;
            if (t > fadeBeg)
            {
                float k = Mathf.InverseLerp(fadeBeg, fadeEnd, t);
                a = Mathf.Lerp(1f, 0f, k);
            }
            try { var c = start; c.a = a; gr.color = c; }
            catch (MissingReferenceException) { yield break; }

            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (gr) Destroy(gr.gameObject);
    }

    IEnumerator CoHardHome(Camera cam, Vector3 home, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            cam.transform.position = home;
            yield return new WaitForEndOfFrame();
        }
    }
}
