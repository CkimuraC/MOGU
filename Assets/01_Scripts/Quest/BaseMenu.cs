using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 拠点のメニュー画面(IMGUI)。Bキー / ×ボタンで開く。開いている間は、ゲームの時間を止める。
///
/// ■ 画面の流れ
///   拠点の外:   「どうしますか?」→「拠点を作成する」/「帰還する」
///                 作成 →「拠点を作成しますか?」→ はい → 作成
///                        ・作れない場所なら、理由を説明する
///                        ・枠がいっぱいなら、古い拠点を選んで削除して作る / 作らない を選ぶ
///                 帰還 →「帰還しますか?」→ はい → 初期位置へ移動(仮)
///   拠点の中:   「休息する」「セーブする」「他の拠点へワープする」「帰還する」
///                 セーブ → 「このまま続ける」/「リスタートする」
///                 リスタート →「どこからスタートしますか?」→ 初期位置 / 各拠点
/// ■ 操作: 上下で選ぶ(矢印・W/S・十字キー・左スティック) / 決定(Enter・Space・×ボタン) / もどる(Esc・Backspace・○ボタン) / マウスもOK
/// </summary>
public class BaseMenu : MonoBehaviour
{
    class Choice
    {
        public string label;
        public string note;
        public Color color = Color.white; // 文字の色(かけらは、感情の色にする)
        public bool enabled = true;
        public Action onChoose;
    }

    class Page
    {
        public string title;
        public List<string> lines = new List<string>();
        public List<Choice> choices = new List<Choice>();
        public Action onCancel; // もどる。nullなら閉じる
    }

    [Header("Meals (ごはん) - スタート地点で食べられる")]
    [Tooltip("名前・最大スタミナの増える量・続く時間(秒)。いまは素材なしで、選ぶだけで食べられる(仮)")]
    public List<MealDef> meals = new List<MealDef>
    {
        new MealDef { name = "おにぎり", description = "ひとくちで食べられる、小さなおにぎり。", staminaBonus = 40f, durationSeconds = 90f },
        new MealDef { name = "ごはん定食", description = "おかずのついた、しっかりした食事。", staminaBonus = 100f, durationSeconds = 180f },
    };

    public static bool IsOpen { get; private set; }

    EmotionDust dust;
    EmotionEffects fx;

    BaseManager mgr;
    MoleController mole;
    Page page;
    int selected;
    float prevTimeScale = 1f;
    string toastText;
    float toastUntil;

    /// <summary>いま開いている画面の題(確認用)</summary>
    public string CurrentTitle { get { return page != null ? page.title : null; } }

    void Start()
    {
        mgr = BaseManager.Instance != null ? BaseManager.Instance : FindFirstObjectByType<BaseManager>();
        mole = FindFirstObjectByType<MoleController>();
        dust = FindFirstObjectByType<EmotionDust>();
        fx = FindFirstObjectByType<EmotionEffects>();
    }

    void OnDisable()
    {
        if (IsOpen) Close();
    }

    void Update()
    {
        if (mgr == null || mole == null) return;

        if (page == null)
        {
            if (MoleInput.MenuOpenPressed()) OpenMenu();
            else if (MoleInput.ShardMenuPressed()) OpenEmotionMenu(); // E / ○: 塵のゲージが満タンのとき、感情を選んで発動
            return; // 開いたフレームでは、決定などを受け付けない
        }

        int mv = MoleInput.MenuMoveEdge();
        if (mv != 0) MoveSelection(-mv); // 上(+1)で、リストの上へ
        if (MoleInput.MenuConfirmPressed()) Choose(selected);
        else if (MoleInput.MenuCancelPressed() || MoleInput.MenuOpenPressed()) Cancel();
    }

    // ---------------- 開く・閉じる・選ぶ ----------------

