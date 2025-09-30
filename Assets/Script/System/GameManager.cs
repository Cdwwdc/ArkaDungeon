using System.Collections;
using System.Collections.Generic;
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

    [Header("멀티볼/매니저(선택)")]
    public BallManager ballManager; // 있으면 멀티볼/카운트/스폰 통합 관리

    private GameObject currentBall;        // 싱글 실행용 레거시 참조(유지)
    private Coroutine blinkRoutine;

    // 스테이지 클리어~다음 방 들어갈 때까지 사망 무시
    public bool isTransitioning { get; private set; } = false;

    // ===== 컨티뉴 표시 상태(중복 방지) =====
    private bool continueShown = false;
    public bool IsContinueShown() => continueShown;

    // ===== 다음 방 속도/개수 이월 =====
    private float lastStageEndSpeed = 0f;     // 직전 방 종료 시 공 속도(최대값 샘플)
    private bool applyCarryNextLaunch = false; // 다음 방 첫 스폰에 한정
    private int carryBallCount = 1;           // 다음 방으로 유지할 공 개수
    private int pendingSpawnCount = 1;        // 이번 카운트다운 후 스폰할 개수

    [Header("Ball Carry-over")]                // ★추가
    public bool carryBallBetweenStages = true; // ★추가: 이월 사용 여부
    public float carrySplitAngle = 12f;        // ★추가: 여러 개 스폰 시 각도 간격(도)

    // ===== 공 관리 =====
    public void HideBall()
    {
        if (currentBall != null) currentBall.SetActive(false);
    }

    /// <summary>잔여 공 싹 정리 (멀티볼 포함)</summary>
    public void KillBall()
    {
        if (ballManager != null) ballManager.ClearAll();

        var leftovers = GameObject.FindGameObjectsWithTag("Ball");
        foreach (var b in leftovers) if (b != null) Destroy(b);

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

    // ===== 공 1개 생성(공용) =====
    public GameObject SpawnBallAt(Vector3 pos, Vector2 dir, float desiredSpeed)
    {
        if (!ballPrefab) { Debug.LogError("[GM] ballPrefab 미설정"); return null; }

        var go = Instantiate(ballPrefab, pos, Quaternion.identity);
        var ball = go.GetComponent<Ball>();
        if (ball) ball.ResetLaunchPhase(); // 항상 출발 단계로

        // 보이게 강제
        ForceVisible(go);

        // 각도 보정 + 속도 세팅
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb)
        {
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;

            // 완전 수평 회피
            dir = (dir + Vector2.up * 0.35f).normalized;

            float maxS = ball ? ball.maxSpeed : 14f;
            float spd = Mathf.Clamp(desiredSpeed, 0f, maxS);
            rb.velocity = dir.normalized * spd;
        }

        // 멀티볼 매니저에 등록(있으면)
        if (ballManager) ballManager.Register(go);

        // 레거시 currentBall 갱신(싱글 기준)
        currentBall = go;
        return go;
    }

    // ===== 카운트다운 후 '그때' 공들 생성/가시화/발사 + 무적 =====
    public IEnumerator CountdownAndLaunch()
    {
        // 카운트다운 동안은 공이 "아예 없어야" 함 — 떠돌이/스텔스 차단
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

        // 무적/유예 적용
        float grace = Mathf.Max(invincibleDuration, deathzoneGrace);
        if (grace > 0f) DeathZone.IgnoreFor(grace);

        // 스폰 방향 기본값
        Vector3 spawnPos = GetSpawnPosition();
        Vector2 baseDir = paddle ? (paddle.position - spawnPos).normalized : Vector2.up;

        // 기본 출발 속도는 Ball 설정 존중
        float baseStart = 8f;
        var tempBall = ballPrefab ? ballPrefab.GetComponent<Ball>() : null;
        if (tempBall) baseStart = Mathf.Clamp(tempBall.startSpeed, 0f, tempBall.maxSpeed);

        // [이월] 다음 방 첫 스폰에 한해서 "상승치 1/2" 반영
        float desired = baseStart;
        if (applyCarryNextLaunch && tempBall)
        {
            float delta = Mathf.Max(0f, lastStageEndSpeed - baseStart);
            desired = Mathf.Clamp(baseStart + delta * 0.5f, 0f, tempBall.maxSpeed);
            applyCarryNextLaunch = false;
        }

        // N개 스폰(개수 유지/컨티뉴=1)
        int n = Mathf.Max(1, pendingSpawnCount);
        float spread = Mathf.Max(0f, carrySplitAngle);
        float mid = (n - 1) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float off = (i - mid) * spread;
            Vector2 dir = RotateDeg(baseDir, off);
            SpawnBallAt(spawnPos, dir, desired);
        }

        // 무적 점멸 + 무적 중 낙하 시 즉시 리스폰 가드(첫 공 기준만 켜도 충분)
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

    // ★추가: 패들 가시/비가시 전환(렌더러만) + 입력 잠금 연계
    void SetPaddleVisible(bool visible) // ★추가
    {
        if (!paddle) return;
        var rends = paddle.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) if (r) r.enabled = visible;
    } // ★추가

    // 무적: 공/패들 점멸 + killY 아래로 떨어지면 즉시 리스폰(느린 출발로 복귀)
    IEnumerator InvincibleFXAndGuard(float duration)
    {
        float end = Time.time + duration;
        SpriteRenderer[] ballR = currentBall ? currentBall.GetComponentsInChildren<SpriteRenderer>(true) : null;
        SpriteRenderer[] padR = paddle ? paddle.GetComponentsInChildren<SpriteRenderer>(true) : null;

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

            // 무적 중 아래로 떨어졌으면 즉시 스폰 위치로 복귀 + 출발 단계로 리셋
            if (currentBall && currentBall.transform.position.y < killY - 0.05f)
            {
                Vector3 sp = GetSpawnPosition();
                currentBall.transform.position = sp;

                var rb = currentBall.GetComponent<Rigidbody2D>();
                var ball = currentBall.GetComponent<Ball>();
                if (ball) ball.ResetLaunchPhase();

                if (rb)
                {
                    Vector2 dir = paddle ? (paddle.position - sp).normalized : Vector2.up;
                    dir = (dir + Vector2.up * 0.35f).normalized;

                    float maxS = ball ? ball.maxSpeed : 14f;
                    float spd = ball ? Mathf.Clamp(ball.startSpeed, 0f, maxS) : 8f;
                    rb.velocity = dir * spd;
                }
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, invincibleBlinkInterval));
        }

        // 투명도 원복
        if (ballR != null) foreach (var r in ballR) if (r) { var c = r.color; c.a = 1f; r.color = c; }
        if (padR != null) foreach (var r in padR) if (r) { var c = r.color; c.a = 1f; r.color = c; }
    }

    // ===== UI/플로우 =====
    public void ShowContinue()
    {
        if (continueShown || isTransitioning) return;

        // ★SlowMo: 대드존 히트(죽음) 연출 — 슬로모+쉐이크
        FindObjectOfType<SlowMoFX>()?.PlayDeathFX(); // ★SlowMo

        continueShown = true;
        if (uiCanvas && !uiCanvas.enabled) uiCanvas.enabled = true;

        HideStartPanel();
        if (continuePanel) continuePanel.SetActive(true);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        // 공은 즉시 제거, 패들 입력 잠금
        KillBall();
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(false);
    }

    public void OnContinueYes()
    {
        if (continuePanel) continuePanel.SetActive(false);
        HideStartPanel();

        continueShown = false;
        pendingSpawnCount = 1;               // 컨티뉴는 항상 1개로 시작
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(true);

        StartCoroutine(CountdownAndLaunch());
    }

    public void OnContinueNo()
    {
        if (continuePanel) continuePanel.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(true);

        continueShown = false;
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(false);

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

        // 새 방 브릭 HP/색 재할당(레벨존 반영)
        FindObjectOfType<BrickHPAssigner>()?.AssignAll();

        if (ballManager != null) ballManager.ResetForNewRoom();
        currentBall = null;

        pendingSpawnCount = 1;

        // ★추가: 다음 라운드 시작 전 패들 복구/입력 활성
        SetPaddleVisible(true);                                                // ★추가
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(true);       // ★추가

        StartCoroutine(CountdownAndLaunch());
    }

    // ===== 스테이지 클리어/다음 방 =====
    public void OnStageClear()
    {
        isTransitioning = true;

        // ★SlowMo: 클리어 순간 연출 — 슬로모+쉐이크
        FindObjectOfType<SlowMoFX>()?.PlayClearFX(); // ★SlowMo

        // 현재 공 개수/속도 샘플 저장(이월용)
        carryBallCount = carryBallBetweenStages ? Mathf.Max(1, CountLiveBallsStrict()) : 1;
        lastStageEndSpeed = SampleMaxBallSpeed();

        if (nextStageText)
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            nextStageText.gameObject.SetActive(true);
            blinkRoutine = StartCoroutine(Blink(nextStageText.gameObject, 0.5f));
        }

        if (continuePanel) continuePanel.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(true);

        // ★추가: 패들 즉시 숨김 + 입력 잠금(클리어 순간부터 멈춤/안 보임)
        SetPaddleVisible(false);                                               // ★추가
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(false);      // ★추가

        // 공은 전환 중엔 제거
        KillBall();

        // ★추가: 스테이지 클리어 시 아이템 정리
        ResolveItemsOnStageClear();                                            // ★추가
    }

    public void OnNextRoomEntered()
    {
        if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        // 방 입장 직후에도 한 번 더 레벨존 반영(안전)
        FindObjectOfType<BrickHPAssigner>()?.AssignAll();

        if (ballManager != null) ballManager.ResetForNewRoom();
        currentBall = null;

        // 다음 방: 개수 유지 + 속도 상승치 1/2 적용
        pendingSpawnCount = carryBallBetweenStages ? Mathf.Max(1, carryBallCount) : 1;
        applyCarryNextLaunch = carryBallBetweenStages;

        // ★추가: 다음 방에서 패들 다시 보이게 + 입력 활성
        SetPaddleVisible(true);                                                // ★추가
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(true);       // ★추가

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

    float SampleMaxBallSpeed()
    {
        float max = 0f;
        var balls = GameObject.FindGameObjectsWithTag("Ball");
        foreach (var b in balls)
        {
            var rb = b.GetComponent<Rigidbody2D>();
            if (rb) max = Mathf.Max(max, rb.velocity.magnitude);
        }
        return max;
    }

    // 활성 공 '정확 계수' (중복/비활성 제외)
    int CountLiveBallsStrict()
    {
        int count = 0;
        var balls = FindObjectsOfType<Ball>(false); // 비활성 컴포넌트 제외
        foreach (var ball in balls)
        {
            if (!ball) continue;
            var go = ball.gameObject;
            if (!go.activeInHierarchy) continue;
            count++;
        }
        return count > 0 ? count : 1;
    }

    // ★추가: 스테이지 클리어 시 아이템 정책 적용
    void ResolveItemsOnStageClear() // ★추가
    {
        var items = FindObjectsOfType<StageItemPolicy>(false);
        foreach (var it in items)
        {
            if (!it || !it.gameObject.activeInHierarchy) continue;

            switch (it.onStageClear)
            {
                case StageItemPolicy.StageClearBehavior.AutoCollect:
                    // 아이템 쪽에 CollectImmediately()가 있으면 호출(없어도 에러 안 남)
                    it.gameObject.SendMessage("CollectImmediately", SendMessageOptions.DontRequireReceiver);
                    Destroy(it.gameObject);
                    break;

                case StageItemPolicy.StageClearBehavior.Disappear:
                    Destroy(it.gameObject);
                    break;

                case StageItemPolicy.StageClearBehavior.None:
                default:
                    break;
            }
        }
    }

    static Vector2 RotateDeg(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }
}
