using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniMapUI : MonoBehaviour
{
    [Header("참조")]
    public DungeonMapRuntime runtime;        // 자동 검색
    public RectTransform gridRoot;           // GridLayoutGroup가 붙은 오브젝트
    public Image cellPrefab;                 // 셀 프리팹(단색 Image면 충분)

    [Header("타일 스프라이트(선택)")]
    [Tooltip("인덱스 = NESW 비트마스크 값(0~15)")]
    public Sprite[] tiles = new Sprite[16];

    [Header("색/스타일")]
    public Color visitedColor = new Color(1, 1, 1, 1);
    public Color discoveredColor = new Color(1, 1, 1, 0.35f);
    public Color hiddenColor = new Color(1, 1, 1, 0.06f);
    public bool tintByState = true;

    [Header("플레이어 마커")]
    public Image playerMarkerPrefab;
    [Tooltip("프리팹 색을 덮어쓸지(끄면 프리팹 색 유지)")]
    public bool overrideMarkerColor = false;
    public Color playerColor = new Color(1f, 0.2f, 0.2f, 1f); // 기본 빨강
    [Tooltip("★ 인스펙터 픽셀 크기를 항상 강제 적용")]
    public Vector2 markerPixels = new Vector2(24, 24);

    public enum CenterMode
    {
        None,                       // 아무 것도 안 함
        CenterContentInsideGrid,    // gridRoot 사각 안에서 컨텐츠(셀)만 가운데
        CenterGridRootInParent      // gridRoot 자체를 부모 중앙에
    }

    [Header("정렬/레이아웃")]
    public CenterMode centerMode = CenterMode.CenterContentInsideGrid;
    [Tooltip("컨텐츠 외곽 여분 패딩 (픽셀)")]
    public Vector2 extraContentPadding = Vector2.zero;

    // 내부
    private Image playerMarker;
    private readonly List<Image> pool = new List<Image>();
    private int lastW = -1, lastH = -1;

    void OnEnable()
    {
        if (!runtime) runtime = DungeonMapRuntime.I;
        Subscribe(true);
        RebuildIfNeeded();
        Render();
    }

    void OnDisable() => Subscribe(false);

    void Subscribe(bool on)
    {
        if (!runtime) return;
        if (on)
        {
            runtime.OnMapGenerated.AddListener(OnDirty);
            runtime.OnMoved.AddListener(OnDirty);
            runtime.OnRoomEntered.AddListener(OnDirty);
        }
        else
        {
            runtime.OnMapGenerated.RemoveListener(OnDirty);
            runtime.OnMoved.RemoveListener(OnDirty);
            runtime.OnRoomEntered.RemoveListener(OnDirty);
        }
    }

    void OnDirty()
    {
        RebuildIfNeeded();
        Render();
    }

    void RebuildIfNeeded()
    {
        if (runtime?.Map == null || !gridRoot || !cellPrefab) return;
        if (lastW == runtime.Map.width && lastH == runtime.Map.height) return;

        lastW = runtime.Map.width;
        lastH = runtime.Map.height;

        // 기존 셀 정리
        foreach (var img in pool) if (img) Destroy(img.gameObject);
        pool.Clear();

        // Grid 설정
        var grid = gridRoot.GetComponent<GridLayoutGroup>();
        if (grid)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = lastW;
        }

        // 셀 생성
        for (int i = 0; i < lastW * lastH; i++)
        {
            var cell = Instantiate(cellPrefab, gridRoot);
            pool.Add(cell);
        }

        // 마커 준비 (한 번만 생성)
        if (playerMarkerPrefab && !playerMarker)
        {
            playerMarker = Instantiate(playerMarkerPrefab, gridRoot);
            playerMarker.raycastTarget = false;
            if (overrideMarkerColor) playerMarker.color = playerColor;
            // ★ 인스펙터 크기 즉시 적용
            playerMarker.rectTransform.sizeDelta = markerPixels;
        }

        ApplyCentering(); // 정렬 적용
    }

    // === 가운데 정렬 로직 ===
    void ApplyCentering()
    {
        var grid = gridRoot.GetComponent<GridLayoutGroup>();
        if (grid == null || runtime?.Map == null) return;

        int cols = runtime.Map.width;
        int rows = runtime.Map.height;

        // 컨텐츠(셀) 폭/높이 계산 (패딩 제외)
        float contentW = cols * grid.cellSize.x + Mathf.Max(0, cols - 1) * grid.spacing.x;
        float contentH = rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
        contentW += extraContentPadding.x;
        contentH += extraContentPadding.y;

        if (centerMode == CenterMode.CenterContentInsideGrid)
        {
            // gridRoot 크기는 그대로 두고 padding으로 중앙 정렬
            Canvas.ForceUpdateCanvases();
            var rect = gridRoot.rect;

            float padX = Mathf.Max(0f, (rect.width - contentW) * 0.5f);
            float padY = Mathf.Max(0f, (rect.height - contentH) * 0.5f);

            int pX = Mathf.RoundToInt(padX);
            int pY = Mathf.RoundToInt(padY);

            grid.padding.left = pX;
            grid.padding.right = pX;
            grid.padding.top = pY;
            grid.padding.bottom = pY;

            grid.childAlignment = TextAnchor.UpperLeft;
        }
        else if (centerMode == CenterMode.CenterGridRootInParent)
        {
            var parent = gridRoot.parent as RectTransform;
            if (!parent) return;

            float w = contentW + grid.padding.left + grid.padding.right;
            float h = contentH + grid.padding.top + grid.padding.bottom;

            gridRoot.anchorMin = gridRoot.anchorMax = new Vector2(0.5f, 0.5f);
            gridRoot.pivot = new Vector2(0.5f, 0.5f);
            gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            gridRoot.anchoredPosition = Vector2.zero;

            grid.childAlignment = TextAnchor.MiddleCenter;
        }
        // CenterMode.None: 아무 것도 안 함
    }

    public void Render()
    {
        if (runtime?.Map == null || pool.Count == 0) return;
        var map = runtime.Map;

        // 셀 채우기
        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                int idx = (map.height - 1 - y) * map.width + x;  // 위(+Y)를 위줄로
                var img = pool[idx];

                var cell = map[x, y];
                var tileIdx = Mathf.Clamp(cell.doors, 0, 15);

                // 스프라이트(없으면 단색)
                if (tiles != null && tileIdx < tiles.Length && tiles[tileIdx] != null)
                    img.sprite = tiles[tileIdx];
                else
                    img.sprite = null;

                // 상태별 색
                if (tintByState)
                {
                    var c = hiddenColor;
                    if (cell.discovered) c = discoveredColor;
                    if (cell.visited) c = visitedColor;
                    img.color = c;
                }
                else img.color = Color.white;
            }
        }

        // 마커 위치/크기 (★ 인스펙터 픽셀 크기 강제)
        if (playerMarker && pool.Count == map.width * map.height)
        {
            int px = map.current.x;
            int py = map.current.y;
            int idx = (map.height - 1 - py) * map.width + px;

            var target = pool[idx].rectTransform;
            var rt = playerMarker.rectTransform;

            if (rt.parent != target) rt.SetParent(target, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();

            // ★ 여기서 매 프레임 확실하게 덮어씀
            rt.sizeDelta = markerPixels;

            if (overrideMarkerColor) playerMarker.color = playerColor;
        }
    }

    // gridRoot 크기/앵커가 바뀌면 다시 가운데 맞춤
    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) ApplyCentering();
    }
}
