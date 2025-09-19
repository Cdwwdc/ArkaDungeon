// Assets/Script/Level/LevelZone.cs
using UnityEngine;

[ExecuteAlways]
public class LevelZone : MonoBehaviour
{
    [Tooltip("���� ��ǥ ���� ���� ũ��")]
    public Vector2 size = new Vector2(4, 2);

    [Tooltip("�� �������� ���� ���� ����(����)")]
    public Vector2Int levelRange = new Vector2Int(1, 6);

    public Rect WorldRect => new Rect((Vector2)transform.position - size / 2f, size);
    public bool Contains(Vector2 p) => WorldRect.Contains(p);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawCube(transform.position, new Vector3(size.x, size.y, 0.1f));
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0.1f));
    }
}
