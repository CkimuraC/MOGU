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

    /// <summary>true の間は、モグラの操作(移動・掘る・スキャン・かけらを食べる・スタミナ回復)を受け付けない。メニューが立てる。</summary>
    public static bool InputBlocked;
    const float TriggerThreshold = 0.3f;

    /// <summary>移動入力。スティックは360度の向きをそのまま返す(長さは最大1)。</summary>
    public static Vector2 Move() { return InputBlocked ? Vector2.zero : MoveRaw(); }

    static Vector2 MoveRaw()
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
    public static bool DigHeld() { return !InputBlocked && DigHeldRaw(); }

    static bool DigHeldRaw()
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
    public static bool RefillPressed() { return !InputBlocked && RefillPressedRaw(); }

    static bool RefillPressedRaw()
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
    public static bool ScanPressed() { return !InputBlocked && ScanPressedRaw(); }

    static bool ScanPressedRaw()
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
public static int ShardEatIndex() { return InputBlocked ? -1 : ShardEatIndexRaw(); }

    static int ShardEatIndexRaw()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return -1;
        // 上段の数字キーでもテンキーでも反応する
        if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) return 0;
        if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) return 1;
        if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) return 2;
        if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) return 3;
        if (k.digit5Key.wasPressedThisFrame || k.numpad5Key.wasPressedThisFrame) return 4;
        if (k.digit6Key.wasPressedThisFrame || k.numpad6Key.wasPressedThisFrame) return 5;
        return -1;
#else
        for (int i = 0; i < 6; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) return i;
        return -1;
#endif
    }

    /// <summary>食べるかけらの選択を切り替える。Tab / L1</summary>
    public static bool ShardCyclePressed() { return !InputBlocked && ShardCyclePressedRaw(); }

    static bool ShardCyclePressedRaw()
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
    public static bool ShardEatSelectedPressed() { return !InputBlocked && ShardEatSelectedPressedRaw(); }

    static bool ShardEatSelectedPressedRaw()
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

/// <summary>デバッグ用:全種類のかけらを1つずつ手に入れる。Gキー</summary>
    public static bool DebugGiveShardsPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.gKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.G);
#endif
    }

/// <summary>デバッグ用:全種類の塵をひとつずつ吸収する。Hキー</summary>
    public static bool DebugGiveDustPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.hKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.H);
#endif
    }

/// <summary>(廃止済み)「かけらを使う」画面を開く。Eキー / ○ボタン。(メニューが閉じているときに調べる)</summary>
    public static bool ShardMenuPressed()
    {
        return ShardEatSelectedPressedRaw();
    }


/// <summary>画面を引いて見る(押している間)。Zキー / R3(右スティックを押し込む)</summary>
    public static bool ZoomOutHeld() { return !InputBlocked && ZoomOutHeldRaw(); }

static bool ZoomOutHeldRaw()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.zKey.isPressed; // Zキーは、押している間だけ引く
#else
        return Input.GetKey(KeyCode.Z);
#endif
    }

    /// <summary>画面を引く / 戻すの切り替え(押した瞬間だけ)。R3(右スティックを押し込む)</summary>
    public static bool ZoomTogglePressed() { return !InputBlocked && ZoomTogglePressedRaw(); }

    static bool ZoomTogglePressedRaw()
    {
#if ENABLE_INPUT_SYSTEM
        var g = Gamepad.current;
        return g != null && g.rightStickButton.wasPressedThisFrame;
#else
        return false;
#endif
    }


/// <summary>デバッグパネルの開閉。F2キー</summary>
    public static bool DebugPanelTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.f2Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F2);
#endif
    }

// ---------------- メニュー用(InputBlocked の影響を受けない) ----------------

    static int prevStickDir;

    /// <summary>拠点メニューを開く(開いているときは、閉じる)。Bキー / ×ボタン</summary>
    public static bool MenuOpenPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.bKey.wasPressedThisFrame) return true;
        var g = Gamepad.current;
        return g != null && g.buttonSouth.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.B);
#endif
    }

    /// <summary>メニューの上下移動(押した瞬間だけ)。上=+1、下=-1、なし=0。矢印 / W・S / 十字キー / 左スティック</summary>
    public static int MenuMoveEdge()
    {
        int edge = 0;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) edge = 1;
            else if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) edge = -1;
        }
        var g = Gamepad.current;
        if (g != null)
        {
            if (g.dpad.up.wasPressedThisFrame) edge = 1;
            else if (g.dpad.down.wasPressedThisFrame) edge = -1;
            float sy = g.leftStick.ReadValue().y;
            int dir = sy > 0.6f ? 1 : (sy < -0.6f ? -1 : 0);
            if (dir != 0 && dir != prevStickDir) edge = dir;
            prevStickDir = dir;
        }
#else
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) edge = 1;
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) edge = -1;
#endif
        return edge;
    }

    /// <summary>メニューの決定。Enter / Space / ×ボタン</summary>
    public static bool MenuConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) return true;
        var g = Gamepad.current;
        return g != null && g.buttonSouth.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    /// <summary>メニューのもどる。Esc / Backspace / ○ボタン</summary>
    public static bool MenuCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)) return true;
        var g = Gamepad.current;
        return g != null && g.buttonEast.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
#endif
    }






}
