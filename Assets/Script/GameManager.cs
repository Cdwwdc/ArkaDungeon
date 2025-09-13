using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("필수 참조")]
    public GameObject ballPrefab;
    public Transform ballSpawn;
    public Transform paddle;
    public ExitDoors exitDoors;

    [Header("UI (TextMeshPro)")]
    public Canvas uiCanvas;
    public GameObject continuePanel;
    public TMP_Text countdownText;
    public TMP_Text nextStageText;
    public TMP_Text gameOverText;

    [Header("설정")]
    public float countdownInterval = 1.0f;
    public float deathzoneGrace = 0.8f;
    public float spawnAbovePaddleOffset = 0.35f;
    public float restartDelay = 3f;  // ★ GameOver 후 재시작 딜레이(초)

    private GameObject currentBall;
    private Coroutine blinkRoutine;

    // ===== 공 관리 =====
    public void HideBall()
    {
        if (currentBall != null) currentBall.SetActive(false);
    }

    public void KillBall()
    {
        if (currentBall != null)
        {
            Destroy(currentBall);
            currentBall = null;
        }
    }

    public void EnsureBallExists()
    {
        if (currentBall == null && ballPrefab && ballSpawn)
            currentBall = Instantiate(ballPrefab, ballSpawn.position, Quaternion.identity);
    }

    public IEnumerator CountdownAndLaunch()
    {
        EnsureBallExists();
        if (!currentBall || !ballSpawn)
        {
            Debug.LogError("[GM] BallPrefab/BallSpawn 미설정");
            yield break;
        }

        var rb = currentBall.GetComponent<Rigidbody2D>();

        // 스폰 위치 보정: 항상 패들 위
        Vector3 spawnPos = ballSpawn.position;
        if (paddle)
        {
            float minY = paddle.position.y + spawnAbovePaddleOffset;
            if (spawnPos.y < minY) spawnPos.y = minY;
        }
        currentBall.transform.position = spawnPos;

        if (rb) rb.velocity = Vector2.zero;
        currentBall.SetActive(false);

        // 카운트다운
        if (countdownText)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "3"; yield return new WaitForSeconds(countdownInterval);
            countdownText.text = "2"; yield return new WaitForSeconds(countdownInterval);
            countdownText.text = "1"; yield return new WaitForSeconds(countdownInterval);
            countdownText.gameObject.SetActive(false);
        }

        // 재출발 직후 데스존 무시
        DeathZone.IgnoreFor(deathzoneGrace);

        // 발사
        currentBall.SetActive(true);
        if (rb && paddle)
        {
            Vector2 dir = (paddle.position - currentBall.transform.position).normalized;
            dir = (dir + Vector2.up * 0.35f).normalized; // 완전 수평 회피
            float speed = 8f;
            var ball = currentBall.GetComponent<Ball>();
            if (ball) speed = Mathf.Clamp(ball.startSpeed, 6f, 14f);
            rb.velocity = dir * speed;
        }
    }

    // ===== 사망 플로우 =====
    public void OnBallDeath()
    {
        // UI가 꺼져 있었다면 켜줌
        if (uiCanvas && !uiCanvas.enabled) uiCanvas.enabled = true;

        HideBall();

        if (continuePanel) continuePanel.SetActive(true);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(false);

        if (exitDoors) exitDoors.Show(false);
    }

    public void OnContinueYes()
    {
        if (continuePanel) continuePanel.SetActive(false);
        StartCoroutine(CountdownAndLaunch());
    }

    public void OnContinueNo()
    {
        if (continuePanel) continuePanel.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(true);
        // 일정 시간 뒤 자동 재시작
        StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, restartDelay));

        // UI 정리
        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (continuePanel) continuePanel.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        // 공 정리 후 새 게임
        KillBall();

        // 방 리빌드 + 배경 전환 + 카운트다운 출발
        var rc = FindObjectOfType<RoomController>();
        if (rc) rc.BuildRoomSimple();
        FindObjectOfType<BackgroundManager>()?.NextRoom();

        EnsureBallExists();
        StartCoroutine(CountdownAndLaunch());
    }

    // ===== 스테이지 클리어 =====
    public void OnStageClear()
    {
        // ★ 두 개 공 방지: Hide가 아니라 Kill!
        KillBall();

        if (nextStageText)
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            nextStageText.gameObject.SetActive(true);
            blinkRoutine = StartCoroutine(Blink(nextStageText.gameObject, 0.5f));
        }

        if (continuePanel) continuePanel.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(false);
    }

    public void OnNextRoomEntered()
    {
        if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
        if (nextStageText) nextStageText.gameObject.SetActive(false);

        if (exitDoors) exitDoors.Show(false);

        KillBall();          // 새 방은 새 공부터
        EnsureBallExists();
        StartCoroutine(CountdownAndLaunch());
    }

    private IEnumerator Blink(GameObject go, float interval)
    {
        while (true)
        {
            go.SetActive(!go.activeSelf);
            yield return new WaitForSeconds(interval);
        }
    }
}