    /// <summary>モグラの居場所に合わせた、最初の画面を開く。</summary>
public void OpenMenu()
    {
        if (mgr == null || mole == null) return;

        // 地上で、大木の近くにいるときは、大木に話しかける(クエスト)
        var tree = GiantTree.Instance;
        if (tree != null && tree.IsMoleNear(mole.transform.position)) { ShowTreeMenu(); return; }

        var at = mgr.NearestBaseWithin(mole.transform.position, mgr.interactRadius);
        if (at != null) ShowBaseMain(at);
        else if (NearStartPoint()) ShowStartPoint();
        else ShowMain();
    }

/// <summary>「かけらを使う」画面を開く(Eキー / ○ボタン)。</summary>
public void OpenEmotionMenu()
    {
        if (dust == null) dust = FindFirstObjectByType<EmotionDust>();
        if (fx == null) fx = FindFirstObjectByType<EmotionEffects>();
        if (dust == null) return;
        if (!dust.AnyReady) { Toast(NotReadyText()); return; }
        ShowEmotionSelect();
    }

// ---------------- 大木(クエストの入口。中身は、これから決める) ----------------
void ShowTreeMenu()
    {
        var q = QuestManager.Instance;
        var p = new Page { title = "大木" };

        if (q == null)
        {
            p.lines.Add("地上にそびえる、大きな木。");
            p.choices.Add(C("はなれる", LeaveTree));
            p.onCancel = Close;
            Show(p);
            return;
        }

        var cur = q.Current;
        if (cur == null)
        {
            p.lines.Add("すべてのお願いを、叶えてくれたね。ありがとう。");
            p.lines.Add("(新しい依頼は、準備中です)");
            p.choices.Add(C("はなれる", LeaveTree));
        }
        else if (!q.Accepted)
        {
            p.lines.Add("「" + cur.message + "」");
            p.lines.Add("依頼: " + q.CurrentTitle);
            p.choices.Add(C("クエストを受ける", ShowQuestDetail, true, q.CurrentTitle));
            p.choices.Add(C("はなれる", LeaveTree));
        }
        else if (q.CanReport)
        {
            p.lines.Add("「おかえり。集めてきてくれたんだね」");
            p.lines.Add(q.CurrentTitle + " を、達成できます。");
            p.choices.Add(C("報告する", OnReport, true, "必要な数だけ、かけらを渡します"));
            p.choices.Add(C("はなれる", LeaveTree));
        }
        else
        {
            p.lines.Add("「がんばってね」");
            p.lines.Add("受けている依頼: " + q.CurrentTitle);
            AddProgressLines(p, q);
            p.choices.Add(C("報告する", OnReport, false, "まだ、必要な数がそろっていません"));
            p.choices.Add(C("クエストをリタイアする", ShowAbandonConfirm, true, "集めたかけらは、持ったままです"));
            p.choices.Add(C("はなれる", LeaveTree));
        }
        p.onCancel = Close;
        Show(p);
    }

    // 集めた数 / 必要数 を、行で足す
    void AddProgressLines(Page p, QuestManager q)
    {
        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            var t = (EmotionType)i;
            int need = q.Need(t), have = q.Have(t);
            p.lines.Add("　 感情のかけら(" + EmotionInfo.NameJp(t) + ")  " + Mathf.Min(have, need) + " / " + need + (have >= need ? "  OK" : ""));
        }
    }

    void ShowQuestDetail()
    {
        var q = QuestManager.Instance;
        var cur = q.Current;
        var p = new Page { title = q.CurrentTitle };
        p.lines.Add("「" + cur.message + "」");
        p.lines.Add("必要なもの:");
        for (int i = 0; i < EmotionInfo.Count; i++)
            p.lines.Add("　 感情のかけら(" + EmotionInfo.NameJp((EmotionType)i) + ") ×" + q.Need((EmotionType)i));
        p.lines.Add("報酬: " + cur.rewardText);
        p.lines.Add("★ 必要数がそろうと、帰還して報告できます。達成前に帰還すると、クエストをリタイアしたことになります。");
        p.choices.Add(C("受ける", () => { q.Accept(); Close(); }));
        p.choices.Add(C("もどる", ShowTreeMenu));
        p.onCancel = ShowTreeMenu;
        Show(p);
    }

    void ShowAbandonConfirm()
    {
        var q = QuestManager.Instance;
        var p = new Page { title = "クエストをリタイアしますか?" };
        p.lines.Add(q.CurrentTitle + " をリタイアします。集めたかけらは、持ったままです。もう一度、ここで受け直せます。");
        p.choices.Add(C("リタイアする", () => { q.Abandon(); ShowTreeMenu(); }));
        p.choices.Add(C("やめる", ShowTreeMenu));
        p.onCancel = ShowTreeMenu;
        Show(p);
    }

    void OnReport()
    {
        var q = QuestManager.Instance;
        if (q == null || !q.CanReport) return;
        var done = q.Current;
        int doneNo = q.CurrentNumber;
        string reward = done.rewardText;
        var handed = new List<string>();
        for (int i = 0; i < EmotionInfo.Count; i++)
            handed.Add(EmotionInfo.NameJp((EmotionType)i) + "×" + q.Need((EmotionType)i));

        q.Report();

        var p = new Page { title = "クエスト達成!" };
        p.lines.Add(QuestManager.TitleOf(doneNo, done) + " を、達成しました。");
        p.lines.Add("渡したかけら: " + string.Join(" ", handed.ToArray()));
        p.lines.Add("報酬: " + reward);
        if (q.AllDone) p.lines.Add("すべての依頼を達成しました! (新しい依頼は、準備中です)");
        else p.lines.Add("新しい依頼が、解放されました: " + q.CurrentTitle);
        p.choices.Add(C("ありがとう", ShowTreeMenu));
        p.onCancel = ShowTreeMenu;
        Show(p);
    }

