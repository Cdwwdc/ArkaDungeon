using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    public enum FitMode { KeepHeight, Envelope, KeepWidth }

    [Header("���")]
    [Tooltip("KeepHeight=���� ���� ����, Envelope=�� ����(���� �þ), KeepWidth=�� ����(���� �پ��)")]
    public FitMode mode = FitMode.KeepHeight;

    [Header("������ ����/������")]
    [Tooltip("������ ����(9:16)")]
    public float designAspect = 9f / 16f;
    [Tooltip("������ ���� OrthographicSize(���� �ݳ���). �����Ϳ��� ��ġ�ϴ� ī�޶� ���� �����ϰ�!")]
    public float designOrthoSize = 8f;

    void Start()
    {
        var cam = GetComponent<Camera>();
        cam.orthographic = true;
        float curAspect = (float)Screen.width / Screen.height;

        switch (mode)
        {
            case FitMode.KeepHeight:
                cam.orthographicSize = designOrthoSize; // ���� ���� ����
                break;

            case FitMode.Envelope: // �� ����: ���ΰ� �� �� ����
                cam.orthographicSize = designOrthoSize * Mathf.Max(1f, designAspect / curAspect);
                break;

            case FitMode.KeepWidth: // �� ���� ����: ���ΰ� �� �� ����
                cam.orthographicSize = designOrthoSize * (designAspect / curAspect);
                break;
        }
    }
}
