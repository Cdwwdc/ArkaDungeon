using UnityEngine;

// ▣ 출구(문) UI를 켜고/끄는 간단 컨트롤러
public class ExitDoors : MonoBehaviour
{
    public Canvas canvas; // ExitCanvas

    public void Show(bool on)
    {
        canvas.gameObject.SetActive(on);
    }
}
