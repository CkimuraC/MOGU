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
        Walls,           // 壁ぎわから: 壁ぎわの全部から穴が狭くなる
        Pile             // 積もる: 天井から砂がこぼれ落ちて、床に山を作りながら埋まる(斜面は約45度まで)
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

    [Header("Pile & Settle (積もる・なじむ)")]
    [Tooltip("埋め戻した直後の砂の色を、時間をかけて土の色になじませる")]
    public bool settleEnabled = false;
    [Range(0f, 1f)]
    [Tooltip("埋まった直後の砂の色の濃さ(0=土の色、1=砂の色)。ここから、Sand Tint の色へもどる")]
    public float freshSandTint = 0.55f;
    [Tooltip("砂の色が土になじむまでの時間(秒)")]
    public float settleSeconds = 20f;
    [Tooltip("「積もる」のとき、落ちてくる砂を見せる")]
    public bool showFalling = true;
    [Tooltip("砂が落ちる速さ(大きいほど速い)")]
    public float fallGravity = 20f;

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
                case FillStyle.Pile: return 8.0f;              // 積もる: 実測で調整
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
        if (terrain == null || hasDug == null) return;
        if (repairEnabled) Step(Now, Time.deltaTime, maxChunksPerFrame);
        ProcessGrains();
        TickSettle();
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
        int fronts = (fillStyle == FillStyle.Floor || fillStyle == FillStyle.Pile) ? 1 : 2; // 砂が迫ってくる向きの数(上下、または左右)
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
            LandPixels(candidates);
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
                if (any && fillStyle == FillStyle.Pile) supported = PileSupported(bx, by, g);
                if (!any || !supported) continue;

                // 埋めない場所か(粒の中心で判定)
                float wx = min.x + (bx + g * 0.5f) / ppu, wy = min.y + (by + g * 0.5f) / ppu;
                if (IsProtected(wx, wy, molePos)) continue;

                fillable++;
                if (fillStyle == FillStyle.Pile)
                {
                    if (Random.value >= p * PileWeight(wx)) continue;
                    if (pendingMask != null && pendingMask[by * width + bx]) continue; // すでに落ちてくる途中
                    ScheduleGrain(bx, by, ex, ey, g);
                    continue;
                }
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
            case FillStyle.Pile:
                return false;                                   // 積もるは、粒ごとに PileSupported で判定する
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

