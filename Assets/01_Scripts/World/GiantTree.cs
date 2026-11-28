using UnityEngine;

/// <summary>
/// 地上にそびえる大木。ここから、クエストを受けられるようにする予定。
///
/// ■ 絵
///   いまは、コードで描いた「仮の絵」(ドット絵風)。丸く茂った葉のかたまり・太い幹・地面に出た根。
///   本物の絵ができたら、Sprite Override に入れると、差し替えられる(足もとが、大木の中央下になるようにする)。
///
/// ■ 大きさ・位置の調整
///   デバッグパネル(F2)の「大木」で、再生中に変えられる。インスペクターでも同じ。
///   ・Scale: 大きさの倍率(1で、高さ約6ユニット)
///   ・Offset X: 地表の中央から、左右にずらす距離
///   ・Offset Y: 地面の高さから、上下にずらす距離
///
/// ■ クエスト(準備中)
///   地上で、大木の近くにいると、Bキー(パッドは×)で話しかけられる。
///   いまは「受けられるクエストがありません」と出るだけ(中身は、これから決める)。
/// </summary>
public class GiantTree : MonoBehaviour
{
    public static GiantTree Instance { get; private set; }

    [Header("Size / Position (大きさ・位置)")]
    [Tooltip("大きさの倍率。1で、高さ約6ユニット(画面の高さの半分くらい)")]
    public float scale = 1.4f;
    [Tooltip("地表の中央から、左右にずらす距離(ユニット)")]
    public float offsetX = 8f;
    [Tooltip("地面の高さから、上下にずらす距離(ユニット)。プラスで上へ")]
    public float offsetY = -0.1f;
    [Tooltip("モグラより後ろに描かれるよう、3〜7にする(草のふさは2)")]
    public int sortingOrder = 3;
    public bool flipX = false;
    public bool visible = true;

    [Header("Look (見た目)")]
    [Tooltip("葉のかたまりの形を決める乱数の種。変えると、形が少し変わる")]
    public int seed = 2026;
    [Tooltip("本物の絵ができたら、ここに入れる。足もとが、画像の中央下になるようにする")]
    public Sprite spriteOverride;

    [Header("Quest (クエスト)")]
    [Tooltip("地上で、大木の中心から、この距離(ユニット)以内なら、話しかけられる")]
    public float interactRadius = 3.5f;
    [Tooltip("「はなれる」を選んだあと、モグラが自動で歩いて止まる位置(大木の中心からの距離)。話しかける範囲より外にする")]
    public float standbyDistance = 5.5f;

    DirtTerrain terrain;
    SpriteRenderer sr;
    Sprite generated;
    int builtSeed;

    /// <summary>いまの大きさ(ユニット)</summary>
    public float WidthUnits { get { return sr != null && sr.sprite != null ? sr.sprite.bounds.size.x * scale : 0f; } }
    public float HeightUnits { get { return sr != null && sr.sprite != null ? sr.sprite.bounds.size.y * scale : 0f; } }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        terrain = FindFirstObjectByType<DirtTerrain>();
        var go = new GameObject("TreeSprite");
        go.transform.SetParent(transform, false);
        sr = go.AddComponent<SpriteRenderer>();
        BuildArt();
        Apply();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (sr == null || terrain == null) return;
        if (seed != builtSeed) BuildArt();   // 乱数の種を変えたら、描き直す
        Apply();
    }

    void BuildArt()
    {
        builtSeed = seed;
        if (spriteOverride != null) { sr.sprite = spriteOverride; return; }
        if (generated != null) Destroy(generated.texture);
        generated = GiantTreeArt.Build(seed, terrain.pixelsPerUnit);
        sr.sprite = spriteOverride != null ? spriteOverride : generated;
    }

    void Apply()
    {
        if (spriteOverride != null && sr.sprite != spriteOverride) sr.sprite = spriteOverride;
        sr.enabled = visible;
        sr.sortingOrder = sortingOrder;
        sr.flipX = flipX;
        transform.position = new Vector3(terrain.WorldRect.center.x + offsetX, terrain.TopY + offsetY, 0f);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>モグラが、話しかけられる位置(地上で、大木の近く)にいるか。</summary>
    public bool IsMoleNear(Vector2 molePos)
    {
        if (!visible || terrain == null) return false;
        if (molePos.y < terrain.TopY - 0.3f) return false;   // 地中にいるときは、話しかけられない
        return Mathf.Abs(molePos.x - transform.position.x) <= interactRadius;
    }

    /// <summary>「はなれる」のあと、モグラが止まる定位置のx。いま居る側(左右)へ、大木から standbyDistance だけ離れた場所。</summary>
    public float StandbyX(float moleX)
    {
        float dx = moleX - transform.position.x;
        float side = dx >= 0f ? 1f : -1f;
        return transform.position.x + side * Mathf.Max(standbyDistance, interactRadius + 0.5f);
    }
}