void ShowQuestList() { ShowTreeMenu(); }


    /// <summary>効果の説明 + 効く時間 + (すでに効いていれば)その旨。</summary>
    string EffectNote(EmotionType t)
    {
        string s = EmotionInfo.EffectText(t);
        if (fx != null)
        {
            for (int i = 0; i < fx.defs.Count; i++)
                if (fx.defs[i].type == t) { s += "(" + Mathf.RoundToInt(fx.defs[i].duration) + "秒)"; break; }
            if (fx.IsActive(t)) s += "  ★効果中(使うと、時間がのびる)";
        }
        return s;
    }

    /// <summary>どのかけらを使うか選ぶ画面。持ち帰る分にもなることを、補足で伝える。</summary>
void ShowEmotionSelect()
    {
        var p = new Page { title = "感情を選んで発動" };
        p.lines.Add("ゲージが満タンの感情を、発動できます。発動すると、その感情のゲージは空になります。");

        for (int i = 0; i < EmotionInfo.Count; i++)
        {
            var t = (EmotionType)i;
            bool ready = dust.IsReady(t);
            string label = EmotionInfo.NameJp(t) + "   " + Mathf.RoundToInt(dust.Gauge(t)) + " / " + Mathf.RoundToInt(dust.maxGauge);
            string note = ready
                ? EffectNote(t) + "  (発動チャンス: あと " + Mathf.CeilToInt(dust.ReadyRemaining(t)) + " 秒)"
                : "ゲージが、まだ満タンではありません";
            var ch = C(label, () => UseEmotion(t), ready, note);
            ch.color = EmotionInfo.ColorOf(t);
            p.choices.Add(ch);
        }
        p.choices.Add(C("あとで(いまは発動しない)", Close));
        p.onCancel = Close;
        Show(p);
    }

    /// <summary>かけらを選んだあと: 「持ち帰る分もあるけど、どうしますか?」と確認する。</summary>
void UseEmotion(EmotionType t)
    {
        bool ok = dust != null && dust.Activate(t);
        Close();
        if (ok) Toast(EmotionInfo.NameJp(t) + "を発動!" + EffectNote(t));
    }

