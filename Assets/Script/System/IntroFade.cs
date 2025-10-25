using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntroFade : MonoBehaviour
{
    [Header("����")]
    public SpriteRenderer logo;            // ȸ�� �ΰ� (����)
    [Tooltip("ȭ���� �� ���� '������' ��������Ʈ(����). ������ �̰ɷ� ȭ�� ���̵�")]
    public SpriteRenderer screenFade;      // ��ü ȭ�� ���� ��������(����)

    [Header("Ÿ�̹�(��)")]
    public float fadeIn = 0.6f;
    public float hold = 1.5f;
    public float fadeOut = 0.6f;

    [Header("��ȯ")]
    public string nextSceneName = "Logo";
    public bool anyKeySkip = true;
    [Tooltip("ON�̸� ��Ʈ�� ���� �� nextSceneName���� �� ��ȯ, OFF�� �� �� ����")]
    public bool loadNextScene = true;      // �� �߰�: �� �ε� ��/����

    bool _skipped; // �� ref ��� ���� �÷��� ���

    void Reset()
    {
        if (!logo) logo = GetComponentInChildren<SpriteRenderer>();
        if (!screenFade) screenFade = null;
    }

    IEnumerator Start()
    {
        _skipped = false;

        // �ʱ� ����: �������̰� ������ �ΰ�� 1(����), ȭ���� 1(��İ�).
        if (screenFade)
        {
            if (logo) SetAlpha(logo, 1f);
            SetAlpha(screenFade, 1f);
        }
        else
        {
            if (logo) SetAlpha(logo, 0f); // �������� ������ �ΰ� ��ü ���̵�
        }

        // �� ���̵� ��
        if (screenFade) yield return FadeSprite(screenFade, 1f, 0f, fadeIn);
        else if (logo) yield return FadeSprite(logo, 0f, 1f, fadeIn);

        // �� Ȧ�� (��ŵ �ݿ�)
        float t = 0f;
        while (t < hold)
        {
            if (_skipped || (anyKeySkip && (Input.anyKeyDown || Input.GetMouseButtonDown(0))))
            { _skipped = true; break; }
            t += Time.deltaTime;
            yield return null;
        }

        // �� ���̵� �ƿ� (��ŵ�̸� ª��)
        float outTime = _skipped ? Mathf.Min(0.15f, fadeOut) : fadeOut;
        if (screenFade) yield return FadeSprite(screenFade, screenFade.color.a, 1f, outTime);
        else if (logo) yield return FadeSprite(logo, logo.color.a, 0f, outTime);

        // �� ���� �� �ε�(�ɼ�)
        if (loadNextScene && !string.IsNullOrEmpty(nextSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
            else
                Debug.LogError($"[IntroFade] '{nextSceneName}' ���� Build Settings�� �����ϴ�.");
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
        SetAlpha(sr, to); // ������ ����
    }

    void SetAlpha(SpriteRenderer sr, float a)
    {
        if (!sr) return;
        var c = sr.color; c.a = a; sr.color = c;
    }
}
