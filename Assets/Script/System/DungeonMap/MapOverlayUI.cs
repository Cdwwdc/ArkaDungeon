using UnityEngine;

public class MapOverlayUI : MonoBehaviour
{
    [Header("참조")]
    public MiniMapUI miniMap;     // 같은 방식의 그리드를 크게 써도 됨
    public GameObject root;       // 패널 루트(이 오브젝트여도 OK)
    public bool showDiscovered = true; // discovered(옅게)도 보여줄지

    void Awake()
    {
        if (!root) root = gameObject;
        root.SetActive(false);
    }

    public void Open()
    {
        if (!root) return;
        root.SetActive(true);

        // 오버레이에서는 미세 스타일 조정(원하면 색상을 바꿔도 OK)
        if (miniMap)
        {
            // 전체맵: visited 선명, discovered 옅게, 나머지 숨김
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
