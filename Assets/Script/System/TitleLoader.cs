using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class TitleLoader : MonoBehaviour
{
    [Header("Spawn Parent (월드용)")]
    public Transform parent; // TitleRoot(월드 Transform)를 드래그. 비우면 this.transform

    [Header("Addressables Key")]
    public string addressKey = "Title/GameTitle_000";

    [Header("Scene Fade (선택, 씬의 UI)")]
    public CanvasGroup sceneFadeOverlay;   // Canvas의 Fade Image에 있는 CanvasGroup
    public SpriteRenderer sceneFadeSprite; // (월드 스프라이트로 페이드할 때만 사용)

    void Start()
    {
        if (!parent) parent = transform;

        // 에디터 미리보기로 TitleRoot에 뭔가 이미 들어있으면, 중복 스폰 방지
        if (parent.childCount > 0) return;

        Addressables.LoadAssetAsync<GameObject>(addressKey).Completed += OnLoaded;
    }

    void OnLoaded(AsyncOperationHandle<GameObject> op)
    {
        if (op.Status != AsyncOperationStatus.Succeeded || !op.Result)
        {
            Debug.LogError($"[TitleLoader] Load failed: {addressKey}");
            return;
        }

        // 월드로 붙임 (Canvas 밑 X)
        var inst = Instantiate(op.Result, parent, false);
        var t = inst.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        // 프리팹 안 컨트롤러에 씬의 페이드 오브젝트 주입
        var ctrl = inst.GetComponentInChildren<LogoSceneController>(true);
        if (ctrl)
        {
            if (sceneFadeOverlay) ctrl.fadeOverlay = sceneFadeOverlay; // CanvasGroup 컴포넌트를 드래그!
            if (sceneFadeSprite) ctrl.fadeSprite = sceneFadeSprite;  // 쓸 때만
        }
        else
        {
            Debug.LogWarning("[TitleLoader] LogoSceneController not found in instance.");
        }
    }
}
