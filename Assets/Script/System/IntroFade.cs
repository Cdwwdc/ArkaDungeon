using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntroFade : MonoBehaviour
{
    [Header("참조")]
    public SpriteRenderer logo;            // 회사 로고 (선택)
    [Tooltip("화면을 꽉 덮는 '검은색' 스프라이트(선택). 있으면 이걸로 화면 페이드")]
    public SpriteRenderer screenFade;      // 전체 화면 검은 오버레이(선택)

    [Header("타이밍(초)")]
    public float fadeIn = 0.6f;
    public float hold = 1.5f;
    public float fadeOut = 0.6f;

    [Header("전환")]
    public string nextSceneName = "Logo";
    public bool anyKeySkip = true;
    [Tooltip("ON이면 인트로 종료 시 nextSceneName으로 씬 전환, OFF면 현 씬 유지")]
    public bool loadNextScene = true;      // ▶ 추가: 씬 로드 온/오프

    bool _skipped; // ▶ ref 대신 내부 플래그 사용

    void Reset()
    {
        if (!logo) logo = GetComponentInChildren<SpriteRenderer>();
        if (!screenFade) screenFade = null;
    }

    IEnumerator Start()
    {
        _skipped = false;

        // 초기 알파: 오버레이가 있으면 로고는 1(보임), 화면은 1(까맣게).
        if (screenFade)
        {
            if (logo) SetAlpha(logo, 1f);
            SetAlpha(screenFade, 1f);
        }
        else
        {
            if (logo) SetAlpha(logo, 0f); // 오버레이 없으면 로고 자체 페이드
        }

        // ■ 페이드 인
        if (screenFade) yield return FadeSprite(screenFade, 1f, 0f, fadeIn);
        else if (logo) yield return FadeSprite(logo, 0f, 1f, fadeIn);

        // ■ 홀드 (스킵 반영)
        float t = 0f;
        while (t < hold)
        {
            if (_skipped || (anyKeySkip && (Input.anyKeyDown || Input.GetMouseButtonDown(0))))
            { _skipped = true; break; }
            t += Time.deltaTime;
            yield return null;
        }

        // ■ 페이드 아웃 (스킵이면 짧게)
        float outTime = _skipped ? Mathf.Min(0.15f, fadeOut) : fadeOut;
        if (screenFade) yield return FadeSprite(screenFade, screenFade.color.a, 1f, outTime);
        else if (logo) yield return FadeSprite(logo, logo.color.a, 0f, outTime);

        // ■ 다음 씬 로드(옵션)
        if (loadNextScene && !string.IsNullOrEmpty(nextSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
            else
                Debug.LogError($"[IntroFade] '{nextSceneName}' 씬이 Build Settings에 없습니다.");
        }
    }

    IEnumerator FadeSprite(SpriteRenderer sr, float from, float to, float dur)
    {
        dur = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / dur);
            SetAlpha(sr, a);

            if (anyKeySkip && !_skipped && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            { _skipped = true; break; }

            yield return null;
        }
        SetAlpha(sr, to); // 최종값 보정
    }

    void SetAlpha(SpriteRenderer sr, float a)
    {
        if (!sr) return;
        var c = sr.color; c.a = a; sr.color = c;
    }
}
