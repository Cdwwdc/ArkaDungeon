using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class TitleLoader : MonoBehaviour
{
    [Header("Spawn Parent (�����)")]
    public Transform parent; // TitleRoot(���� Transform)�� �巡��. ���� this.transform

    [Header("Addressables Key")]
    public string addressKey = "Title/GameTitle_000";

    [Header("Scene Fade (����, ���� UI)")]
    public CanvasGroup sceneFadeOverlay;   // Canvas�� Fade Image�� �ִ� CanvasGroup
    public SpriteRenderer sceneFadeSprite; // (���� ��������Ʈ�� ���̵��� ���� ���)

    void Start()
    {
        if (!parent) parent = transform;

        // ������ �̸������ TitleRoot�� ���� �̹� ���������, �ߺ� ���� ����
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

        // ����� ���� (Canvas �� X)
        var inst = Instantiate(op.Result, parent, false);
        var t = inst.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        // ������ �� ��Ʈ�ѷ��� ���� ���̵� ������Ʈ ����
        var ctrl = inst.GetComponentInChildren<LogoSceneController>(true);
        if (ctrl)
        {
            if (sceneFadeOverlay) ctrl.fadeOverlay = sceneFadeOverlay; // CanvasGroup ������Ʈ�� �巡��!
            if (sceneFadeSprite) ctrl.fadeSprite = sceneFadeSprite;  // �� ����
        }
        else
        {
            Debug.LogWarning("[TitleLoader] LogoSceneController not found in instance.");
        }
    }
}
