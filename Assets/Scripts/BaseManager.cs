using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>セーブファイルに入れる、拠点1つぶん。</summary>
[Serializable]
public class SavedBase
{
    public int slot;
    public int seq;
    public Vector2 position;
}

/// <summary>セーブファイルの中身。プロトタイプでは「拠点の位置」だけを保存する。</summary>
[Serializable]
public class BaseSaveData
{
    public int version = 1;
    public int seqCounter;
    public int lastSavedSlot;
    public List<SavedBase> bases = new List<SavedBase>();
}

/// <summary>拠点1つぶんの情報(実行中)。</summary>
public class BaseSite
{
    public int slot;            // 枠の番号(1, 2, …)。名前「拠点1」「拠点2」になる
    public int seq;             // 作った順(小さいほど古い)
    public Vector2 position;    // 空洞の中心
    public GameObject marker;   // 目印の絵
    public string Name { get { return "拠点" + slot; } }
}

/// <summary>
/// 拠点の管理。モグラの居場所に、休息とセーブができる小さな空洞(拠点)を作る。
///
/// ■ 役割
///   休息: スタミナが全回復する(切れた回数もリセットできる)
///   セーブ: 拠点の位置を保存する。リスタートのとき、開始地点に選べる
/// ■ ルール(仮。値は後で調整する)
///   ・地表に近い所には作れない / 他の拠点に近い所には作れない / 地形の端・障害物の近くには作れない
///   ・最大数は Max Bases(今は2)。いっぱいのときは、古い拠点を選んで削除すれば作れる
///   ・拠点の中でだけ、休息・セーブ・ワープができる
/// ■ メニューの画面は BaseMenu が担当する
/// </summary>
public class BaseManager : MonoBehaviour
{
    public static BaseManager Instance { get; private set; }

    public const int NoPendingStart = int.MinValue;
    /// <summary>リスタートのあとに始める場所。-1=初期位置、1以上=その番号の拠点、NoPendingStart=指定なし</summary>
    public static int PendingStartSlot = NoPendingStart;

    [Header("Rules (拠点のルール・仮)")]
    [Tooltip("拠点の最大数。アイテムなどで増やす想定")]
    public int maxBases = 2;
    [Tooltip("土の上端から、この深さより浅い場所には作れない(ユニット)。表土の層の厚み(6)に合わせた仮の値")]
    public float minDepthFromSurface = 6f;
    [Tooltip("他の拠点との最小の距離(ユニット)。画面の高さが約12ユニット")]
    public float minDistanceBetweenBases = 8f;
    [Tooltip("初期位置(ゲーム開始時のモグラの位置)からの最小の距離(ユニット)。初期位置の近くには作れない")]
    public float minDistanceFromInitial = 8f;
    [Tooltip("拠点を作るとき掘る空洞の半径(px)。開始時の空洞と同じ大きさ")]
    public float cavityRadiusPx = 36f;
    [Tooltip("拠点の中とみなす距離(ユニット)。この中でメニューを開くと、休息・セーブ・ワープが選べる")]
    public float interactRadius = 2f;
    [Tooltip("地形の端から、この距離より内側にしか作れない(ユニット)")]
    public float edgeMargin = 3f;
    [Tooltip("休息で、スタミナ切れの回数もリセットする")]
    public bool restResetsDepletion = true;

    [Header("Save (セーブ)")]
    [Tooltip("ゲーム開始時に、セーブした拠点を読み込む")]
    public bool loadSavedBasesOnLaunch = true;
    [Tooltip("ゲーム開始時に、セーブした拠点があれば「どこからスタートしますか?」を出す")]
    public bool showStartMenuOnLaunch = false;

    DirtTerrain terrain;
    MoleController mole;
    CameraFollow cam;
    Sprite markerSprite;
    readonly List<BaseSite> bases = new List<BaseSite>();
    int seqCounter;
    int lastSavedSlot;
    bool started;
    Vector2 initialPosition;

