using UnityEngine;

/// <summary>スタミナゲージ(OnGUIによる簡易表示)。</summary>
public class StaminaHud : MonoBehaviour
{
    public MoleController mole;

    [Header("補助の説明(プレイヤー向け・仮)")]
    [Tooltip("スタミナ切れで回復がだんだん遅くなることを説明する文を、画面の左に出す。不要になったらオフにする")]
    public bool showRecoveryHelp = true;

    [Header("ゲージの色(増えた分)")]
    [Tooltip("ごはんで増えた最大スタミナの部分の色")]
    public Color mealGaugeColor = new Color(1f, 0.62f, 0.15f);
    [Tooltip("★ブーストで増えた最大スタミナの部分の色")]
    public Color starGaugeColor = new Color(1f, 0.92f, 0.3f);
    [Tooltip("スタミナが切れた瞬間のお知らせを出す秒数")]
    public float depletedBannerSeconds = 4f;
    ScanPulse scan;
    DirtTerrain terrain;

void Awake()
    {
        if (mole == null) mole = FindFirstObjectByType<MoleController>();
        if (mole != null) scan = mole.GetComponent<ScanPulse>();
        terrain = FindFirstObjectByType<DirtTerrain>();
    }

void OnGUI()
    {
        if (mole == null) return;

        float lh = UiText.Line(18);
        float x = UiText.Px(20);
        float y = UiText.Px(16);
        float w = UiText.Px(900);

        // スタミナのゲージ(★ブースト中は長くなる)
        float max = mole.MaxStaminaCurrent;
        float baseMax = Mathf.Max(1f, mole.maxStamina);
        float wound = mole.WoundAmount;                  // ぶつかって、一時的に減っている分
        float effBase = Mathf.Max(1f, baseMax - wound);  // いまの、ふだんの分の長さ(緑)
        float mealB = mole.MealBonus;
        float itemB = mole.ItemBonus;
        float total = Mathf.Max(1f, effBase + mealB + itemB);
        float barW = UiText.Px(320) * max / baseMax; // 削られた分、ゲージそのものが短くなる
        var frame = new Rect(x, y, barW, UiText.Px(28));
        float inner = frame.width - 4f;
        float st = mole.Stamina;
        float k = inner / total;                                  // 1スタミナあたりの幅
        float x0 = frame.x + 2f, yy = frame.y + 2f, hh = frame.height - 4f;

        // ゲージは、左から ふだんの分 → ごはんで増えた分 → アイテムで増えた分 の順
        float stBase = Mathf.Min(st, effBase);
        float stMeal = Mathf.Clamp(st - effBase, 0f, mealB);
        float stItem = Mathf.Clamp(st - effBase - mealB, 0f, itemB);
        float baseRatio = Mathf.Clamp01(stBase / effBase);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(frame, Texture2D.whiteTexture);

        // 増えた分の枠(からの部分)を、それぞれの色でほんのり色づけ
        float mx = x0 + effBase * k;
        float ix = mx + mealB * k;
        if (mealB > 0.5f) { GUI.color = new Color(mealGaugeColor.r, mealGaugeColor.g, mealGaugeColor.b, 0.25f); GUI.DrawTexture(new Rect(mx, yy, mealB * k, hh), Texture2D.whiteTexture); }
        if (itemB > 0.5f) { GUI.color = new Color(starGaugeColor.r, starGaugeColor.g, starGaugeColor.b, 0.25f); GUI.DrawTexture(new Rect(ix, yy, itemB * k, hh), Texture2D.whiteTexture); }

        // 中身
        GUI.color = Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.45f, 0.85f, 0.3f), baseRatio);
        GUI.DrawTexture(new Rect(x0, yy, stBase * k, hh), Texture2D.whiteTexture);
        if (stMeal > 0.01f) { GUI.color = mealGaugeColor; GUI.DrawTexture(new Rect(mx, yy, stMeal * k, hh), Texture2D.whiteTexture); }
        if (stItem > 0.01f) { GUI.color = starGaugeColor; GUI.DrawTexture(new Rect(ix, yy, stItem * k, hh), Texture2D.whiteTexture); }

        // 区切りの細い線(ふだん|ごはん|アイテム)
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        if (mealB > 0.5f || itemB > 0.5f) GUI.DrawTexture(new Rect(mx - 1f, yy, 2f, hh), Texture2D.whiteTexture);
        if (mealB > 0.5f && itemB > 0.5f) GUI.DrawTexture(new Rect(ix - 1f, yy, 2f, hh), Texture2D.whiteTexture);
        GUI.color = prev;

        y = frame.yMax + UiText.Px(6);
        UiText.Label(new Rect(x, y, w, lh),
            "スタミナ  " + Mathf.CeilToInt(mole.Stamina) + " / " + Mathf.CeilToInt(max), 18, Color.white);
        y += lh;

        // 今いる地層(深いほど硬い)
        if (terrain != null)
        {
            Vector2 mp = mole.transform.position;
            bool onSurface = mp.y > terrain.TopY;
            string layerName = onSurface ? "地上" : terrain.LayerNameAt(mp);
            if (!string.IsNullOrEmpty(layerName))
            {
                string hardText = onSurface ? "" : "  (硬さ ×" + terrain.HardnessAt(mp).ToString("0.0") + ")";
                UiText.Label(new Rect(x, y, w, lh), "いる場所: " + layerName + hardText, 18, Color.white);
                y += lh;
            }
        }

        if (mole.MealBonus > 0.5f)
        {
            UiText.Label(new Rect(x, y, w, lh),
                "◆" + mole.MealLabel + "の効果中: 最大スタミナ +" + Mathf.CeilToInt(mole.MealBonus) + "  (あと" + Mathf.CeilToInt(mole.MealTimeRemaining) + "秒)",
                18, mealGaugeColor);
            y += lh;
        }
        if (mole.ItemBonus > 0.5f)
        {
            UiText.Label(new Rect(x, y, w, lh),
                "★ブースト中: 最大スタミナ +" + Mathf.CeilToInt(mole.ItemBonus) + "  (あと" + Mathf.CeilToInt(mole.ItemTimeRemaining) + "秒)",
                18, starGaugeColor);
            y += lh;
        }
        if (mole.WoundAmount > 0.5f)
        {
            UiText.Label(new Rect(x, y, w, lh),
                "▼ぶつかって、最大スタミナ -" + Mathf.CeilToInt(mole.WoundAmount) + (float.IsInfinity(mole.WoundTimeRemaining) ? "" : "  (あと" + Mathf.CeilToInt(mole.WoundTimeRemaining) + "秒で元に戻る)"),
                18, new Color(1f, 0.45f, 0.4f));
            y += lh;
        }
        if (mole.Exhausted)
        {
            string tired = "疲れた… スタミナが" + Mathf.CeilToInt(mole.exhaustRecoverThreshold) + "まで回復すると、また掘れます";
            if (mole.DepletionCount > 0) tired += "(回復の速さ ×" + mole.RegenMultiplier.ToString("0.00") + ")";
            UiText.Label(new Rect(x, y, w, lh), tired, 18, new Color(1f, 0.45f, 0.4f));
            y += lh;
        }

        // スタミナが少ないあいだは、ゆっくり掘る
        if (!mole.Exhausted && mole.TiredDigMul < 0.99f)
        {
            UiText.Label(new Rect(x, y, w, lh),
                "スタミナが少ないので、ゆっくり掘ります(掘る速さ ×" + mole.TiredDigMul.ToString("0.00") + ")",
                18, new Color(1f, 0.8f, 0.5f));
            y += lh;
        }

        // スタミナが切れた回数と、回復の速さ
        if (mole.DepletionCount > 0)
        {
            string line;
            Color c;
            if (mole.Recovering)
            {
                line = "スタミナ切れ: " + mole.DepletionCount + "回目   今の回復の速さ ×" + mole.RegenMultiplier.ToString("0.00");
                c = mole.RegenMultiplier < 0.5f ? new Color(1f, 0.55f, 0.35f) : new Color(1f, 0.85f, 0.4f);
            }
            else
            {
                line = "スタミナ切れ: " + mole.DepletionCount + "回   次に切れると回復は ×" + mole.RegenMultiplierFor(mole.DepletionCount + 1).ToString("0.00");
                c = new Color(0.85f, 0.85f, 0.7f);
            }
            UiText.Label(new Rect(x, y, w, lh), line, 18, c);
            y += lh;
        }

        if (scan != null)
        {
            Color c = scan.Ready ? new Color(0.5f, 0.95f, 1f) : new Color(0.75f, 0.75f, 0.75f);
            UiText.Label(new Rect(x, y, w, lh),
                scan.Ready ? "スキャン: 使えます" : "スキャン: あと" + scan.CooldownLeft.ToString("0.0") + "秒", 18, c);
            y += lh;
        }

        // 拠点の数と、拠点の中にいるときの案内
        var bm = BaseManager.Instance;
        if (bm != null)
        {
            bool atBase = bm.NearestBaseWithin(mole.transform.position, bm.interactRadius) != null;
            string bs = "拠点: " + bm.Count + " / " + bm.maxBases +
                        (atBase ? "   拠点の中: Bキーで 休息・セーブ・ワープ" : "   Bキーで拠点メニュー");
            UiText.Label(new Rect(x, y, w, lh), bs, 18, atBase ? new Color(1f, 0.85f, 0.4f) : new Color(0.9f, 0.9f, 0.9f));
            y += lh;
        }

        // 補助の説明(プレイヤー向け・仮)。デバッグパネルを開いている間は、重ならないよう隠す。
        if (showRecoveryHelp && !DebugPanel.IsOpen)
        {
            y += lh * 0.4f;
            float sl = UiText.Line(15);
            var hint = new Color(0.82f, 0.9f, 1f, 0.9f);
            int n = (mole.regenMulByDepletion != null && mole.regenMulByDepletion.Length > 0) ? mole.regenMulByDepletion.Length : 4;
            UiText.Label(new Rect(x, y, w, sl), "【スタミナのヒント】", 15, hint); y += sl;
            UiText.Label(new Rect(x, y, w, sl), "スタミナを使い切ると、満タンに戻るまで回復がゆっくりになります。", 15, hint); y += sl;
            UiText.Label(new Rect(x, y, w, sl), "切れるたびにもっと遅くなり、" + n + "回目からは、ほとんど回復しません。", 15, hint); y += sl;
            if (mole.resetDepletionOnSurface)
            {
                UiText.Label(new Rect(x, y, w, sl), "地上に戻ると、切れた回数はリセットされます。", 15, hint); y += sl;
            }
        }

        DrawControls();
        DrawDepletedBanner();
        DrawFatigueMessage();
    }

    // スタミナが切れた瞬間のお知らせ(画面の上のほう)
    void DrawDepletedBanner()
    {
        if (mole.DepletionCount <= 0) return; // 休息などで回数が0に戻ったら、お知らせは出さない
        if (Time.time - mole.LastDepletedTime >= depletedBannerSeconds) return;

        float m = mole.RegenMultiplierFor(mole.DepletionCount);
        string tail = m >= 0.99f ? "休むと回復します" : (m < 0.1f ? "ほとんど回復しません" : "回復がさらに遅くなります");
        float big = UiText.Line(26);
        UiText.Label(new Rect(0, UiText.Px(215), Screen.width, big),
            "スタミナ切れ!(" + mole.DepletionCount + "回目)  " + tail,
            26, new Color(1f, 0.7f, 0.45f, 0.97f), TextAnchor.UpperCenter);
    }

