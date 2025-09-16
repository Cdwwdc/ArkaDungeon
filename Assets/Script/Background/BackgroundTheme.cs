using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ArkaDungeon/BackgroundTheme")]
public class BackgroundTheme : ScriptableObject
{
    [Serializable]
    public class WeightedSprite
    {
        public Sprite sprite;
        public float weight = 1f;
    }

    [Header("테마 ID (예: fire / water / undead)")]
    public string themeId;

    [Header("후보 스프라이트 (가중치 선택)")]
    public List<WeightedSprite> candidates = new List<WeightedSprite>();

    public Sprite PickRandomSprite(System.Random rng = null)
    {
        if (candidates == null || candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0].sprite;

        float total = 0f;
        foreach (var c in candidates) total += Mathf.Max(0.0001f, c.weight);

        float r = (float)((rng ?? new System.Random()).NextDouble()) * total;
        foreach (var c in candidates)
        {
            float w = Mathf.Max(0.0001f, c.weight);
            if (r < w) return c.sprite;
            r -= w;
        }
        return candidates[0].sprite;
    }
}
