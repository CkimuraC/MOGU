using UnityEngine;

/// <summary>
/// 画面表示(OnGUI)の文字を、日本語対応フォント・画面サイズに合わせた大きさ・読みやすい影つきで描く共通部品。
///
/// ・文字の大きさは「画面の高さ ÷ 1080」の倍率で自動的に拡大する
/// ・さらに UserScale(デバッグパネルの「文字の大きさ」)で全体を拡大・縮小できる。値は PlayerPrefs に保存される
/// ・基準の大きさは「1080pの画面で何pxか」で指定する(例: 18)
/// ・OnGUI の中からだけ呼ぶこと
/// </summary>
public static class UiText
{
    static readonly string[] OsFontCandidates =
    {
        "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic", "MS UI Gothic",
        "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "Noto Sans JP", "Arial Unicode MS"
    };

    const string PrefKey = "UiTextScale";

    static Font font;
    static GUIStyle style;
    static float userScale = -1f;

    /// <summary>日本語対応のフォント(OSのフォントを探す)</summary>
    public static Font Font
    {
        get
        {
            if (font == null) font = Font.CreateDynamicFontFromOSFont(OsFontCandidates, 32);
            return font;
        }
    }

    /// <summary>全体の文字の大きさの倍率(1=標準)。デバッグパネルから変えられる。</summary>
    public static float UserScale
    {
        get
        {
            if (userScale < 0f) userScale = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKey, 1f), 0.6f, 3f);
            return userScale;
        }
        set
        {
            userScale = Mathf.Clamp(value, 0.6f, 3f);
            PlayerPrefs.SetFloat(PrefKey, userScale);
        }
    }

    /// <summary>画面サイズ × UserScale の倍率</summary>
    public static float Factor { get { return Mathf.Max(0.5f, Screen.height / 1080f) * UserScale; } }

    /// <summary>基準の大きさ(1080pでのpx)を、今の画面でのpxにする。</summary>
    public static int Px(float basePx) { return Mathf.Max(8, Mathf.RoundToInt(basePx * Factor)); }

    /// <summary>1行ぶんの高さ(px)</summary>
    public static float Line(float basePx) { return Px(basePx) * 1.4f; }

    /// <summary>
    /// 文字を描く。背景が何色でも読めるよう、暗い影を付ける。
    /// </summary>
    public static void Label(Rect rect, string text, float basePx, Color color,
        TextAnchor anchor = TextAnchor.UpperLeft, bool shadow = true)
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0)
            };
            style.font = Font;
        }
        style.font = Font;
        style.fontSize = Px(basePx);
        style.alignment = anchor;

        if (shadow)
        {
            float o = Mathf.Max(1f, style.fontSize * 0.07f);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f * color.a);
            GUI.Label(new Rect(rect.x + o, rect.y + o, rect.width, rect.height), text, style);
        }
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }
}
