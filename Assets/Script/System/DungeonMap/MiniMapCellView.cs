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
    [Range(0.05f, 0.45f)] public float doorThickness = 0.28f; // 셀 한 변 대비 비율
    [Range(0.0f, 0.35f)] public float cornerInset = 0.18f;   // 문 길이(모서리 여백)

    Image center, doorN, doorE, doorS, doorW, border;

    void Awake()
    {
        Ensure(ref center, "Center");
        Ensure(ref border, "Border");
        Ensure(ref doorN, "DoorN");
        Ensure(ref doorE, "DoorE");
        Ensure(ref doorS, "DoorS");
        Ensure(ref doorW, "DoorW");

        // 기본 스프라이트 = 흰 사각 (Unity 내장)
        var imgs = GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            if (img.sprite == null)
                img.sprite = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            img.type = Image.Type.Sliced; // 테두리도 깔끔하게
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

        // Border(현재 위치 하이라이트) - 살짝 두껍게
        var rtB = border.rectTransform;
        rtB.anchorMin = new Vector2(0, 0);
        rtB.anchorMax = new Vector2(1, 1);
        float bw = 0.08f; // 셀 두께 비율
        rtB.offsetMin = new Vector2(-bw, -bw);
        rtB.offsetMax = new Vector2(+bw, +bw);
        border.color = Color.clear; // 기본은 숨김 효과

        // N (위쪽 가로 바)
        AnchorBar(doorN.rectTransform,
            new Vector2(inset, 1f - t), new Vector2(1f - inset, 1f));

        // S (아래쪽 가로 바)
        AnchorBar(doorS.rectTransform,
            new Vector2(inset, 0f), new Vector2(1f - inset, t));

        // E (오른쪽 세로 바)
        AnchorBar(doorE.rectTransform,
            new Vector2(1f - t, inset), new Vector2(1f, 1f - inset));

        // W (왼쪽 세로 바)
        AnchorBar(doorW.rectTransform,
            new Vector2(0f, inset), new Vector2(t, 1f - inset));
    }

    void AnchorBar(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>문 비트(N=1,E=2,S=4,W=8)와 상태로 셀을 갱신</summary>
    public void Apply(int doorsMask, CellState state, bool isCurrent)
    {
        // 바탕색
        switch (state)
        {
            case CellState.Unseen: center.color = unseenColor; break;
            case CellState.Discovered: center.color = discoveredColor; break;
            case CellState.Visited: center.color = visitedColor; break;
        }

        // 미발견이면 문도 숨김(원한다면 흐리게 켜도 됨)
        bool showDoors = state != CellState.Unseen;
        doorN.enabled = showDoors && (doorsMask & 1) != 0;   // N
        doorE.enabled = showDoors && (doorsMask & 2) != 0;   // E
        doorS.enabled = showDoors && (doorsMask & 4) != 0;   // S
        doorW.enabled = showDoors && (doorsMask & 8) != 0;   // W

        // 현재 위치 강조(테두리만 색)
        border.color = isCurrent ? borderColor : new Color(0, 0, 0, 0);

        // 문 색은 바탕보다 약간 더 진하게
        Color doorCol = center.color;
        doorCol.a = Mathf.Clamp01(center.color.a + 0.25f);
        doorN.color = doorE.color = doorS.color = doorW.color = doorCol;
    }
}
