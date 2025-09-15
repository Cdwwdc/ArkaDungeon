using System.Collections.Generic;
using UnityEngine;

public class BallManager : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;
    public GameObject ballPrefab;
    public Transform ballSpawn;  // 없어도 됨: 패들 위 폴백
    public Transform paddle;

    [Header("설정")]
    public int maxBalls = 4;
    public string ballTag = "Ball";
    public string ballLayerName = "Ball"; // Project Settings > Tags and Layers 에서 생성 추천

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

        // 태그/레이어 보정
        if (!string.IsNullOrEmpty(ballTag)) b.tag = ballTag;
        int L = LayerMask.NameToLayer(ballLayerName);
        if (L >= 0) b.layer = L;
    }

    // 스폰 폴백: ballSpawn 없으면 패들 위로, 다 없으면 (0,0)
    Vector3 GetSpawnPosition()
    {
        if (ballSpawn) return ballSpawn.position;
        if (paddle) return paddle.position + Vector3.up * 0.35f;
        return Vector3.zero;
    }

    public void InitIfNeeded()
    {
        if (balls.Count > 0) return;

        // 내 슬롯 비어있으면 GM에서라도 가져와서 써준다 (Fail-safe)
        if (!ballPrefab && gameManager && gameManager.ballPrefab)
        {
            ballPrefab = gameManager.ballPrefab;
            Debug.LogWarning("[BallManager] ballPrefab 비어있어 GameManager.ballPrefab 사용");
        }

        if (!ballPrefab)
        {
            Debug.LogError("[BallManager] ballPrefab 미설정 → 공 생성 불가");
            return;
        }

        var b = Instantiate(ballPrefab, GetSpawnPosition(), Quaternion.identity);
        SetupBallGO(b);
        balls.Add(b);

        if (!b.activeSelf)
            Debug.LogWarning("[BallManager] 생성된 Ball이 Inactive입니다. 프리팹 루트를 Active로 바꿔주세요.");
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

        if (ActiveCount() > 0) return; // 아직 살아있는 볼이 있으면 계속

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

        int target = Mathf.Min(maxBalls, NextPowerCount(alive.Count)); // 1→2→4
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
