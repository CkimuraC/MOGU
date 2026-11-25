using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 掘った穴の自動修復。時間がたつと、穴が砂で埋まっていく。
///
/// ■ 動き
///   1. 土を掘ると、その場所が「掘った時刻」を覚える(地形を小さなブロックに分けて管理)。
///   2. 掘ってから Repair Delay Seconds たつと、そのブロックの修復が始まる。
///   3. 修復中は、穴の「砂が乗れる所」が少しずつ砂(土)に戻る。
///        ・Fill From Floor がオン: 砂は下から溜まる。穴の床が、じわじわ上がっていく。
///        ・オフ: 壁ぎわの全部から、穴がじわじわ狭くなる。
///   4. 穴(モグラの通路の幅)が完全にふさがるまでの時間が Fill Duration Seconds。
///   5. 埋め戻された所は、砂の色が少し混ざった色になる(Sand Tint で濃さを変える)。
///
/// ■ 埋まらない場所
///   ・モグラの周り(Protect Radius Around Mole)。埋もれて動けなくなるのを防ぐ
///   ・開始時の空洞、拠点の空洞(AddProtected で登録する)
///
/// ■ 時間の調整はデバッグパネル(F2)の「穴の修復」で行う。
/// </summary>
public class TerrainRepair : MonoBehaviour
{
    public static TerrainRepair Instance { get; private set; }

    /// <summary>砂の埋まり方。</summary>
    public enum FillStyle
    {
        FloorAndCeiling, // 上下から: 床と天井の両方から砂が溜まり、穴が真ん中で閉じる
        Floor,           // 下から: 床だけから砂が溜まる
        Walls            // 壁ぎわから: 壁ぎわの全部から穴が狭くなる
    }

    class Circle
    {
        public int id;
        public Vector2 center;
        public float radius;
    }

    [Header("On/Off")]
    [Tooltip("オフにすると、穴は埋まらない")]
    public bool repairEnabled = true;

    [Header("Time (修復の時間)")]
    [Tooltip("掘ってから、埋まり始めるまでの時間(秒)")]
    public float repairDelaySeconds = 60f;
    [Tooltip("埋まり始めてから、モグラの通路(幅)が完全にふさがるまでの目安の時間(秒)")]
    public float fillDurationSeconds = 10f;

    [Header("Look (砂の見た目)")]
    [Tooltip("埋まり方。上下から=床と天井の両方から砂が溜まる / 下から=床だけ / 壁ぎわから=壁ぎわの全部から穴が狭くなる")]
    public FillStyle fillStyle = FillStyle.FloorAndCeiling;
    [Tooltip("埋め戻された所に混ぜる、砂の色")]
    public Color sandColor = new Color32(214, 184, 122, 255);
    [Range(0f, 1f)]
    [Tooltip("砂の色の混ぜ具合。0=元の土の色のまま、1=砂の色")]
    public float sandTint = 0f;
    [Range(1, 8)]
    [Tooltip("砂の粒の大きさ(画素)。大きいほど、四角いかたまりで埋まる。1・2・4・8のどれかに丸められる")]
    public int grainPx = 4;

    [Header("Protection (埋まらない場所)")]
    [Tooltip("モグラの周り(ユニット)は埋めない。埋もれて動けなくなるのを防ぐ。0で無効")]
    public float protectRadiusAroundMole = 2.5f;
    [Tooltip("開始時の空洞は、埋めない")]
    public bool protectInitialCavity = true;

    [Header("Performance")]
    [Tooltip("管理するブロックの大きさ(px)")]
    public int chunkSizePx = 32;
    [Tooltip("1フレームに調べるブロックの数の上限")]
    public int maxChunksPerFrame = 160;

    [Header("Advanced (実測で決めた補正)")]
    [Tooltip("埋まる速さの補正。砂は1粒ずつ、ランダムに溜まるので、最後の粒が埋まるまでに遅れが出る。設定の秒数が、実際にふさがる時間の目安になるよう、実測で決めた値。ふだんは触らない")]
    public float fillCalibration = 1f;

