using System.Collections.Generic;
using UnityEngine;

/// <summary>感情1種類ぶんの効果の設定。1=変化なし。</summary>
[System.Serializable]
public class EmotionEffectDef
{
    public EmotionType type;
    [Tooltip("効果が続く時間(秒)")]
    public float duration = 25f;

    [Header("Movement")]
    [Tooltip("移動速度の倍率")]
    public float moveSpeedMul = 1f;
    [Tooltip("掘削速度の倍率")]
    public float digSpeedMul = 1f;

    [Header("Stamina")]
    [Tooltip("掘るときのスタミナ消費の倍率(小さいほど節約)")]
    public float staminaCostMul = 1f;
    [Tooltip("効果中、毎秒どれだけスタミナが回復するか(掘っていても回復)")]
    public float regenBonusPerSecond = 0f;

    [Header("Digging")]
    [Tooltip("掘削力の加算。基本は1。地層の Min Dig Power がこれ以下なら掘れる")]
    public float digPowerBonus = 0f;

    [Header("Sensing")]
    [Tooltip("スキャンの範囲の倍率")]
    public float scanRangeMul = 1f;
    [Tooltip("「特殊な空間」の探知範囲の倍率(隠された空洞など。Detectable の Is Special Space が対象)")]
    public float specialSenseMul = 1f;
}

/// <summary>
/// 感情のかけらを食べたときの一時的な効果。モグラに付ける。
/// 同じ感情を重ねて食べると、時間が延長される。違う感情は同時に効く(倍率は掛け算)。
/// </summary>
public class EmotionEffects : MonoBehaviour
{
    public List<EmotionEffectDef> defs = new List<EmotionEffectDef>
    {
        new EmotionEffectDef { type = EmotionType.Joy,       duration = 25f, moveSpeedMul = 1.3f, digSpeedMul = 1.4f },
        new EmotionEffectDef { type = EmotionType.Anger,     duration = 25f, digSpeedMul = 1.3f, staminaCostMul = 1.6f, digPowerBonus = 1f },
        new EmotionEffectDef { type = EmotionType.Sadness,   duration = 30f, moveSpeedMul = 0.7f, staminaCostMul = 0.5f },
        new EmotionEffectDef { type = EmotionType.Fear,      duration = 30f, scanRangeMul = 1.6f },
        new EmotionEffectDef { type = EmotionType.Calm,      duration = 30f, regenBonusPerSecond = 6f },
        new EmotionEffectDef { type = EmotionType.Curiosity, duration = 30f, specialSenseMul = 2f }
    };

    class ActiveEffect
    {
        public EmotionType type;
        public float remaining;
    }
    readonly List<ActiveEffect> active = new List<ActiveEffect>();

    public int ActiveCount { get { return active.Count; } }
    public EmotionType ActiveType(int i) { return active[i].type; }
    public float ActiveRemaining(int i) { return active[i].remaining; }

    public bool IsActive(EmotionType t)
    {
        for (int i = 0; i < active.Count; i++) if (active[i].type == t) return true;
        return false;
    }

    EmotionEffectDef Def(EmotionType t)
    {
        for (int i = 0; i < defs.Count; i++) if (defs[i].type == t) return defs[i];
        return null;
    }

    /// <summary>感情の効果を発動する(すでに効いていれば時間を延長)。</summary>
    public void Apply(EmotionType t)
    {
        var d = Def(t);
        if (d == null) return;
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].type == t) { active[i].remaining = d.duration; return; }
        }
        active.Add(new ActiveEffect { type = t, remaining = d.duration });
    }

    void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            active[i].remaining -= Time.deltaTime;
            if (active[i].remaining <= 0f) active.RemoveAt(i);
        }
    }

    // ---- 効いている効果をまとめた値 ----
    public float MoveSpeedMul { get { float m = 1f; for (int i = 0; i < active.Count; i++) { var d = Def(active[i].type); if (d != null) m *= d.moveSpeedMul; } return m; } }
    public float DigSpeedMul { get { float m = 1f; for (int i = 0; i < active.Count; i++) { var d = Def(active[i].type); if (d != null) m *= d.digSpeedMul; } return m; } }
    public float StaminaCostMul { get { float m = 1f; for (int i = 0; i < active.Count; i++) { var d = Def(active[i].type); if (d != null) m *= d.staminaCostMul; } return m; } }
    public float ScanRangeMul { get { float m = 1f; for (int i = 0; i < active.Count; i++) { var d = Def(active[i].type); if (d != null) m *= d.scanRangeMul; } return m; } }
    public float SpecialSenseMul { get { float m = 1f; for (int i = 0; i < active.Count; i++) { var d = Def(active[i].type); if (d != null) m *= d.specialSenseMul; } return m; } }
    public float RegenBonusPerSecond { get { float s = 0f; for (int i = 0; i < active.Count; i++) { var d = Def(active[i].type); if (d != null) s += d.regenBonusPerSecond; } return s; } }
    public float DigPowerBonus { get { float s = 0f; for (int i = 0; i < active.Count; i++) { var d = Def(active[i].type); if (d != null) s += d.digPowerBonus; } return s; } }
}
