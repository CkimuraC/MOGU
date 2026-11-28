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
            DrawBases();
            GUILayout.Space(UiText.Px(10));
            DrawRepair();
        DrawDigEffect();
            GUILayout.Space(UiText.Px(10));
            DrawCamera();
            GUILayout.Space(UiText.Px(10));
            DrawTree();
            GUILayout.Space(UiText.Px(10));
            DrawBody();
            GUILayout.Space(UiText.Px(10));
            DrawDust();
            GUILayout.Space(UiText.Px(10));
            DrawQuest();
            GUILayout.Space(UiText.Px(10));
            DrawResetSave();
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

        // ---- 疲れ(掘れる量・ゆっくり掘る・フリーズ) ----
        GUILayout.Label("疲れ: " + (mole.Exhausted ? "掘れない" : "掘れる") + "   掘る速さ ×" + mole.TiredDigMul.ToString("0.00") +
                        "   フリーズ: " + (mole.Frozen ? "あと" + mole.FreezeRemaining.ToString("0.0") + "秒" : "なし") +
                        "   失敗 " + mole.DigAttempts + "/" + mole.freezeAfterAttempts, GUILayout.Height(RowH));
        mole.exhaustRecoverThreshold = Slider("掘れるようになる量(スタミナ)", mole.exhaustRecoverThreshold, 0f, 40f, "0");
        mole.tiredFullSpeedStamina = Slider("ふつうの速さで掘れる量", mole.tiredFullSpeedStamina, 0f, 80f, "0");
        mole.tiredMinDigSpeedMul = Slider("いちばん疲れたときの掘る速さ(倍)", mole.tiredMinDigSpeedMul, 0.05f, 1f);
        mole.freezeAfterAttempts = Mathf.RoundToInt(Slider("フリーズまでの失敗回数", mole.freezeAfterAttempts, 1f, 10f, "0"));
        mole.freezeSeconds = Slider("フリーズの長さ(秒)", mole.freezeSeconds, 0f, 10f, "0.0");
        if (GUILayout.Button("いますぐフリーズさせる(確認用)", GUILayout.Height(RowH))) mole.DebugFreezeNow();
    }

// ---------------- 拠点 ----------------
    void DrawBases()
    {
        GUILayout.Label("―― 拠点 ――", GUILayout.Height(RowH));
        var bm = BaseManager.Instance;
        if (bm == null)
        {
            GUILayout.Label("(BaseManager が見つかりません)", GUILayout.Height(RowH));
            return;
        }

        GUILayout.Label("拠点 " + bm.Count + " / " + bm.maxBases + "   セーブデータ: " + (bm.HasSaveFile ? "あり" : "なし"), GUILayout.Height(RowH));

        bm.maxBases = Mathf.RoundToInt(Slider("最大拠点数", bm.maxBases, 1f, 5f, "0"));
        bm.minDepthFromSurface = Slider("作れない深さ(地表から)", bm.minDepthFromSurface, 0f, 20f, "0.0");
        bm.minDistanceBetweenBases = Slider("拠点どうしの最小の距離", bm.minDistanceBetweenBases, 0f, 30f, "0.0");
        bm.minDistanceFromInitial = Slider("初期位置からの最小の距離", bm.minDistanceFromInitial, 0f, 30f, "0.0");
        bm.interactRadius = Slider("拠点の中とみなす距離", bm.interactRadius, 0.5f, 6f, "0.0");
        bm.restResetsDepletion = Toggle("休息でスタミナ切れ回数もリセット", bm.restResetsDepletion);

        if (GUILayout.Button("ここに拠点を作る(ルール無視)", GUILayout.Height(RowH))) bm.DebugBuildHere();
        if (GUILayout.Button("拠点をすべて削除(空洞も埋める)", GUILayout.Height(RowH))) bm.DeleteAll();
        if (GUILayout.Button("拠点のセーブデータを削除", GUILayout.Height(RowH))) bm.DeleteSaveFile();
        if (GUILayout.Button("初期位置へ移動", GUILayout.Height(RowH))) bm.ReturnToInitial();
    }

