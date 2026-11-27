using System.Collections.Generic;
using UnityEngine;

/// <summary>クエスト1つぶんの設定。</summary>
[System.Serializable]
public class QuestDef
{
    public string title = "";
    [TextArea]
    [Tooltip("大木のセリフ")]
    public string message = "";
    [Tooltip("必要な感情のかけらの数。順番は 喜 / 怒 / 哀 / 楽")]
    public int[] shardNeeds = new int[EmotionInfo.Count];
    [Tooltip("報酬の説明(いまは仮)")]
    public string rewardText = "(報酬は準備中)";

    public static QuestDef Make(string title, string message, int eachShards)
    {
        var q = new QuestDef { title = title, message = message };
        for (int i = 0; i < q.shardNeeds.Length; i++) q.shardNeeds[i] = eachShards;
        return q;
    }
}

/// <summary>
/// ゲームサイクルの根幹: クエスト。
///
///  ① 大木からクエストを受ける(依頼は順番に進む)
///  ② 地下を探索して、必要な感情のかけらを集める
///  ③ 必要数が集まると『達成』。いつでも帰還できる(達成後も、探索を続けてもよい)
///     達成前に帰還すると、クエストは『諦めた』ことになる(集めたかけらは、持ったまま)
///  ④ 大木に報告すると、必要数だけ、かけらを大木に渡す。報酬をもらい、次の依頼が解放される
///
/// 進み具合(いくつ達成したか)は、PlayerPrefs に保存する。
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public List<QuestDef> quests = DefaultQuests();

    public static List<QuestDef> DefaultQuests()
    {
        return new List<QuestDef>
        {
            QuestDef.Make("はじめてのお願い", "感情のかけらを、喜・怒・哀・楽、それぞれ1個ずつ、集めてきてほしい。", 1),
            QuestDef.Make("もう少しほしいな", "もう少しほしいな…。感情のかけらを、それぞれ2個ずつ、お願いしてもいいかな。", 2),
            QuestDef.Make("もっとほしいな", "もっとほしいな…。感情のかけらを、それぞれ3個ずつ、集めてきてほしい。", 3)
        };
    }

    public void ResetDefs() { quests = DefaultQuests(); }

    const string PrefKey = "mogura.quest.completed";

    /// <summary>達成した(報告まで済ませた)クエストの数。</summary>
    public int CompletedCount { get; private set; }
    /// <summary>いま、クエストを受けている最中か。</summary>
    public bool Accepted { get; private set; }

    public string ToastText { get; private set; }
    public float ToastUntil { get; private set; }

    ShardInventory inv;
    bool announced;

    /// <summary>いま挑戦できる(または挑戦中の)クエスト。全部終わっていたら null。</summary>
    public QuestDef Current { get { return CompletedCount < quests.Count ? quests[CompletedCount] : null; } }
    public int CurrentNumber { get { return CompletedCount + 1; } }
    public bool AllDone { get { return Current == null; } }

    public static string TitleOf(int number, QuestDef q) { return "QUEST " + number.ToString("00") + "  " + q.title; }
    public string CurrentTitle { get { return Current == null ? "" : TitleOf(CurrentNumber, Current); } }

    void Awake()
    {
        Instance = this;
        CompletedCount = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, quests.Count);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    ShardInventory Inv
    {
        get { if (inv == null) inv = FindFirstObjectByType<ShardInventory>(); return inv; }
    }

    public int Need(EmotionType t)
    {
        var q = Current;
        int i = (int)t;
        return q != null && i < q.shardNeeds.Length ? q.shardNeeds[i] : 0;
    }

    public int Have(EmotionType t) { return Inv != null ? Inv.carried[(int)t] : 0; }

    /// <summary>必要数が、全部そろっているか。</summary>
    public bool Satisfied
    {
        get
        {
            if (Current == null || Inv == null) return false;
            for (int i = 0; i < EmotionInfo.Count; i++)
                if (Inv.carried[i] < Need((EmotionType)i)) return false;
            return true;
        }
    }

    /// <summary>報告できる(受けていて、必要数がそろっている)。</summary>
    public bool CanReport { get { return Accepted && Satisfied; } }

    public void Accept()
    {
        if (Current == null) return;
        Accepted = true;
        announced = false;

        // クエストを受けるたびに、感情の塵が、もう一度ランダムに復活する
        var spawner = FindFirstObjectByType<ItemSpawner>();
        if (spawner != null) spawner.RespawnDust();

        Toast("クエストを受けた: " + CurrentTitle + "  (感情の塵が、地中にまた湧いた)", 3.5f);
    }

    /// <summary>クエストを諦める(集めたかけらは、持ったまま)。</summary>
    public void Abandon()
    {
        if (!Accepted) return;
        Accepted = false;
        announced = false;
        Toast("クエストを諦めた。もう一度、大木で受けられます", 3.5f);
    }

    /// <summary>大木に報告する。必要数だけ、かけらを渡して、次の依頼を解放する。</summary>
    public bool Report()
    {
        if (!CanReport) return false;
        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            int n = Need((EmotionType)i);
            Inv.carried[i] -= n;
            Inv.delivered[i] += n;
        }
        CompletedCount++;
        Accepted = false;
        announced = false;
        PlayerPrefs.SetInt(PrefKey, CompletedCount);
        PlayerPrefs.Save();
        return true;
    }

    // ---- デバッグ ----
    public void DebugGiveNeeded()
    {
        if (Current == null || Inv == null) return;
        for (int i = 0; i < EmotionInfo.Count; i++)
            Inv.carried[i] = Mathf.Max(Inv.carried[i], Need((EmotionType)i));
    }

    public void DebugResetProgress()
    {
        CompletedCount = 0;
        Accepted = false;
        announced = false;
        PlayerPrefs.SetInt(PrefKey, 0);
        PlayerPrefs.Save();
    }

    public void DebugSkip()
    {
        if (Current == null) return;
        CompletedCount++;
        Accepted = false;
        announced = false;
        PlayerPrefs.SetInt(PrefKey, CompletedCount);
        PlayerPrefs.Save();
    }

    void Update()
    {
        // 必要数がそろった瞬間に、知らせる
        bool ok = CanReport;
        if (ok && !announced)
        {
            announced = true;
            Toast("クエスト達成!  いつでも帰還して、大木に報告できます", 4f);
        }
        else if (!ok) announced = false;
    }

    void Toast(string text, float seconds)
    {
        ToastText = text;
        ToastUntil = Time.time + seconds;
    }
}
