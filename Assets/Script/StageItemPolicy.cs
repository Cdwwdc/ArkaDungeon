using UnityEngine;

public class StageItemPolicy : MonoBehaviour
{
    public enum StageClearBehavior
    {
        Disappear,   // 스테이지 클리어 시 즉시 소멸(버프류)
        AutoCollect, // 스테이지 클리어 시 즉시 자동 획득(루팅/재화/퀘스트)
        None         // 아무 것도 안 함(특수 케이스)
    }

    [Header("스테이지 클리어 시 동작")]
    public StageClearBehavior onStageClear = StageClearBehavior.Disappear;
}