// ---------------- 穴の修復 ----------------
    void DrawRepair()
    {
        GUILayout.Label("―― 穴の修復(砂で埋まる) ――", GUILayout.Height(RowH));
        var tr = TerrainRepair.Instance;
        if (tr == null)
        {
            GUILayout.Label("(TerrainRepair が見つかりません)", GUILayout.Height(RowH));
            return;
        }

        tr.repairEnabled = Toggle("掘った穴を自動で修復する", tr.repairEnabled);
        tr.repairDelaySeconds = Slider("埋まり始めるまで(秒)", tr.repairDelaySeconds, 0f, 300f, "0");
        tr.fillDurationSeconds = Slider("完全に埋まるまでの目安(秒)", tr.fillDurationSeconds, 1f, 300f, "0");
        string styleName = tr.fillStyle == TerrainRepair.FillStyle.FloorAndCeiling ? "上下から" : (tr.fillStyle == TerrainRepair.FillStyle.Floor ? "下から" : (tr.fillStyle == TerrainRepair.FillStyle.Pile ? "積もる(天井からこぼれて山に)" : "壁ぎわから"));
        if (GUILayout.Button("埋まり方: " + styleName + "  (押して切り替え)", GUILayout.Height(RowH)))
            tr.fillStyle = (TerrainRepair.FillStyle)(((int)tr.fillStyle + 1) % 4);
        tr.grainPx = Mathf.RoundToInt(Slider("砂の粒の大きさ(画素)", tr.grainPx, 1f, 8f, "0"));
        tr.sandTint = Slider("砂の色の濃さ", tr.sandTint, 0f, 1f);
        tr.protectRadiusAroundMole = Slider("モグラの周りは埋めない距離", tr.protectRadiusAroundMole, 0f, 8f, "0.0");
        tr.settleEnabled = Toggle("埋まった砂の色を、土になじませる", tr.settleEnabled);
        tr.freshSandTint = Slider("埋まった直後の砂の色の濃さ", tr.freshSandTint, 0f, 1f);
        tr.settleSeconds = Slider("なじむまでの時間(秒)", tr.settleSeconds, 1f, 120f, "0");
        tr.showFalling = Toggle("落ちてくる砂を見せる(積もるのとき)", tr.showFalling);
        tr.fallGravity = Slider("砂が落ちる速さ", tr.fallGravity, 4f, 60f, "0");
        var dt = FindFirstObjectByType<DirtTerrain>();
        if (dt != null)
        {
            GUILayout.Label("―― 通路の境目 ――", GUILayout.Height(RowH));
            dt.edgeShading = Toggle("境目の見た目をつける(次に掘るときから)", dt.edgeShading);
            if (GUILayout.Button("見た目: " + (dt.edgeStyle == DirtTerrain.EdgeStyle.Glow ? "縁に沿って明るい輪" : "天井は暗く・床は明るく") + "  (押して切り替え)", GUILayout.Height(RowH)))
                dt.edgeStyle = dt.edgeStyle == DirtTerrain.EdgeStyle.Glow ? DirtTerrain.EdgeStyle.Shade : DirtTerrain.EdgeStyle.Glow;
            if (dt.edgeStyle == DirtTerrain.EdgeStyle.Glow)
            {
                dt.glowWidthPx = Slider("輪の太さ(画素)", dt.glowWidthPx, 0.5f, 6f, "0.0");
                dt.glowUseSoilColor = Toggle("輪の色を、土(地層)の色から作る", dt.glowUseSoilColor);
                if (dt.glowUseSoilColor) dt.glowBrighten = Slider("土の色より明るくする量", dt.glowBrighten, 0f, 0.6f);
                else dt.glowStrength = Slider("輪の濃さ(固定色)", dt.glowStrength, 0f, 1f);
                dt.glowSpotChance = Slider("小石(暗い粒)の割合", dt.glowSpotChance, 0f, 0.3f);
            }
            dt.ceilingShadow = Slider("天井の暗さ", dt.ceilingShadow, 0f, 0.9f);
            dt.floorHighlight = Slider("床の明るさ", dt.floorHighlight, 0f, 0.8f);
            dt.wallShadow = Slider("横の壁の暗さ", dt.wallShadow, 0f, 0.8f);
            dt.edgeRoughness = Slider("縁のギザギザ(掘り残しの確率)", dt.edgeRoughness, 0f, 0.8f);
        }
        GUILayout.Label("修復待ち・修復中のブロック: " + tr.DugChunkCount, GUILayout.Height(RowH));

        if (GUILayout.Button("テスト用に早くする(開始3秒・完了10秒)", GUILayout.Height(RowH)))
        {
            tr.repairDelaySeconds = 3f;
            tr.fillDurationSeconds = 10f;
        }
        if (GUILayout.Button("修復だけ10秒進める", GUILayout.Height(RowH))) tr.FastForward(10f);
        if (GUILayout.Button("すべての穴を今すぐ埋める", GUILayout.Height(RowH))) tr.FillAllNow();
    }

