using UnityEngine;

/// <summary>
/// 開発用デバッグパネル。F2 で開閉する(ゲーム画面の左側に出る)。
/// ・マウスでスライダーやチェックを操作して、再生中に値を調整できる
/// ・エディターと開発用ビルドでだけ動く(製品版では何もしない)
///
/// ■ 項目を足すとき
///   DrawXxx() というメソッドを作って、OnGUI の「ここに足す」の場所で呼ぶだけ。
///   スライダーは Slider("名前", 値, 最小, 最大) が使える。
/// ■ 調整した値を残したいとき
///   再生中に変えた値は、再生を止めると元に戻る。
///   パネルに出ている数字を見て、シーンの該当コンポーネントのインスペクターに入れる。
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Tooltip("ゲーム開始時からパネルを開いておく")]
    public bool startOpen = false;

    bool open;
    DarknessOverlay darkness;
    MoleController mole;
    Vector2 scroll;

    /// <summary>パネルが開いているか(画面の説明文が重ならないよう、他の表示が参照する)</summary>
    public static bool IsOpen { get; private set; }

    // 「元に戻す」用に、開始時の値を覚えておく
    bool darknessDefaultsSaved;
    bool defEnabled;
    float defAmbient, defInner, defOuter, defSurface;

    void Start()
    {
        open = startOpen;
        darkness = FindFirstObjectByType<DarknessOverlay>();
        mole = FindFirstObjectByType<MoleController>();
        SaveDarknessDefaults();
    }

void Update()
    {
        if (MoleInput.DebugPanelTogglePressed()) open = !open;
        IsOpen = open;
    }

    void SaveDarknessDefaults()
    {
        if (darkness == null) return;
        defEnabled = darkness.darknessEnabled;
        defAmbient = darkness.ambient;
        defInner = darkness.lightInnerRadius;
        defOuter = darkness.lightOuterRadius;
        defSurface = darkness.surfaceLightDepth;
        darknessDefaultsSaved = true;
    }

void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUI.depth = -2000; // 他のGUIより手前に描く

        if (!open)
        {
            float lh0 = UiText.Line(16);
            UiText.Label(new Rect(Screen.width - UiText.Px(280), Screen.height - lh0 - UiText.Px(10), UiText.Px(270), lh0),
                "F2: デバッグパネル", 16, new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperRight);
            return;
        }

        var skin = GUI.skin;
        Font f = UiText.Font;
        int px = UiText.Px(17);

        // パネルの部品(ラベル・チェック・ボタン・スライダー)の文字と大きさを一時的に合わせる。終わったら元に戻す。
        Font oldLabelFont = skin.label.font, oldToggleFont = skin.toggle.font, oldButtonFont = skin.button.font;
        int oldLabelSize = skin.label.fontSize, oldToggleSize = skin.toggle.fontSize, oldButtonSize = skin.button.fontSize;
        TextAnchor oldLabelAnchor = skin.label.alignment, oldToggleAnchor = skin.toggle.alignment;
        float oldThumbW = skin.horizontalSliderThumb.fixedWidth, oldThumbH = skin.horizontalSliderThumb.fixedHeight;
        try
        {
            skin.label.font = f; skin.label.fontSize = px; skin.label.alignment = TextAnchor.MiddleLeft;
            skin.toggle.font = f; skin.toggle.fontSize = px; skin.toggle.alignment = TextAnchor.MiddleLeft;
            skin.button.font = f; skin.button.fontSize = px;
            skin.horizontalSliderThumb.fixedWidth = px * 0.8f;
            skin.horizontalSliderThumb.fixedHeight = px * 0.95f;

            // 背景(自前で描く)。高さは、画面の下の操作説明に重ならない範囲まで
            float top = UiText.Px(240);
            float height = Mathf.Min(UiText.Px(1000), Screen.height - top - UiText.Px(120));
            var area = new Rect(UiText.Px(20), top, UiText.Px(660), height);
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = prev;

            var inner = new Rect(area.x + UiText.Px(12), area.y + UiText.Px(8), area.width - UiText.Px(24), area.height - UiText.Px(16));
            GUILayout.BeginArea(inner);
            GUILayout.Label("デバッグパネル  (F2で閉じる)", GUILayout.Height(RowH));
            GUILayout.Space(UiText.Px(4));

            // 項目が多くて収まらないときは、スクロールできる
            scroll = GUILayout.BeginScrollView(scroll);
            DrawDarkness();
            GUILayout.Space(UiText.Px(10));
            DrawStamina();
            GUILayout.Space(UiText.Px(10));
            DrawDisplay();
            // ★ここに足す: DrawTeleport(); DrawShards(); など
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
        finally
        {
            skin.label.font = oldLabelFont; skin.label.fontSize = oldLabelSize; skin.label.alignment = oldLabelAnchor;
            skin.toggle.font = oldToggleFont; skin.toggle.fontSize = oldToggleSize; skin.toggle.alignment = oldToggleAnchor;
            skin.button.font = oldButtonFont; skin.button.fontSize = oldButtonSize;
            skin.horizontalSliderThumb.fixedWidth = oldThumbW;
            skin.horizontalSliderThumb.fixedHeight = oldThumbH;
        }
#endif
    }

    // 1行の高さ(日本語フォントの行の高さがレイアウト計算に反映されにくいので、はっきり指定する)
    static float RowH { get { return UiText.Px(17) * 1.7f; } }

    // ---------------- 暗闇 ----------------
