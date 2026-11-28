using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// モグラの思考(吹き出し)。モグラの頭上に、ドット調の白い吹き出しを出す。
/// ・感情のかけらを食べたとき、その感情に対応した言葉を出す(Emotion Texts)
/// ・他の場面でも MoleThought.Say("…") で好きな言葉を出せる
/// ・吹き出しは、考え中を表す小さな丸が2つ付いた形。背景なし、白地に暗い縁取り。
/// ・文字は日本語対応フォントが必要。既定ではOSの日本語フォントを探す。
///   ドット風フォントを用意したら Custom Font に入れると差し替わる。
/// </summary>
public class MoleThought : MonoBehaviour
{
    [Header("Texts (食べた感情 → モグラの思考)")]
    [Tooltip("順番は 喜 / 怒 / 哀 / 楽")]
    public string[] emotionTexts =
    {
        "ふふっ♪",
        "むむむ…！",
        "……",
        "わーい！"
    };

    [Header("Display")]
    [Tooltip("吹き出しが出ている時間(秒)")]
    public float displaySeconds = 2.5f;
    [Tooltip("モグラの位置からの高さ(ユニット)")]
    public float heightAbove = 0.55f;
    [Tooltip("文字の大きさ(ドット単位)")]
    public float fontTexels = 7f;
    [Tooltip("1ドットが画面上で何pxか。0なら画面の高さから自動で決める")]
    public int pixelScaleOverride = 0;

    [Header("Look")]
    public Color outlineColor = new Color32(70, 52, 44, 255);
    public Color fillColor = Color.white;
    [Tooltip("吹き出しの内側の下・右に入れる薄い影(立体感)")]
    public Color shadeColor = new Color32(222, 220, 214, 255);
    public Color textColor = new Color32(60, 44, 38, 255);

    [Header("Font")]
    [Tooltip("日本語対応フォント。空ならOSの日本語フォントを探す。ドット風フォントを入れると差し替わる")]
    public Font customFont;

    [Header("Debug (開発用)")]
    [Tooltip("画面左下に、吹き出しの状態(言葉が指示された回数・描画された回数・フォントなど)を表示する")]
    public bool showDebugInfo = false;

    string current;
    string lastSaid;
    int sayCount;
    int drawnCount;
    float until;
    Font font;
    Camera cam;
    GUIStyle style;
    int styleSize;
    readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    static readonly string[] OsFontCandidates =
    {
        "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic", "MS UI Gothic",
        "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "Noto Sans JP", "Arial Unicode MS"
    };

    /// <summary>感情に対応した思考を出す。</summary>
    public void ShowEmotion(EmotionType t)
    {
        int i = (int)t;
        if (emotionTexts == null || i < 0 || i >= emotionTexts.Length) return;
        Say(emotionTexts[i]);
    }

    /// <summary>好きな言葉を吹き出しで出す。seconds が 0 以下なら Display Seconds を使う。</summary>
public void Say(string text, float seconds = 0f)
    {
        if (string.IsNullOrEmpty(text)) return;
        current = text;
        lastSaid = text;
        sayCount++;
        until = Time.time + (seconds > 0f ? seconds : displaySeconds);
        Debug.Log("[MoleThought] Say len=" + text.Length + " until=" + until + " (now " + Time.time + ")");
    }

