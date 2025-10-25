using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    public enum FitMode { KeepHeight, Envelope, KeepWidth }

    [Header("모드")]
    [Tooltip("KeepHeight=세로 완전 고정, Envelope=폭 유지(세로 늘어남), KeepWidth=폭 고정(세로 줄어듦)")]
    public FitMode mode = FitMode.KeepHeight;

    [Header("디자인 비율/사이즈")]
    [Tooltip("세로형 게임(9:16)")]
    public float designAspect = 9f / 16f;
    [Tooltip("디자인 기준 OrthographicSize(세로 반높이). 에디터에서 배치하던 카메라 값과 동일하게!")]
    public float designOrthoSize = 8f;

    void Start()
    {
        var cam = GetComponent<Camera>();
        cam.orthographic = true;
        float curAspect = (float)Screen.width / Screen.height;

        switch (mode)
        {
            case FitMode.KeepHeight:
                cam.orthographicSize = designOrthoSize; // 세로 완전 고정
                break;

            case FitMode.Envelope: // 폭 유지: 세로가 늘 수 있음
                cam.orthographicSize = designOrthoSize * Mathf.Max(1f, designAspect / curAspect);
                break;

            case FitMode.KeepWidth: // 폭 완전 고정: 세로가 줄 수 있음
                cam.orthographicSize = designOrthoSize * (designAspect / curAspect);
                break;
        }
    }
}
