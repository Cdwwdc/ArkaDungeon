using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;

public class ExitGateUI : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("패널 루트(이 오브젝트 자체여도 됨)")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public Button continueButton;   // 계속 탐험
    public Button exitButton;       // 던전 밖으로 나가기

    [Header("안전 옵션")]
    [Tooltip("패널을 열 때 비활성화된 부모 GameObject들을 자동으로 켭니다.")]
    public bool autoEnableParents = true;
    [Tooltip("패널을 열 때 같은 Canvas 안에서 맨 위로 올립니다(SetAsLastSibling).")]
    public bool bringToFrontOnShow = true;

    static ExitGateUI _inst;

    void Awake()
    {
        if (_inst != null && _inst != this)
        {
            Destroy(gameObject);
            return;
        }
        _inst = this;

        if (!panelRoot) panelRoot = gameObject;
        Hide();
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    // ==== 외부에서 호출하는 정적 API ====

    /// <summary>
    /// 패널 표시. title이 null/빈문자열이면 기존 프리팹 텍스트를 절대로 변경하지 않음.
    /// </summary>
    public static void Show(string title, Action onContinue, Action onExit)
    {
        var ui = Ensure();
        if (!ui) return;

        // 타이틀은 "비어있지 않을 때만" 변경 (핵심 유지)
        if (!string.IsNullOrEmpty(title) && ui.titleText)
            ui.titleText.text = title;

        // 리스너 갱신
        if (ui.continueButton)
        {
            ui.continueButton.onClick.RemoveAllListeners();
            ui.continueButton.onClick.AddListener(() =>
            {
                try { onContinue?.Invoke(); }
                finally { Hide(); }
            });
        }
        if (ui.exitButton)
        {
            ui.exitButton.onClick.RemoveAllListeners();
            ui.exitButton.onClick.AddListener(() =>
            {
                try { onExit?.Invoke(); }
                finally { Hide(); }
            });
        }

        // === 자가 복구: 상위가 비활성화면 전부 켠다 ===
        if (ui.autoEnableParents && ui.panelRoot)
            ActivateParents(ui.panelRoot.transform);

        // 패널 켜기
        if (ui.panelRoot) ui.panelRoot.SetActive(true);

        // 같은 Canvas 안에서 맨 위로 올려 가려지지 않게
        if (ui.bringToFrontOnShow)
            ui.transform.SetAsLastSibling();

        // 디버깅: 그래도 안 보이면 원인 로그
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (ui.panelRoot && !ui.panelRoot.activeInHierarchy)
        {
            Debug.LogWarning("[ExitGateUI] panelRoot가 activeInHierarchy가 아닙니다. 상위 오브젝트가 비활성 상태인지 확인하세요.", ui.panelRoot);
        }
        else
        {
            var canvas = ui.GetComponentInParent<Canvas>(true);
            if (!canvas)
            {
                Debug.LogWarning("[ExitGateUI] Canvas를 찾지 못했습니다. ExitGateUI는 Canvas 하위에 있어야 화면에 표시됩니다.", ui.gameObject);
            }
        }
#endif
    }

    public static void Hide()
    {
        var ui = _inst;
        if (ui && ui.panelRoot) ui.panelRoot.SetActive(false);
    }

    public static bool IsOpen => _inst && _inst.panelRoot && _inst.panelRoot.activeSelf;

    public static void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        else
            Debug.LogError($"[ExitGateUI] '{sceneName}' 씬을 로드할 수 없습니다. Build Settings 확인 요망.");
    }

    // ==== 내부 유틸 ====
    static ExitGateUI Ensure()
    {
        if (_inst) return _inst;

        _inst = FindObjectOfType<ExitGateUI>(true);
        if (_inst)
        {
            if (!_inst.panelRoot) _inst.panelRoot = _inst.gameObject;
            return _inst;
        }

        Debug.LogError("[ExitGateUI] 씬에 ExitGateUI 오브젝트가 없습니다.");
        return null;
    }

    static void ActivateParents(Transform t)
    {
        // 루트까지 올라가며 비활성 부모를 기록했다가, 루트부터 순서대로 켬
        // (부모를 먼저 켜야 자식 활성화가 반영됨)
        var stack = new System.Collections.Generic.Stack<Transform>();
        var cur = t;
        while (cur != null)
        {
            if (!cur.gameObject.activeSelf)
                stack.Push(cur);
            cur = cur.parent;
        }
        while (stack.Count > 0)
        {
            var x = stack.Pop();
            x.gameObject.SetActive(true);
        }
    }
}