    // (埋まる速さの補正は、下の Fill Calibration で決める)
    // 砂は1粒ずつ、ランダムに溜まるため、実際にふさがるまでの時間は、補正なしだと設定より長くなる。
    // (旧: 固定の補正。今は fillCalibration と、粒・向きの数に応じた計算に置き換えた)

    DirtTerrain terrain;
    MoleController mole;

    int cols, rows, width, height;
    float[] lastDug;      // ブロックごとの「最後に掘った時刻」
    float[] lastVisit;    // ブロックごとの「最後に調べた時刻」
    float[] sleepUntil;   // 埋められる所がないブロックは、この時刻まで調べない
    bool[] hasDug;        // 掘られた画素が残っているブロックか

    int cursor;
    float timeOffset;     // 「修復だけ時間を進める」ための分
    int nextId;
    readonly List<Circle> protectedAreas = new List<Circle>();
    readonly List<int> candidates = new List<int>();

    float Now { get { return Time.time + timeOffset; } }

    /// <summary>砂の粒の大きさ(画素)。ブロックの区切りに合わせ、1・2・4・8のどれかにする。</summary>
    int Grain { get { return grainPx <= 1 ? 1 : (grainPx <= 2 ? 2 : (grainPx <= 5 ? 4 : 8)); } }

    /// <summary>埋まり方ごとの補正。実測で決めた値(粒4px)。設定の秒数で、実際にふさがるようにする。</summary>
    float StyleCalibration
    {
        get
        {
            switch (fillStyle)
            {
                case FillStyle.FloorAndCeiling: return 2.7f; // 補正なしだと、設定の約2.7倍かかった
                case FillStyle.Floor: return 2.1f;           // 約2.1倍
                default: return 2.3f;                         // 壁ぎわ: 約2.3倍
            }
        }
    }

    /// <summary>掘った画素が残っているブロックの数(修復待ち・修復中)</summary>
    public int DugChunkCount
    {
        get
        {
            if (hasDug == null) return 0;
            int n = 0;
            for (int i = 0; i < hasDug.Length; i++) if (hasDug[i]) n++;
            return n;
        }
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        terrain = FindFirstObjectByType<DirtTerrain>();
        mole = FindFirstObjectByType<MoleController>();
        if (terrain == null) return;

        width = terrain.widthPx;
        height = terrain.heightPx;
        int cs = Mathf.Max(8, chunkSizePx);
        cols = Mathf.CeilToInt(width / (float)cs);
        rows = Mathf.CeilToInt(height / (float)cs);
        int total = cols * rows;
        lastDug = new float[total];
        lastVisit = new float[total];
        sleepUntil = new float[total];
        hasDug = new bool[total];

        terrain.Dug += OnDug;

        // 開始時の空洞は、いつまでも残す
        if (protectInitialCavity && mole != null)
            AddProtected(mole.transform.position, mole.startCavityRadiusPx / terrain.pixelsPerUnit + 0.25f);
    }

    void OnDestroy()
    {
        if (terrain != null) terrain.Dug -= OnDug;
        if (Instance == this) Instance = null;
    }

    // ---------------- 埋めない場所 ----------------

    /// <summary>この円の中は、埋め戻さない。あとで RemoveProtected で外せるよう、番号を返す。</summary>
    public int AddProtected(Vector2 center, float radiusUnits)
    {
        var c = new Circle { id = ++nextId, center = center, radius = radiusUnits };
        protectedAreas.Add(c);
        return c.id;
    }

    public void RemoveProtected(int id)
    {
        protectedAreas.RemoveAll(c => c.id == id);
    }

    bool IsProtected(float wx, float wy, Vector2 molePos)
    {
        if (protectRadiusAroundMole > 0f)
        {
            float dx = wx - molePos.x, dy = wy - molePos.y;
            if (dx * dx + dy * dy < protectRadiusAroundMole * protectRadiusAroundMole) return true;
        }
        for (int i = 0; i < protectedAreas.Count; i++)
        {
            var c = protectedAreas[i];
            float dx = wx - c.center.x, dy = wy - c.center.y;
            if (dx * dx + dy * dy < c.radius * c.radius) return true;
        }
        return false;
    }

