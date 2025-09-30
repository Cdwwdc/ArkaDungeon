using UnityEngine;
using System.Collections;

public class SlowMoFX : MonoBehaviour
{
    [Header("공통")]
    public bool overrideIfRunning = true;
    public bool debugLog = false;

    [Header("죽음(Dead) 슬로우/쉐이크")]
    public float deadSlowDuration = 1.0f;
    [Range(0.01f, 1f)] public float deadTimeScale = 0.2f;
    public float deadShakeDuration = 0.15f;
    public float deadShakeAmplitude = 0.12f;

    [Header("클리어(Clear) 슬로우/쉐이크")]
    public float clearSlowDuration = 1.0f;
    [Range(0.01f, 1f)] public float clearTimeScale = 0.2f;
    public float clearShakeDuration = 0.12f;
    public float clearShakeAmplitude = 0.10f;

    [Header("옵션")]
    public Transform cameraOverride;

    Coroutine slowCR;
    float baseFixedDelta;

    void Awake()
    {
        baseFixedDelta = Time.fixedDeltaTime;
    }

    public void PlayDeathFX() => PlaySlow(deadSlowDuration, deadTimeScale, deadShakeDuration, deadShakeAmplitude);
    public void PlayClearFX() => PlaySlow(clearSlowDuration, clearTimeScale, clearShakeDuration, clearShakeAmplitude);

    void PlaySlow(float duration, float targetScale, float shakeDur, float shakeAmp)
    {
        duration = Mathf.Max(0.01f, duration);
        targetScale = Mathf.Clamp(targetScale, 0.01f, 1f);

        if (slowCR != null)
        {
            if (!overrideIfRunning) return;
            StopCoroutine(slowCR);
            // 원복해두고 다시 시작
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDelta;
            if (debugLog) Debug.Log("[SlowMoFX] override running slow-mo");
        }
        slowCR = StartCoroutine(CoSlow(duration, targetScale, shakeDur, shakeAmp));
    }

    IEnumerator CoSlow(float duration, float targetScale, float shakeDur, float shakeAmp)
    {
        // 현재 상태 백업
        float prevScale = Time.timeScale;
        float prevFixed = Time.fixedDeltaTime;

        // 1) 슬로모 먼저 ‘즉시’ 적용
        Time.timeScale = targetScale;
        Time.fixedDeltaTime = baseFixedDelta * targetScale;
        if (debugLog) Debug.Log($"[SlowMoFX] ENTER slow-mo ts={targetScale}, dur={duration}");

        // 2) 다음 프레임부터 쉐이크 시작 (슬로모가 먼저 체감되도록)
        yield return null;
        if (shakeDur > 0f && shakeAmp > 0f) StartCoroutine(CoShake(shakeDur, shakeAmp));

        // 3) 유지 구간 — 매 프레임 ‘강제로’ 유지 (다른 스크립트가 덮어써도 다시 세팅)
        float t = 0f;
        while (t < duration)
        {
            // 강제 유지
            if (!Mathf.Approximately(Time.timeScale, targetScale))
                Time.timeScale = targetScale;
            if (!Mathf.Approximately(Time.fixedDeltaTime, baseFixedDelta * targetScale))
                Time.fixedDeltaTime = baseFixedDelta * targetScale;

            t += Time.unscaledDeltaTime; // 리얼타임 기준
            yield return null;
        }

        // 4) 원복
        Time.timeScale = prevScale;         // 보통 1
        Time.fixedDeltaTime = prevFixed;
        if (debugLog) Debug.Log("[SlowMoFX] EXIT slow-mo");
        slowCR = null;
    }

    IEnumerator CoShake(float duration, float amplitude)
    {
        var cam = cameraOverride ? cameraOverride : (Camera.main ? Camera.main.transform : null);
        if (!cam) yield break;

        Vector3 origin = cam.localPosition;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime; // 슬로모 무시하고 일정 시간 유지
            float k = 1f - Mathf.Clamp01(time / duration);
            float offX = (Random.value * 2f - 1f) * amplitude * k;
            float offY = (Random.value * 2f - 1f) * amplitude * k;
            cam.localPosition = origin + new Vector3(offX, offY, 0f);
            yield return null;
        }

        cam.localPosition = origin;
    }
}
