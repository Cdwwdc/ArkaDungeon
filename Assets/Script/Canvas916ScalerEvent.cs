// Canvas916ScalerEvent.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[ExecuteAlways]
[RequireComponent(typeof(CanvasScaler))]
public class Canvas916ScalerEvent : UIBehaviour
{
    [Header("UI 기준 해상도 (9:16)")]
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

    // 화면/캔버스 사이즈 변동 시에만 호출됨(프레임마다 아님)
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (!isActiveAndEnabled) return;
        Apply();
    }

    void Apply()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        // 길다 → 가로를 맞춤(0), 넓다 → 세로를 맞춤(1)
        if (scaler) scaler.matchWidthOrHeight = (aspect < targetAspect) ? 0f : 1f;
    }
}
