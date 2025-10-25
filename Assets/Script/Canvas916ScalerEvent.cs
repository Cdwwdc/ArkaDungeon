// Canvas916ScalerEvent.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[ExecuteAlways]
[RequireComponent(typeof(CanvasScaler))]
public class Canvas916ScalerEvent : UIBehaviour
{
    [Header("UI ���� �ػ� (9:16)")]
    public Vector2 referenceResolution = new Vector2(1080, 1920);

    [Tooltip("9:16 = 0.5625")]
    public float targetAspect = 9f / 16f;

    CanvasScaler scaler;

    protected override void OnEnable()
    {
        base.OnEnable();
        scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        Apply();
    }

    // ȭ��/ĵ���� ������ ���� �ÿ��� ȣ���(�����Ӹ��� �ƴ�)
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (!isActiveAndEnabled) return;
        Apply();
    }

    void Apply()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        // ��� �� ���θ� ����(0), �д� �� ���θ� ����(1)
        if (scaler) scaler.matchWidthOrHeight = (aspect < targetAspect) ? 0f : 1f;
    }
}