void DrawDigEffect()
    {
        var de = FindFirstObjectByType<DigEffect>();
        if (de == null) return;
        GUILayout.Label("■ 掘ったときのエフェクト");
        de.effectEnabled = Toggle("土のかけらを出す", de.effectEnabled);
        if (!de.effectEnabled) return;
        de.particlesPerPixel = Slider("量", de.particlesPerPixel, 0f, 0.3f, "0.00");
        de.maxPerEvent = Mathf.RoundToInt(Slider("1回の上限", de.maxPerEvent, 1, 12, "0"));
        de.speedMax = Slider("勢い", de.speedMax, 1f, 6f, "0.0");
        de.gravity = Slider("落ちる強さ", de.gravity, 2f, 25f, "0.0");
        de.lifeMax = Slider("消えるまで(秒)", de.lifeMax, 0.2f, 1.2f, "0.00");
        de.brighten = Slider("明るさ", de.brighten, 0f, 0.4f, "0.00");
    }


// ---------------- カメラ(引いて見る) ----------------
    CameraFollow camFollow;

    void DrawCamera()
    {
        GUILayout.Label("―― カメラ(引いて見る) ――", GUILayout.Height(RowH));
        if (camFollow == null) camFollow = FindFirstObjectByType<CameraFollow>();
        if (camFollow == null)
        {
            GUILayout.Label("(CameraFollow が見つかりません)", GUILayout.Height(RowH));
            return;
        }

        camFollow.orthoSize = Slider("ふだんの見える範囲(縦の半分)", camFollow.orthoSize, 3f, 14f, "0.0");
        camFollow.zoomOutFactor = Slider("引いたときの倍率", camFollow.zoomOutFactor, 1f, 3.5f);
        camFollow.zoomSmoothTime = Slider("切り替えのなめらかさ(秒)", camFollow.zoomSmoothTime, 0.02f, 1.5f);
        camFollow.zoomLocked = Toggle("引いたままにする(キー不要)", camFollow.zoomLocked);
        GUILayout.Label("Zキー: 押している間だけ引く", GUILayout.Height(RowH));
        GUILayout.Label("R3(パッド): 押すたびに「引く / 戻す」を切り替え  (いま: " + (camFollow.ZoomToggled ? "引いている" : "ふだん") + ")", GUILayout.Height(RowH));
    }

// ---------------- クエスト ----------------
    void DrawQuest()
    {
        GUILayout.Label("―― クエスト ――", GUILayout.Height(RowH));
        var q = QuestManager.Instance;
        if (q == null) { GUILayout.Label("(QuestManager が見つかりません)", GUILayout.Height(RowH)); return; }

        string st = q.AllDone ? "すべて達成済み" : (q.CurrentTitle + "  " + (!q.Accepted ? "(未受注)" : (q.CanReport ? "(達成! 報告できる)" : "(挑戦中)")));
        GUILayout.Label("達成済み: " + q.CompletedCount + " / " + q.quests.Count + "   " + st, GUILayout.Height(RowH));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("必要なかけらをもらう", GUILayout.Height(RowH))) q.DebugGiveNeeded();
        if (GUILayout.Button("このクエストを飛ばす", GUILayout.Height(RowH))) q.DebugSkip();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("クエストの進行を最初からやり直す", GUILayout.Height(RowH))) q.DebugResetProgress();
        GUILayout.Label("進行は保存されます(PlayerPrefs)。大木で B / × → 受ける・報告", GUILayout.Height(RowH));
    }

// ---------------- セーブデータのリセット ----------------
    bool resetConfirm;
    bool restartConfirm;