// ---------------- 積もる(Pile)・なじむ(Settle) ----------------

    class FallingGrain
    {
        public int[] idx;
        public int bx, by, ex, ey;
        public Vector2 from, to;
        public float t0, land;
        public Transform viz;
    }

    class Settle
    {
        public int[] idx;
        public float t0;
    }

    bool[] pendingMask;   // 落ちている途中の粒の画素(二重に予約しない)
    bool skipFall;
    float settleTick;
    Sprite dotSprite;
    readonly List<FallingGrain> grains = new List<FallingGrain>();
    readonly List<Settle> settles = new List<Settle>();
    readonly List<Transform> vizPool = new List<Transform>();
    readonly List<int> landing = new List<int>();

    /// <summary>粒(g×g)が積もれるか。真下と、左右の下がすべて土(または砂)なら、積もれる。これで、斜面が約45度より急にならない。</summary>
    bool PileSupported(int bx, int by, int g)
    {
        int cx = Mathf.Min(width - 1, bx + g / 2);
        return Solid(cx, by - 1) && Solid(cx - g, by - 1) && Solid(cx + g, by - 1);
    }

    /// <summary>山のできやすさ。横へゆるやかに変わるので、砂がこぼれる場所が偏って、山になる。</summary>
    float PileWeight(float wx)
    {
        float n = 0.5f + 0.5f * Mathf.Sin(wx * 0.8f + 1.7f);
        float m = 0.5f + 0.5f * Mathf.Sin(wx * 0.31f + 0.4f);
        return 0.4f + 0.6f * n * n * (0.4f + 0.6f * m);
    }

    void EnsurePending()
    {
        if (pendingMask == null) pendingMask = new bool[width * height];
    }

    Transform GetViz(int g)
    {
        Transform t;
        if (vizPool.Count > 0)
        {
            t = vizPool[vizPool.Count - 1];
            vizPool.RemoveAt(vizPool.Count - 1);
        }
        else
        {
            if (dotSprite == null)
            {
                var tx = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tx.SetPixel(0, 0, Color.white);
                tx.Apply(false);
                dotSprite = Sprite.Create(tx, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            var go = new GameObject("SandGrain");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.sortingOrder = 1;
            t = go.transform;
        }
        t.GetComponent<SpriteRenderer>().color = sandColor;
        t.localScale = Vector3.one * (g / terrain.pixelsPerUnit);
        t.gameObject.SetActive(true);
        return t;
    }

    void ReleaseViz(Transform t)
    {
        if (t == null) return;
        t.gameObject.SetActive(false);
        vizPool.Add(t);
    }

    /// <summary>粒を1つ、落とす。天井から落ちて、着いたところで埋まる。</summary>
    void ScheduleGrain(int bx, int by, int ex, int ey, int g)
    {
        var idx = new List<int>();
        for (int y = by; y < ey; y++)
            for (int x = bx; x < ex; x++)
            {
                int i = y * width + x;
                if (terrain.IsDugIndex(i)) idx.Add(i);
            }
        if (idx.Count == 0) return;

        if (skipFall || !showFalling) { candidates.AddRange(idx); return; }

        EnsurePending();
        for (int y = by; y < ey; y++)
            for (int x = bx; x < ex; x++) pendingMask[y * width + x] = true;

        float ppu = terrain.pixelsPerUnit;
        Vector2 min = terrain.MinWorld;
        int cx = Mathf.Min(width - 1, bx + g / 2);
        int yTop = ey;
        int limit = Mathf.Min(height - 1, ey + 96);
        while (yTop < limit && terrain.IsDugIndex(yTop * width + cx)) yTop++;

        var gr = new FallingGrain();
        gr.idx = idx.ToArray();
        gr.bx = bx; gr.by = by; gr.ex = ex; gr.ey = ey;
        gr.to = new Vector2(min.x + (bx + g * 0.5f) / ppu, min.y + (by + g * 0.5f) / ppu);
        gr.from = new Vector2(gr.to.x, Mathf.Max(gr.to.y + 0.3f, min.y + yTop / ppu - g * 0.5f / ppu));
        float h = Mathf.Max(0.1f, gr.from.y - gr.to.y);
        float dur = Mathf.Min(0.6f, Mathf.Sqrt(2f * h / Mathf.Max(1f, fallGravity)));
        gr.t0 = Time.time;
        gr.land = Time.time + dur;
        gr.viz = GetViz(g);
        gr.viz.position = new Vector3(gr.from.x, gr.from.y, 0f);
        grains.Add(gr);
    }

    /// <summary>落ちている粒を動かし、着いた粒を埋める。</summary>
    void ProcessGrains()
    {
        if (grains.Count == 0) return;
        float t = Time.time;
        landing.Clear();
        for (int k = grains.Count - 1; k >= 0; k--)
        {
            var gr = grains[k];
            float u = (t - gr.t0) / Mathf.Max(0.01f, gr.land - gr.t0);
            if (u >= 1f)
            {
                landing.AddRange(gr.idx);
                for (int y = gr.by; y < gr.ey; y++)
                    for (int x = gr.bx; x < gr.ex; x++) pendingMask[y * width + x] = false;
                ReleaseViz(gr.viz);
                grains.RemoveAt(k);
            }
            else if (gr.viz != null)
            {
                float e = u * u; // 落ちるとだんだん速くなる
                gr.viz.position = new Vector3(gr.from.x, Mathf.Lerp(gr.from.y, gr.to.y, e), 0f);
            }
        }
        if (landing.Count > 0)
        {
            LandPixels(landing);
            WakeAround(landing);
        }
    }

    /// <summary>埋め戻す。なじむ演出がオンなら、最初は砂の色を濃くして、あとで土の色へ戻す。</summary>
    void LandPixels(List<int> list)
    {
        float tint = settleEnabled ? freshSandTint : sandTint;
        int n = terrain.RefillPixels(list, sandColor, tint);
        if (settleEnabled && n > 0)
            settles.Add(new Settle { idx = list.ToArray(), t0 = Now });
    }

    /// <summary>砂の色を、少しずつ土の色へ戻す(0.25秒ごと)。</summary>
    void TickSettle()
    {
        if (settles.Count == 0) return;
        if (Time.time < settleTick) return;
        settleTick = Time.time + 0.25f;
        float now = Now;
        for (int k = settles.Count - 1; k >= 0; k--)
        {
            var s = settles[k];
            float u = (now - s.t0) / Mathf.Max(0.1f, settleSeconds);
            if (u >= 1f || !settleEnabled)
            {
                terrain.SetTintPixels(s.idx, sandColor, sandTint);
                settles.RemoveAt(k);
            }
            else
            {
                terrain.SetTintPixels(s.idx, sandColor, Mathf.Lerp(freshSandTint, sandTint, u));
            }
        }
    }


    // ---------------- デバッグ用 ----------------

    /// <summary>修復だけ、時間を seconds 秒ぶん進める(待たずに、埋まる様子を確かめる)。</summary>
public void FastForward(float seconds)
    {
        if (terrain == null || hasDug == null) return;
        const float step = 0.1f;
        int n = Mathf.CeilToInt(seconds / step);
        skipFall = true; // 早送り中は、落ちる演出を飛ばして、すぐ積もらせる
        for (int i = 0; i < n; i++)
        {
            timeOffset += step;
            Step(Now, step, int.MaxValue);
        }
        skipFall = false;
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

/// <summary>
    /// この円の中の穴を、「待たずに、いますぐ埋める対象」にする。(拠点を消したあとの空洞など)
    /// instant=true なら、その場で埋める。false なら、待ち時間なしで、砂が溜まり始める。
    /// モグラの周りは、埋もれないよう、残す(モグラが離れたら、埋まる)。
    /// 呼ぶ前に、その場所の「埋めない」登録は、外しておくこと。
    /// </summary>
    public int ReleaseArea(Vector2 center, float radiusUnits, bool instant)
    {
        if (terrain == null || hasDug == null) return 0;
        if (!repairEnabled) instant = true; // 自動修復を切っているときは、待っても埋まらないので、その場で埋める

        float ppu = terrain.pixelsPerUnit;
        Vector2 min = terrain.MinWorld;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - radiusUnits - min.x) * ppu), 0, width - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + radiusUnits - min.x) * ppu), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt((center.y - radiusUnits - min.y) * ppu), 0, height - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt((center.y + radiusUnits - min.y) * ppu), 0, height - 1);

        // 範囲にかかるブロックを、「掘ってから十分に時間がたった」状態にして、すぐ修復が始まるようにする
        int cs = Mathf.Max(8, chunkSizePx);
        float due = Now - repairDelaySeconds - 0.01f;
        for (int cy = y0 / cs; cy <= y1 / cs; cy++)
        {
            for (int cx = x0 / cs; cx <= x1 / cs; cx++)
            {
                int c = cy * cols + cx;
                if (lastDug[c] > due) lastDug[c] = due;
                hasDug[c] = true;
                sleepUntil[c] = 0f;
            }
        }
        if (!instant) return 0;

        // その場で埋める(円の中の、掘られた画素。モグラの周りと、埋めない場所は除く)
        Vector2 molePos = mole != null ? (Vector2)mole.transform.position : Vector2.zero;
        var list = new List<int>();
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int i = y * width + x;
                if (!terrain.IsDugIndex(i)) continue;
                float wx = min.x + (x + 0.5f) / ppu, wy = min.y + (y + 0.5f) / ppu;
                float dx = wx - center.x, dy = wy - center.y;
                if (dx * dx + dy * dy > radiusUnits * radiusUnits) continue;
                if (IsProtected(wx, wy, molePos)) continue;
                list.Add(i);
            }
        }
        return terrain.RefillPixels(list, sandColor, sandTint);
    }

}