void DrawDarkness()
    {
        GUILayout.Label("―― 暗闇 ――", GUILayout.Height(RowH));
        if (darkness == null)
        {
            GUILayout.Label("(DarknessOverlay が見つかりません)", GUILayout.Height(RowH));
            return;
        }

        darkness.darknessEnabled = Toggle("暗闇をオンにする (Lキー)", darkness.darknessEnabled);

        // 透過度:0=真っ暗、1=完全に透明(暗闇なし)
        darkness.ambient = Slider("透過度 (0=真っ暗)", darkness.ambient, 0f, 1f);

        // モグラから明るさが届く範囲(ユニット)
        darkness.lightOuterRadius = Slider("明るさの届く範囲(外側)", darkness.lightOuterRadius, 0.5f, 14f);
        darkness.lightInnerRadius = Slider("完全に明るい範囲(内側)", darkness.lightInnerRadius, 0f, 12f);
        if (darkness.lightInnerRadius > darkness.lightOuterRadius)
            darkness.lightInnerRadius = darkness.lightOuterRadius;

        // 地表から差し込む光の深さ(0で無効)
        darkness.surfaceLightDepth = Slider("地表の光の深さ (0=なし)", darkness.surfaceLightDepth, 0f, 10f);

        if (GUILayout.Button("暗闇の設定をリセット", GUILayout.Height(RowH)) && darknessDefaultsSaved)
        {
            darkness.darknessEnabled = defEnabled;
            darkness.ambient = defAmbient;
            darkness.lightInnerRadius = defInner;
            darkness.lightOuterRadius = defOuter;
            darkness.surfaceLightDepth = defSurface;
        }

        GUILayout.Label("現在の値: 透過度=" + darkness.ambient.ToString("0.00") +
                        "  内側=" + darkness.lightInnerRadius.ToString("0.0") +
                        "  外側=" + darkness.lightOuterRadius.ToString("0.0") +
                        "  地表=" + darkness.surfaceLightDepth.ToString("0.0"), GUILayout.Height(RowH));
    }

// ---------------- スタミナ ----------------
    void DrawStamina()
    {
        GUILayout.Label("―― スタミナ ――", GUILayout.Height(RowH));
        if (mole == null)
        {
            GUILayout.Label("(モグラが見つかりません)", GUILayout.Height(RowH));
            return;
        }

        GUILayout.Label("スタミナ切れ " + mole.DepletionCount + "回   今の回復の速さ ×" + mole.RegenMultiplier.ToString("0.00") +
                        (mole.Recovering ? "  (回復中)" : ""), GUILayout.Height(RowH));

        // 切れた回数ごとの、回復の速さの倍率(最後の値は、それ以降すべてに使われる)
        var m = mole.regenMulByDepletion;
        if (m != null)
        {
            for (int i = 0; i < m.Length; i++)
            {
                string name = i == m.Length - 1 ? (i + 1) + "回目以降の回復の速さ" : (i + 1) + "回目の回復の速さ";
                m[i] = Slider(name, m[i], 0f, 1f);
            }
        }

        mole.resetDepletionOnSurface = Toggle("地上に戻ると回数をリセット", mole.resetDepletionOnSurface);
        if (GUILayout.Button("切れた回数をリセット", GUILayout.Height(RowH))) mole.ResetDepletion();
        if (GUILayout.Button("スタミナを全回復", GUILayout.Height(RowH))) mole.DebugRefill();
    }


