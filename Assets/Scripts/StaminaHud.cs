using UnityEngine;

/// <summary>スタミナゲージ(OnGUIによる簡易表示)。</summary>
public class StaminaHud : MonoBehaviour
{
    public MoleController mole;
    ScanPulse scan;

void Awake()
    {
        if (mole == null) mole = FindFirstObjectByType<MoleController>();
        if (mole != null) scan = mole.GetComponent<ScanPulse>();
    }

void OnGUI()
    {
        if (mole == null) return;

        float max = mole.MaxStaminaCurrent;
        float ratio = Mathf.Clamp01(mole.Stamina / Mathf.Max(1f, max));
        // ★ブースト中はバーが長くなる
        float width = 260f * max / Mathf.Max(1f, mole.maxStamina);

        var frame = new Rect(20, 20, width, 22);
        var fill = new Rect(frame.x + 2, frame.y + 2, (frame.width - 4) * ratio, frame.height - 4);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(frame, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.45f, 0.85f, 0.3f), ratio);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = prev;

        float y = frame.yMax + 4;
        GUI.Label(new Rect(frame.x, y, 600, 22),
            "Stamina " + Mathf.CeilToInt(mole.Stamina) + " / " + Mathf.CeilToInt(max));
        y += 20;

        if (mole.BoostBonus > 0.5f)
        {
            GUI.color = new Color(1f, 0.9f, 0.3f);
            GUI.Label(new Rect(frame.x, y, 600, 22),
                "Star boost: max +" + Mathf.CeilToInt(mole.BoostBonus) + "  (" + Mathf.CeilToInt(mole.BoostTimeRemaining) + "s)");
            GUI.color = prev;
            y += 20;
        }
        if (mole.Exhausted)
        {
            GUI.color = new Color(1f, 0.4f, 0.35f);
            GUI.Label(new Rect(frame.x, y, 600, 22), "Exhausted - rest to recover");
            GUI.color = prev;
            y += 20;
        }

        if (scan != null)
        {
            GUI.color = scan.Ready ? new Color(0.5f, 0.95f, 1f) : new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(frame.x, y, 600, 22),
                scan.Ready ? "Scan: READY" : "Scan: " + scan.CooldownLeft.ToString("0.0") + "s");
            GUI.color = prev;
            y += 20;
        }

        GUI.Label(new Rect(frame.x, y, 600, 22),
            "Keyboard: WASD/Arrows + Shift(dig) + Q(scan) + R(refill) + L(darkness on/off)");
        y += 20;
        GUI.Label(new Rect(frame.x, y, 600, 22),
            "Gamepad: L-Stick/D-pad + R2/R1/Square(dig) + L2/Triangle(scan) + Options(refill)");
    }
}