    public IReadOnlyList<BaseSite> Bases { get { return bases; } }
    public int Count { get { return bases.Count; } }
    public bool IsFull { get { return bases.Count >= maxBases; } }
    public Vector2 InitialPosition { get { return initialPosition; } }
    public int LastSavedSlot { get { return lastSavedSlot; } }
    public static string SavePath { get { return Path.Combine(Application.persistentDataPath, "bases.json"); } }
    public bool HasSaveFile { get { return File.Exists(SavePath); } }

    void Awake()
    {
        Instance = this;
        terrain = FindFirstObjectByType<DirtTerrain>();
        mole = FindFirstObjectByType<MoleController>();
        cam = FindFirstObjectByType<CameraFollow>();
        if (mole != null) initialPosition = mole.transform.position; // 開始時の位置を「初期位置」として覚える
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (started || terrain == null || mole == null) return;
        started = true; // 全ての Start が終わった、最初のフレームで1回だけ行う

        if (loadSavedBasesOnLaunch) LoadFromSave();
        ApplyPendingStart();
    }

    // ---------------- 場所の調べ方 ----------------

    /// <summary>土の上端からの深さ(ユニット)</summary>
    public float DepthAt(Vector2 p) { return terrain.TopY - p.y; }

    public BaseSite FindBySlot(int slot)
    {
        for (int i = 0; i < bases.Count; i++) if (bases[i].slot == slot) return bases[i];
        return null;
    }

    /// <summary>pos から radius 以内にある、いちばん近い拠点。なければ null。</summary>
    public BaseSite NearestBaseWithin(Vector2 pos, float radius)
    {
        BaseSite best = null;
        float bd = radius;
        for (int i = 0; i < bases.Count; i++)
        {
            float d = Vector2.Distance(bases[i].position, pos);
            if (d <= bd) { bd = d; best = bases[i]; }
        }
        return best;
    }

    // ---------------- 作れるかどうか ----------------

    /// <summary>地表・地形の端・障害物に関するルール(他の拠点との距離は見ない)。</summary>
    public bool CheckTerrainRules(Vector2 pos, out string reason)
    {
        reason = null;

        float depth = DepthAt(pos);
        if (depth < minDepthFromSurface)
        {
            reason = "地表に近すぎます。もっと深い場所で作ってください。(いまの深さ " + depth.ToString("0.0") +
                     " / 必要 " + minDepthFromSurface.ToString("0.0") + " 以上)";
            return false;
        }

        Rect r = terrain.WorldRect;
        if (pos.x < r.xMin + edgeMargin || pos.x > r.xMax - edgeMargin || pos.y < r.yMin + edgeMargin)
        {
            reason = "地形の端に近すぎます。もう少し中央寄りで作ってください。";
            return false;
        }

        // 初期位置に近すぎないか(拠点を削除しても変わらないルールなので、ここで見る)
        float dInit = Vector2.Distance(initialPosition, pos);
        if (dInit < minDistanceFromInitial)
        {
            reason = "初期位置に近すぎます。あと " + (minDistanceFromInitial - dInit).ToString("0.0") +
                     " 離れてください。(いまの距離 " + dInit.ToString("0.0") + " / 必要 " + minDistanceFromInitial.ToString("0.0") + ")";
            return false;
        }

        float cav = cavityRadiusPx / terrain.pixelsPerUnit;
        var obs = Obstacle.All;
        for (int i = 0; i < obs.Count; i++)
        {
            var o = obs[i];
            if (o != null && Vector2.Distance(o.transform.position, pos) < cav + o.radius + 0.3f)
            {
                reason = "近くに障害物があり、作れません。少し離れた場所で作ってください。";
                return false;
            }
        }
        return true;
    }

    /// <summary>他の拠点に近すぎないか。ignoreSlot の拠点は、削除する前提で数えない(-1で全部数える)。</summary>
    public bool CheckDistance(Vector2 pos, int ignoreSlot, out string reason)
    {
        reason = null;
        for (int i = 0; i < bases.Count; i++)
        {
            var b = bases[i];
            if (b.slot == ignoreSlot) continue;
            float d = Vector2.Distance(b.position, pos);
            if (d < minDistanceBetweenBases)
            {
                reason = "他の拠点(" + b.Name + ")に近すぎます。あと " + (minDistanceBetweenBases - d).ToString("0.0") +
                         " 離れてください。(いまの距離 " + d.ToString("0.0") + " / 必要 " + minDistanceBetweenBases.ToString("0.0") + ")";
                return false;
            }
        }
        return true;
    }

