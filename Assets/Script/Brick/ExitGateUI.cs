using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;

public class ExitGateUI : MonoBehaviour
{
    [Header("����")]
    [Tooltip("�г� ��Ʈ(�� ������Ʈ ��ü���� ��)")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public Button continueButton;   // ��� Ž��
    public Button exitButton;       // ���� ������ ������

    [Header("���� �ɼ�")]
    [Tooltip("�г��� �� �� ��Ȱ��ȭ�� �θ� GameObject���� �ڵ����� �մϴ�.")]
    public bool autoEnableParents = true;
    [Tooltip("�г��� �� �� ���� Canvas �ȿ��� �� ���� �ø��ϴ�(SetAsLastSibling).")]
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

    // ==== �ܺο��� ȣ���ϴ� ���� API ====

    /// <summary>
    /// �г� ǥ��. title�� null/���ڿ��̸� ���� ������ �ؽ�Ʈ�� ����� �������� ����.
    /// </summary>
    public static void Show(string title, Action onContinue, Action onExit)
    {
        var ui = Ensure();
        if (!ui) return;

        // Ÿ��Ʋ�� "������� ���� ����" ���� (�ٽ� ����)
        if (!string.IsNullOrEmpty(title) && ui.titleText)
            ui.titleText.text = title;

        // ������ ����
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

        // === �ڰ� ����: ������ ��Ȱ��ȭ�� ���� �Ҵ� ===
        if (ui.autoEnableParents && ui.panelRoot)
            ActivateParents(ui.panelRoot.transform);

        // �г� �ѱ�
        if (ui.panelRoot) ui.panelRoot.SetActive(true);

        // ���� Canvas �ȿ��� �� ���� �÷� �������� �ʰ�
        if (ui.bringToFrontOnShow)
            ui.transform.SetAsLastSibling();

        // �����: �׷��� �� ���̸� ���� �α�
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (ui.panelRoot && !ui.panelRoot.activeInHierarchy)
        {
            Debug.LogWarning("[ExitGateUI] panelRoot�� activeInHierarchy�� �ƴմϴ�. ���� ������Ʈ�� ��Ȱ�� �������� Ȯ���ϼ���.", ui.panelRoot);
        }
        else
        {
            var canvas = ui.GetComponentInParent<Canvas>(true);
            if (!canvas)
            {
                Debug.LogWarning("[ExitGateUI] Canvas�� ã�� ���߽��ϴ�. ExitGateUI�� Canvas ������ �־�� ȭ�鿡 ǥ�õ˴ϴ�.", ui.gameObject);
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
            Debug.LogError($"[ExitGateUI] '{sceneName}' ���� �ε��� �� �����ϴ�. Build Settings Ȯ�� ���.");
    }

    // ==== ���� ��ƿ ====
    static ExitGateUI Ensure()
    {
        if (_inst) return _inst;

        _inst = FindObjectOfType<ExitGateUI>(true);
        if (_inst)
        {
            if (!_inst.panelRoot) _inst.panelRoot = _inst.gameObject;
            return _inst;
        }

        Debug.LogError("[ExitGateUI] ���� ExitGateUI ������Ʈ�� �����ϴ�.");
        return null;
    }

    static void ActivateParents(Transform t)
    {
        // ��Ʈ���� �ö󰡸� ��Ȱ�� �θ� ����ߴٰ�, ��Ʈ���� ������� ��
        // (�θ� ���� �Ѿ� �ڽ� Ȱ��ȭ�� �ݿ���)
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
