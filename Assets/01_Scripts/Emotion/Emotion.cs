using UnityEngine;

/// <summary>感情のかけらの種類。</summary>
public enum EmotionType
{
    Joy = 0,       // 喜び
    Anger = 1,     // 怒り
    Sadness = 2,   // 悲しみ
    Fun = 3,       // 楽

    // ---- 今後、増やす予定の感情(まだ使っていない。EmotionInfo.Count に含まれない)----
    Fear = 4,      // 恐れ
    Calm = 5,      // 安心
    Curiosity = 6  // 好奇心
}

/// <summary>感情ごとの名前・色。本番画像に差し替えるときも、色や名前はここで管理する。</summary>
public static class EmotionInfo
{
    /// <summary>いま使っている感情の数(喜怒哀楽の4つ)。増やすときは、ここと下の配列を増やす。</summary>
    public const int Count = 4;

    static readonly Color[] colors =
    {
        new Color32(235, 190, 80, 255),   // 喜び:黄
        new Color32(220, 110, 95, 255),   // 怒り:赤
        new Color32(100, 150, 205, 255),  // 哀:青
        new Color32(235, 150, 65, 255),   // 楽:オレンジ
        new Color32(160, 130, 200, 255),  // 恐れ:紫
        new Color32(120, 170, 145, 255),  // 安心:緑
        new Color32(210, 160, 110, 255)   // 好奇心(予約)
    };

    static readonly string[] namesEn = { "Joy", "Anger", "Sadness", "Fun", "Fear", "Calm", "Curiosity" };
    static readonly string[] namesJp = { "喜", "怒", "哀", "楽", "恐れ", "安心", "好奇心" };

    public static Color ColorOf(EmotionType t) { return colors[(int)t]; }
    public static string NameEn(EmotionType t) { return namesEn[(int)t]; }
    public static string NameJp(EmotionType t) { return namesJp[(int)t]; }

    /// <summary>効果の短い説明(かけらを使う画面に出す)</summary>
    public static string EffectText(EmotionType t)
    {
        switch (t)
        {
            case EmotionType.Joy: return "スタミナがじわじわ回復(明るさ・幸福感)";
            case EmotionType.Fun: return "移動と掘る速さがアップ(活発・楽しさ)";
            case EmotionType.Anger: return "掘る力がアップ(硬い土も掘れる)。スタミナ消費は増える";
            case EmotionType.Sadness: return "移動はゆっくりになるが、スタミナ消費が減る";
            case EmotionType.Fear: return "スキャンの範囲がアップ";
            case EmotionType.Calm: return "スタミナがじわじわ回復";
            default: return "特殊な空間の探知範囲がアップ";
        }
    }
}
