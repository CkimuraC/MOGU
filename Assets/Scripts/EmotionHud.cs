using UnityEngine;

/// <summary>
/// 感情のかけらの表示(OnGUIによる簡易表示)。画面右上。
/// 持っているかけら / 納めた数 / 効果中の感情 / 通知メッセージ
/// </summary>
public class EmotionHud : MonoBehaviour
{
    ShardInventory inv;
    EmotionEffects fx;
    MoleController mole;
    GUIStyle toastStyle;

    void Awake()
    {
        inv = FindFirstObjectByType<ShardInventory>();
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

        UiText.Label(new Rect(x, y, w, lh), "持っているかけら", 18, Color.white);
        y += lh;
        UiText.Label(new Rect(x, y, w, lh),
            inv.autoEatOnPickup ? "触れると自動で食べます" : "食べる: 1〜6 / E    選ぶ: Tab(パッド: L1 / ○)",
            15, gray);
        y += lh;

        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            var t = (EmotionType)i;
            Color c = inv.carried[i] > 0 ? EmotionInfo.ColorOf(t) : new Color(0.62f, 0.62f, 0.62f, 0.8f);
            string mark = i == inv.selected ? "▶ " : "　 ";
            UiText.Label(new Rect(x, y, w, lh),
                mark + (i + 1) + "  " + EmotionInfo.NameJp(t) + "  ×" + inv.carried[i], 18, c);
            y += lh;
        }

        UiText.Label(new Rect(x, y, w, lh), "持ち帰った数: " + inv.DeliveredTotal, 18, Color.white);
        y += lh * 1.3f;

        // 効いている感情の効果
        if (fx != null && fx.ActiveCount > 0)
        {
            UiText.Label(new Rect(x, y, w, lh), "効いている効果", 18, Color.white);
            y += lh;
            for (int i = 0; i < fx.ActiveCount; i++)
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
        if (mole != null && Time.time < mole.HardBlockedUntil)
        {
            UiText.Label(new Rect(0, cy, Screen.width, big), "ここは硬すぎて掘れません!(「怒り」を食べると掘れるかも)",
                26, new Color(1f, 0.6f, 0.5f, 0.97f), TextAnchor.UpperCenter);
        }
    }
}
