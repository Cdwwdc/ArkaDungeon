using UnityEngine;

public class Brick : MonoBehaviour
{
    [Header("�긯 ����")]
    public int hitPoints = 1;            // ü�� (1=�� ���� �ı�)
    public int score = 10;               // ����(����)
    public bool destroyOnAnyHit = false; // ��� �浹�� �ٷ� �ı�

    [Header("����Ʈ(�ɼ�)")]
    public ParticleSystem breakFx;       // �ı� ����Ʈ(��� ��)
    public AudioSource breakSfx;         // ����(��� ��)

    [Header("������ ���")]
    public GameObject powerUpPrefab;     // PowerUpItem ������
    [Range(0f, 1f)] public float dropChance = 0.2f;

    // ����
    RoomController room;
    bool isBreaking = false;             // �ߺ� �ı� ����

    // RoomController�� ���� �� ȣ��
    public void Init(RoomController rc)
    {
        room = rc;
        isBreaking = false;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.collider.CompareTag("Ball")) return;

        if (destroyOnAnyHit) { Break(); return; }
        Hit(1);
    }

    public void Hit(int dmg)
    {
        if (isBreaking) return;
        hitPoints -= Mathf.Max(1, dmg);

        // ���߰�: HP ���� ��� �� ������(BrickSkin.mapByHitPoints=ON�� �� �ܰ������� �ٿ�׷��̵��)
        var skin = GetComponent<BrickSkin>();
        if (skin) skin.ApplyColor();

        if (hitPoints <= 0) Break();
    }

    public void Break()
    {
        if (isBreaking) return;
        isBreaking = true;

        // ����Ʈ/����
        if (breakFx)
        {
            var fx = Instantiate(breakFx, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.5f);
        }
        if (breakSfx) breakSfx.Play();

        // ���
        if (powerUpPrefab && Random.value < dropChance)
        {
            Instantiate(powerUpPrefab, transform.position, Quaternion.identity);
        }

        // �ı� �� �˸�
        Destroy(gameObject);
        if (room != null) room.NotifyBrickDestroyed();
        else Debug.LogWarning("[Brick] room ������ ����(Init ����?)");
    }
}
