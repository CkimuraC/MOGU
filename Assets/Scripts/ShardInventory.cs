using UnityEngine;

/// <summary>
/// 感情のかけらの持ち物。モグラに付ける。
/// ・拾うと「持ち物」に入る
/// ・食べると、その感情の効果が一時的につく(EmotionEffects)
/// ・地上に出ると、持っているかけらを自動で「納めた」ことになる(持ち帰り)
/// </summary>
public class ShardInventory : MonoBehaviour
{
    public int[] carried = new int[EmotionInfo.Count];   // 持っている数
    public int[] delivered = new int[EmotionInfo.Count]; // 持ち帰って納めた数
    [Tooltip("ゲームパッドで食べる対象(L1で切り替え)")]
    public int selected;

    [Tooltip("地上に出たとき、持っているかけらを自動で納める")]
    public bool autoDeliverOnSurface = true;

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
    }

    void Update()
    {
        // キーボード1〜6:その感情を食べる
        int idx = MoleInput.ShardEatIndex();
        if (idx >= 0 && idx < EmotionInfo.Count)
        {
            selected = idx;
            TryEat((EmotionType)idx);
        }
        // 選択切り替え(Tab / L1) と 選択中を食べる(E / ○)
        if (MoleInput.ShardCyclePressed()) selected = (selected + 1) % EmotionInfo.Count;
        if (MoleInput.ShardEatSelectedPressed()) TryEat((EmotionType)selected);

        // 地上に出たら納める
        if (autoDeliverOnSurface && terrain != null && CarriedTotal > 0 && transform.position.y > terrain.TopY)
            DeliverAll();
    }

    public void Add(EmotionType t)
    {
        carried[(int)t]++;
        Toast("Got a " + EmotionInfo.NameEn(t) + " shard", 1.5f);
    }

    public bool TryEat(EmotionType t)
    {
        int i = (int)t;
        if (carried[i] <= 0)
        {
            Toast("No " + EmotionInfo.NameEn(t) + " shard to eat", 1.2f);
            return false;
        }
        carried[i]--;
        if (fx != null) fx.Apply(t);
        Toast("Ate " + EmotionInfo.NameEn(t), 1.5f);
        return true;
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
        if (n > 0) Toast("Delivered " + n + " shard" + (n > 1 ? "s" : "") + "!", 2.5f);
    }

    void Toast(string text, float seconds)
    {
        ToastText = text;
        ToastUntil = Time.time + seconds;
    }
}