string NotReadyText()
    {
        return "感情の塵のゲージが、まだどれも満タンになっていません";
    }


    public void Choose(int index)
    {
        if (page == null || index < 0 || index >= page.choices.Count) return;
        var c = page.choices[index];
        if (!c.enabled) return;
        selected = index;
        if (c.onChoose != null) c.onChoose();
    }

    public void Cancel()
    {
        if (page == null) return;
        if (page.onCancel != null) page.onCancel(); else Close();
    }

    void MoveSelection(int delta)
    {
        int n = page.choices.Count;
        if (n == 0) return;
        for (int i = 0; i < n; i++)
        {
            selected = ((selected + delta) % n + n) % n;
            if (page.choices[selected].enabled) return;
        }
    }

    void Show(Page p)
    {
        if (!IsOpen)
        {
            prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;          // 開いている間は、ゲームの時間を止める
            IsOpen = true;
            MoleInput.InputBlocked = true; // モグラの操作を受け付けない
        }
        page = p;
        selected = 0;
        while (selected < p.choices.Count && !p.choices[selected].enabled) selected++;
        if (selected >= p.choices.Count) selected = 0;
    }

    // ---------------- スタート地点(初期位置)。ごはんが食べられる ----------------

    bool NearStartPoint()
    {
        return Vector2.Distance(mole.transform.position, mgr.InitialPosition) <= mgr.interactRadius;
    }

    static string FormatSeconds(float s)
    {
        int t = Mathf.RoundToInt(s);
        if (t < 60) return t + "秒";
        return (t % 60 == 0) ? (t / 60) + "分" : (t / 60) + "分" + (t % 60) + "秒";
    }

    static string MealNote(MealDef m)
    {
        return "最大スタミナ +" + Mathf.RoundToInt(m.staminaBonus) + "  /  " + FormatSeconds(m.durationSeconds) + "つづく";
    }

    void ShowStartPoint()
    {
        var p = new Page { title = "スタート地点" };
        p.lines.Add("大木の下の、はじまりの空洞です。");
        p.lines.Add("ごはんを食べると、しばらくのあいだ、最大スタミナが増えます。");
        AddMealStatus(p);
        p.choices.Add(C("ごはんを食べる", ShowMealSelect, meals != null && meals.Count > 0, "効果は、時間がたつと切れます"));
        p.choices.Add(C("やめる", Close));
        p.onCancel = Close;
        Show(p);
    }

    void AddMealStatus(Page p)
    {
        if (mole.MealLabel != null)
            p.lines.Add("いまの効果: " + mole.MealLabel + "  最大スタミナ +" + Mathf.CeilToInt(mole.BoostBonus) + "  (あと" + FormatSeconds(mole.BoostTimeRemaining) + ")");
    }

    void ShowMealSelect()
    {
        var p = new Page { title = "なにを食べますか?" };
        p.lines.Add("食べると、スタミナも満タンになります。");
        p.lines.Add("効果中にもう一度食べると、新しい方の効果になります(重なりません)。");
        AddMealStatus(p);
        for (int i = 0; i < meals.Count; i++)
        {
            var m = meals[i];
            p.choices.Add(C(m.name, () => EatMeal(m), true, MealNote(m) + "  ―  " + m.description));
        }
        p.choices.Add(C("もどる", ShowStartPoint));
        p.onCancel = ShowStartPoint;
        Show(p);
    }

    void EatMeal(MealDef m)
    {
        mole.SetMealBoost(m.name, m.staminaBonus, m.durationSeconds);
        Close();
        Toast(m.name + "を食べた! 最大スタミナ +" + Mathf.RoundToInt(m.staminaBonus) + "(" + FormatSeconds(m.durationSeconds) + ")");
    }

    void LeaveTree()
    {
        Close();
        if (mole == null || mgr == null) return;
        // 大木から離れて、初期位置まで自動で歩く。土でふさがっていて進めないときは、初期位置へ移す
        mole.AutoWalkTo(mgr.InitialPosition, () => { if (!IsOpen) mgr.ReturnToInitial(); });
    }

    void Close()
    {
        page = null;
        if (IsOpen)
        {
            Time.timeScale = prevTimeScale;
            IsOpen = false;
            MoleInput.InputBlocked = false;
        }
    }

    public void Toast(string text, float seconds = 3.5f)
    {
        toastText = text;
        toastUntil = Time.unscaledTime + seconds;
    }

    static Choice C(string label, Action onChoose, bool enabled = true, string note = null)
    {
        return new Choice { label = label, onChoose = onChoose, enabled = enabled, note = note };
    }

    // ---------------- 画面(ページ) ----------------

    /// <summary>拠点の外: 「拠点を作成する」/「帰還する」の2択。</summary>
    void ShowMain()
    {
        var p = new Page { title = "どうしますか?" };
        p.lines.Add("いまの場所に拠点を作るか、初期位置へ帰還できます。");
        p.lines.Add("拠点: " + mgr.Count + " / " + mgr.maxBases);
        p.choices.Add(C("拠点を作成する", ShowConfirmBuild, true, "休息とセーブができる空洞を作る"));
        p.choices.Add(C(ReturnLabel(), () => ShowConfirmReturn(ShowMain), true, ReturnNote()));
        Show(p);
    }

    void ShowConfirmBuild()
    {
        var p = new Page { title = "拠点を作成しますか?" };
        p.lines.Add("いまの場所に、休息とセーブができる空洞(拠点)を作ります。");
        p.lines.Add("拠点は最大" + mgr.maxBases + "個までです。(いま " + mgr.Count + "個)");
        p.choices.Add(C("はい", OnBuildYes));
        p.choices.Add(C("いいえ", ShowMain));
        p.onCancel = ShowMain;
        Show(p);
    }

    void OnBuildYes()
    {
        Vector2 pos = mole.transform.position;
        string reason;

        // 地表の近さ・地形の端・障害物
        if (!mgr.CheckTerrainRules(pos, out reason))
        {
            ShowInfo("拠点を作成できません", reason, ShowMain);
            return;
        }

        if (!mgr.IsFull)
        {
            if (!mgr.CheckDistance(pos, -1, out reason))
            {
                ShowInfo("拠点を作成できません", reason, ShowMain);
                return;
            }
            var site = mgr.Build(pos);
            Close();
            Toast(site.Name + "を作成しました。拠点の中でBキーを押すと、休息・セーブ・ワープができます。");
            return;
        }

        ShowFullChoice(pos);
    }

    /// <summary>枠がいっぱいのとき: 古い拠点を選んで削除して作る / 作らない。</summary>
    void ShowFullChoice(Vector2 pos)
    {
        var p = new Page { title = "拠点の枠がいっぱいです" };
        p.lines.Add("拠点は最大" + mgr.maxBases + "個までで、いまは枠が全部埋まっています。");
        p.lines.Add("古い拠点を1つ削除すると、ここに新しい拠点を作れます。");
        p.lines.Add("削除する拠点を選ぶか、作らないを選んでください。");

        var list = new List<BaseSite>(mgr.Bases);
        list.Sort((a, b) => a.seq.CompareTo(b.seq)); // 古い順
        for (int i = 0; i < list.Count; i++)
        {
            var target = list[i];
            string reason;
            bool ok = mgr.CheckDistance(pos, target.slot, out reason);

            string age = i == 0 ? "一番古い" : (i == list.Count - 1 ? "一番新しい" : "");
            string note = (age.Length > 0 ? age + " / " : "") + "深さ " + mgr.DepthAt(target.position).ToString("0.0");
            if (!ok) note = "削除しても、残る拠点に近すぎるため、ここには作れません";

            p.choices.Add(C(target.Name + "を削除して、ここに作る", () =>
            {
                mgr.Delete(target);
                var site = mgr.Build(pos);
                Close();
                Toast(target.Name + "を削除して、" + site.Name + "を作成しました。");
            }, ok, note));
        }
        p.choices.Add(C("作らない(もどる)", ShowMain));
        p.onCancel = ShowMain;
        Show(p);
    }

    /// <summary>説明だけの画面(作れない理由など)。</summary>
    void ShowInfo(string title, string text, Action back)
    {
        var p = new Page { title = title };
        p.lines.Add(text);
        p.choices.Add(C("もどる", back));
        p.onCancel = back;
        Show(p);
    }