/// <summary>
/// 大木の仮の絵を、コードで描く(ドット絵風)。
/// 丸く茂った葉のかたまりを、奥(暗い)から手前(明るい)へ重ね、太い幹と、地面に出た根を描く。
/// </summary>
public static class GiantTreeArt
{
    public const int W = 208;
    public const int H = 192;

    // 葉(暗い → 明るい)
    static readonly Color32 GDark2 = new Color32(30, 62, 38, 255);
    static readonly Color32 GDark1 = new Color32(46, 92, 44, 255);
    static readonly Color32 GMid2 = new Color32(72, 128, 48, 255);
    static readonly Color32 GMid1 = new Color32(106, 162, 58, 255);
    static readonly Color32 GLight = new Color32(146, 192, 74, 255);
    // 幹・根(明るい → 暗い)
    static readonly Color32 BLight = new Color32(166, 108, 72, 255);
    static readonly Color32 BMid = new Color32(124, 76, 52, 255);
    static readonly Color32 BDark = new Color32(86, 54, 40, 255);
    static readonly Color32 BDeep = new Color32(58, 36, 30, 255);

    public static Sprite Build(int seed, float ppu)
    {
        var px = new Color32[W * H];
        var rng = new System.Random(seed);

        DrawLooseRoots(px, seed);   // 地面に転がった根の破片
        DrawTrunk(px, seed);        // 幹
        DrawRoots(px, seed);        // 地面に出た根(幹の根元にかぶせる)
        DrawCanopy(px, rng, seed);  // 葉のかたまり(幹の上にかぶせる)
        DrawVines(px, rng);         // 垂れ下がった葉
        AddOutline(px);             // ふちどり

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "GiantTree",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels32(px);
        tex.Apply(false);
        // 足もと(根の下)が、中央下になるようにする
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), ppu);
    }

    // ---------------- 部品 ----------------

    static void DrawTrunk(Color32[] px, int seed)
    {
        const int yTop = 128; // 幹の上端(葉の中に隠れる)
        for (int y = 4; y < yTop; y++)
        {
            // 根元は広がり、上へいくほど細くなる
            float hw = (15f + 21f * Mathf.Exp(-(y - 4) / 13f)) * (1f - 0.12f * y / yTop);
            float cx = 104f + 3f * Mathf.Sin(y * 0.045f);
            int x0 = Mathf.FloorToInt(cx - hw), x1 = Mathf.CeilToInt(cx + hw);
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || x >= W) continue;
                float u = Mathf.InverseLerp(cx - hw, cx + hw, x + 0.5f); // 0=左 1=右
                Color c = Color.Lerp(BLight, BDark, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 1.15f)));
                c *= 0.88f + 0.24f * Hash(x / 2, 11, seed);            // 樹皮の縦のすじ
                if (Hash(x / 5, y / 7, seed + 3) > 0.9f) c *= 0.72f;   // 節・ごつごつ
                if (y < 14) c *= 0.82f + 0.18f * (y / 14f);            // 根元は暗く
                px[y * W + x] = To32(c);
            }
        }
    }

    static void DrawRoots(Color32[] px, int seed)
    {
        // 幹の根元から、外へ向かって、うねるように伸びる根
        Root(px, new Vector2(90, 24), new Vector2(66, 36), new Vector2(40, 26), new Vector2(30, 10), 16f, 6f, seed);
        Root(px, new Vector2(120, 24), new Vector2(146, 38), new Vector2(172, 26), new Vector2(182, 10), 16f, 6f, seed + 1);
        Root(px, new Vector2(96, 14), new Vector2(76, 12), new Vector2(62, 18), new Vector2(54, 6), 10f, 4f, seed + 2);
        Root(px, new Vector2(112, 14), new Vector2(132, 10), new Vector2(146, 18), new Vector2(154, 6), 10f, 4f, seed + 3);
    }

    static void DrawLooseRoots(Color32[] px, int seed)
    {
        // 地面に転がった、根のかたまり
        BrownBlob(px, 24f, 11f, 8f, 6f, seed + 10);
        BrownBlob(px, 46f, 5f, 8f, 4f, seed + 11);
        BrownBlob(px, 160f, 6f, 7f, 4f, seed + 12);
        BrownBlob(px, 184f, 12f, 8f, 6f, seed + 13);
    }

    static void Root(Color32[] px, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float w0, float w1, int seed)
    {
        for (float t = 0f; t <= 1f; t += 0.004f)
        {
            Vector2 p = Bezier(p0, p1, p2, p3, t);
            float r = Mathf.Lerp(w0, w1, t) * 0.5f;
            int xa = Mathf.FloorToInt(p.x - r), xb = Mathf.CeilToInt(p.x + r);
            int ya = Mathf.FloorToInt(p.y - r), yb = Mathf.CeilToInt(p.y + r);
            for (int y = ya; y <= yb; y++)
            {
                for (int x = xa; x <= xb; x++)
                {
                    if (x < 0 || x >= W || y < 0 || y >= H) continue;
                    float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y;
                    if (dx * dx + dy * dy > r * r) continue;
                    float v = dy / r; // +1=上 -1=下
                    Color c = v > 0.35f ? (Color)BLight : (v < -0.35f ? (Color)BDark : (Color)BMid);
                    c *= 0.92f + 0.16f * Hash(x, y, seed);
                    px[y * W + x] = To32(c);
                }
            }
        }
    }

    static void BrownBlob(Color32[] px, float cx, float cy, float rx, float ry, int seed)
    {
        for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - ry)); y <= Mathf.Min(H - 1, Mathf.CeilToInt(cy + ry)); y++)
        {
            for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - rx)); x <= Mathf.Min(W - 1, Mathf.CeilToInt(cx + rx)); x++)
            {
                float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                if (nx * nx + ny * ny > 1f) continue;
                float s = -nx * 0.4f + ny * 0.8f;
                Color c = s > 0.3f ? (Color)BLight : (s < -0.3f ? (Color)BDark : (Color)BMid);
                c *= 0.92f + 0.16f * Hash(x, y, seed);
                px[y * W + x] = To32(c);
            }
        }
    }

    static void DrawCanopy(Color32[] px, System.Random rng, int seed)
    {
        const float cx = 104f, cy = 132f;

        // 1) 奥(暗い): ふちをぐるりと。横へ垂れ下がる形にする
        const int back = 12;
        for (int i = 0; i < back; i++)
        {
            float a = Mathf.Lerp(-25f, 205f, i / (back - 1f)) * Mathf.Deg2Rad;
            float ry = Mathf.Sin(a) >= 0f ? 40f : 26f;
            Clump(px, cx + Mathf.Cos(a) * 78f + J(rng, 5f), cy + Mathf.Sin(a) * ry + J(rng, 4f),
                22f + R(rng, 0f, 6f), 17f + R(rng, 0f, 5f), GDark2, GDark1, GMid2, seed + i);
        }

        // 幹と葉のあいだの、暗いかげ
        Clump(px, 104f, 108f, 36f, 12f, GDark2, GDark2, GDark1, seed + 50);

        // 2) 中
        const int mid = 14;
        for (int i = 0; i < mid; i++)
        {
            float a = Mathf.Lerp(0f, 180f, (i + R(rng, 0f, 1f)) / mid) * Mathf.Deg2Rad;
            float rf = 0.35f + 0.5f * (float)rng.NextDouble();
            Clump(px, cx + Mathf.Cos(a) * 72f * rf, cy + 4f + Mathf.Sin(a) * 36f * rf,
                22f + R(rng, 0f, 6f), 16f + R(rng, 0f, 4f), GDark1, GMid2, GMid1, seed + 100 + i);
        }

        // 3) 手前(明るい): 上側に多く
        const int front = 12;
        for (int i = 0; i < front; i++)
        {
            float a = Mathf.Lerp(15f, 165f, (i + R(rng, 0f, 1f)) / front) * Mathf.Deg2Rad;
            float rf = 0.3f + 0.55f * (float)rng.NextDouble();
            Clump(px, cx + Mathf.Cos(a) * 66f * rf, cy + 8f + Mathf.Sin(a) * 34f * rf,
                20f + R(rng, 0f, 6f), 14f + R(rng, 0f, 4f), GMid2, GMid1, GLight, seed + 200 + i);
        }
    }

    // 葉のかたまり1つ。左上から光が当たる。ふちは、少しでこぼこにする
    static void Clump(Color32[] px, float cx, float cy, float rx, float ry,
        Color32 dark, Color32 mid, Color32 light, int seed)
    {
        float phase = Hash(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), seed) * 6.28f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx * 1.2f)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + rx * 1.2f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry * 1.2f)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + ry * 1.2f));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float bump = 1f + 0.10f * Mathf.Sin(Mathf.Atan2(ny, nx) * 5f + phase);
                if (d > bump) continue;

                float s = -nx * 0.45f + ny * 0.75f; // +: 上・左(明るい)  -: 下・右(暗い)
                Color32 c = s > 0.30f ? light : (s < -0.28f ? dark : mid);
                float n = Hash(x, y, seed);
                if (n > 0.94f && s > -0.3f) c = light;   // 葉のつぶ(明)
                else if (n < 0.05f) c = dark;            // 葉のつぶ(暗)
                if (d > bump * 0.84f && ny < 0f) c = dark; // 下側のふちは暗く
                px[y * W + x] = c;
            }
        }
    }

    static void DrawVines(Color32[] px, System.Random rng)
    {
        // 葉のふちから、細く垂れ下がる葉
        for (int i = 0; i < 12; i++)
        {
            int x = Mathf.RoundToInt(R(rng, 24f, 184f));
            if (Mathf.Abs(x - 104) < 26) continue; // 幹の前は避ける
            int yb = -1;
            for (int y = 70; y < 140; y++) if (px[y * W + x].a > 0) { yb = y; break; } // 葉の下のふち
            if (yb < 0) continue;
            int len = Mathf.RoundToInt(R(rng, 5f, 14f));
            for (int k = 1; k <= len; k++)
            {
                int yy = yb - k;
                if (yy < 2) break;
                px[yy * W + x] = k > len - 3 ? GMid1 : GDark1;
                if (k < 4 && x + 1 < W) px[yy * W + x + 1] = GDark1;
            }
        }
    }

    // まわりに、暗いふちどりをつける(となりの色を暗くした色)
    static void AddOutline(Color32[] px)
    {
        var result = (Color32[])px.Clone();
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (px[i].a != 0) continue;
                Color32 n = default(Color32);
                bool found = false;
                if (x > 0 && px[i - 1].a > 0) { n = px[i - 1]; found = true; }
                else if (x < W - 1 && px[i + 1].a > 0) { n = px[i + 1]; found = true; }
                else if (y > 0 && px[i - W].a > 0) { n = px[i - W]; found = true; }
                else if (y < H - 1 && px[i + W].a > 0) { n = px[i + W]; found = true; }
                if (found) result[i] = new Color32((byte)(n.r * 0.33f), (byte)(n.g * 0.33f), (byte)(n.b * 0.33f), 255);
            }
        }
        System.Array.Copy(result, px, px.Length);
    }

    // ---------------- 小さな道具 ----------------

    static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    static Color32 To32(Color c)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255), 255);
    }

    static float R(System.Random rng, float a, float b) { return a + (b - a) * (float)rng.NextDouble(); }
    static float J(System.Random rng, float m) { return R(rng, -m, m); }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
