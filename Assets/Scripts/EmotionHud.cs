using UnityEngine;

/// <summary>
/// 感情のかけらの表示(OnGUIによる簡易表示)。画面右上。
/// 持っているかけら / 納めた数 / 効果中の感情 / 通知メッセージ
/// </summary>
public class EmotionHud : MonoBehaviour
{
    ShardInventory inv;
    EmotionDust dust;
    EmotionEffects fx;
    MoleController mole;
    GUIStyle toastStyle;

    void Awake()
    {
        inv = FindFirstObjectByType<ShardInventory>();
        dust = FindFirstObjectByType<EmotionDust>();
        fx = FindFirstObjectByType<EmotionEffects>();
        mole = FindFirstObjectByType<MoleController>();
    }

void OnGUI()
    {
        if (inv == null) return;

        float lh = UiText.Line(18);
        float w = UiText.Px(420);
        float x = Screen.width - w - UiText.Px(20);
        float y = UiText.Px(16);
        var gray = new Color(0.75f, 0.75f, 0.75f);
        int fxCount = fx != null ? fx.ActiveCount : 0;

        // 背景が明るいとき(大木など)でも読めるように、うすい黒い板を敷く
        {
            var pc = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            float rows = 6.5f + (dust != null ? 7.2f : 0f) + (fxCount > 0 ? 1.3f + fxCount : 0f);
            GUI.DrawTexture(new Rect(x - UiText.Px(10), y - UiText.Px(6), w + UiText.Px(20), lh * rows + UiText.Px(8)), Texture2D.whiteTexture);
            GUI.color = pc;
        }

        // ---- 感情のかけら(個数) ----
        UiText.Label(new Rect(x, y, w, lh), "感情のかけら(大木に届ける)", 18, Color.white);
        y += lh;
        UiText.Label(new Rect(x, y, w, lh), "持っている: " + inv.CarriedTotal + " 個   届けた: " + inv.DeliveredTotal + " 個", 15, gray);
        y += lh;
        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            var t = (EmotionType)i;
            Color c = inv.carried[i] > 0 ? EmotionInfo.ColorOf(t) : new Color(0.62f, 0.62f, 0.62f, 0.8f);
            UiText.Label(new Rect(x, y, w, lh), "　 " + EmotionInfo.NameJp(t) + "  ×" + inv.carried[i], 18, c);
            y += lh;
        }
        y += lh * 0.5f;

        // ---- 感情の塵(感情ごとのゲージ) ----
        if (dust != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);

            UiText.Label(new Rect(x, y, w, lh), "感情の塵(体の中の感情エネルギー)", 18, Color.white);
            y += lh;
            UiText.Label(new Rect(x, y, w, lh),
                dust.AnyReady ? "ゲージが満タン!  E / ○ で感情を選んで発動" : "塵を吸収して、ゲージを満タンにすると発動できます",
                15, dust.AnyReady ? Color.Lerp(new Color(1f, 0.9f, 0.4f), Color.white, pulse) : gray);
            y += lh;

            float labelW = UiText.Px(40);
            float barX = x + UiText.Px(6) + labelW;
            float timeW = UiText.Px(70);
            float barW = w - UiText.Px(12) - labelW - timeW;
            float barH = UiText.Px(14);
            for (int i = 0; i < EmotionInfo.Count; i++)
            {
                var t = (EmotionType)i;
                var st = dust.StateOf(t);
                bool ready = st == EmotionDust.State.Ready;
                bool fading = st == EmotionDust.State.Fading;
                Color c = EmotionInfo.ColorOf(t);
                float by = y + (lh - barH) * 0.5f;

                UiText.Label(new Rect(x + UiText.Px(6), y, labelW, lh), EmotionInfo.NameJp(t), 18, c);

                var prev = GUI.color;
                if (ready)
                {
                    float g = UiText.Px(3) + pulse * UiText.Px(3);
                    GUI.color = new Color(c.r, c.g, c.b, 0.25f + 0.4f * pulse);
                    GUI.DrawTexture(new Rect(barX - g, by - g, barW + g * 2f, barH + g * 2f), Texture2D.whiteTexture);
                }
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(new Rect(barX - 2, by - 2, barW + 4, barH + 4), Texture2D.whiteTexture);
                GUI.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
                GUI.DrawTexture(new Rect(barX, by, barW, barH), Texture2D.whiteTexture);
                Color fill = ready ? Color.Lerp(c, Color.white, pulse * 0.6f) : (fading ? new Color(c.r, c.g, c.b, 0.45f) : new Color(c.r, c.g, c.b, 0.8f));
                GUI.color = fill;
                GUI.DrawTexture(new Rect(barX, by, barW * dust.Gauge01(t), barH), Texture2D.whiteTexture);
                GUI.color = prev;

                if (ready)
                    UiText.Label(new Rect(barX + barW + UiText.Px(6), y, timeW, lh), "あと" + Mathf.CeilToInt(dust.ReadyRemaining(t)) + "秒", 15, Color.Lerp(c, Color.white, pulse));
                else if (fading)
                    UiText.Label(new Rect(barX + barW + UiText.Px(6), y, timeW, lh), "減少中", 14, gray);
                y += lh * 1.15f;
            }
        }

        // ---- 効いている能力 ----
        if (fxCount > 0)
        {
            y += lh * 0.3f;
            UiText.Label(new Rect(x, y, w, lh), "効いている能力", 18, Color.white);
            y += lh;
            for (int i = 0; i < fxCount; i++)
            {
                var t = fx.ActiveType(i);
                UiText.Label(new Rect(x, y, w, lh),
                    "　 " + EmotionInfo.NameJp(t) + "   あと" + Mathf.CeilToInt(fx.ActiveRemaining(i)) + "秒",
                    18, EmotionInfo.ColorOf(t));
                y += lh;
            }
        }

        // 画面の上のほうに出す通知
        float big = UiText.Line(28);
        float cy = UiText.Px(90);
        if (Time.time < inv.ToastUntil && !string.IsNullOrEmpty(inv.ToastText))
        {
            UiText.Label(new Rect(0, cy, Screen.width, big), inv.ToastText, 28, new Color(1f, 1f, 1f, 0.97f), TextAnchor.UpperCenter);
            cy += big;
        }
        if (dust != null && Time.time < dust.ToastUntil && !string.IsNullOrEmpty(dust.ToastText))
        {
            UiText.Label(new Rect(0, cy, Screen.width, big), dust.ToastText, 24, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperCenter);
            cy += big;
        }
        if (mole != null && Time.time < mole.SlowDigUntil && Time.time >= mole.HardBlockedUntil)
        {
            UiText.Label(new Rect(0, cy, Screen.width, big), "硬い層を、ゆっくり掘っています…(「怒」を発動すると、普通に掘れます)",
                24, new Color(1f, 0.85f, 0.6f, 0.97f), TextAnchor.UpperCenter);
            cy += big;
        }
        if (mole != null && Time.time < mole.HardBlockedUntil)
        {
            UiText.Label(new Rect(0, cy, Screen.width, big), "ここは硬すぎて掘れません!(「怒」を発動すると掘れるかも)",
                26, new Color(1f, 0.6f, 0.5f, 0.97f), TextAnchor.UpperCenter);
        }
    }
}
