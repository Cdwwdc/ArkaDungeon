using UnityEngine;

public class DeathZone : MonoBehaviour
{
    static float ignoreUntilTime = 0f;
    public static void IgnoreFor(float seconds)
    {
        ignoreUntilTime = Mathf.Max(ignoreUntilTime, Time.time + seconds);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;
        if (Time.time < ignoreUntilTime) return;

        var gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            other.gameObject.SetActive(false);
            gm.OnBallDeath();
        }
        else
        {
            Destroy(other.gameObject);
            Debug.LogWarning("[DeathZone] GameManager ¾øÀ½ ¡æ °ø ÆÄ±«");
        }
    }
}
