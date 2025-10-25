using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using Spine.Unity;
using Spine;

public class LogoSceneController : MonoBehaviour
{
    [Header("Spine")]
    public SkeletonAnimation logoAnimation;
    public string startAnim = "start";
    public string loopAnim = "loop";

    [Header("애니 지연 옵션")]
    public bool delayAnimationStart = false;
    [Min(0f)] public float animationDelay = 0.3f;

    [Header("워밍업(히치 숨기기)")]
    [Range(0, 5)] public int warmupFrames = 2;

    [Header("수동 갱신(Manual 모드용)")]
    public bool forceManualAdvance = false;

    [Header("블렌딩(믹스)")]
    [Min(0f)] public float startToLoopMix = 0.12f;
    [Min(0f)] public float skipToLoopMix = 0.0f;
    public bool restartLoopOnSkip = true;

    [Header("루프로 전환 시 안전 리셋")]
    public bool resetPoseAndAlphaOnLoop = true;
    public bool resetPoseAndAlphaOnSkip = true;

    [Header("UI")]
    public TextMeshProUGUI tapText;        // 씬 오브젝트를 수동 지정(선택)
    [Tooltip("tapText가 비어 있을 때 자동 탐색용 Tag")]
    public string tapTextTag = "";
    [Tooltip("tapText가 비어 있고 Tag도 없을 때 이름으로 탐색")]
    public string tapTextName = "Text (TMP)";

    [Header("Fade (둘 중 하나 연결)")]
    public CanvasGroup fadeOverlay;   // 권장
    public SpriteRenderer fadeSprite;

    [Header("Times")]
    public float fadeInTime = 0.8f;
    public float fadeOutTime = 0.8f;
    public float skipFade = 0.18f;

    [Header("Flow")]
    public string nextSceneName = "Town";

    enum Phase { Starting, Looping, Exiting }
    Phase _phase = Phase.Starting;
    bool _canTap;

    void Awake()
    {
        // ▼ UI 자동 연결 (tapText만)
        if (tapText == null)
        {
            if (!string.IsNullOrEmpty(tapTextTag))
            {
                var go = GameObject.FindGameObjectWithTag(tapTextTag);
                tapText = go ? go.GetComponent<TextMeshProUGUI>() : null;
            }
            if (tapText == null && !string.IsNullOrEmpty(tapTextName))
            {
                var go = GameObject.Find(tapTextName);
                tapText = go ? go.GetComponent<TextMeshProUGUI>() : null;
            }
            if (tapText == null)
                Debug.LogWarning("[Logo] tapText 자동 연결 실패(Tag/Name 확인). 점멸 텍스트 없이 진행합니다.", this);
        }

        SetFade(1f); // 시작은 항상 검정

        if (logoAnimation)
        {
            logoAnimation.Initialize(true);
            if (logoAnimation.AnimationState != null)
                logoAnimation.AnimationState.ClearTrack(0);
        }

        if (tapText) tapText.alpha = 0f;
    }

    IEnumerator Start()
    {
        // 1) Spine 세팅
        SetupSpineAtFirstFrame(out bool hasStart, out bool hasLoop);

        // 2) 워밍업
        for (int i = 0; i < Mathf.Max(0, warmupFrames); i++)
            yield return new WaitForEndOfFrame();

        // 3) 페이드인
        StartCoroutine(Fade(1f, 0f, fadeInTime));

        // 4) 지연 재생
        if (delayAnimationStart && animationDelay > 0f)
            yield return new WaitForSeconds(animationDelay);

        if (logoAnimation && logoAnimation.AnimationState != null)
            logoAnimation.AnimationState.TimeScale = 1f;

        _canTap = true;
    }