// 疲れて掘れないとき・フリーズ中の説明(画面の上のほう)
    void DrawFatigueMessage()
    {
        float big = UiText.Line(24);
        float y = UiText.Px(300);

        if (mole.Frozen)
        {
            UiText.Label(new Rect(0, y, Screen.width, big),
                "疲れて動けません…(あと" + mole.FreezeRemaining.ToString("0.0") + "秒)",
                24, new Color(1f, 0.5f, 0.45f, 0.97f), TextAnchor.UpperCenter);
            return;
        }

        if (Time.time - mole.LastFailedDigTime < 2f)
        {
            int left = Mathf.Max(0, mole.freezeAfterAttempts - mole.DigAttempts);
            UiText.Label(new Rect(0, y, Screen.width, big),
                "疲れていて、掘れません…あと" + left + "回むりに掘ると、動けなくなります",
                22, new Color(1f, 0.75f, 0.5f, 0.97f), TextAnchor.UpperCenter);
        }
    }


    // 操作の説明(画面の左下)
    void DrawControls()
    {
        float lh = UiText.Line(15);
        float x = UiText.Px(20);
        float w = Screen.width - x * 2f;
        string[] lines =
        {
            "【キーボード】 WASD/矢印=移動   Shift=掘る   Q=スキャン   E=感情を発動(塵満タン時)   B=拠点メニュー   Z=引いて見る   F2=デバッグ",
            "【パッド】 左スティック=移動   R2/R1/□=掘る   L2/△=スキャン   ○=感情を発動(塵満タン時)   ×=拠点メニュー   R3=引く/戻す",
            "【開発用】 L=暗闇オンオフ   R=スタミナ回復   G=かけらを各1個入手   H=塵を1つ吸収"
        };
        float y = Screen.height - lh * lines.Length - UiText.Px(10);
        for (int i = 0; i < lines.Length; i++)
        {
            UiText.Label(new Rect(x, y, w, lh), lines[i], 15, new Color(1f, 1f, 1f, 0.8f));
            y += lh;
        }
    }
}
