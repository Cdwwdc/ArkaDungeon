using UnityEngine;

/// <summary>
/// ī�޶� �̵� ��� ������ �����̴� �з����� ���̾�.
/// factor=0 �� ī�޶�� ����(���� ���), factor=1 �� ����� ����.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    public Transform cameraTransform;
    [Tooltip("0=ī�޶� ����, 1=����� ����. 0.2~0.8 ��õ")]
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
        // ���θ� �����̰� ������ y�� 0����
        camDelta.y = 0f;
        transform.position += camDelta * (1f - factor);
        prevCamPos = cameraTransform.position;
    }
}
