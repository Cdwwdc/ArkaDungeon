using UnityEngine;

public class Brick : MonoBehaviour
{
    [Header("기본 체력")]
    public int hp = 1;

    // RoomController 참조(Init로 주입)
    private RoomController room;

    /// <summary>
    /// RoomController가 Instantiate 직후 호출해서 소유자 주입
    /// </summary>
    public void Init(RoomController owner, int hpOverride = -1)
    {
        room = owner;
        if (hpOverride > 0) hp = hpOverride;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // 공이 아닐 땐 무시
        if (!col.collider.CompareTag("Ball")) return;
        Hit(1);
    }

    public void Hit(int dmg)
    {
        hp -= Mathf.Max(1, dmg);
        if (hp <= 0) Break();
    }

    void Break()
    {
        // 안전: room이 없으면 경고만 하고 진행
        if (room != null) room.NotifyBrickDestroyed();
        else Debug.LogWarning("Brick: room reference is null. Did you call Init(this)?");

        // (선택) 파편 연출 프리팹 생성 자리
        // if (debrisPrefab) { var d = Instantiate(debrisPrefab, transform.position, Quaternion.identity); Destroy(d, 2f); }

        Destroy(gameObject);
    }
}
