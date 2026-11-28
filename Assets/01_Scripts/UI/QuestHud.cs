using UnityEngine;

/// <summary>
/// クエストの表示(OnGUI)。画面の左側。
/// 受けているクエストの名前・集めた数 / 必要数・次にやること。
/// </summary>
public class QuestHud : MonoBehaviour
{
    QuestManager q;

    void OnGUI()
    {
        if (q == null) q = QuestManager.Instance;
        if (q == null) return;

        float lh = UiText.Line(17);
        float w = UiText.Px(380);
        float x = UiText.Px(20);
        float y = UiText.Px(300);
        var gray = new Color(0.75f, 0.75f, 0.75f);

        var cur = q.Current;

        // 通知(クエストを受けた・リタイアした・達成した)
        if (Time.time < q.ToastUntil && !string.IsNullOrEmpty(q.ToastText))
        {
            float big = UiText.Line(26);
            UiText.Label(new Rect(0, UiText.Px(130), Screen.width, big), q.ToastText, 26, new Color(1f, 0.95f, 0.6f, 0.97f), TextAnchor.UpperCenter);
        }
        int rows = cur == null ? 2 : (q.Accepted ? 7 : 3);

        var pc = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - UiText.Px(8), y - UiText.Px(6), w + UiText.Px(16), lh * rows + UiText.Px(10)), Texture2D.whiteTexture);
        GUI.color = pc;

        if (cur == null)
        {
            UiText.Label(new Rect(x, y, w, lh), "クエスト", 17, Color.white);
            y += lh;
            UiText.Label(new Rect(x, y, w, lh), "すべての依頼を達成しました(新しい依頼は準備中)", 15, gray);
            return;
        }

        if (!q.Accepted)
        {
            UiText.Label(new Rect(x, y, w, lh), "次の依頼: " + q.CurrentTitle, 17, Color.white);
            y += lh;
            UiText.Label(new Rect(x, y, w, lh), "地上の大木のそばで B / × を押すと、受けられます", 15, gray);
            return;
        }

        UiText.Label(new Rect(x, y, w, lh), q.CurrentTitle, 17, Color.white);
        y += lh;
        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            var t = (EmotionType)i;
            int need = q.Need(t), have = q.Have(t);
            bool done = have >= need;
            Color c = done ? new Color(0.6f, 0.95f, 0.6f) : EmotionInfo.ColorOf(t);
            UiText.Label(new Rect(x, y, w, lh),
                "　 かけら(" + EmotionInfo.NameJp(t) + ")  " + Mathf.Min(have, need) + " / " + need + (done ? "  OK" : ""), 16, c);
            y += lh;
        }

        if (q.CanReport)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            UiText.Label(new Rect(x, y, w, lh), "達成! B / × →「帰還する」で、大木へ報告できます", 15,
                Color.Lerp(new Color(1f, 0.9f, 0.4f), Color.white, pulse));
        }
        else
        {
            UiText.Label(new Rect(x, y, w, lh), "帰還すると、クエストをリタイアしたことになります", 15, gray);
        }
        }
}
