using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Start UI")]
    public GameObject startPanel;   // Start 버튼의 OnClick → OnPressStartButton() 연결

    [Header("설정")]
    public float countdownInterval = 1.0f;
    public float deathzoneGrace = 0.0f;
    public float spawnAbovePaddleOffset = 0.35f;
    public float restartDelay = 3f;

    [Header("무적 설정")]
    public float invincibleDuration = 3f;
    public float invincibleBlinkInterval = 0.15f;

    [Header("멀티볼/매니저(선택)")]
    public BallManager ballManager;

    private GameObject currentBall;
    private Coroutine blinkRoutine;
    private Coroutine countdownCR;

    public bool isTransitioning { get; private set; } = false;

    private bool continueShown = false;
    public bool IsContinueShown() => continueShown;

    // 직전 방 → 다음 방 이월 파라미터
    private float lastStageEndSpeed = 0f;
    private bool applyCarryNextLaunch = false;
    private int carryBallCount = 1;      // 직전 방에서 살아남은 공 수(최소 1)
    private int pendingSpawnCount = 1;   // 다음 방에서 실제로 생성할 공 수

    [Header("Ball Carry-over")]
    public bool carryBallBetweenStages = true;
    public float carrySplitAngle = 12f;

    [Header("Game Over")]
    public bool loadSceneOnGameOver = true;
    public string gameOverSceneName = "Title";

    [Header("Game Over FX")]
    public Image gameOverOverlay;
    public float gameOverFadeIn = 0.6f;
    public float gameOverHold = 1.2f;
    public float gameOverTextFadeOut = 0.35f;
    public bool gameOverBlinkIfNoFade = false;
    public float gameOverBlinkInterval = 0.25f;
    public float gameOverPostHold = 0.6f;
    public bool allowSkipToTitleOnClick = true;

    [Header("Cinematic 중 UI 숨김 대상")]
    public Canvas[] canvasesToToggle;
    public GameObject[] uiRootsToToggle;

    // 탐험(비전투) 방에서 컨티뉴 노출 금지
    private bool allowContinueUI = true;
    public void SetAllowContinue(bool v)
    {
        allowContinueUI = v;
        if (!v) DismissAllModals(); // 탐험 모드 진입 시 혹시 뜬 모달 제거
    }

    // ====== 외부에서 호출하는 UI/상태 유틸 ======
    public void DismissAllModals()
    {
        if (continuePanel) continuePanel.SetActive(false);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        // ExitGateUI 등 외부 모달은 각자 CloseIfOpen 패턴 권장
    }

    public void CancelStartCountdown()
    {
        if (countdownCR != null)
        {
            StopCoroutine(countdownCR);
            countdownCR = null;
        }
        if (countdownText) countdownText.gameObject.SetActive(false);
    }

    public void SetStartUIVisible(bool v)
    {
        if (startPanel) startPanel.SetActive(v);
        if (uiCanvas && v && !uiCanvas.enabled) uiCanvas.enabled = true;
    }

    /// <summary>
    /// 전투 방 입장 준비: 자동 시작 금지, Start 누를 때까지 대기
    /// </summary>
    public void PrepareCombatRoomEntry()
    {
        SetAllUIVisible(true);
        if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        FindObjectOfType<BrickHPAssigner>()?.AssignAll();

        if (ballManager != null) ballManager.ResetForNewRoom();
        currentBall = null;

        // “직전 방에서 살아남은 공 수”를 딱 1회 반영
        pendingSpawnCount = carryBallBetweenStages ? Mathf.Max(1, carryBallCount) : 1;
        applyCarryNextLaunch = carryBallBetweenStages;

        SetPaddleVisible(true);
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(true);

        // 전투 방에서는 컨티뉴 허용
        SetAllowContinue(true);

        isTransitioning = false;

        // ★자동 시작 금지: Start UI 노출, 버튼 눌러야 시작
        SetStartUIVisible(true);
    }

    /// <summary>
    /// Start 버튼 OnClick. 자동 시작 시에도 이걸 호출하면 동일 흐름.
    /// </summary>
    public void OnPressStartButton()
    {
        if (countdownCR != null) return;   // 중복 방지
        SetStartUIVisible(false);
        StartCountdownOnce();
    }

    // ===== 공/발사 =====
    public void HideBall()
    {
        if (currentBall != null) currentBall.SetActive(false);
    }

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
            p.y += Mathf.Abs(spawnAbovePaddleOffset);
            return p;
        }
        return Vector3.zero;
    }

    public GameObject SpawnBallAt(Vector3 pos, Vector2 dir, float desiredSpeed)
    {
        if (!ballPrefab) { Debug.LogError("[GM] ballPrefab 미설정"); return null; }

        var go = Instantiate(ballPrefab, pos, Quaternion.identity);
        var ball = go.GetComponent<Ball>();
        if (ball) ball.ResetLaunchPhase();

        ForceVisible(go);

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir = (dir + Vector2.up * 0.35f).normalized;

            float maxS = ball ? ball.maxSpeed : 14f;
            float spd = Mathf.Clamp(desiredSpeed, 0f, maxS);
            rb.velocity = dir.normalized * spd;
        }

        if (ballManager) ballManager.Register(go);

        currentBall = go;
        return go;
    }

    public IEnumerator CountdownAndLaunch()
    {
        KillBall(); // 방 시작 직전 잔여 공 정리

        if (countdownText)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "3"; yield return new WaitForSecondsRealtime(countdownInterval);
            countdownText.text = "2"; yield return new WaitForSecondsRealtime(countdownInterval);
            countdownText.text = "1"; yield return new WaitForSecondsRealtime(countdownInterval);
            countdownText.gameObject.SetActive(false);
        }

        float grace = Mathf.Max(invincibleDuration, deathzoneGrace);
        if (grace > 0f) DeathZone.IgnoreFor(grace);

        Vector3 spawnPos = GetSpawnPosition();
        Vector2 baseDir = paddle ? (paddle.position - spawnPos).normalized : Vector2.up;

        float baseStart = 8f;
        var tempBall = ballPrefab ? ballPrefab.GetComponent<Ball>() : null;
        if (tempBall) baseStart = Mathf.Clamp(tempBall.startSpeed, 0f, tempBall.maxSpeed);

        float desired = baseStart;
        if (applyCarryNextLaunch && tempBall)
        {
            float delta = Mathf.Max(0f, lastStageEndSpeed - baseStart);
            desired = Mathf.Clamp(baseStart + delta * 0.5f, 0f, tempBall.maxSpeed);
            applyCarryNextLaunch = false;
        }

        int n = Mathf.Max(1, pendingSpawnCount);
        float spread = Mathf.Max(0f, carrySplitAngle);
        float mid = (n - 1) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float off = (i - mid) * spread;
            Vector2 dir = RotateDeg(baseDir, off);
            SpawnBallAt(spawnPos, dir, desired);
        }

        if (invincibleDuration > 0f) StartCoroutine(InvincibleFXAndGuard(invincibleDuration));
    }

    private void StartCountdownOnce()
    {
        if (countdownCR != null) return;
        countdownCR = StartCoroutine(Co_CountdownOnce());
    }
    private IEnumerator Co_CountdownOnce()
    {
        yield return StartCoroutine(CountdownAndLaunch());
        countdownCR = null;
    }

    void ForceVisible(GameObject go)
    {
        if (!go) return;
        var ts = go.GetComponentsInChildren<Transform>(true);
        foreach (var t in ts) if (t && !t.gameObject.activeSelf) t.gameObject.SetActive(true);
        var rs = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rs) if (r && !r.enabled) r.enabled = true;
    }

    void SetPaddleVisible(bool visible)
    {
        if (!paddle) return;
        var rends = paddle.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) if (r) r.enabled = visible;
    }

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
            float a = on ? 1f : 0.35f;
            if (ballR != null) foreach (var r in ballR) if (r) { var c = r.color; c.a = a; r.color = c; }
            if (padR != null) foreach (var r in padR) if (r) { var c = r.color; c.a = a; r.color = c; }
            on = !on;

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

        if (ballR != null) foreach (var r in ballR) if (r) { var c = r.color; c.a = 1f; r.color = c; }
        if (padR != null) foreach (var r in padR) if (r) { var c = r.color; c.a = 1f; r.color = c; }
    }

    // ===== UI/플로우 =====
    public void ShowContinue()
    {
        // 탐험(비전투) 또는 전환중일 때는 컨티뉴 금지
        if (!allowContinueUI || isTransitioning) return;

        FindObjectOfType<SlowMoFX>()?.PlayDeathFX();

        continueShown = true;
        if (uiCanvas && !uiCanvas.enabled) uiCanvas.enabled = true;

        HideStartPanel();
        if (continuePanel) continuePanel.SetActive(true);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        KillBall();
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(false);
    }

    public void OnContinueYes()
    {
        if (continuePanel) continuePanel.SetActive(false);
        HideStartPanel();

        continueShown = false;
        pendingSpawnCount = 1;
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(true);

        StartCountdownOnce();
    }

    public void OnContinueNo()
    {
        if (continuePanel) continuePanel.SetActive(false);
        if (gameOverText) gameOverText.gameObject.SetActive(true);

        continueShown = false;
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(false);

        StartCoroutine(GameOverSequence());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, restartDelay));

        if (loadSceneOnGameOver && !string.IsNullOrEmpty(gameOverSceneName)
            && Application.CanStreamedLevelBeLoaded(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName, LoadSceneMode.Single);
            yield break;
        }

        if (gameOverText) gameOverText.gameObject.SetActive(false);
        if (nextStageText) nextStageText.gameObject.SetActive(false);
        if (continuePanel) continuePanel.SetActive(false);
        if (exitDoors) exitDoors.Show(false);

        KillBall();

        var rc = FindObjectOfType<RoomController>();
        if (rc) rc.BuildRoomSimple();
        FindObjectOfType<BackgroundManager>()?.NextRoom();

        FindObjectOfType<BrickHPAssigner>()?.AssignAll();

        if (ballManager != null) ballManager.ResetForNewRoom();
        currentBall = null;

        pendingSpawnCount = 1;

        SetPaddleVisible(true);
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(true);

        // 재시작(같은 씬)일 때는 편의상 자동 시작
        StartCountdownOnce();
    }

    public void OnStageClear()
    {
        isTransitioning = true;

        FindObjectOfType<SlowMoFX>()?.PlayClearFX();

        // 직전 방 ‘살아남은’ 공 수 + 속도 기록 (다음 방에서 정확히 반영)
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

        SetPaddleVisible(false);
        paddle?.GetComponent<PaddleController>()?.SetInputEnabled(false);

        KillBall();
        ResolveItemsOnStageClear();
    }

    // (레거시 호환) 외부에서 호출 중이면 내부적으로 전투 방 준비만 수행
    public void OnNextRoomEntered()
    {
        PrepareCombatRoomEntry();
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
        var sp = GameObject.Find("StartPanel");
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

    int CountLiveBallsStrict()
    {
        int byComponent = 0;
        var comps = FindObjectsOfType<Ball>(false);
        foreach (var c in comps)
        {
            if (!c) continue;
            var go = c.gameObject;
            if (!go.activeInHierarchy) continue;
            byComponent++;
        }

        int byTag = 0;
        var gos = GameObject.FindGameObjectsWithTag("Ball");
        foreach (var go in gos)
        {
            if (!go) continue;
            if (!go.activeInHierarchy) continue;
            byTag++;
        }

        return Mathf.Max(1, Mathf.Max(byComponent, byTag));
    }

    void ResolveItemsOnStageClear()
    {
        var items = FindObjectsOfType<StageItemPolicy>(false);
        foreach (var it in items)
        {
            if (!it || !it.gameObject.activeInHierarchy) continue;

            switch (it.onStageClear)
            {
                case StageItemPolicy.StageClearBehavior.AutoCollect:
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

    // ===== Game Over 연출 =====
    IEnumerator GameOverSequence()
    {
        bool skip = false;

        if (gameOverOverlay)
        {
            var rt = gameOverOverlay.rectTransform;
            rt.SetAsFirstSibling();
            gameOverOverlay.gameObject.SetActive(true);
            SetImageAlpha(gameOverOverlay, 0f);
        }

        if (gameOverOverlay && gameOverFadeIn > 0f)
        {
            yield return Co_FadeImageSkippable(gameOverOverlay, 0f, 1f, gameOverFadeIn, () =>
            {
                if (!allowSkipToTitleOnClick) return false;
                return Input.GetMouseButtonDown(0) || Input.anyKeyDown;
            });
            if (allowSkipToTitleOnClick && (Input.GetMouseButton(0) || Input.anyKey)) skip = true;
        }
        else if (gameOverOverlay)
        {
            SetImageAlpha(gameOverOverlay, 1f);
        }

        if (skip && allowSkipToTitleOnClick) { GotoTitleOrFallback(); yield break; }

        if (gameOverHold > 0f)
        {
            bool s = false;
            yield return WaitRealtimeOrSkip(gameOverHold, allowSkipToTitleOnClick, v => s = v);
            if (s) { GotoTitleOrFallback(); yield break; }
        }

        if (gameOverText)
        {
            if (gameOverTextFadeOut > 0f)
            {
                yield return Co_FadeTMPSkippable(gameOverText, 1f, 0f, gameOverTextFadeOut, () =>
                {
                    if (!allowSkipToTitleOnClick) return false;
                    return Input.GetMouseButtonDown(0) || Input.anyKeyDown;
                });
                if (allowSkipToTitleOnClick && (Input.GetMouseButton(0) || Input.anyKey)) skip = true;
            }
            else if (gameOverBlinkIfNoFade)
            {
                float t = 0f;
                while (t < 0.9f)
                {
                    if (allowSkipToTitleOnClick && (Input.GetMouseButtonDown(0) || Input.anyKeyDown))
                    { skip = true; break; }
                    gameOverText.gameObject.SetActive(!gameOverText.gameObject.activeSelf);
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, gameOverBlinkInterval));
                    t += Mathf.Max(0.05f, gameOverBlinkInterval);
                }
                gameOverText.gameObject.SetActive(true);
            }
        }

        if (skip && allowSkipToTitleOnClick) { GotoTitleOrFallback(); yield break; }

        if (gameOverPostHold > 0f)
        {
            bool s = false;
            yield return WaitRealtimeOrSkip(gameOverPostHold, allowSkipToTitleOnClick, v => s = v);
            if (s) { GotoTitleOrFallback(); yield break; }
        }

        GotoTitleOrFallback();
    }

    void GotoTitleOrFallback()
    {
        if (loadSceneOnGameOver && !string.IsNullOrEmpty(gameOverSceneName)
            && Application.CanStreamedLevelBeLoaded(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName, LoadSceneMode.Single);

        }
        else
        {
            if (gameOverOverlay) { gameOverOverlay.gameObject.SetActive(false); }
            StartCoroutine(RestartAfterDelay());
        }
    }

    IEnumerator WaitRealtimeOrSkip(float seconds, bool skippable, System.Action<bool> onCompleted)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (skippable && (Input.GetMouseButtonDown(0) || Input.anyKeyDown))
            {
                onCompleted?.Invoke(true);
                yield break;
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        onCompleted?.Invoke(false);
    }

    IEnumerator Co_FadeImageSkippable(Image img, float from, float to, float dur, System.Func<bool> shouldSkip)
    {
        if (!img) yield break;
        dur = Mathf.Max(0.01f, dur);
        float t = 0f;
        SetImageAlpha(img, from);
        while (t < dur)
        {
            if (shouldSkip != null && shouldSkip()) yield break;
            t += Time.unscaledDeltaTime;
            SetImageAlpha(img, Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        SetImageAlpha(img, to);
    }

    IEnumerator Co_FadeTMPSkippable(TMP_Text txt, float from, float to, float dur, System.Func<bool> shouldSkip)
    {
        if (!txt) yield break;
        dur = Mathf.Max(0.01f, dur);
        float t = 0f;
        SetTMPAlpha(txt, from);
        while (t < dur)
        {
            if (shouldSkip != null && shouldSkip()) yield break;
            t += Time.unscaledDeltaTime;
            SetTMPAlpha(txt, Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        SetTMPAlpha(txt, to);
    }

    void SetImageAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    void SetTMPAlpha(TMP_Text txt, float a)
    {
        var c = txt.color; c.a = a; txt.color = c;
    }

    public void HideNextStageUIAndStopBlink()
    {
        if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
        if (nextStageText) nextStageText.gameObject.SetActive(false);
    }

    public void SetExitDoorsVisible(bool v)
    {
        if (exitDoors) exitDoors.Show(v);
    }

    public void ShowNextStageBlink(float interval = 0.5f)
    {
        if (!nextStageText) return;
        nextStageText.gameObject.SetActive(true);
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(Blink(nextStageText.gameObject, interval));
    }

    public void ShowNextStageUIAndBlink(float interval = 0.5f)
    {
        ShowNextStageBlink(interval);
        if (exitDoors) exitDoors.Show(true);
    }

    public void SetAllUIVisible(bool v)
    {
        if (canvasesToToggle != null)
        {
            for (int i = 0; i < canvasesToToggle.Length; i++)
            {
                var c = canvasesToToggle[i];
                if (c) c.enabled = v;
            }
        }
        if (uiRootsToToggle != null)
        {
            for (int i = 0; i < uiRootsToToggle.Length; i++)
            {
                var go = uiRootsToToggle[i];
                if (go) go.SetActive(v);
            }
        }
    }
}
