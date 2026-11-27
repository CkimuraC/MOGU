using UnityEngine;

/// <summary>
/// 感情の塵。地中に溶け込んだ感情が、細かくなったもの。モグラに付ける。
/// 塵は、モグラの中に溜まる『感情エネルギー』のゲージになる。ゲージは、感情(喜・怒・哀・楽)ごとに1本ずつ。
///
///  ① その感情の塵を吸収すると、その感情のゲージが増える(0〜最大)
///  ② ゲージが最大になると、その感情に『発動チャンス』が始まる(Ready)。ゲージが光って知らせる
///     この間に E / ○ を押すと、感情を選ぶ画面が出て、選んだ感情の能力が発動する。発動後、その感情のゲージは0になる
///  ③ 発動せずに時間が過ぎると、その感情のチャンスが終わり、ゲージが少しずつ減っていく(Fading)
///     また塵を吸収すれば、ゲージを増やせる(最大まで溜めれば、再びチャンス)
/// </summary>
public class EmotionDust : MonoBehaviour
{
    public enum State
    {
        Gathering, // 溜めている(通常蓄積)
        Ready,     // 最大。発動できる
        Fading     // 発動しなかったので、減っていく
    }

    [Header("Gauge")]
    [Tooltip("ゲージの最大値(感情ごと)")]
    public float maxGauge = 100f;
    [Tooltip("塵1つで増えるゲージ量(その感情のゲージが増える)")]
    public float absorbAmount = 20f;
    [Tooltip("感情ごとの増えやすさの倍率(1=基準)。順番は 喜 / 怒 / 哀 / 楽。怒は岩盤を掘るために必要なので、少し多め")]
    public float[] absorbMultipliers = { 1f, 1.5f, 1f, 1f };

    [Header("発動チャンス")]
    [Tooltip("ゲージが最大になってから、発動できる時間(秒)")]
    public float readySeconds = 10f;
    [Tooltip("チャンスが終わったあと、1秒あたりゲージが減る量")]
    public float decayPerSecond = 5f;

    [Header("Debug (開発用)")]
    [Tooltip("ゲーム開始時のゲージ(各感情)。0なら空から始まる")]
    public float startGauge = 0f;

    public float[] gauges = new float[EmotionInfo.Count];
    State[] states = new State[EmotionInfo.Count];
    float[] readyRemain = new float[EmotionInfo.Count];

    public string ToastText { get; private set; }
    public float ToastUntil { get; private set; }

    EmotionEffects fx;
    MoleThought thought;

    public float Gauge(EmotionType t) { return gauges[(int)t]; }
    public float Gauge01(EmotionType t) { return maxGauge <= 0f ? 0f : Mathf.Clamp01(gauges[(int)t] / maxGauge); }
    public State StateOf(EmotionType t) { return states[(int)t]; }
    public bool IsReady(EmotionType t) { return states[(int)t] == State.Ready; }
    public float ReadyRemaining(EmotionType t) { return readyRemain[(int)t]; }

    /// <summary>どれか1つでも、発動できる状態か。</summary>
    public bool AnyReady
    {
        get { for (int i = 0; i < states.Length; i++) if (states[i] == State.Ready) return true; return false; }
    }

    void Awake()
    {
        fx = GetComponent<EmotionEffects>();
        thought = GetComponent<MoleThought>();
        if (gauges == null || gauges.Length != EmotionInfo.Count) gauges = new float[EmotionInfo.Count];
        states = new State[EmotionInfo.Count];
        readyRemain = new float[EmotionInfo.Count];
        for (int i = 0; i < gauges.Length; i++)
        {
            gauges[i] = Mathf.Clamp(startGauge, 0f, maxGauge);
            if (gauges[i] >= maxGauge) EnterReady(i, false);
        }
    }

    /// <summary>塵を吸収する。amount が負なら、absorbAmount を使う。</summary>
    public void Absorb(EmotionType t, float amount = -1f)
    {
        int i = (int)t;
        float mul = absorbMultipliers != null && i < absorbMultipliers.Length ? absorbMultipliers[i] : 1f;
        gauges[i] = Mathf.Min(maxGauge, gauges[i] + (amount < 0f ? absorbAmount * mul : amount));
        Toast(EmotionInfo.NameJp(t) + "の塵を吸収した", 1.0f);
        if (gauges[i] >= maxGauge && states[i] != State.Ready) EnterReady(i, true);
    }

    void EnterReady(int i, bool announce)
    {
        states[i] = State.Ready;
        readyRemain[i] = readySeconds;
        if (announce) Toast(EmotionInfo.NameJp((EmotionType)i) + "のゲージが満タン!  E / ○ で発動できます", 3f);
    }

    /// <summary>選んだ感情の能力を発動する。発動できるのは、その感情がチャンス中のときだけ。</summary>
    public bool Activate(EmotionType t)
    {
        int i = (int)t;
        if (states[i] != State.Ready) return false;
        if (fx != null) fx.Apply(t);
        if (thought != null) thought.ShowEmotion(t);
        gauges[i] = 0f;
        states[i] = State.Gathering;
        return true;
    }

    /// <summary>その感情のゲージを最大にする(デバッグ用)。</summary>
    public void FillNow(EmotionType t)
    {
        int i = (int)t;
        gauges[i] = maxGauge;
        if (states[i] != State.Ready) EnterReady(i, false);
    }

    /// <summary>すべてのゲージを空にする(デバッグ用)。</summary>
    public void ClearAll()
    {
        for (int i = 0; i < gauges.Length; i++) { gauges[i] = 0f; states[i] = State.Gathering; }
    }

    void Update()
    {
        // デバッグ: H キーで塵を1つ吸収(感情はランダム)
        if (MoleInput.DebugGiveDustPressed()) Absorb((EmotionType)Random.Range(0, EmotionInfo.Count));

        float dt = Time.deltaTime;
        for (int i = 0; i < gauges.Length; i++)
        {
            if (states[i] == State.Ready)
            {
                readyRemain[i] -= dt;
                if (readyRemain[i] <= 0f)
                {
                    readyRemain[i] = 0f;
                    states[i] = State.Fading;
                    Toast(EmotionInfo.NameJp((EmotionType)i) + "の発動チャンスが終わった…ゲージが減っていく", 2.5f);
                }
            }
            else if (states[i] == State.Fading)
            {
                gauges[i] = Mathf.Max(0f, gauges[i] - decayPerSecond * dt);
                if (gauges[i] <= 0f) states[i] = State.Gathering;
            }
        }
    }

    void Toast(string text, float seconds)
    {
        ToastText = text;
        ToastUntil = Time.time + seconds;
    }
}
