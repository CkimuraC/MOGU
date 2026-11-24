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
        var prev = GUI.color;

        float x = Screen.width - 300f;
        float y = 20f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 290, 22), "Shards (carrying)");
        y += 20f;
        GUI.color = new Color(0.75f, 0.75f, 0.75f);
        GUI.Label(new Rect(x, y, 290, 22), "Eat: 1-6 / E     Select: Tab (Pad: L1 / O)");
        y += 22f;

        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            var t = (EmotionType)i;
            GUI.color = inv.carried[i] > 0 ? EmotionInfo.ColorOf(t) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            string mark = i == inv.selected ? "> " : "   ";
            GUI.Label(new Rect(x, y, 290, 22),
                mark + (i + 1) + " " + EmotionInfo.NameEn(t) + "  x" + inv.carried[i]);
            y += 20f;
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 290, 22), "Delivered: " + inv.DeliveredTotal);
        y += 26f;

        // 効いている感情の効果
        if (fx != null && fx.ActiveCount > 0)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 290, 22), "Active effects");
            y += 20f;
            for (int i = 0; i < fx.ActiveCount; i++)
            {
                var t = fx.ActiveType(i);
                GUI.color = EmotionInfo.ColorOf(t);
                GUI.Label(new Rect(x, y, 290, 22),
                    "  " + EmotionInfo.NameEn(t) + "  " + Mathf.CeilToInt(fx.ActiveRemaining(i)) + "s");
                y += 20f;
            }
        }

        // 画面中央上の通知
        if (toastStyle == null)
        {
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        }
        float cy = 90f;
        if (Time.time < inv.ToastUntil && !string.IsNullOrEmpty(inv.ToastText))
        {
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.Label(new Rect(0, cy, Screen.width, 30), inv.ToastText, toastStyle);
            cy += 30f;
        }
        if (mole != null && Time.time < mole.HardBlockedUntil)
        {
            GUI.color = new Color(1f, 0.55f, 0.45f, 0.95f);
            GUI.Label(new Rect(0, cy, Screen.width, 30), "Too hard to dig here! (Anger might help)", toastStyle);
        }

        GUI.color = prev;
    }
}
