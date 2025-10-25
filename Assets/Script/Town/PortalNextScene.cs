using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// ��Ż Ŭ�� �� UI Image(���� ȭ��)�� ���̵� �� �� �� �ε�
/// - overlayImage: ��ü ȭ���� ���� ������ Image (�ʱ� ��Ȱ��ȭ OK)
/// - Canvas�� Screen Space - Overlay ����
/// - �� ������Ʈ�� ��Ż ������Ʈ(2D Collider ����)�� �ٿ� ���
/// - �� ���̵� �ƿ�(�������)�� ���ŵ�. ���� ������ ���� ���̵� ���� �����ϼ���.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PortalNextScene_UIFadeSimple : MonoBehaviour
{
    [Header("��ȯ ���")]
    public string nextSceneName = "Dungeon";

    [Header("UI �������� (���� ȭ��)")]
    public Image overlayImage;                // �ݵ�� �Ҵ�(���� Image, Ǯ��ũ��)
    public bool blockInputDuringFade = true;  // ���̵� �� �ٸ� Ŭ�� ����

    [Header("Ÿ�̹�(��)")]
    public float fadeInTime = 0.4f;   // Ŭ�� �� ������� �ð�
    [Tooltip("�̻��(����: �� �� ���̵� �ƿ�). �ʿ� ������ �ʵ� ��ü�� �����ص� �˴ϴ�.")]
    public float fadeOutTime = 0.35f; // �� �� �̻� ������� ����

    [Header("Ŭ�� ����(�巡�׿� ����)")]
    public float clickMaxPixelMove = 12f;
    public float clickMaxSeconds = 0.3f;

    // ����
    Camera cam;
    Collider2D col;
    bool pressed;
    Vector2 startScreenPos;
    float startTime;
    bool transitioning;

    void Awake()
    {
        cam = Camera.main ? Camera.main : GetComponentInParent<Camera>();
        col = GetComponent<Collider2D>();

        // �ʱ⿣ ���δ� �� ������ ����
        if (overlayImage)
        {
            var go = overlayImage.gameObject;
            // �� DontDestroyOnLoad ����: ���� ���� �������̰� ���� �ʰ� ��
            go.SetActive(false);
            SetAlpha(overlayImage, 0f);
        }
    }

    void Update()
    {
        if (transitioning) return;

        if (Input.GetMouseButtonDown(0))
        {
            pressed = true;
            startScreenPos = Input.mousePosition;
            startTime = Time.time;
        }
        else if (Input.GetMouseButtonUp(0) && pressed)
        {
            pressed = false;

            // �巡�׿� ����
            float movePix = (Input.mousePosition - (Vector3)startScreenPos).magnitude;
            float dur = Time.time - startTime;
            bool isClick = (movePix <= clickMaxPixelMove) && (dur <= clickMaxSeconds);
            if (!isClick) return;

            // ��Ż �ݶ��̴� ������
            Vector3 wp = cam ? cam.ScreenToWorldPoint(Input.mousePosition) : (Vector3)Input.mousePosition;
            Vector2 p = new Vector2(wp.x, wp.y);
            if (col && col.OverlapPoint(p))
            {
                if (string.IsNullOrEmpty(nextSceneName))
                {
                    Debug.LogWarning("[PortalNextScene_UIFadeSimple] nextSceneName �������.");
                    return;
                }
                if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
                {
                    Debug.LogError($"[PortalNextScene_UIFadeSimple] '{nextSceneName}' ���� Build Settings�� ����.");
                    return;
                }
                StartCoroutine(Co_Transition());
            }
        }
    }

    IEnumerator Co_Transition()
    {
        transitioning = true;

        // 1) ���̵� �� (�������)
        if (overlayImage)
        {
            var go = overlayImage.gameObject;
            go.SetActive(true);
            if (blockInputDuringFade) overlayImage.raycastTarget = true;
            yield return Co_Fade(overlayImage, 0f, 1f, Mathf.Max(0.01f, fadeInTime));
        }

        // 2) �� �ε� (�񵿱� ����)
        AsyncOperation op = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        // �� ���̵� �ƿ� ����: �� ������ ������� ������ ���� ����(SceneFadeIn ��)
        // �������̴� Ÿ�� ���� �Բ� ������

        transitioning = false;
    }

    IEnumerator Co_Fade(Image img, float from, float to, float dur)
    {
        float t = 0f;
        SetAlpha(img, from);
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / dur);
            SetAlpha(img, a);
            yield return null;
        }
        SetAlpha(img, to);
    }

    void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }
}
