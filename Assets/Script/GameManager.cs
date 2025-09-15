using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("필수 참조")]
    public GameObject ballPrefab;
    public Transform ballSpawn;   // 없어도 동작: 패들 위로 폴백
    public Transform paddle;
    public ExitDoors exitDoors;   // 없어도 동작

    [Header("UI (TextMeshPro)")]
    public Canvas uiCanvas;             // UI Canvas (없어도 동작)
    public GameObject continuePanel;    // Continue? 패널
    public TMP_Text countdownText;      // 3-2-1 텍스트
    public TMP_Text nextStageText;      // Next Stage 점멸 텍스트
    public TMP_Text gameOverText;       // Game Over 텍스트

    [Header("Start UI")]
    public GameObject startPanel;       // 스타트 버튼 패널(눌리면 숨김)

    [Header("설정")]
    public float countdownInterval = 1.0f;

    [Tooltip("재출발 직후 데스존 무시 시간(초). 무적 연출과 별개로 사용하지 않음.")]
    public float deathzoneGrace = 0.0f; // (무적 시스템으로 대체, 필요 시 가산)

    public float spawnAbovePaddleOffset = 0.35f;
    public float restartDelay = 3f;     // Game Over 후 자동 재시작 딜레이(초)

    [Header("무적 설정")]
    [Tooltip("시작/컨티뉴 직후 무적 시간(초)")]
    public float invincibleDuration = 3f;
    [Tooltip("무적 점멸 간격(초)")]
    public float invincibleBlinkInterval = 0.15f;

    [Header("멀티볼 연동(옵션)")]
    public BallManager ballManager; // 있으면 새 방/재시작 시 BallManager 통해 정리만 사용

    private GameObject currentBall;
    private Coroutine blinkRoutine;

    // 스테이지 클리어~다음 방 들어갈 때까지 사망 무시
    public bool isTransitioning { get; private set; } = false;

    // ===== 공 관리 =====
    public void HideBall()
    {
        if (currentBall != null) currentBall.SetActive(false);
    }

    /// <summary>잔여 공 싹 정리 (멀티볼 포함)</summary>
    public void KillBall()
    {
        if (ballManager != null) ballManager.ClearAll();   // 멀티볼 내부 리스트 정리

        var leftovers = GameObject.FindGameObjectsWithTag("Ball");
        foreach (var b in leftovers) if (b != null) Destroy(b); // 씬에 남은 건 전부 제거

        currentBall = null;
    }

    Vector3 GetSpawnPosition()
    {
        if (ballSpawn) return ballSpawn.position;
        if (paddle)
        {
            var p = paddle.position;
            p.y += Mathf.Abs(spawnAbovePaddleOffset); // 무조건 패들 ‘위’로
            return p;
        }
        return Vector3.zero;
    }

    // ===== 카운트다운 후 '그때' 공 생성/가시화/발사 + 무적 =====
    public IEnumerator CountdownAndLaunch()
    {
        // (중요) 카운트다운 동안은 공이 "아예 없어야" 함 — 떠돌이/스텔스 차단
        KillBall();

        // 카운트다운 (Realtime — 타임스케일 0 무시)
        if (countdownText)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "3"; yield return new WaitForSecondsRealtime(countdownInterval);
            countdownText.text = "2"; yield return new WaitForSecondsRealtime(countdownInterval);
            countdownText.text = "1"; yield return new WaitForSecondsRealtime(countdownInterval);
            countdownText.gameObject.SetActive(false);
        }

        // 무적/유예 적용 — 트리거/경계 체크 모두 이 시간 동안 건너뜀
        float grace = Mathf.Max(invincibleDuration, deathzoneGrace);
        if (grace > 0f) DeathZone.IgnoreFor(grace);

        // 여기서 '새 공'을 만든다 → 카운트다운 중 들이박는 스텔스 공 원천 차단
        Vector3 spawnPos = GetSpawnPosition();
        if (!ballPrefab)
        {
            Debug.LogError("[GM] ballPrefab 미설정 → 공 생성 불가");
            yield break;
        }

        currentBall = Instantiate(ballPrefab, spawnPos, Quaternion.identity);

        // [PATCH] 출발 단계로 리셋 (느린 출발 보장)
        var ballComp = currentBall.GetComponent<Ball>();
        if (ballComp) ballComp.ResetLaunchPhase();

        // 보이게 강제
        ForceVisible(currentBall);

        // 발사
        var rb = currentBall.GetComponent<Rigidbody2D>();
        if (rb)
        {
            Vector2 dir = Vector2.up;
            if (paddle) dir = (paddle.position - spawnPos).normalized;
            dir = (dir + Vector2.up * 0.35f).normalized; // 완전 수평 회피

            // [PATCH] Ball 설정을 존중: 하한 0, 상한은 프리팹 maxSpeed
            float speed = 8f;
            if (ballComp) speed = Mathf.Clamp(ballComp.startSpeed, 0f, ballComp.maxSpeed);
            rb.velocity = dir * speed;
        }

        // 무적 점멸 + 무적 중 낙하 시 즉시 리스폰 가드
        if (invincibleDuration > 0f) StartCoroutine(InvincibleFXAndGuard(invincibleDuration));
    }

    // 자식/렌더러가 꺼져도 강제 켜기(한 번만)
    void ForceVisible(GameObject go)
    {
        if (!go) return;
        var ts = go.GetComponentsInChildren<Transform>(true);
        foreach (var t in ts) if (t && !t.gameObject.activeSelf) t.gameObject.SetActive(true);
        var rs = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rs) if (r && !r.enabled) r.enabled = true;
    }

    // 무적: 공/패들 점멸 + killY 아래로 떨어지면 즉시 리스폰(느린 출발로 복귀)
    IEnumerator InvincibleFXAndGuard(float duration)
    {
        float end = Time.time + duration;
        // 점멸용 렌더러 캐시
        SpriteRenderer[] ballR = currentBall ? currentBall.GetComponentsInChildren<SpriteRenderer>(true) : null;
        SpriteRenderer[] padR = paddle ? paddle.GetComponentsInChildren<SpriteRenderer>(true) : null;

        // killY 기준선 확보 (DeathZone에서 가져오거나 폴백)
        float killY = (paddle ? paddle.position.y - 5f : -5f);
        var dz = FindObjectOfType<DeathZone>();
        if (dz) killY = dz.GetKillY();

        bool on = true;
        while (Time.time < end)
        {
            float a = on ? 1f : 0.35f; // 반투명 점멸
            if (ballR != null) foreach (var r in ballR) if (r) { var c = r.color; c.a = a; r.color = c; }
            if (padR != null) foreach (var r in padR) if (r) { var c = r.color; c.a = a; r.color = c; }
            on = !on;

            // [PATCH] 무적 중 아래로 떨어졌으면 즉시 스폰 위치로 복귀 + 출발 단계로 리셋
            if (currentBall && currentBall.transform.position.y < killY - 0.05f)
            {
                Vector3 spawnPos = GetSpawnPosition();
                currentBall.transform.position = spawnPos;

                var rb = currentBall.GetComponent<Rigidbody2D>();
                var ball = currentBall.GetComponent<Ball>();

                if (ball) ball.ResetLaunchPhase(); // ← 출발 단계로 되돌림

                if (rb)
                {
                    Vector2 dir = Vector2.up;
                    if (paddle) dir = (paddle.position - spawnPos).normalized;
                    dir = (dir + Vector2.up * 0.35f).normalized;

                    float speed = 8f;
                    if (ball) speed = Mathf.Clamp(ball.startSpeed, 0f, ball.maxSpeed); // ← 느린 출발 보장
                    rb.velocity = dir * speed;
                }
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, invincibleBlinkInterval));
        }

        // 투명도 원복
        if (ballR != null) foreach (var r in ballR) if (r) { var c = r.color; c.a = 1f; r.color = c; }
        if (padR != null) foreach (var r in padR) if (r) { var c = r.color; c.a = 1f; r.color = c; }
    }

    // ===== 사망 플로우 =====
    public void OnBallDeath()
    {
        if (isTransitioning) return; // 방 전환 중이면 무시

        if (uiCanvas && !uiCanvas.enabled) uiCanvas.enabled = true;

        HideStartPanel();                    // 컨티뉴 가려지는 문제 방지
        if (continuePanel) continuePanel.SetActive(true);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(false);
    }

    public void OnContinueYes()
    {
        if (continuePanel) continuePanel.SetActive(false);
        HideStartPanel();                    // 혹시 남아있으면 숨김
        StartCoroutine(CountdownAndLaunch()); // 카운트다운 끝에 '새 공' 생성(+무적)
    }

    public void OnContinueNo()
    {
        if (continuePanel) continuePanel.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(true);
        StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, restartDelay));

        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (continuePanel) continuePanel.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        KillBall();

        var rc = FindObjectOfType<RoomController>();
        if (rc) rc.BuildRoomSimple();
        FindObjectOfType<BackgroundManager>()?.NextRoom();

        if (ballManager != null) ballManager.ResetForNewRoom();
        currentBall = null;

        StartCoroutine(CountdownAndLaunch());
    }

    // ===== 스테이지 클리어/다음 방 =====
    public void OnStageClear()
    {
        isTransitioning = true;

        KillBall(); // 중복 공 원천 차단

        if (nextStageText)
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            nextStageText.gameObject.SetActive(true);
            blinkRoutine = StartCoroutine(Blink(nextStageText.gameObject, 0.5f));
        }

        if (continuePanel) continuePanel.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(true);
    }

    public void OnNextRoomEntered()
    {
        if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        // 다음 방 준비 — 중복 공 방지
        KillBall();
        if (ballManager != null) ballManager.ResetForNewRoom();
        currentBall = null;

        isTransitioning = false;

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

    void HideStartPanel()
    {
        if (startPanel && startPanel.activeSelf) { startPanel.SetActive(false); return; }
        var sp = GameObject.Find("StartPanel"); // 이름 폴백
        if (sp) sp.SetActive(false);
    }
}
