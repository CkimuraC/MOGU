using UnityEngine;

/// <summary>
/// 掘ったときの効果音(仮)。音声ファイルは使わず、コードで音を合成する。
/// 土の層ごとに音色が変わる(表土=やわらかい / 粘土=にぶい / 砂利=ジャリッ / 硬い層=こもった硬さ / 岩盤=カッ)。
/// 好みの音ができたら、overrideClips に音声ファイルを入れると、そちらが優先される(層の順番)。
/// 調整はデバッグパネル(F2)の「掘ったときの音」で行う。
/// </summary>
public class DigSound : MonoBehaviour
{
    public bool soundEnabled = true;
    [Range(0f, 1f)] public float volume = 0.5f;
    [Tooltip("連続で鳴らすときの最短の間隔(秒)")]
    public float minInterval = 0.07f;
    [Tooltip("音程のばらつき(0.1=±10%)")]
    public float pitchRandom = 0.12f;
    [Tooltip("1回で、これより多く削れたとき(拠点づくりなど)は鳴らさない")]
    public int ignoreIfPixelsOver = 300;
    [Tooltip("音声ファイルを使うとき用。層の順番(表土,粘土,砂利,硬い,岩盤)ごとに、複数入れるとランダムに選ぶ")]
    public AudioClip[] overrideClips;

    const int Layers = 5;
    const int Variants = 3;
    static readonly float[] Dur    = { 0.16f, 0.18f, 0.14f, 0.15f, 0.12f };
    static readonly float[] Smooth = { 0.10f, 0.16f, 0.45f, 0.26f, 0.70f }; // 大きいほど高い音(ローパスが開く)
    static readonly float[] Decay  = { 22f, 20f, 30f, 26f, 42f };
    static readonly float[] Thump  = { 85f, 105f, 130f, 115f, 190f };
    static readonly float[] ThumpV = { 0.65f, 0.7f, 0.35f, 0.55f, 0.3f };
    static readonly float[] Grit   = { 0f, 0f, 0.6f, 0.15f, 0.25f };     // ジャリジャリ感

    AudioClip[,] clips = new AudioClip[Layers, Variants];
    AudioSource[] voices;
    int voiceIdx;
    DirtTerrain terrain;
    float lastTime = -10f;

    void Start()
    {
        terrain = FindFirstObjectByType<DirtTerrain>();
        voices = new AudioSource[4];
        for (int i = 0; i < voices.Length; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            voices[i] = s;
        }
        for (int l = 0; l < Layers; l++)
            for (int v = 0; v < Variants; v++)
                clips[l, v] = Make(l, 1000 + l * 31 + v * 7);
        if (terrain != null) terrain.DugWorld += OnDug;
    }

    void OnDestroy()
    {
        if (terrain != null) terrain.DugWorld -= OnDug;
    }

    void OnDug(Vector2 at, int pixels, Color32 soil)
    {
        if (!soundEnabled || pixels <= 0 || pixels > ignoreIfPixelsOver) return;
        if (Time.unscaledTime - lastTime < minInterval) return;
        lastTime = Time.unscaledTime;

        int layer = Mathf.Clamp(terrain.LayerIndexAt(at), 0, Layers - 1);
        AudioClip clip = PickOverride(layer);
        if (clip == null) clip = clips[layer, Random.Range(0, Variants)];

        var src = voices[voiceIdx];
        voiceIdx = (voiceIdx + 1) % voices.Length;
        src.pitch = 1f + Random.Range(-pitchRandom, pitchRandom);
        float size = Mathf.Clamp01(0.6f + pixels / 150f);   // たくさん削れたときは少し大きく
        src.PlayOneShot(clip, volume * size);
    }

    AudioClip PickOverride(int layer)
    {
        if (overrideClips == null || overrideClips.Length == 0) return null;
        // 層ごとに分けず、入っている音から選ぶ(層の数だけ入っていれば、その層の音)
        if (overrideClips.Length >= Layers) return overrideClips[layer];
        return overrideClips[Random.Range(0, overrideClips.Length)];
    }

    static AudioClip Make(int layer, int seed)
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * Dur[layer]);
        var data = new float[n];
        var rng = new System.Random(seed);
        float lp = 0f, lp2 = 0f;
        float a = Smooth[layer];
        float f = Thump[layer] * (0.92f + (float)rng.NextDouble() * 0.16f);
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * Decay[layer]) * Mathf.Clamp01(t / 0.003f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += a * (noise - lp);
            lp2 += a * (lp - lp2);
            float s = lp2 * 2.2f * env;
            // ジャリッとした粒(まばらなクリック)
            if (Grit[layer] > 0f && rng.NextDouble() < 0.012)
                s += (float)(rng.NextDouble() * 2.0 - 1.0) * Grit[layer] * Mathf.Exp(-t * 18f);
            // 低いドスッ
            s += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 38f) * ThumpV[layer];
            data[i] = s;
            float ab = Mathf.Abs(s);
            if (ab > peak) peak = ab;
        }
        float g = 0.9f / peak;
        for (int i = 0; i < n; i++) data[i] *= g;
        var clip = AudioClip.Create("DigSound_" + layer, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
