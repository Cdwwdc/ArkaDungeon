using UnityEngine;

public class Brick : MonoBehaviour
{
    [Header("�⺻ ü��")]
    public int hp = 1;

    // RoomController ����(Init�� ����)
    private RoomController room;

    /// <summary>
    /// RoomController�� Instantiate ���� ȣ���ؼ� ������ ����
    /// </summary>
    public void Init(RoomController owner, int hpOverride = -1)
    {
        room = owner;
        if (hpOverride > 0) hp = hpOverride;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // ���� �ƴ� �� ����
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
        // ����: room�� ������ ��� �ϰ� ����
        if (room != null) room.NotifyBrickDestroyed();
        else Debug.LogWarning("Brick: room reference is null. Did you call Init(this)?");

        // (����) ���� ���� ������ ���� �ڸ�
        // if (debrisPrefab) { var d = Instantiate(debrisPrefab, transform.position, Quaternion.identity); Destroy(d, 2f); }

        Destroy(gameObject);
    }
}
