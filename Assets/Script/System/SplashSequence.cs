using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Canvas))]
public class SplashSequence : MonoBehaviour
{
    public SplashConfig config;
    public Image image;           // 풀스크린 Image
    public CanvasGroup canvasGroup; // 페이드용

    AsyncOperation preloadOp;

    void Awake()
    {
        if (!canvasGroup) canvasGroup = image.GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = image.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        Application.targetFrameRate = 60;
    }

    IEnumerator Start()
    {
        if (config == null || config.slides == null || config.slides.Length == 0)
        {
            yield return LoadNextSceneNow();
            yield break;
        }

        // 미리 로드
        if (config.loadNextAsync && !string.IsNullOrEmpty(config.nextSceneName))
        {
            preloadOp = SceneManager.LoadSceneAsync(config.nextSceneName);
            preloadOp.allowSceneActivation = false;
        }

        for (int i = 0; i < config.slides.Length; i++)
        {
            var s = config.slides[i];
            if (!s.sprite) continue;

            image.color = s.color;
            image.sprite = s.sprite;
            image.SetNativeSize(); // 필요 시 주석 처리(해상도 맞출 거면 PreserveAspect 활용)
            image.preserveAspect = true;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;

            // Fade In
            yield return FadeTo(1f, s.fadeIn, s);

            // Hold
            float t = 0f;
            while (t < s.showSeconds)
            {
                t += Time.deltaTime;
                if (ShouldSkip(s)) break;
                yield return null;
            }

            // Fade Out (스킵 시 즉시 아웃)
            float outTime = ShouldSkip(s) ? Mathf.Min(0.12f, s.fadeOut) : s.fadeOut;
            yield return FadeTo(0f, outTime, s);

            if (ShouldSkip(s)) break; // 다음 슬라이드 생략
        }

        yield return LoadNextSceneNow();
    }

    bool ShouldSkip(SplashConfig.Slide s)
    {
        if (!s.skippable) return false;
        if (config.anyKeySkip && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) return true;
        if (Input.GetKeyDown(config.skipKey)) return true;
        return false;
    }

    IEnumerator FadeTo(float target, float duration, SplashConfig.Slide s)
    {
        duration = Mathf.Max(0.01f, duration);
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = t / duration;
            canvasGroup.alpha = Mathf.Lerp(start, target, u);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    IEnumerator LoadNextSceneNow()
    {
        if (string.IsNullOrEmpty(config?.nextSceneName))
            yield break;

        if (preloadOp == null && config.loadNextAsync)
        {
            preloadOp = SceneManager.LoadSceneAsync(config.nextSceneName);
            preloadOp.allowSceneActivation = false;
        }

        if (config.loadNextAsync)
        {
            if (config.waitUntilLoaded)
            {
                // 0.9f까지 로딩됨 (Unity 규칙)
                while (preloadOp.progress < 0.9f)
                    yield return null;
            }
            preloadOp.allowSceneActivation = true;
            yield break;
        }
        else
        {
            SceneManager.LoadScene(config.nextSceneName);
        }
    }
}