// ---------------- 表示 ----------------
void DrawDisplay()
    {
        GUILayout.Label("―― 表示 ――", GUILayout.Height(RowH));
        UiText.UserScale = Slider("文字の大きさ (倍率)", UiText.UserScale, 0.6f, 3f);
        if (GUILayout.Button("文字の大きさをリセット", GUILayout.Height(RowH))) UiText.UserScale = 1f;
    }


    // ---------------- 部品 ----------------
static float Slider(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(RowH));
        GUILayout.Label(label, GUILayout.Width(UiText.Px(300)), GUILayout.Height(RowH));

        // スライダーの場所を確保して、線・値までの色・つまみを自前で描く
        Rect r = GUILayoutUtility.GetRect(UiText.Px(220), RowH, GUILayout.Width(UiText.Px(220)));
        float t = Mathf.InverseLerp(min, max, value);

        if (Event.current.type == EventType.Repaint)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            float trackH = Mathf.Max(5f, RowH * 0.24f);
            var track = new Rect(r.x, r.y + (r.height - trackH) * 0.5f, r.width, trackH);
            var prev = GUI.color;

            // 線(みぞ)
            GUI.color = new Color(0.40f, 0.40f, 0.46f, 1f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            // 今の値までを色で塗る
            GUI.color = new Color(0.98f, 0.68f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * t, track.height), Texture2D.whiteTexture);

            // つまみ(縁取り付き。マウスを乗せると明るくなる)
            float tw = RowH * 0.46f, th = RowH * 0.86f;
            var thumb = new Rect(r.x + r.width * t - tw * 0.5f, r.y + (r.height - th) * 0.5f, tw, th);
            GUI.color = new Color(0f, 0f, 0f, 1f);
            GUI.DrawTexture(new Rect(thumb.x - 2f, thumb.y - 2f, thumb.width + 4f, thumb.height + 4f), Texture2D.whiteTexture);
            GUI.color = hover ? new Color(1f, 1f, 0.75f, 1f) : Color.white;
            GUI.DrawTexture(thumb, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // 操作の受け付け(見た目は上で描いたものを使うので、スタイルは空にする)
        value = GUI.HorizontalSlider(r, value, min, max, GUIStyle.none, GUIStyle.none);

        GUILayout.Label(value.ToString("0.00"), GUILayout.Width(UiText.Px(70)), GUILayout.Height(RowH));
        GUILayout.EndHorizontal();
        return value;
    }

/// <summary>大きなチェックボックス。オン=オレンジの四角、オフ=枠だけ。行のどこをクリックしても切り替わる。</summary>
    static bool Toggle(string label, bool value)
    {
        Rect r = GUILayoutUtility.GetRect(UiText.Px(500), RowH, GUILayout.Height(RowH));

        if (Event.current.type == EventType.Repaint)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            float s = RowH * 0.62f;
            var box = new Rect(r.x + 2f, r.y + (r.height - s) * 0.5f, s, s);
            var prev = GUI.color;

            GUI.color = hover ? new Color(1f, 1f, 1f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);                       // 枠
            GUI.color = new Color(0.12f, 0.12f, 0.14f, 1f);
            GUI.DrawTexture(new Rect(box.x + 3f, box.y + 3f, box.width - 6f, box.height - 6f), Texture2D.whiteTexture); // 中
            if (value)
            {
                GUI.color = new Color(0.98f, 0.68f, 0.18f, 1f);
                GUI.DrawTexture(new Rect(box.x + 6f, box.y + 6f, box.width - 12f, box.height - 12f), Texture2D.whiteTexture);
            }
            GUI.color = prev;

            UiText.Label(new Rect(box.xMax + UiText.Px(10), r.y, r.width - box.width, r.height),
                label + (value ? "   [オン]" : "   [オフ]"), 17, Color.white, TextAnchor.MiddleLeft);
        }

        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) value = !value;
        return value;
    }

}
