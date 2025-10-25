using UnityEngine;

[CreateAssetMenu(fileName = "SplashConfig", menuName = "Game/Splash Config")]
public class SplashConfig : ScriptableObject
{
    [System.Serializable]
    public class Slide
    {
        public Sprite sprite;         // ������ �̹���
        public float showSeconds = 1.5f; // ���� �ð�
        public float fadeIn = 0.35f;
        public float fadeOut = 0.35f;
        public bool skippable = true;
        public Color color = Color.white; // ƾƮ
    }

    public Slide[] slides;
    [Header("��ȯ")]
    public string nextSceneName = "Town";   // ���÷��� ������ �̵��� ��
    public bool loadNextAsync = true;     // ��׶��� �ε�
    public bool waitUntilLoaded = true;   // �� �ε��Ǹ� �Ѿ��
    [Header("�Է�")]
    public KeyCode skipKey = KeyCode.Escape; // ���� ��ŵ Ű
    public bool anyKeySkip = true;          // �ƹ� Ű/Ŭ������ ��ŵ
}