    // ---------------- 作る・消す ----------------

    int FirstFreeSlot()
    {
        for (int s = 1; s <= maxBases; s++) if (FindBySlot(s) == null) return s;
        return -1;
    }

    /// <summary>拠点を作る。ルールの確認は、呼ぶ側で済ませておく。空いている枠がなければ null。</summary>
    public BaseSite Build(Vector2 pos)
    {
        int slot = FirstFreeSlot();
        if (slot < 0) return null;
        return CreateSite(slot, ++seqCounter, pos);
    }

    public void Delete(BaseSite b)
    {
        if (b == null) return;
        if (b.marker != null) Destroy(b.marker);
        bases.Remove(b);
    }

    public void DeleteAll()
    {
        for (int i = bases.Count - 1; i >= 0; i--) Delete(bases[i]);
    }

    BaseSite CreateSite(int slot, int seq, Vector2 pos)
    {
        var site = new BaseSite { slot = slot, seq = seq, position = pos };
        EnsureCavity(pos);

        var go = new GameObject("Base_" + slot);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(pos.x + 0.6f, pos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetMarkerSprite();
        sr.sortingOrder = 9; // 暗闇(8)の上・モグラ(10)の下。暗闇の中でも目印が見える
        site.marker = go;

        bases.Add(site);
        return site;
    }

    /// <summary>拠点の空洞を掘る(スタミナは使わない)。重なる障害物は取り除く。</summary>
    void EnsureCavity(Vector2 p)
    {
        terrain.Dig(p, cavityRadiusPx);
        float rad = cavityRadiusPx / terrain.pixelsPerUnit + 0.3f;
        var list = new List<Obstacle>(Obstacle.All);
        for (int i = 0; i < list.Count; i++)
        {
            var o = list[i];
            if (o != null && Vector2.Distance(o.transform.position, p) < rad + o.radius) Destroy(o.gameObject);
        }
    }

    // ---------------- 拠点でできること ----------------

    /// <summary>休息: スタミナを全回復する(設定により、切れた回数もリセット)。</summary>
    public void Rest()
    {
        if (restResetsDepletion) mole.ResetDepletion();
        mole.DebugRefill();
    }

    public void TeleportMole(Vector2 p)
    {
        mole.transform.position = new Vector3(p.x, p.y, mole.transform.position.z);
        if (cam != null) cam.Snap();
    }

    /// <summary>他の拠点へワープする。</summary>
    public void WarpTo(BaseSite b)
    {
        EnsureCavity(b.position);
        TeleportMole(b.position);
    }

    /// <summary>帰還: 初期位置へ移動する(今は仮の動き)。</summary>
    public void ReturnToInitial()
    {
        terrain.Dig(initialPosition, mole.startCavityRadiusPx);
        TeleportMole(initialPosition);
    }

    // ---------------- セーブ・読み込み・リスタート ----------------

    /// <summary>いまの拠点の位置を保存する。at は、セーブした拠点。</summary>
    public bool Save(BaseSite at)
    {
        var data = new BaseSaveData { seqCounter = seqCounter, lastSavedSlot = at != null ? at.slot : 0 };
        for (int i = 0; i < bases.Count; i++)
            data.bases.Add(new SavedBase { slot = bases[i].slot, seq = bases[i].seq, position = bases[i].position });
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            lastSavedSlot = data.lastSavedSlot;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BaseManager] セーブに失敗: " + e.Message);
            return false;
        }
    }

    public static BaseSaveData ReadSave()
    {
        try
        {
            if (!File.Exists(SavePath)) return null;
            return JsonUtility.FromJson<BaseSaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BaseManager] セーブの読み込みに失敗: " + e.Message);
            return null;
        }
    }

    public void DeleteSaveFile()
    {
        try { if (File.Exists(SavePath)) File.Delete(SavePath); }
        catch (Exception e) { Debug.LogWarning("[BaseManager] セーブの削除に失敗: " + e.Message); }
    }

    void LoadFromSave()
    {
        var data = ReadSave();
        if (data == null || data.bases == null) return;
        seqCounter = data.seqCounter;
        lastSavedSlot = data.lastSavedSlot;
        for (int i = 0; i < data.bases.Count; i++)
        {
            if (bases.Count >= maxBases) break;
            var sb = data.bases[i];
            if (FindBySlot(sb.slot) != null) continue;
            CreateSite(sb.slot, sb.seq, sb.position);
        }
    }

    /// <summary>リスタート: ゲームを読み込み直して、指定の場所から始める。-1=初期位置、1以上=拠点の番号。</summary>
    public void RestartFrom(int slot)
    {
        PendingStartSlot = slot;
        MoleInput.InputBlocked = false;
        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
    }

    void ApplyPendingStart()
    {
        int slot = PendingStartSlot;
        PendingStartSlot = NoPendingStart;

        if (slot == NoPendingStart)
        {
            if (showStartMenuOnLaunch && bases.Count > 0)
            {
                var menu = FindFirstObjectByType<BaseMenu>();
                if (menu != null) menu.ShowStartSelect();
            }
            return;
        }
        if (slot == -1) { ReturnToInitial(); return; }
        var b = FindBySlot(slot);
        if (b != null) WarpTo(b);
    }

    // ---------------- デバッグ用 ----------------

    /// <summary>ルールを無視して、いまの場所に拠点を作る。いっぱいなら、一番古い拠点を消す。</summary>
    public BaseSite DebugBuildHere()
    {
        Vector2 pos = mole.transform.position;
        if (IsFull)
        {
            BaseSite oldest = null;
            for (int i = 0; i < bases.Count; i++) if (oldest == null || bases[i].seq < oldest.seq) oldest = bases[i];
            Delete(oldest);
        }
        return Build(pos);
    }

    // ---------------- 見た目 ----------------

    Sprite GetMarkerSprite()
    {
        if (markerSprite != null) return markerSprite;

        // 仮の目印: たき火(やわらかい光 + 薪 + 炎)
        const int s = 32;
        float c = s * 0.5f;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.Clamp01(1f - d / 15f);
                a = a * a * 0.5f;
                Color32 col = new Color32(255, 170, 70, (byte)(a * 255f));

                // 薪(×の形)
                float ly = y + 0.5f - 9f;
                if (ly > -3.5f && ly < 3.5f && Mathf.Abs(dx) < 9f &&
                    (Mathf.Abs(dx - ly * 2f) < 2f || Mathf.Abs(dx + ly * 2f) < 2f))
                    col = new Color32(110, 70, 40, 255);

                // 炎(しずく形)
                float fy = (y + 0.5f - 10f) / 11f;
                if (fy > 0f && fy < 1f)
                {
                    float hw = 5.5f * Mathf.Sin(fy * Mathf.PI) * (1f - fy * 0.35f);
                    if (Mathf.Abs(dx) < hw)
                        col = (Mathf.Abs(dx) < hw * 0.5f && fy < 0.7f) ? new Color32(255, 235, 120, 255) : new Color32(255, 140, 40, 255);
                }
                px[y * s + x] = col;
            }
        }
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels32(px);
        tex.Apply(false);
        markerSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), terrain != null ? terrain.pixelsPerUnit : 32f);
        return markerSprite;
    }

    /// <summary>拠点の名前を、目印の上に出す。</summary>
    void OnGUI()
    {
        if (terrain == null || bases.Count == 0) return;
        var c = Camera.main;
        if (c == null) return;
        GUI.depth = 100; // 他のGUIより奥

        for (int i = 0; i < bases.Count; i++)
        {
            var b = bases[i];
            Vector3 sp = c.WorldToScreenPoint(new Vector3(b.position.x + 0.6f, b.position.y + 0.75f, 0f));
            if (sp.z < 0f) continue;
            float x = sp.x, y = Screen.height - sp.y;
            if (x < -300f || x > Screen.width + 300f || y < -60f || y > Screen.height + 60f) continue;
            float w = UiText.Px(240), h = UiText.Line(16);
            UiText.Label(new Rect(x - w * 0.5f, y - h, w, h), b.Name, 16, new Color(1f, 0.9f, 0.6f), TextAnchor.UpperCenter);
        }
    }
}
