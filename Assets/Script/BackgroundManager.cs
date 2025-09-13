using System.Collections.Generic;
using UnityEngine;

public class BackgroundManager : MonoBehaviour
{
    [System.Serializable]
    public class ThemeGroup
    {
        public string id;         // 예: "BlueDungeon", "Catacomb", "Wood"
        public Sprite[] sprites;  // 해당 테마의 map001, map002 ...
    }

    [Header("표시 대상")]
    public SpriteRenderer targetRenderer; // 배경을 표시할 SpriteRenderer

    [Header("테마 세트")]
    public ThemeGroup[] themes;          // 테마별 스프라이트 묶음

    [Header("선택 옵션")]
    public string currentThemeId = "";   // 비우면 첫 번째 테마 사용
    public bool pickRandomThemeOnStart = false;    // 시작 시 테마도 랜덤 선택
    public bool pickRandomSpriteEveryRoom = true;  // 방 이동 때마다 새 이미지 선택

    private ThemeGroup currentTheme;

    // ▶ 테마별로 "아직 안 쓴 인덱스" 리스트를 유지 (셔플 가방)
    private Dictionary<string, List<int>> _unusedIndexBag = new Dictionary<string, List<int>>();

    void Start()
    {
        if (!targetRenderer)
            Debug.LogWarning("[BackgroundManager] targetRenderer 미지정");

        // 시작 테마 결정
        if (!string.IsNullOrEmpty(currentThemeId))
            currentTheme = FindTheme(currentThemeId);
        else
            currentTheme = pickRandomThemeOnStart ? RandomTheme() : FirstTheme();

        // 시작 이미지 적용
        ApplyNextNonRepeatingSprite();
    }

    // 방 이동 시 호출 (RoomController 버튼 끝에 한 줄: FindObjectOfType<BackgroundManager>()?.NextRoom();)
    public void NextRoom()
    {
        if (pickRandomSpriteEveryRoom)
            ApplyNextNonRepeatingSprite();
    }

    // 특정 테마로 강제 전환 (보스방 등에서 사용 가능)
    public void SetTheme(string id)
    {
        currentThemeId = id;
        currentTheme = FindTheme(id);
        if (currentTheme == null)
        {
            Debug.LogWarning($"[BackgroundManager] theme '{id}' 못 찾음 → 첫 테마 사용");
            currentTheme = FirstTheme();
        }
        ApplyNextNonRepeatingSprite();
    }

    // ===== 내부 도우미 =====
    ThemeGroup FirstTheme()
    {
        if (themes != null && themes.Length > 0) return themes[0];
        return null;
    }

    ThemeGroup FindTheme(string id)
    {
        if (themes == null) return null;
        foreach (var t in themes)
        {
            if (t != null && t.id == id) return t;
        }
        return null;
    }

    ThemeGroup RandomTheme()
    {
        if (themes == null || themes.Length == 0) return null;
        return themes[Random.Range(0, themes.Length)];
    }

    // 핵심: "해당 테마에서 아직 안 뽑힌 인덱스"를 섞어서 하나씩 소비
    void ApplyNextNonRepeatingSprite()
    {
        if (!targetRenderer || currentTheme == null) return;

        var arr = currentTheme.sprites;
        if (arr == null || arr.Length == 0) return;

        // 1) 현재 테마의 가방을 가져오거나, 없으면 새로 채움+셔플
        var bag = GetOrFillBag(currentTheme);

        // 2) 가방에서 하나 꺼내기
        int idx = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);

        // 3) 적용
        if (idx >= 0 && idx < arr.Length && arr[idx] != null)
            targetRenderer.sprite = arr[idx];
    }

    // 테마별 가방 반환. 비어있다면 0..n-1을 섞어서 채운다.
    List<int> GetOrFillBag(ThemeGroup theme)
    {
        if (theme == null || theme.sprites == null) return new List<int>();

        // key = theme.id (비어있으면 배열 참조 주소로 대체)
        string key = string.IsNullOrEmpty(theme.id) ? theme.GetHashCode().ToString() : theme.id;

        if (!_unusedIndexBag.TryGetValue(key, out var bag) || bag == null || bag.Count == 0)
        {
            int n = theme.sprites.Length;
            bag = new List<int>(n);
            for (int i = 0; i < n; i++) bag.Add(i);

            // Fisher–Yates shuffle
            for (int i = n - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            _unusedIndexBag[key] = bag;
        }
        return bag;
    }
}
