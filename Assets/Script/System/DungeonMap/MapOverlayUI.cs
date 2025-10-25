using UnityEngine;

public class MapOverlayUI : MonoBehaviour
{
    [Header("����")]
    public MiniMapUI miniMap;     // ���� ����� �׸��带 ũ�� �ᵵ ��
    public GameObject root;       // �г� ��Ʈ(�� ������Ʈ���� OK)
    public bool showDiscovered = true; // discovered(����)�� ��������

    void Awake()
    {
        if (!root) root = gameObject;
        root.SetActive(false);
    }

    public void Open()
    {
        if (!root) return;
        root.SetActive(true);

        // �������̿����� �̼� ��Ÿ�� ����(���ϸ� ������ �ٲ㵵 OK)
        if (miniMap)
        {
            // ��ü��: visited ����, discovered ����, ������ ����
            miniMap.visitedColor = new Color(1, 1, 1, 1);
            miniMap.discoveredColor = showDiscovered ? new Color(1, 1, 1, 0.25f) : new Color(1, 1, 1, 0f);
            miniMap.hiddenColor = new Color(1, 1, 1, 0f);
            miniMap.Render();
        }
    }

    public void Close()
    {
        if (root) root.SetActive(false);
    }
}
