using UnityEngine;

public class StageItemPolicy : MonoBehaviour
{
    public enum StageClearBehavior
    {
        Disappear,   // �������� Ŭ���� �� ��� �Ҹ�(������)
        AutoCollect, // �������� Ŭ���� �� ��� �ڵ� ȹ��(����/��ȭ/����Ʈ)
        None         // �ƹ� �͵� �� ��(Ư�� ���̽�)
    }

    [Header("�������� Ŭ���� �� ����")]
    public StageClearBehavior onStageClear = StageClearBehavior.Disappear;
}