void DrawResetSave()
    {
        GUILayout.Label("―― リセット ――", GUILayout.Height(RowH));

        GUILayout.Label("セーブデータだけ: 拠点・クエスト進行・かけら・塵ゲージを最初に戻す", GUILayout.Height(RowH));
        if (!resetConfirm)
        {
            if (GUILayout.Button("セーブデータをリセットする...", GUILayout.Height(RowH))) { resetConfirm = true; restartConfirm = false; }
        }
        else
        {
            GUILayout.Label("本当にリセットしますか? 元には戻せません", GUILayout.Height(RowH));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("リセットする", GUILayout.Height(RowH))) { resetConfirm = false; ResetAllSaveData(); }
            if (GUILayout.Button("やめる", GUILayout.Height(RowH))) resetConfirm = false;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(UiText.Px(6));
        GUILayout.Label("すべて: セーブデータ＋掘った穴・アイテム・スタミナ・位置も最初の状態にして、ゲームをやり直す", GUILayout.Height(RowH));
        if (!restartConfirm)
        {
            if (GUILayout.Button("すべてリセットして最初から始める...", GUILayout.Height(RowH))) { restartConfirm = true; resetConfirm = false; }
        }
        else
        {
            GUILayout.Label("本当にすべてリセットしますか? 元には戻せません", GUILayout.Height(RowH));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("すべてリセットする", GUILayout.Height(RowH))) { restartConfirm = false; RestartFromScratch(); }
            if (GUILayout.Button("やめる", GUILayout.Height(RowH))) restartConfirm = false;
            GUILayout.EndHorizontal();
        }
    }

    public static void ResetAllSaveData()
    {
        var bm = FindFirstObjectByType<BaseManager>();
        if (bm != null) { bm.DeleteAll(); bm.DeleteSaveFile(); bm.ReturnToInitial(); }
        else { try { if (System.IO.File.Exists(BaseManager.SavePath)) System.IO.File.Delete(BaseManager.SavePath); } catch { } }

        var q = QuestManager.Instance;
        if (q != null) q.DebugResetProgress();

        var inv = FindFirstObjectByType<ShardInventory>();
        if (inv != null)
            for (int i = 0; i < inv.carried.Length; i++) { inv.carried[i] = 0; if (i < inv.delivered.Length) inv.delivered[i] = 0; }

        var dust = FindFirstObjectByType<EmotionDust>();
        if (dust != null) dust.ClearAll();

        Debug.Log("[DebugPanel] セーブデータをリセットしました");
    }

// セーブデータを消して、シーンを読み直す(地形・アイテム・スタミナ・位置も初期化される)
    public static void RestartFromScratch()
    {
        ResetAllSaveData();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }



    // ---------------- 感情の塵 ----------------
    EmotionDust dustComp;
void DrawDust()
    {
        GUILayout.Label("―― 感情の塵(感情ごとのゲージ) ――", GUILayout.Height(RowH));
        if (dustComp == null) dustComp = FindFirstObjectByType<EmotionDust>();
        if (dustComp == null) { GUILayout.Label("(EmotionDust が見つかりません)", GUILayout.Height(RowH)); return; }

        dustComp.absorbAmount = Mathf.Round(Slider("塵１つで増えるゲージ量(満タン=" + Mathf.RoundToInt(dustComp.maxGauge) + ")", dustComp.absorbAmount, 1f, 50f, "0"));
        dustComp.readySeconds = Mathf.Round(Slider("発動チャンスの長さ(秒)", dustComp.readySeconds, 2f, 60f, "0"));
        dustComp.decayPerSecond = Slider("チャンス終了後、ゲージが減る速さ(毎秒)", dustComp.decayPerSecond, 0.5f, 30f, "0.0");

        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            var t = (EmotionType)i;
            var s = dustComp.StateOf(t);
            string st = s == EmotionDust.State.Ready ? "チャンス中(あと " + Mathf.CeilToInt(dustComp.ReadyRemaining(t)) + " 秒)" : (s == EmotionDust.State.Fading ? "減少中" : "蓄積中");
            GUILayout.BeginHorizontal();
            GUILayout.Label(EmotionInfo.NameJp(t) + "  " + Mathf.RoundToInt(dustComp.Gauge(t)) + "  " + st, GUILayout.Height(RowH));
            if (GUILayout.Button("塵+1", GUILayout.Height(RowH))) dustComp.Absorb(t);
            if (GUILayout.Button("満タン", GUILayout.Height(RowH))) dustComp.FillNow(t);
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("ゲージをすべて空にする", GUILayout.Height(RowH))) dustComp.ClearAll();
        GUILayout.Label("満タンのあと E / ○ で感情を選んで発動", GUILayout.Height(RowH));

        // 塵の常時生成(上限つき)
        var sp = FindFirstObjectByType<ItemSpawner>();
        if (sp != null)
        {
            sp.dustRespawnEnabled = Toggle("塵を常にランダム生成する", sp.dustRespawnEnabled);
            sp.dustCount = Mathf.RoundToInt(Slider("塵の上限数", sp.dustCount, 10f, 200f, "0"));
            sp.dustRespawnInterval = Slider("生成間隔(秒)", sp.dustRespawnInterval, 0.5f, 20f, "0.0");
            sp.dustRespawnMinDistance = Slider("モグラからの最低距離", sp.dustRespawnMinDistance, 3f, 20f, "0.0");
            GUILayout.Label("―― 回復アイテム(●★)の常時生成 ――", GUILayout.Height(RowH));
            sp.itemRespawnEnabled = Toggle("●★を常にランダム生成する", sp.itemRespawnEnabled);
            sp.healCount = Mathf.RoundToInt(Slider("●(スタミナ回復)の上限数", sp.healCount, 0f, 60f, "0"));
            sp.starCount = Mathf.RoundToInt(Slider("★(最大スタミナ増加)の上限数", sp.starCount, 0f, 30f, "0"));
            sp.itemRespawnInterval = Slider("●★の生成間隔(秒)", sp.itemRespawnInterval, 0.5f, 30f, "0.0");
        }
    }

    // ---------------- 大木 ----------------
    void DrawTree()
    {
        GUILayout.Label("―― 大木(クエスト) ――", GUILayout.Height(RowH));
        var t = GiantTree.Instance;
        if (t == null) { GUILayout.Label("(GiantTree が見つかりません)", GUILayout.Height(RowH)); return; }

        t.visible = Toggle("大木を表示する", t.visible);
        t.scale = Slider("大きさ(倍率)", t.scale, 0.3f, 10f, "0.00");
        t.offsetX = Slider("左右の位置(中央から)", t.offsetX, -24f, 24f, "0.0");
        t.offsetY = Slider("上下の位置(地面から)", t.offsetY, -3f, 3f, "0.00");
        t.interactRadius = Slider("話しかけられる距離", t.interactRadius, 1f, 10f, "0.0");
        t.flipX = Toggle("左右を反転", t.flipX);
        t.seed = Mathf.RoundToInt(Slider("葉の形(乱数の種)", t.seed, 0f, 9999f, "0"));
        GUILayout.Label("いまの大きさ: 幅 " + t.WidthUnits.ToString("0.0") + " × 高さ " + t.HeightUnits.ToString("0.0") + " ユニット", GUILayout.Height(RowH));
        GUILayout.Label("地上で大木の近くにいて B / × を押すと、話しかけられます", GUILayout.Height(RowH));
    }

