using UnityEngine;

/// <summary>感情のかけらの種類。</summary>
public enum EmotionType
{
    Joy = 0,       // 喜び
    Anger = 1,     // 怒り
    Sadness = 2,   // 悲しみ
    Fear = 3,      // 恐れ
    Calm = 4,      // 安心
    Curiosity = 5  // 好奇心
}

/// <summary>感情ごとの名前・色。本番画像に差し替えるときも、色や名前はここで管理する。</summary>
public static class EmotionInfo
{
    public const int Count = 6;

    static readonly Color[] colors =
    {
        new Color32(235, 190, 80, 255),   // 喜び:黄
        new Color32(220, 110, 95, 255),   // 怒り:赤
        new Color32(100, 150, 205, 255),  // 悲しみ:青
        new Color32(160, 130, 200, 255),  // 恐れ:紫
        new Color32(120, 170, 145, 255),  // 安心:緑
        new Color32(210, 160, 110, 255)   // 好奇心:橙
    };

    static readonly string[] namesEn = { "Joy", "Anger", "Sadness", "Fear", "Calm", "Curiosity" };
    static readonly string[] namesJp = { "喜び", "怒り", "悲しみ", "恐れ", "安心", "好奇心" };

    public static Color ColorOf(EmotionType t) { return colors[(int)t]; }
    public static string NameEn(EmotionType t) { return namesEn[(int)t]; }
    public static string NameJp(EmotionType t) { return namesJp[(int)t]; }
}
