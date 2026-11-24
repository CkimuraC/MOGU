using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// キーボード + ゲームパッド(PS5 DualSense等)の入力ヘルパー。
/// 新Input System / 旧Input Manager のどちらの設定でもコンパイルできる。
/// ※ゲームパッド対応は新Input System(Active Input Handling)が有効な場合のみ。
/// </summary>
public static class MoleInput
{
    const float StickDeadzone = 0.25f;
    const float TriggerThreshold = 0.3f;

    /// <summary>移動入力。スティックは360度の向きをそのまま返す(長さは最大1)。</summary>
    public static Vector2 Move()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 v = Vector2.zero;

        var k = Keyboard.current;
        if (k != null)
        {
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f)
                    - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f)
                    - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            v = new Vector2(x, y);
        }

        var g = Gamepad.current;
        if (v.sqrMagnitude < 0.0001f && g != null)
        {
            // 左スティック
            Vector2 s = g.leftStick.ReadValue();
            if (s.magnitude > StickDeadzone) v = s;
            else
            {
                // 十字キー
                v = g.dpad.ReadValue();
            }
        }
        return v;
#else
        float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        return new Vector2(x, y);
#endif
    }

    /// <summary>掘削ボタン(押している間)。Shift / R2 / R1 / □</summary>
    public static bool DigHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed)) return true;

        var g = Gamepad.current;
        if (g != null)
        {
            if (g.rightTrigger.ReadValue() > TriggerThreshold) return true;
            if (g.rightShoulder.isPressed) return true;
            if (g.buttonWest.isPressed) return true; // PS5: □
        }
        return false;
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    /// <summary>スタミナ全回復(デバッグ)。R / OPTIONSボタン</summary>
    public static bool RefillPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.rKey.wasPressedThisFrame) return true;
        var g = Gamepad.current;
        return g != null && g.startButton.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

/// <summary>探知(スキャン)ボタン。Q / L2 / △</summary>
    public static bool ScanPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.qKey.wasPressedThisFrame) return true;
        var g = Gamepad.current;
        return g != null && (g.leftTrigger.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }

/// <summary>暗闇のオンオフ切り替え(開発用)。Lキー</summary>
    public static bool DarkTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.lKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.L);
#endif
    }

/// <summary>感情のかけらを食べる(キーボード1〜6)。押された番号(0〜5)、なければ-1</summary>
    public static int ShardEatIndex()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return -1;
        if (k.digit1Key.wasPressedThisFrame) return 0;
        if (k.digit2Key.wasPressedThisFrame) return 1;
        if (k.digit3Key.wasPressedThisFrame) return 2;
        if (k.digit4Key.wasPressedThisFrame) return 3;
        if (k.digit5Key.wasPressedThisFrame) return 4;
        if (k.digit6Key.wasPressedThisFrame) return 5;
        return -1;
#else
        for (int i = 0; i < 6; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
        return -1;
#endif
    }

    /// <summary>食べるかけらの選択を切り替える。Tab / L1</summary>
    public static bool ShardCyclePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.tabKey.wasPressedThisFrame) return true;
        var g = Gamepad.current;
        return g != null && g.leftShoulder.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    /// <summary>選択中のかけらを食べる。E / ○</summary>
    public static bool ShardEatSelectedPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame) return true;
        var g = Gamepad.current;
        return g != null && g.buttonEast.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }



}