// ---------------- モグラの体(曲がる表現の試作) ----------------
    void DrawBody()
    {
        GUILayout.Label("―― 通路の幅・モグラの大きさ ――", GUILayout.Height(RowH));
        if (mole != null)
        {
            mole.tunnelWidthPx = Mathf.Round(Slider("通路の幅(画素)", mole.tunnelWidthPx, 8f, 40f, "0"));
            mole.visualScale = Slider("モグラの絵の大きさ(倍)", mole.visualScale, 0.4f, 1.5f, "0.00");
            GUILayout.Label("通路の幅は、絵の大きさとは別です。細くすると、同じ距離でも、スタミナの消費が減ります", GUILayout.Height(RowH));
        }
        GUILayout.Space(UiText.Px(6));
        GUILayout.Label("―― モグラの体(曲がる表現) ――", GUILayout.Height(RowH));
        var b = SegmentedMoleVisual.Instance;
        if (b == null) { GUILayout.Label("(SegmentedMoleVisual が見つかりません)", GUILayout.Height(RowH)); return; }

        b.bendEnabled = Toggle("体を曲げる(オフ=元の1枚絵)", b.bendEnabled);
        b.segmentCount = Mathf.RoundToInt(Slider("体を切る数", b.segmentCount, 2f, 8f, "0"));
        b.overlapPx = Mathf.RoundToInt(Slider("切れ目の重なり(画素)", b.overlapPx, 0f, 3f, "0"));
        b.turnResponse = Slider("頭が向きを変える速さ", b.turnResponse, 2f, 40f, "0");
        b.tangentSpan = Slider("曲がり方のゆるやかさ", b.tangentSpan, 0.05f, 0.8f, "0.00");
    }







// ---------------- 表示 ----------------
void DrawDisplay()
    {
        GUILayout.Label("―― 表示 ――", GUILayout.Height(RowH));
        UiText.UserScale = Slider("文字の大きさ (倍率)", UiText.UserScale, 0.6f, 3f);
        if (GUILayout.Button("文字の大きさをリセット", GUILayout.Height(RowH))) UiText.UserScale = 1f;
    }


    // ---------------- 部品 ----------------
static float Slider(string label, float value, float min, float max, string format = "0.00")
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

        GUILayout.Label(value.ToString(format), GUILayout.Width(UiText.Px(70)), GUILayout.Height(RowH));
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