    // ---------------- 掘った通知 ----------------

    void OnDug(int x0, int y0, int x1, int y1)
    {
        int cs = Mathf.Max(8, chunkSizePx);
        int cx0 = Mathf.Clamp(x0 / cs, 0, cols - 1), cx1 = Mathf.Clamp(x1 / cs, 0, cols - 1);
        int cy0 = Mathf.Clamp(y0 / cs, 0, rows - 1), cy1 = Mathf.Clamp(y1 / cs, 0, rows - 1);
        float now = Now;
        for (int cy = cy0; cy <= cy1; cy++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = cy * cols + cx;
                lastDug[c] = now;
                hasDug[c] = true;
                sleepUntil[c] = 0f;
            }
        }
    }

    // ---------------- 修復 ----------------

    void Update()
    {
        if (!repairEnabled || terrain == null || hasDug == null) return;
        Step(Now, Time.deltaTime, maxChunksPerFrame);
    }

    /// <summary>修復を1回ぶん進める。maxChunks は、今回調べるブロックの上限。</summary>
    void Step(float now, float dt, int maxChunks)
    {
        if (dt <= 0f && maxChunks != int.MaxValue) return;

        int total = cols * rows;
        int visited = 0, processed = 0;
        Vector2 molePos = mole != null ? (Vector2)mole.transform.position : Vector2.zero;
        float tunnelPx = mole != null ? mole.tunnelWidthPx : 24f;
        int g = Grain;
        int fronts = fillStyle == FillStyle.Floor ? 1 : 2; // 砂が迫ってくる向きの数(上下、または左右)
        // 1秒に、粒が何層ぶん埋まるか。通路の幅を、粒の大きさと向きの数で割って、設定の秒数で埋めきる速さにする。
        float rate = (tunnelPx / g / fronts) / Mathf.Max(0.1f, fillDurationSeconds) * fillCalibration * StyleCalibration;

        candidates.Clear();
        while (visited < total && processed < maxChunks)
        {
            int c = cursor;
            cursor = (cursor + 1) % total;
            visited++;

            if (!hasDug[c]) continue;
            float start = lastDug[c] + repairDelaySeconds;
            if (now < start) continue;          // まだ、埋まり始める時間ではない
            if (now < sleepUntil[c]) continue;  // 埋められる所がないので、少し休み中

            if (lastVisit[c] < start) lastVisit[c] = start;
            float dtc = Mathf.Min(2f, now - lastVisit[c]);
            lastVisit[c] = now;
            processed++;
            ScanChunk(c, now, dtc, rate, molePos);
        }

        if (candidates.Count > 0)
        {
            terrain.RefillPixels(candidates, sandColor, sandTint);
            WakeAround(candidates);
        }
    }