    void Update()
    {
        if (forceManualAdvance && logoAnimation)
        {
            logoAnimation.Update(Time.deltaTime);
            logoAnimation.LateUpdate();
        }

        if (!_canTap) return;

        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            if (_phase == Phase.Starting)
                StartCoroutine(SkipStartToLoop());
            else if (_phase == Phase.Looping)
            {
                _phase = Phase.Exiting;
                _canTap = false;
                StartCoroutine(GoNextScene());
            }
        }
    }

    void SetupSpineAtFirstFrame(out bool hasStart, out bool hasLoop)
    {
        hasStart = hasLoop = false;
        if (!logoAnimation || logoAnimation.Skeleton == null) return;

        var sd = logoAnimation.Skeleton.Data;
        hasStart = !string.IsNullOrEmpty(startAnim) && sd.FindAnimation(startAnim) != null;
        hasLoop = !string.IsNullOrEmpty(loopAnim) && sd.FindAnimation(loopAnim) != null;

        var state = logoAnimation.AnimationState;
        state.ClearTrack(0);

        if (state.Data != null)
        {
            state.Data.DefaultMix = 0f;
            if (hasStart && hasLoop) state.Data.SetMix(startAnim, loopAnim, Mathf.Max(0f, startToLoopMix));
        }

        if (hasStart && hasLoop)
        {
            var startEntry = state.SetAnimation(0, startAnim, false);
            var loopEntry = state.AddAnimation(0, loopAnim, true, 0f);
            _phase = Phase.Starting;
            loopEntry.Start += OnLoopStart;
        }
        else if (hasLoop)
        {
            state.SetAnimation(0, loopAnim, true);
            EnterLoopPhase();
        }
        else
        {
            EnterLoopPhase();
        }

        state.TimeScale = 0f;
        state.Update(0f);
        state.Apply(logoAnimation.Skeleton);
        logoAnimation.LateUpdate();
    }

    void OnLoopStart(TrackEntry _)
    {
        if (resetPoseAndAlphaOnLoop)
        {
            ForceSetupPoseAndOpaque();
            var st = logoAnimation.AnimationState;
            st.Update(0f);
            st.Apply(logoAnimation.Skeleton);
            logoAnimation.LateUpdate();
        }
        EnterLoopPhase();
    }

    void EnterLoopPhase()
    {
        _phase = Phase.Looping;
        if (tapText) StartCoroutine(BlinkTapText());
    }

    IEnumerator SkipStartToLoop()
    {
        _canTap = false;

        yield return Fade(CurrentFade(), 1f, skipFade);

        var state = logoAnimation ? logoAnimation.AnimationState : null;
        if (state != null)
        {
            if (state.Data != null && !string.IsNullOrEmpty(startAnim) && !string.IsNullOrEmpty(loopAnim))
                state.Data.SetMix(startAnim, loopAnim, Mathf.Max(0f, skipToLoopMix));

            state.ClearTrack(0);

            if (resetPoseAndAlphaOnSkip)
                ForceSetupPoseAndOpaque();

            var loopEntry = state.SetAnimation(0, loopAnim, true);
            if (restartLoopOnSkip && loopEntry != null) loopEntry.TrackTime = 0f;

            state.TimeScale = 1f;
            state.Update(0f);
            state.Apply(logoAnimation.Skeleton);
            logoAnimation.LateUpdate();

            if (state.Data != null && !string.IsNullOrEmpty(startAnim) && !string.IsNullOrEmpty(loopAnim))
                state.Data.SetMix(startAnim, loopAnim, Mathf.Max(0f, startToLoopMix));
        }

        EnterLoopPhase();

        yield return Fade(1f, 0f, skipFade);
        _canTap = true;
    }

    void ForceSetupPoseAndOpaque()
    {
        if (logoAnimation == null || logoAnimation.Skeleton == null) return;
        var sk = logoAnimation.Skeleton;
        sk.SetToSetupPose();

        var slots = sk.Slots;
        for (int i = 0; i < slots.Count; i++)
            slots.Items[i].A = 1f;
    }

    IEnumerator GoNextScene()
    {
        yield return Fade(0f, 1f, fadeOutTime);
        if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            Debug.LogError($"[Logo] '{nextSceneName}' 씬을 로드할 수 없습니다. Build Settings 확인!");
    }

    IEnumerator BlinkTapText()
    {
        if (!tapText) yield break;
        while (_phase == Phase.Looping)
        {
            float t = 0f;
            while (t < 0.7f && _phase == Phase.Looping)
            { t += Time.deltaTime; tapText.alpha = Mathf.Clamp01(t / 0.7f); yield return null; }
            yield return new WaitForSeconds(0.2f);
            t = 0f;
            while (t < 0.7f && _phase == Phase.Looping)
            { t += Time.deltaTime; tapText.alpha = 1f - Mathf.Clamp01(t / 0.7f); yield return null; }
            yield return new WaitForSeconds(0.1f);
        }
        tapText.alpha = 0f;
    }

    void SetFade(float a)
    {
        if (fadeOverlay) fadeOverlay.alpha = a;
        if (fadeSprite) { var c = fadeSprite.color; c.a = a; fadeSprite.color = c; }
    }
    float CurrentFade()
    {
        if (fadeOverlay) return fadeOverlay.alpha;
        if (fadeSprite) return fadeSprite.color.a;
        return 0f;
    }
    IEnumerator Fade(float from, float to, float dur)
    {
        dur = Mathf.Max(0.01f, dur);
        float t = 0f;
        SetFade(from);
        while (t < dur)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        SetFade(to);
    }
}
