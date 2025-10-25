using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 포탈 클릭 → UI Image(검은 화면)로 페이드 인 → 씬 로드
/// - overlayImage: 전체 화면을 덮는 검은색 Image (초기 비활성화 OK)
/// - Canvas는 Screen Space - Overlay 권장
/// - 이 컴포넌트는 포탈 오브젝트(2D Collider 포함)에 붙여 사용
/// - ※ 페이드 아웃(밝아지기)은 제거됨. 던전 씬에서 따로 페이드 인을 구성하세요.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PortalNextScene_UIFadeSimple : MonoBehaviour
{
    [Header("전환 대상")]
    public string nextSceneName = "Dungeon";

    [Header("UI 오버레이 (검은 화면)")]
    public Image overlayImage;                // 반드시 할당(검정 Image, 풀스크린)
    public bool blockInputDuringFade = true;  // 페이드 중 다른 클릭 차단

    [Header("타이밍(초)")]
    public float fadeInTime = 0.4f;   // 클릭 후 까매지는 시간
    [Tooltip("미사용(과거: 새 씬 페이드 아웃). 필요 없으면 필드 자체를 삭제해도 됩니다.")]
    public float fadeOutTime = 0.35f; // ← 더 이상 사용하지 않음

    [Header("클릭 판정(드래그와 구분)")]
    public float clickMaxPixelMove = 12f;
    public float clickMaxSeconds = 0.3f;

    // 내부
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

        // 초기엔 꺼두는 걸 전제로 동작
        if (overlayImage)
        {
            var go = overlayImage.gameObject;
            // ★ DontDestroyOnLoad 제거: 던전 씬에 오버레이가 남지 않게 함
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

            // 드래그와 구분
            float movePix = (Input.mousePosition - (Vector3)startScreenPos).magnitude;
            float dur = Time.time - startTime;
            bool isClick = (movePix <= clickMaxPixelMove) && (dur <= clickMaxSeconds);
            if (!isClick) return;

            // 포탈 콜라이더 위인지
            Vector3 wp = cam ? cam.ScreenToWorldPoint(Input.mousePosition) : (Vector3)Input.mousePosition;
            Vector2 p = new Vector2(wp.x, wp.y);
            if (col && col.OverlapPoint(p))
            {
                if (string.IsNullOrEmpty(nextSceneName))
                {
                    Debug.LogWarning("[PortalNextScene_UIFadeSimple] nextSceneName 비어있음.");
                    return;
                }
                if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
                {
                    Debug.LogError($"[PortalNextScene_UIFadeSimple] '{nextSceneName}' 씬이 Build Settings에 없음.");
                    return;
                }
                StartCoroutine(Co_Transition());
            }
        }
    }

    IEnumerator Co_Transition()
    {
        transitioning = true;

        // 1) 페이드 인 (까매지기)
        if (overlayImage)
        {
            var go = overlayImage.gameObject;
            go.SetActive(true);
            if (blockInputDuringFade) overlayImage.raycastTarget = true;
            yield return Co_Fade(overlayImage, 0f, 1f, Mathf.Max(0.01f, fadeInTime));
        }

        // 2) 씬 로드 (비동기 권장)
        AsyncOperation op = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        // ★ 페이드 아웃 제거: 새 씬에서 밝아지는 연출은 별도 구성(SceneFadeIn 등)
        // 오버레이는 타운 씬과 함께 삭제됨

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
