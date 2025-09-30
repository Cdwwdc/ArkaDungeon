// Assets/Script/Level/LevelZone.cs
using UnityEngine;

[ExecuteAlways]
public class LevelZone : MonoBehaviour
{
    [Tooltip("월드 좌표 기준 구역 크기")]
    public Vector2 size = new Vector2(4, 2);

    [Tooltip("이 구역에서 사용될 레벨 범위(포함)")]
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
