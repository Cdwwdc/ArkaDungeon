using UnityEngine;

/// <summary>
/// 카메라 이동 대비 비율로 움직이는 패럴랙스 레이어.
/// factor=0 은 카메라와 같이(고정 배경), factor=1 은 월드와 동일.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    public Transform cameraTransform;
    [Tooltip("0=카메라 고정, 1=월드와 동일. 0.2~0.8 추천")]
    public float factor = 0.5f;
    Vector3 prevCamPos;
    Vector3 basePos;

    void Start()
    {
        if (!cameraTransform) cameraTransform = Camera.main.transform;
        prevCamPos = cameraTransform.position;
        basePos = transform.position;
    }

    void LateUpdate()
    {
        if (!cameraTransform) return;
        Vector3 camDelta = cameraTransform.position - prevCamPos;
        // 가로만 움직이고 싶으면 y를 0으로
        camDelta.y = 0f;
        transform.position += camDelta * (1f - factor);
        prevCamPos = cameraTransform.position;
    }
}
