using UnityEngine;

// �� �ⱸ(��) UI�� �Ѱ�/���� ���� ��Ʈ�ѷ�
public class ExitDoors : MonoBehaviour
{
    public Canvas canvas; // ExitCanvas

    public void Show(bool on)
    {
        canvas.gameObject.SetActive(on);
    }
}
