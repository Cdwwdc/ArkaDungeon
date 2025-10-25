using UnityEngine;

/// <summary>
/// 9:16 기준 화면비 및 모바일 Safe Area에 대응하여 카메라 Size와 Viewport Rect를 조정합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraAspectFitter : MonoBehaviour
{
    [Header("기준 비율 (수정 불필요)")]
    [Tooltip("작업 기준 화면비. 9:16 고정 권장")]
    public float referenceAspect = 9f / 16f;

    [Header("기준 사이즈 (자동 캡처)")]
    [Tooltip("9:16 기준에서의 '높이 맞춤' orthographicSize.")]
    public float referenceOrthoSize = -1f;

    [Header("재계산 트리거")]
    [Tooltip("해상도/회전이 바뀌면 자동으로 1프레임 뒤 재계산")]
    public bool reactToResolutionChange = true;

    Camera cam;
    int lastW, lastH;
    Rect lastSafeArea; // Safe Area 변경 감지용

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        if (!cam.orthographic)
        {
            Debug.LogWarning("[CameraAspectFitter] Orthographic 카메라가 아닙니다. 스크립트를 비활성화합니다.");
            enabled = false;
            return;
        }

        if (referenceOrthoSize <= 0f)
        {
            // 현재 값을 9:16 기준 높이로 캡처
            referenceOrthoSize = cam.orthographicSize;
        }

        // 최초 1회 즉시 적용 + 다음 프레임 한 번 더
        Apply();
        if (reactToResolutionChange) Invoke(nameof(Apply), 0f);

        lastW = Screen.width;
        lastH = Screen.height;
        lastSafeArea = Screen.safeArea;
    }

    void Update()
    {
        if (!reactToResolutionChange) return;

        // 1. 해상도/회전 변경 감지
        bool resolutionChanged = Screen.width != lastW || Screen.height != lastH;
        // 2. Safe Area 변경 감지 (노치 바 등장/사라짐 등)
        bool safeAreaChanged = Screen.safeArea != lastSafeArea;

        if (resolutionChanged || safeAreaChanged)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            lastSafeArea = Screen.safeArea;

            CancelInvoke(nameof(Apply));
            Invoke(nameof(Apply), 0f); // 다음 프레임에 1회만
        }
    }

    /// <summary>
    /// 카메라 Size와 Viewport Rect를 Safe Area 및 9:16 콘텐츠 비율에 맞게 조정
    /// </summary>
    void Apply()
    {
        if (!cam) return;

        // 1. Viewport Rect 조정 (Safe Area 처리)
        Rect safeArea = Screen.safeArea;
        float screenW = Screen.width;
        float screenH = Screen.height;

        // 픽셀 좌표를 0~1 사이의 정규화된 뷰포트 좌표로 변환
        float viewportX = safeArea.x / screenW;
        float viewportY = safeArea.y / screenH;
        float viewportW = safeArea.width / screenW;
        float viewportH = safeArea.height / screenH;

        // 카메라의 Viewport Rect에 적용: 검은 띠(시스템 영역) 문제 해결
        cam.rect = new Rect(viewportX, viewportY, viewportW, viewportH);


        // 2. Orthographic Size 조정 (9:16 콘텐츠 비율 유지)

        // **Safe Area의 비율**을 기준으로 콘텐츠 크기를 계산합니다.
        // Screen.width 대신 safeArea.width를 사용해야 정확한 비율이 나옵니다.
        float safeAreaAspect = safeArea.width / Mathf.Max(1, safeArea.height);
        float ratioScale = safeAreaAspect / referenceAspect; // <1: 길쭉 / >=1: 넓음

        if (ratioScale < 1.0f)
        {
            // 길쭉(가로가 더 좁음) → 폭을 맞추기 위해 orthoSize를 '키움'
            cam.orthographicSize = referenceOrthoSize / ratioScale;
        }
        else
        {
            // 넓음(가로가 더 큼) → 높이 유지
            cam.orthographicSize = referenceOrthoSize;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Current Size As Reference")]
    void EditorCapture()
    {
        cam = GetComponent<Camera>();
        if (cam && cam.orthographic)
        {
            referenceOrthoSize = cam.orthographicSize;
            Debug.Log($"[CameraAspectFitter] referenceOrthoSize 캡처: {referenceOrthoSize}");
        }
    }
#endif
}