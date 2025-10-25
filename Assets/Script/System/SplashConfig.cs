using UnityEngine;

[CreateAssetMenu(fileName = "SplashConfig", menuName = "Game/Splash Config")]
public class SplashConfig : ScriptableObject
{
    [System.Serializable]
    public class Slide
    {
        public Sprite sprite;         // 보여줄 이미지
        public float showSeconds = 1.5f; // 정지 시간
        public float fadeIn = 0.35f;
        public float fadeOut = 0.35f;
        public bool skippable = true;
        public Color color = Color.white; // 틴트
    }

    public Slide[] slides;
    [Header("전환")]
    public string nextSceneName = "Town";   // 스플래시 끝나고 이동할 씬
    public bool loadNextAsync = true;     // 백그라운드 로딩
    public bool waitUntilLoaded = true;   // 다 로딩되면 넘어가게
    [Header("입력")]
    public KeyCode skipKey = KeyCode.Escape; // 강제 스킵 키
    public bool anyKeySkip = true;          // 아무 키/클릭으로 스킵
}
