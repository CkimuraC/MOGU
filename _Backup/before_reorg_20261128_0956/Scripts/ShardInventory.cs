using UnityEngine;

/// <summary>
/// 感情のかけらの持ち物。モグラに付ける。
/// ・拾うと「持ち物」に入る
/// ・食べるものではない(能力強化は、感情の塵 EmotionDust が担当)
/// ・地上に出ると、持っているかけらを自動で「納めた」ことになる(持ち帰り)
/// </summary>
public class ShardInventory : MonoBehaviour
{
    public int[] carried = new int[EmotionInfo.Count];   // 持っている数
    public int[] delivered = new int[EmotionInfo.Count]; // 持ち帰って納めた数
    [Tooltip("ゲームパッドで食べる対象(L1で切り替え)")]
    public int selected;

    [Tooltip("地上に出たとき、持っているかけらを自動で納める")]
    public bool autoDeliverOnSurface = false;

    // 感情のかけらは「大木に届けるもの」。能力強化は感情の塵(EmotionDust)が担当。

    [Header("Debug (開発用)")]
    [Tooltip("ゲーム開始時に、各種類のかけらをこの数ずつ持った状態で始める。0なら何も持たない")]
    public int startWithEach = 0;

    public string ToastText { get; private set; }
    public float ToastUntil { get; private set; }

    MoleController mole;
    EmotionEffects fx;
    DirtTerrain terrain;

    public int CarriedTotal { get { int n = 0; for (int i = 0; i < carried.Length; i++) n += carried[i]; return n; } }
    public int DeliveredTotal { get { int n = 0; for (int i = 0; i < delivered.Length; i++) n += delivered[i]; return n; } }

    void Awake()
    {
        mole = GetComponent<MoleController>();
        fx = GetComponent<EmotionEffects>();
        terrain = FindFirstObjectByType<DirtTerrain>();
        if (carried == null || carried.Length != EmotionInfo.Count) carried = new int[EmotionInfo.Count];
        if (delivered == null || delivered.Length != EmotionInfo.Count) delivered = new int[EmotionInfo.Count];
        if (startWithEach > 0) for (int i = 0; i < carried.Length; i++) carried[i] += startWithEach;
    }

void Update()
    {
        // かけらを使う操作は、BaseMenu の「かけらを使う」画面(Eキー / ○ボタンで開く)から行う。

        // デバッグ:Gキーで全種類を1つずつ手に入れる
        if (MoleInput.DebugGiveShardsPressed())
        {
            for (int i = 0; i < carried.Length; i++) carried[i]++;
            Toast("デバッグ: かけらを各1個入手", 1.5f);
        }

        // 地上に出たら納める
        if (autoDeliverOnSurface && terrain != null && CarriedTotal > 0 && transform.position.y > terrain.TopY)
            DeliverAll();
    }

public void Add(EmotionType t)
    {
        // 持ち物に入れる
        carried[(int)t]++;
        Toast("かけらを拾った: " + EmotionInfo.NameJp(t), 1.5f);
    }

public bool TryEat(EmotionType t)
    {
        int i = (int)t;
        if (carried[i] <= 0)
        {
            Toast("「" + EmotionInfo.NameJp(t) + "」のかけらを持っていません", 1.2f);
            return false;
        }
        carried[i]--;
        EatNow(t);
        return true;
    }

/// <summary>かけらを食べた結果(効果・モグラの吹き出し・通知)を出す。持ち物の増減はしない。</summary>
    void EatNow(EmotionType t)
    {
        if (fx != null) fx.Apply(t);

        // モグラの思考(吹き出し)
        var thought = GetComponent<MoleThought>();
        if (thought != null) thought.ShowEmotion(t);

        Toast("食べた: " + EmotionInfo.NameJp(t), 1.5f);
    }


    void DeliverAll()
    {
        int n = 0;
        for (int i = 0; i < carried.Length; i++)
        {
            delivered[i] += carried[i];
            n += carried[i];
            carried[i] = 0;
        }
        if (n > 0) Toast("かけらを " + n + " 個、持ち帰りました!", 2.5f);
    }

    void Toast(string text, float seconds)
    {
        ToastText = text;
        ToastUntil = Time.time + seconds;
    }
}
