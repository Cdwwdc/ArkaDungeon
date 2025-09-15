using System.Collections.Generic;
using UnityEngine;

public class BallManager : MonoBehaviour
{
    [Header("����")]
    public GameManager gameManager;
    public GameObject ballPrefab;
    public Transform ballSpawn;  // ��� ��: �е� �� ����
    public Transform paddle;

    [Header("����")]
    public int maxBalls = 4;
    public string ballTag = "Ball";
    public string ballLayerName = "Ball"; // Project Settings > Tags and Layers ���� ���� ��õ

    readonly List<GameObject> balls = new List<GameObject>();
    bool collisionIgnored = false;

    void EnsureIgnoreBallBall()
    {
        if (collisionIgnored) return;
        int L = LayerMask.NameToLayer(ballLayerName);
        if (L >= 0)
        {
            Physics2D.IgnoreLayerCollision(L, L, true);
            collisionIgnored = true;
        }
    }

    void SetupBallGO(GameObject b)
    {
        if (!b) return;
        EnsureIgnoreBallBall();

        // �±�/���̾� ����
        if (!string.IsNullOrEmpty(ballTag)) b.tag = ballTag;
        int L = LayerMask.NameToLayer(ballLayerName);
        if (L >= 0) b.layer = L;
    }

    // ���� ����: ballSpawn ������ �е� ����, �� ������ (0,0)
    Vector3 GetSpawnPosition()
    {
        if (ballSpawn) return ballSpawn.position;
        if (paddle) return paddle.position + Vector3.up * 0.35f;
        return Vector3.zero;
    }

    public void InitIfNeeded()
    {
        if (balls.Count > 0) return;

        // �� ���� ��������� GM������ �����ͼ� ���ش� (Fail-safe)
        if (!ballPrefab && gameManager && gameManager.ballPrefab)
        {
            ballPrefab = gameManager.ballPrefab;
            Debug.LogWarning("[BallManager] ballPrefab ����־� GameManager.ballPrefab ���");
        }

        if (!ballPrefab)
        {
            Debug.LogError("[BallManager] ballPrefab �̼��� �� �� ���� �Ұ�");
            return;
        }

        var b = Instantiate(ballPrefab, GetSpawnPosition(), Quaternion.identity);
        SetupBallGO(b);
        balls.Add(b);

        if (!b.activeSelf)
            Debug.LogWarning("[BallManager] ������ Ball�� Inactive�Դϴ�. ������ ��Ʈ�� Active�� �ٲ��ּ���.");
    }

    public void ClearAll()
    {
        foreach (var b in balls) if (b) Destroy(b);
        balls.Clear();
    }

    public int ActiveCount()
    {
        int c = 0;
        foreach (var b in balls) if (b && b.activeInHierarchy) c++;
        return c;
    }

    public void OnBallLost(GameObject ball)
    {
        if (ball != null) ball.SetActive(false);

        if (ActiveCount() > 0) return; // ���� ����ִ� ���� ������ ���

        gameManager?.OnBallDeath();
    }

    public void PowerUp_MultiBall()
    {
        List<GameObject> alive = new List<GameObject>();
        foreach (var b in balls) if (b && b.activeInHierarchy) alive.Add(b);

        if (alive.Count == 0)
        {
            InitIfNeeded();
            alive.Clear();
            foreach (var b in balls) if (b && b.activeInHierarchy) alive.Add(b);
        }

        int target = Mathf.Min(maxBalls, NextPowerCount(alive.Count)); // 1��2��4
        int need = target - alive.Count;
        if (need <= 0) return;

        int spawned = 0;
        int idx = 0;
        while (spawned < need && alive.Count > 0)
        {
            var src = alive[idx % alive.Count];
            if (src)
            {
                var clone = Instantiate(ballPrefab, src.transform.position, Quaternion.identity);
                SetupBallGO(clone);
                balls.Add(clone);

                var rb = clone.GetComponent<Rigidbody2D>();
                var srcRb = src.GetComponent<Rigidbody2D>();
                if (rb && srcRb)
                {
                    Vector2 dir = srcRb.velocity.normalized;
                    float deg = (spawned % 2 == 0) ? +12f : -12f;
                    float rad = deg * Mathf.Deg2Rad;
                    float cs = Mathf.Cos(rad);
                    float sn = Mathf.Sin(rad);
                    dir = new Vector2(dir.x * cs - dir.y * sn, dir.x * sn + dir.y * cs);
                    float spd = srcRb.velocity.magnitude;
                    rb.velocity = dir * spd;
                }
                spawned++;
            }
            idx++;
        }
    }

    int NextPowerCount(int current)
    {
        if (current <= 1) return 2;
        if (current <= 2) return 4;
        return current;
    }

    public void ResetForNewRoom()
    {
        ClearAll();
        InitIfNeeded();
    }
}
