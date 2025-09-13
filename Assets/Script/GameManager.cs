using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("�ʼ� ����")]
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

    [Header("����")]
    public float countdownInterval = 1.0f;
    public float deathzoneGrace = 0.8f;
    public float spawnAbovePaddleOffset = 0.35f;
    public float restartDelay = 3f;  // �� GameOver �� ����� ������(��)

    private GameObject currentBall;
    private Coroutine blinkRoutine;

    // ===== �� ���� =====
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
            Debug.LogError("[GM] BallPrefab/BallSpawn �̼���");
            yield break;
        }

        var rb = currentBall.GetComponent<Rigidbody2D>();

        // ���� ��ġ ����: �׻� �е� ��
        Vector3 spawnPos = ballSpawn.position;
        if (paddle)
        {
            float minY = paddle.position.y + spawnAbovePaddleOffset;
            if (spawnPos.y < minY) spawnPos.y = minY;
        }
        currentBall.transform.position = spawnPos;

        if (rb) rb.velocity = Vector2.zero;
        currentBall.SetActive(false);

        // ī��Ʈ�ٿ�
        if (countdownText)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "3"; yield return new WaitForSeconds(countdownInterval);
            countdownText.text = "2"; yield return new WaitForSeconds(countdownInterval);
            countdownText.text = "1"; yield return new WaitForSeconds(countdownInterval);
            countdownText.gameObject.SetActive(false);
        }

        // ����� ���� ������ ����
        DeathZone.IgnoreFor(deathzoneGrace);

        // �߻�
        currentBall.SetActive(true);
        if (rb && paddle)
        {
            Vector2 dir = (paddle.position - currentBall.transform.position).normalized;
            dir = (dir + Vector2.up * 0.35f).normalized; // ���� ���� ȸ��
            float speed = 8f;
            var ball = currentBall.GetComponent<Ball>();
            if (ball) speed = Mathf.Clamp(ball.startSpeed, 6f, 14f);
            rb.velocity = dir * speed;
        }
    }

    // ===== ��� �÷ο� =====
    public void OnBallDeath()
    {
        // UI�� ���� �־��ٸ� ����
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
        // ���� �ð� �� �ڵ� �����
        StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, restartDelay));

        // UI ����
        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (continuePanel) continuePanel.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        // �� ���� �� �� ����
        KillBall();

        // �� ������ + ��� ��ȯ + ī��Ʈ�ٿ� ���
        var rc = FindObjectOfType<RoomController>();
        if (rc) rc.BuildRoomSimple();
        FindObjectOfType<BackgroundManager>()?.NextRoom();

        EnsureBallExists();
        StartCoroutine(CountdownAndLaunch());
    }

    // ===== �������� Ŭ���� =====
    public void OnStageClear()
    {
        // �� �� �� �� ����: Hide�� �ƴ϶� Kill!
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

        KillBall();          // �� ���� �� ������
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
