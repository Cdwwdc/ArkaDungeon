using UnityEngine;
using UnityEngine.UI;

public class MiniMapCellView : MonoBehaviour
{
    public enum CellState { Unseen, Discovered, Visited }

    [Header("Colors")]
    public Color unseenColor = new Color(1, 1, 1, 0.12f);
    public Color discoveredColor = new Color(1, 1, 1, 0.45f);
    public Color visitedColor = new Color(1, 1, 1, 0.9f);
    public Color borderColor = new Color(1, 1, 0.2f, 1f);

    [Header("Layout")]
    [Range(0.05f, 0.45f)] public float doorThickness = 0.28f; // �� �� �� ��� ����
    [Range(0.0f, 0.35f)] public float cornerInset = 0.18f;   // �� ����(�𼭸� ����)

    Image center, doorN, doorE, doorS, doorW, border;

    void Awake()
    {
        Ensure(ref center, "Center");
        Ensure(ref border, "Border");
        Ensure(ref doorN, "DoorN");
        Ensure(ref doorE, "DoorE");
        Ensure(ref doorS, "DoorS");
        Ensure(ref doorW, "DoorW");

        // �⺻ ��������Ʈ = �� �簢 (Unity ����)
        var imgs = GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            if (img.sprite == null)
                img.sprite = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            img.type = Image.Type.Sliced; // �׵θ��� ����ϰ�
        }

        LayoutDoors();
    }

    void Ensure(ref Image img, string name)
    {
        if (img) return;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        img = go.GetComponent<Image>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void LayoutDoors()
    {
        float t = Mathf.Clamp01(doorThickness);
        float inset = Mathf.Clamp01(cornerInset);

        // Center
        var rtC = center.rectTransform;
        rtC.anchorMin = new Vector2(0, 0);
        rtC.anchorMax = new Vector2(1, 1);
        rtC.offsetMin = rtC.offsetMax = Vector2.zero;

        // Border(���� ��ġ ���̶���Ʈ) - ��¦ �β���
        var rtB = border.rectTransform;
        rtB.anchorMin = new Vector2(0, 0);
        rtB.anchorMax = new Vector2(1, 1);
        float bw = 0.08f; // �� �β� ����
        rtB.offsetMin = new Vector2(-bw, -bw);
        rtB.offsetMax = new Vector2(+bw, +bw);
        border.color = Color.clear; // �⺻�� ���� ȿ��

        // N (���� ���� ��)
        AnchorBar(doorN.rectTransform,
            new Vector2(inset, 1f - t), new Vector2(1f - inset, 1f));

        // S (�Ʒ��� ���� ��)
        AnchorBar(doorS.rectTransform,
            new Vector2(inset, 0f), new Vector2(1f - inset, t));

        // E (������ ���� ��)
        AnchorBar(doorE.rectTransform,
            new Vector2(1f - t, inset), new Vector2(1f, 1f - inset));

        // W (���� ���� ��)
        AnchorBar(doorW.rectTransform,
            new Vector2(0f, inset), new Vector2(t, 1f - inset));
    }

    void AnchorBar(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>�� ��Ʈ(N=1,E=2,S=4,W=8)�� ���·� ���� ����</summary>
    public void Apply(int doorsMask, CellState state, bool isCurrent)
    {
        // ������
        switch (state)
        {
            case CellState.Unseen: center.color = unseenColor; break;
            case CellState.Discovered: center.color = discoveredColor; break;
            case CellState.Visited: center.color = visitedColor; break;
        }

        // �̹߰��̸� ���� ����(���Ѵٸ� �帮�� �ѵ� ��)
        bool showDoors = state != CellState.Unseen;
        doorN.enabled = showDoors && (doorsMask & 1) != 0;   // N
        doorE.enabled = showDoors && (doorsMask & 2) != 0;   // E
        doorS.enabled = showDoors && (doorsMask & 4) != 0;   // S
        doorW.enabled = showDoors && (doorsMask & 8) != 0;   // W

        // ���� ��ġ ����(�׵θ��� ��)
        border.color = isCurrent ? borderColor : new Color(0, 0, 0, 0);

        // �� ���� �������� �ణ �� ���ϰ�
        Color doorCol = center.color;
        doorCol.a = Mathf.Clamp01(center.color.a + 0.25f);
        doorN.color = doorE.color = doorS.color = doorW.color = doorCol;
    }
}
