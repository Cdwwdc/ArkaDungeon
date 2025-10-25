using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MiniMapRenderer : MonoBehaviour
{
    public RectTransform gridRoot;      // GridLayoutGroup�� ���� ������Ʈ
    public GridLayoutGroup grid;        // ���� ���� ������Ʈ�� ������Ʈ
    public int width = 9, height = 9;

    // ������ �ҽ� (����� DungeonMapRuntime ������ ��ü)
    public System.Func<int, int, int> GetDoors;             // (x,y)->mask
    public System.Func<int, int, bool> IsVisited;           // (x,y)
    public System.Func<int, int, bool> IsDiscovered;        // (x,y)
    public System.Func<int, int, bool> IsCurrent;           // (x,y)

    readonly List<MiniMapCellView> cells = new();

    void Start()
    {
        if (!grid) grid = gridRoot ? gridRoot.GetComponent<GridLayoutGroup>() : null;
        BuildGrid();
        Redraw();
    }

    public void BuildGrid()
    {
        // ���� �ڽ� ����
        for (int i = gridRoot.childCount - 1; i >= 0; --i)
            Destroy(gridRoot.GetChild(i).gameObject);
        cells.Clear();

        // �� �� ����
        if (grid) { grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = width; }

        // �� ����
        for (int i = 0; i < width * height; i++)
        {
            var go = new GameObject($"Cell_{i}", typeof(RectTransform), typeof(MiniMapCellView));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(gridRoot, false);
            rt.localScale = Vector3.one;
            cells.Add(go.GetComponent<MiniMapCellView>());
        }
    }

    public void Redraw()
    {
        if (cells.Count != width * height) return;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                var cell = cells[idx];

                int doors = GetDoors != null ? GetDoors(x, y) : 0;
                bool visited = IsVisited != null && IsVisited(x, y);
                bool disc = IsDiscovered != null && IsDiscovered(x, y);
                bool current = IsCurrent != null && IsCurrent(x, y);

                var state = visited ? MiniMapCellView.CellState.Visited
                          : disc ? MiniMapCellView.CellState.Discovered
                                    : MiniMapCellView.CellState.Unseen;

                cell.Apply(doors, state, current);
            }
    }
}
