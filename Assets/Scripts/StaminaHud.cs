using UnityEngine;

/// <summary>スタミナゲージ(OnGUIによる簡易表示)。</summary>
public class StaminaHud : MonoBehaviour
{
    public MoleController mole;
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
            UiText.Label(new Rect(x, y, w, lh), "疲れた… 休むと回復します", 18, new Color(1f, 0.45f, 0.4f));
            y += lh;
        }

        if (scan != null)
        {
            Color c = scan.Ready ? new Color(0.5f, 0.95f, 1f) : new Color(0.75f, 0.75f, 0.75f);
            UiText.Label(new Rect(x, y, w, lh),
                scan.Ready ? "スキャン: 使えます" : "スキャン: あと" + scan.CooldownLeft.ToString("0.0") + "秒", 18, c);
            y += lh;
        }

        DrawControls();
    }

    // 操作の説明(画面の左下)
    void DrawControls()
    {
        float lh = UiText.Line(15);
        float x = UiText.Px(20);
        float w = Screen.width - x * 2f;
        string[] lines =
        {
            "【キーボード】 WASD/矢印=移動   Shift=掘る   Q=スキャン   1〜6/E=かけらを食べる   Tab=選ぶ   F2=デバッグ",
            "【パッド】 左スティック=移動   R2/R1/□=掘る   L2/△=スキャン   L1=選ぶ   ○=食べる",
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