    void Awake()
    {
        font = customFont != null ? customFont : Font.CreateDynamicFontFromOSFont(OsFontCandidates, 32);
    }

void OnGUI()
    {
        if (cam == null) cam = Camera.main;
        if (showDebugInfo) DrawDebug();

        if (string.IsNullOrEmpty(current) || Time.time >= until) return;
        if (cam == null) return;
        if (customFont != null && font != customFont) font = customFont;

        GUI.depth = -1000; // 他のGUIより手前に描く

        int scale = pixelScaleOverride > 0 ? pixelScaleOverride : Mathf.Max(2, Screen.height / 260);
        int fontPx = Mathf.Max(8, Mathf.RoundToInt(fontTexels * scale));

        if (style == null || styleSize != fontPx)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = fontPx,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Overflow
            };
            styleSize = fontPx;
        }
        style.normal.textColor = textColor;

        // 文字の大きさに合わせて吹き出しの大きさ(ドット数)を決める
        Vector2 ts = style.CalcSize(new GUIContent(current));
        const int padX = 4, padY = 3, tail = 11;
        int bodyW = Mathf.Max(18, Mathf.CeilToInt(ts.x / scale) + padX * 2);
        int bodyH = Mathf.Max(10, Mathf.CeilToInt(ts.y / scale) + padY * 2);
        Texture2D tex = GetBubble(bodyW, bodyH, tail);

        int wPx = tex.width * scale;
        int hPx = tex.height * scale;

        // モグラの頭上。吹き出しの下端中央を合わせる(ドットがぼやけないよう整数位置に)
        Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * heightAbove);
        float x = Mathf.Round(sp.x - wPx * 0.5f);
        float y = Mathf.Round((Screen.height - sp.y) - hPx);
        x = Mathf.Clamp(x, 4f, Mathf.Max(4f, Screen.width - wPx - 4f));
        y = Mathf.Max(4f, y);

        var prev = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, y, wPx, hPx), tex, ScaleMode.StretchToFill, true);
        GUI.Label(new Rect(x, y, wPx, bodyH * scale), current, style);
        GUI.color = prev;

        if (Event.current.type == EventType.Repaint) drawnCount++;
    }

    void DrawDebug()
    {
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 0.6f, 0.95f);
        float remain = Mathf.Max(0f, until - Time.time);
        string t = "[Thought] said=" + sayCount + "  lastLen=" + (lastSaid == null ? 0 : lastSaid.Length) +
                   "  remaining=" + remain.ToString("0.0") + "s  drawn=" + drawnCount +
                   "  font=" + (font != null ? font.name : "null") + "  cam=" + (cam != null ? "ok" : "NULL") +
                   "  screen=" + Screen.width + "x" + Screen.height;
        GUI.Label(new Rect(20, Screen.height - 30, 1400, 22), t);
        GUI.color = prev;
    }

    Texture2D GetBubble(int bodyW, int bodyH, int tail)
    {
        string key = bodyW + "x" + bodyH;
        Texture2D t;
        if (cache.TryGetValue(key, out t) && t != null) return t;
        t = BuildBubble(bodyW, bodyH, tail);
        cache[key] = t;
        return t;
    }

    /// <summary>角を欠いたドット絵の吹き出し + 下に小さな丸2つ(考え中)を描く。</summary>
    Texture2D BuildBubble(int w, int bodyH, int tail)
    {
        int h = bodyH + tail;
        var inside = new bool[w * h];

        // 本体(テクスチャの上側。Yは上向き)
        for (int y = tail; y < h; y++)
            for (int x = 0; x < w; x++)
                inside[y * w + x] = InRoundRect(x, y - tail, w, bodyH, 3);

        // 考え中の小さな丸(本体との間に1ドットのすき間を空ける)
        StampCircle(inside, w, h, w * 0.5f + 1.5f, 6.4f, 3.3f);
        StampCircle(inside, w, h, w * 0.5f - 5.5f, 2.3f, 2.2f);

        // 縁(内側で、上下左右のどれかが外の画素)
        var edge = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inside[i]) continue;
                bool outL = x == 0 || !inside[i - 1];
                bool outR = x == w - 1 || !inside[i + 1];
                bool outD = y == 0 || !inside[i - w];
                bool outU = y == h - 1 || !inside[i + w];
                edge[i] = outL || outR || outD || outU;
            }
        }

        var px = new Color32[w * h];
        Color32 cOutline = outlineColor, cFill = fillColor, cShade = shadeColor;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inside[i]) continue;
                if (edge[i]) { px[i] = cOutline; continue; }

                // 下か右が縁なら薄い影を入れる
                bool shade = (y > 0 && edge[i - w]) || (x < w - 1 && edge[i + 1]);
                px[i] = shade ? cShade : cFill;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "ThoughtBubble_" + w + "x" + bodyH
        };
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    static bool InRoundRect(int x, int y, int w, int h, int r)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        int cx = x < r ? r : (x >= w - r ? w - 1 - r : x);
        int cy = y < r ? r : (y >= h - r ? h - 1 - r : y);
        float dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= r * r + 1f;
    }

    static void StampCircle(bool[] mask, int w, int h, float cx, float cy, float r)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) mask[y * w + x] = true;
            }
        }
    }
}