// 帰還 = クエストをリタイア(達成前)。達成済みなら、報告に戻る帰還。
    bool ReturnGivesUp()
    {
        var q = QuestManager.Instance;
        return q != null && q.Accepted && !q.CanReport;
    }

    string ReturnLabel() { return ReturnGivesUp() ? "帰還する(クエストをリタイア)" : "帰還する"; }

    string ReturnNote()
    {
        if (ReturnGivesUp()) return "達成前に帰還すると、クエストをリタイアしたことになります";
        var q = QuestManager.Instance;
        return q != null && q.CanReport ? "大木に報告できます" : "初期位置に戻る";
    }

void ShowConfirmReturn(Action back)
    {
        var q = QuestManager.Instance;
        bool giveUp = ReturnGivesUp();
        var p = new Page { title = giveUp ? "クエストをリタイアして帰還しますか?" : "帰還しますか?" };
        p.lines.Add("初期位置(大木の下)に戻ります。");

        if (q != null && q.Accepted)
        {
            if (q.CanReport) p.lines.Add("クエストは達成済みです。帰還して、大木に報告しましょう。");
            else
            {
                p.lines.Add("★ いま帰還すると、「" + q.CurrentTitle + "」はリタイアになります。大丈夫ですか?");
                p.lines.Add("集めたかけらは、持ったままです。もう一度、大木で受け直せます。");
            }
        }

        p.choices.Add(C(giveUp ? "リタイアして帰還する" : "はい", () =>
        {
            if (giveUp) q.Abandon();
            mgr.ReturnToInitial();
            Close();
            Toast("初期位置に帰還しました。" + (q != null && q.CanReport ? "大木で B / × を押すと、報告できます。" : ""));
        }));
        p.choices.Add(C(giveUp ? "やめる(クエストを続ける)" : "いいえ", back));
        p.onCancel = back;
        Show(p);
    }

    /// <summary>拠点の中: 休息 / セーブ / ワープ / 帰還。</summary>
    void ShowBaseMain(BaseSite site)
    {
        var p = new Page { title = site.Name + "の中です" };
        p.lines.Add("休息するとスタミナが回復します。セーブすると、この拠点がリスタートの開始地点に選べるようになります。");
        p.choices.Add(C("休息する(スタミナ回復)", () =>
        {
            mgr.Rest();
            Close();
            Toast("休息しました。スタミナが回復しました。");
        }, true, mgr.restResetsDepletion ? "スタミナを全回復し、切れた回数もリセット" : "スタミナを全回復"));
        p.choices.Add(C("セーブする", () =>
        {
            if (mgr.Save(site)) ShowSaveDone(site);
            else ShowInfo("セーブできませんでした", "保存先に書き込めませんでした。", () => ShowBaseMain(site));
        }, true, "拠点の位置を保存する"));
        p.choices.Add(C("他の拠点へワープする", () => ShowWarp(site), mgr.Count >= 2,
            mgr.Count >= 2 ? "ほかの拠点へ移動する" : "他の拠点がありません"));
        p.choices.Add(C(ReturnLabel(), () => ShowConfirmReturn(() => ShowBaseMain(site)), true, ReturnNote()));
        Show(p);
    }

    void ShowWarp(BaseSite from)
    {
        var p = new Page { title = "どの拠点へワープしますか?" };
        var list = new List<BaseSite>(mgr.Bases);
        list.Sort((a, b) => a.slot.CompareTo(b.slot));
        for (int i = 0; i < list.Count; i++)
        {
            var target = list[i];
            if (target == from) continue;
            p.choices.Add(C(target.Name + "へワープ", () =>
            {
                mgr.WarpTo(target);
                Close();
                Toast(target.Name + "へワープしました。");
            }, true, "深さ " + mgr.DepthAt(target.position).ToString("0.0")));
        }
        p.choices.Add(C("もどる", () => ShowBaseMain(from)));
        p.onCancel = () => ShowBaseMain(from);
        Show(p);
    }

    /// <summary>セーブ後: 「このまま続ける」/「リスタートする」の2択。</summary>
    void ShowSaveDone(BaseSite site)
    {
        var p = new Page { title = "セーブしました" };
        p.lines.Add(site.Name + "でセーブしました。次回は、拠点か初期位置から再開できます。");
        p.lines.Add("このまま続けますか? リスタートしますか?");
        p.choices.Add(C("このまま続ける", Close));
        p.choices.Add(C("リスタートする", () => ShowRestartSelect(site), true, "ゲームを読み込み直して、始める場所を選ぶ"));
        p.onCancel = Close;
        Show(p);
    }

    /// <summary>リスタート: 初期位置 / 作成した各拠点 から、始める場所を選ぶ。</summary>
    void ShowRestartSelect(BaseSite site)
    {
        var p = new Page { title = "どこからスタートしますか?" };
        p.lines.Add("リスタートすると、ゲームを読み込み直します。");
        p.lines.Add("(掘った地形は元に戻り、セーブした拠点の空洞だけが残ります)");
        p.choices.Add(C("初期位置からスタート", () => mgr.RestartFrom(-1)));
        var list = new List<BaseSite>(mgr.Bases);
        list.Sort((a, b) => a.slot.CompareTo(b.slot));
        for (int i = 0; i < list.Count; i++)
        {
            var target = list[i];
            string note = "深さ " + mgr.DepthAt(target.position).ToString("0.0") + (target.slot == mgr.LastSavedSlot ? " / 最後にセーブした拠点" : "");
            p.choices.Add(C(target.Name + "からスタート", () => mgr.RestartFrom(target.slot), true, note));
        }
        p.choices.Add(C("もどる", () => ShowSaveDone(site)));
        p.onCancel = () => ShowSaveDone(site);
        Show(p);
    }

    /// <summary>ゲーム開始時: セーブした拠点があるとき、どこから始めるかを選ぶ。(読み込み直しはしない)</summary>
    public void ShowStartSelect()
    {
        if (mgr == null) mgr = BaseManager.Instance;
        if (mgr == null) return;
        var p = new Page { title = "どこからスタートしますか?" };
        p.lines.Add("セーブした拠点か、初期位置から始められます。");
        p.choices.Add(C("初期位置からスタート", () => { mgr.ReturnToInitial(); Close(); }));
        var list = new List<BaseSite>(mgr.Bases);
        list.Sort((a, b) => a.slot.CompareTo(b.slot));
        for (int i = 0; i < list.Count; i++)
        {
            var target = list[i];
            string note = "深さ " + mgr.DepthAt(target.position).ToString("0.0") + (target.slot == mgr.LastSavedSlot ? " / 最後にセーブした拠点" : "");
            p.choices.Add(C(target.Name + "からスタート", () => { mgr.WarpTo(target); Close(); }, true, note));
        }
        p.onCancel = Close; // 初期位置のまま始める
        Show(p);
    }

    // ---------------- 描画 ----------------

    void OnGUI()
    {
        GUI.depth = -1500; // HUDより手前、デバッグパネルより奥
        DrawToast();
        if (page == null) return;

        var ev = Event.current;
        var prev = GUI.color;

        // 画面全体を暗くする
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;

        float W = Mathf.Min(UiText.Px(940), Screen.width - UiText.Px(40));
        float pad = UiText.Px(28);
        float titleH = UiText.Line(32);
        float lineH = UiText.Line(20);
        float gap = UiText.Px(10);
        float hintH = UiText.Line(15);

        // 本文を、幅に合わせて折り返す
        int maxChars = Mathf.Max(8, Mathf.FloorToInt((W - pad * 2f) / UiText.Px(20)));
        var wrapped = new List<string>();
        for (int i = 0; i < page.lines.Count; i++) Wrap(page.lines[i], maxChars, wrapped);

        // 選択肢の高さ(補足があるものは高め)
        var rowHs = new float[page.choices.Count];
        float rowsTotal = 0f;
        for (int i = 0; i < rowHs.Length; i++)
        {
            rowHs[i] = string.IsNullOrEmpty(page.choices[i].note) ? UiText.Px(56) : UiText.Px(82);
            rowsTotal += rowHs[i] + gap;
        }

        float H = pad + titleH + UiText.Px(8) + wrapped.Count * lineH + UiText.Px(14) + rowsTotal + hintH + pad * 0.6f;
        var panel = new Rect((Screen.width - W) * 0.5f, Mathf.Max(UiText.Px(10), (Screen.height - H) * 0.5f), W, H);

        // 枠と背景
        Fill(new Rect(panel.x - 3, panel.y - 3, panel.width + 6, panel.height + 6), new Color(0.98f, 0.68f, 0.18f, 0.9f));
        Fill(panel, new Color(0.09f, 0.08f, 0.07f, 0.97f));

        float y = panel.y + pad;
        UiText.Label(new Rect(panel.x + pad, y, W - pad * 2f, titleH), page.title, 32, new Color(1f, 0.9f, 0.6f));
        y += titleH + UiText.Px(8);
        for (int i = 0; i < wrapped.Count; i++)
        {
            UiText.Label(new Rect(panel.x + pad, y, W - pad * 2f, lineH), wrapped[i], 20, new Color(0.95f, 0.95f, 0.95f));
            y += lineH;
        }
        y += UiText.Px(14);

        for (int i = 0; i < page.choices.Count; i++)
        {
            var c = page.choices[i];
            var rect = new Rect(panel.x + pad, y, W - pad * 2f, rowHs[i]);
            bool hover = rect.Contains(ev.mousePosition);
            bool sel = i == selected;

            if (ev.type == EventType.Repaint)
            {
                Color bg = !c.enabled ? new Color(0.16f, 0.16f, 0.16f, 1f)
                         : sel ? new Color(0.98f, 0.68f, 0.18f, 0.30f)
                         : hover ? new Color(1f, 1f, 1f, 0.12f)
                         : new Color(1f, 1f, 1f, 0.06f);
                Fill(rect, bg);
                if (sel && c.enabled)
                {
                    float t = Mathf.Max(2f, UiText.Px(3));
                    var oc = new Color(0.98f, 0.68f, 0.18f, 1f);
                    Fill(new Rect(rect.x, rect.y, rect.width, t), oc);
                    Fill(new Rect(rect.x, rect.yMax - t, rect.width, t), oc);
                    Fill(new Rect(rect.x, rect.y, t, rect.height), oc);
                    Fill(new Rect(rect.xMax - t, rect.y, t, rect.height), oc);
                }
                Color tc = !c.enabled ? new Color(0.5f, 0.5f, 0.5f) : c.color;
                bool hasNote = !string.IsNullOrEmpty(c.note);
                float lx = rect.x + UiText.Px(18);
                if (hasNote)
                {
                    UiText.Label(new Rect(lx, rect.y + UiText.Px(8), rect.width - UiText.Px(30), UiText.Line(22)), (sel ? "▶ " : "　 ") + c.label, 22, tc);
                    UiText.Label(new Rect(lx + UiText.Px(30), rect.y + UiText.Px(8) + UiText.Line(22), rect.width - UiText.Px(60), UiText.Line(16)), c.note, 16,
                        c.enabled ? new Color(0.78f, 0.78f, 0.78f) : new Color(0.62f, 0.5f, 0.5f));
                }
                else
                {
                    UiText.Label(new Rect(lx, rect.y, rect.width - UiText.Px(30), rect.height), (sel ? "▶ " : "　 ") + c.label, 22, tc, TextAnchor.MiddleLeft);
                }
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && c.enabled)
            {
                Choose(i);
                return; // 画面が切り替わるので、この描画はここまで
            }
            y += rowHs[i] + gap;
        }

        UiText.Label(new Rect(panel.x + pad, y, W - pad * 2f, hintH),
            "↑↓で選ぶ  /  Enter・Space・×ボタンで決定  /  Esc・○ボタンでもどる  (マウスでも操作できます)",
            15, new Color(0.7f, 0.7f, 0.7f));
    }

    void DrawToast()
    {
        if (string.IsNullOrEmpty(toastText) || Time.unscaledTime >= toastUntil) return;
        float big = UiText.Line(26);
        UiText.Label(new Rect(0, UiText.Px(255), Screen.width, big), toastText, 26, new Color(1f, 0.95f, 0.75f, 0.98f), TextAnchor.UpperCenter);
    }

    static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

static void Wrap(string s, int maxChars, List<string> outLines)
    {
        if (string.IsNullOrEmpty(s)) { outLines.Add(""); return; }

        int i = 0;
        while (i < s.Length)
        {
            int len = Mathf.Min(maxChars, s.Length - i);
            if (i + len < s.Length)
            {
                // 数字や言葉の途中で切れないよう、直前の区切り(。、スペース / ) 」)まで戻す
                for (int j = i + len - 1; j > i + len / 2; j--)
                {
                    char c = s[j];
                    if (c == '。' || c == '、' || c == ' ' || c == '/' || c == ')' || c == '」')
                    {
                        len = j + 1 - i;
                        break;
                    }
                }
            }
            outLines.Add(s.Substring(i, len).TrimEnd());
            i += len;
            while (i < s.Length && s[i] == ' ') i++;
        }
    }
}

/// <summary>ごはん1つぶんの設定(名前・説明・最大スタミナの増える量・続く時間)</summary>
[System.Serializable]
public class MealDef
{
    public string name;
    public string description;
    [Tooltip("最大スタミナが増える量(基本は200)")]
    public float staminaBonus = 40f;
    [Tooltip("効果が続く時間(秒)")]
    public float durationSeconds = 90f;
}