void ScanChunk(int c, float now, float dtc, float rate, Vector2 molePos)
    {
        int cs = Mathf.Max(8, chunkSizePx);
        int g = Grain;
        int cx = c % cols, cy = c / cols;
        int x0 = cx * cs, y0 = cy * cs;
        int x1 = Mathf.Min(width, x0 + cs), y1 = Mathf.Min(height, y0 + cs);

        float p = 1f - Mathf.Exp(-rate * dtc); // 砂が1粒ぶん溜まる確率(この間に)
        float ppu = terrain.pixelsPerUnit;
        Vector2 min = terrain.MinWorld;

        int dugCount = 0, fillable = 0;
        for (int by = y0; by < y1; by += g)
        {
            int ey = Mathf.Min(by + g, y1);
            for (int bx = x0; bx < x1; bx += g)
            {
                int ex = Mathf.Min(bx + g, x1);

                // この粒(g×g のブロック)の中に、掘られた画素があるか。砂が乗れる(土に接している)画素があるか。
                bool any = false, supported = false;
                for (int y = by; y < ey; y++)
                {
                    for (int x = bx; x < ex; x++)
                    {
                        if (!terrain.IsDugIndex(y * width + x)) continue;
                        dugCount++;
                        any = true;
                        if (!supported && CanFill(x, y)) supported = true;
                    }
                }
                if (!any || !supported) continue;

                // 埋めない場所か(粒の中心で判定)
                float wx = min.x + (bx + g * 0.5f) / ppu, wy = min.y + (by + g * 0.5f) / ppu;
                if (IsProtected(wx, wy, molePos)) continue;

                fillable++;
                if (Random.value >= p) continue;

                // 粒ごと埋める(粒の中の、掘られた画素すべて)
                for (int y = by; y < ey; y++)
                {
                    for (int x = bx; x < ex; x++)
                    {
                        int i = y * width + x;
                        if (terrain.IsDugIndex(i)) candidates.Add(i);
                    }
                }
            }
        }

        if (dugCount == 0) hasDug[c] = false;
        else if (fillable == 0) sleepUntil[c] = now + 1.5f; // 埋められる所がないなら、少し休む
    }

    /// <summary>その場所は「土」か。範囲の外は、上だけ空(地上)、それ以外は土として扱う。</summary>
    bool Solid(int x, int y)
    {
        if (x < 0 || x >= width || y < 0) return true;
        if (y >= height) return !terrain.topOpen;
        return !terrain.IsDugIndex(y * width + x);
    }

    /// <summary>この画素は、いま埋まれるか。</summary>
bool CanFill(int x, int y)
    {
        switch (fillStyle)
        {
            case FillStyle.Floor:
                return Solid(x, y - 1);                         // 下に土がある所に、砂が溜まる
            case FillStyle.FloorAndCeiling:
                return Solid(x, y - 1) || Solid(x, y + 1);     // 下か上に土がある所に、砂が溜まる(上下から埋まる)
            default:
                return Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1); // 壁ぎわから
        }
    }

    /// <summary>埋まった場所の周りのブロックを、起こす(新しく埋められる場所ができるため)。</summary>
    void WakeAround(List<int> filled)
    {
        int cs = Mathf.Max(8, chunkSizePx);
        for (int k = 0; k < filled.Count; k++)
        {
            int i = filled[k];
            int x = i % width, y = i / width;
            int cx = x / cs, cy = y / cs;
            Wake(cx, cy);
            int dx = x % cs, dy = y % cs;
            if (dx == 0) Wake(cx - 1, cy);
            if (dx == cs - 1) Wake(cx + 1, cy);
            if (dy == 0) Wake(cx, cy - 1);
            if (dy == cs - 1) Wake(cx, cy + 1);
        }
    }

    void Wake(int cx, int cy)
    {
        if (cx < 0 || cx >= cols || cy < 0 || cy >= rows) return;
        sleepUntil[cy * cols + cx] = 0f;
    }

    // ---------------- デバッグ用 ----------------

    /// <summary>修復だけ、時間を seconds 秒ぶん進める(待たずに、埋まる様子を確かめる)。</summary>
    public void FastForward(float seconds)
    {
        if (terrain == null || hasDug == null) return;
        const float step = 0.1f;
        int n = Mathf.CeilToInt(seconds / step);
        for (int i = 0; i < n; i++)
        {
            timeOffset += step;
            Step(Now, step, int.MaxValue);
        }
    }

    /// <summary>埋まらない場所以外の、掘った場所をすべて、いますぐ埋める。</summary>
    public int FillAllNow()
    {
        if (terrain == null) return 0;
        Vector2 molePos = mole != null ? (Vector2)mole.transform.position : Vector2.zero;
        float ppu = terrain.pixelsPerUnit;
        Vector2 min = terrain.MinWorld;
        var list = new List<int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (!terrain.IsDugIndex(i)) continue;
                if (IsProtected(min.x + (x + 0.5f) / ppu, min.y + (y + 0.5f) / ppu, molePos)) continue;
                list.Add(i);
            }
        }
        int n = terrain.RefillPixels(list, sandColor, sandTint);
        if (hasDug != null) for (int c = 0; c < hasDug.Length; c++) sleepUntil[c] = 0f;
        return n;
    }
}
