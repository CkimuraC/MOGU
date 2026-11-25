using UnityEngine;

/// <summary>スタミナゲージ(OnGUIによる簡易表示)。</summary>
public class StaminaHud : MonoBehaviour
{
    public MoleController mole;

    [Header("補助の説明(プレイヤー向け・仮)")]
    [Tooltip("スタミナ切れで回復がだんだん遅くなることを説明する文を、画面の左に出す。不要になったらオフにする")]
    public bool showRecoveryHelp = true;
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
        float ratio = Mathf.Clamp01(mole.Stamina / Mathf.Max(1f, max));
        float barW = UiText.Px(320) * max / Mathf.Max(1f, mole.maxStamina);
        var frame = new Rect(x, y, barW, UiText.Px(28));
        var fill = new Rect(frame.x + 2, frame.y + 2, (frame.width - 4) * ratio, frame.height - 4);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(frame, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.45f, 0.85f, 0.3f), ratio);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
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

        if (mole.BoostBonus > 0.5f)
        {
            UiText.Label(new Rect(x, y, w, lh),
                "★ブースト中: 最大スタミナ +" + Mathf.CeilToInt(mole.BoostBonus) + "  (あと" + Mathf.CeilToInt(mole.BoostTimeRemaining) + "秒)",
                18, new Color(1f, 0.9f, 0.3f));
            y += lh;
        }
        if (mole.Exhausted)
        {
            string tired = "疲れた… 休むと回復します";
            if (mole.DepletionCount > 0) tired += "(回復の速さ ×" + mole.RegenMultiplier.ToString("0.00") + ")";
            UiText.Label(new Rect(x, y, w, lh), tired, 18, new Color(1f, 0.45f, 0.4f));
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

    // 操作の説明(画面の左下)
    void DrawControls()
    {
        float lh = UiText.Line(15);
        float x = UiText.Px(20);
        float w = Screen.width - x * 2f;
        string[] lines =
        {
            "【キーボード】 WASD/矢印=移動   Shift=掘る   Q=スキャン   1〜6/E=かけらを食べる   Tab=選ぶ   B=拠点メニュー   F2=デバッグ",
            "【パッド】 左スティック=移動   R2/R1/□=掘る   L2/△=スキャン   L1=選ぶ   ○=食べる   ×=拠点メニュー",
            "【開発用】 L=暗闇オンオフ   R=スタミナ回復   G=かけらを各1個入手"
        };
        float y = Screen.height - lh * lines.Length - UiText.Px(10);
        for (int i = 0; i < lines.Length; i++)
        {
            UiText.Label(new Rect(x, y, w, lh), lines[i], 15, new Color(1f, 1f, 1f, 0.8f));
            y += lh;
        }
    }
}
