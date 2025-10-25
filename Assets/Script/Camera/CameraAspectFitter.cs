using UnityEngine;

/// <summary>
/// 9:16 ���� ȭ��� �� ����� Safe Area�� �����Ͽ� ī�޶� Size�� Viewport Rect�� �����մϴ�.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraAspectFitter : MonoBehaviour
{
    [Header("���� ���� (���� ���ʿ�)")]
    [Tooltip("�۾� ���� ȭ���. 9:16 ���� ����")]
    public float referenceAspect = 9f / 16f;

    [Header("���� ������ (�ڵ� ĸó)")]
    [Tooltip("9:16 ���ؿ����� '���� ����' orthographicSize.")]
    public float referenceOrthoSize = -1f;

    [Header("���� Ʈ����")]
    [Tooltip("�ػ�/ȸ���� �ٲ�� �ڵ����� 1������ �� ����")]
    public bool reactToResolutionChange = true;

    Camera cam;
    int lastW, lastH;
    Rect lastSafeArea; // Safe Area ���� ������

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        if (!cam.orthographic)
        {
            Debug.LogWarning("[CameraAspectFitter] Orthographic ī�޶� �ƴմϴ�. ��ũ��Ʈ�� ��Ȱ��ȭ�մϴ�.");
            enabled = false;
            return;
        }

        if (referenceOrthoSize <= 0f)
        {
            // ���� ���� 9:16 ���� ���̷� ĸó
            referenceOrthoSize = cam.orthographicSize;
        }

        // ���� 1ȸ ��� ���� + ���� ������ �� �� ��
        Apply();
        if (reactToResolutionChange) Invoke(nameof(Apply), 0f);

        lastW = Screen.width;
        lastH = Screen.height;
        lastSafeArea = Screen.safeArea;
    }

    void Update()
    {
        if (!reactToResolutionChange) return;

        // 1. �ػ�/ȸ�� ���� ����
        bool resolutionChanged = Screen.width != lastW || Screen.height != lastH;
        // 2. Safe Area ���� ���� (��ġ �� ����/����� ��)
        bool safeAreaChanged = Screen.safeArea != lastSafeArea;

        if (resolutionChanged || safeAreaChanged)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            lastSafeArea = Screen.safeArea;

            CancelInvoke(nameof(Apply));
            Invoke(nameof(Apply), 0f); // ���� �����ӿ� 1ȸ��
        }
    }

    /// <summary>
    /// ī�޶� Size�� Viewport Rect�� Safe Area �� 9:16 ������ ������ �°� ����
    /// </summary>
    void Apply()
    {
        if (!cam) return;

        // 1. Viewport Rect ���� (Safe Area ó��)
        Rect safeArea = Screen.safeArea;
        float screenW = Screen.width;
        float screenH = Screen.height;

        // �ȼ� ��ǥ�� 0~1 ������ ����ȭ�� ����Ʈ ��ǥ�� ��ȯ
        float viewportX = safeArea.x / screenW;
        float viewportY = safeArea.y / screenH;
        float viewportW = safeArea.width / screenW;
        float viewportH = safeArea.height / screenH;

        // ī�޶��� Viewport Rect�� ����: ���� ��(�ý��� ����) ���� �ذ�
        cam.rect = new Rect(viewportX, viewportY, viewportW, viewportH);


        // 2. Orthographic Size ���� (9:16 ������ ���� ����)

        // **Safe Area�� ����**�� �������� ������ ũ�⸦ ����մϴ�.
        // Screen.width ��� safeArea.width�� ����ؾ� ��Ȯ�� ������ ���ɴϴ�.
        float safeAreaAspect = safeArea.width / Mathf.Max(1, safeArea.height);
        float ratioScale = safeAreaAspect / referenceAspect; // <1: ���� / >=1: ����

        if (ratioScale < 1.0f)
        {
            // ����(���ΰ� �� ����) �� ���� ���߱� ���� orthoSize�� 'Ű��'
            cam.orthographicSize = referenceOrthoSize / ratioScale;
        }
        else
        {
            // ����(���ΰ� �� ŭ) �� ���� ����
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
            Debug.Log($"[CameraAspectFitter] referenceOrthoSize ĸó: {referenceOrthoSize}");
        }
    }
#endif
}