using UnityEngine;

/// <summary>
/// 地上エリア。土(DirtTerrain)の上端の上に、空・遠景の森(仮)・草を置く。
/// 土の上端を掘り抜くと、モグラはここへ出られる。
///
/// ■ あとから要素を足すとき
///   子オブジェクト "SurfaceContent"(土の上端の中央に置かれる)の下に、好きなオブジェクトを置く。
///   ・位置: SurfaceContent の下で Y=0 が地面の高さ、Y が増えるほど上空。
///   ・描画順(Sorting Order): 土=0、草=2、暗闇=8、モグラ=10。地上の物は 3〜7 を使うとモグラの後ろに描かれる。
///   ・地上のエリアは SurfaceArea.Instance.Rect で取得できる。
/// </summary>
public class SurfaceArea : MonoBehaviour
{
    public static SurfaceArea Instance { get; private set; }

    [Header("Size")]
    [Tooltip("地上エリアの高さ(ユニット)。画面の高さは約12ユニット")]
    public float heightUnits = 4f;
    [Tooltip("モグラが地面から上へ出られる高さ(ユニット)。重力がないので、上げすぎると空を飛べてしまう")]
    public float moleCeilingAboveGround = 1.5f;

    [Header("Sky")]
    public Color skyTop = new Color(0.45f, 0.70f, 0.92f);
    public Color skyBottom = new Color(0.80f, 0.92f, 0.97f);

    [Header("Far Forest (仮)")]
    public bool showFarForest = true;
    public Color forestColor = new Color(0.22f, 0.40f, 0.27f);
    public int forestHeightPx = 56;

    [Header("Grass Tufts (地表の草)")]
    public bool showGrassTufts = true;
    public int randomSeed = 4242;
    [Tooltip("草の絵(複数)。入れると、左から右へランダムに並べて貼る(同じ絵が続かない)。空なら仮の草を自動で作る")]
    public Sprite[] grassSprites;
    [Tooltip("草の上にときどき置く飾り(花・きのこ・石など)。空なら置かない")]
    public Sprite[] grassDecorSprites;
    [Range(0f, 1f)]
    [Tooltip("草を1枚貼るごとに、飾りを置く確率")]
    public float grassDecorChance = 0.15f;
    [Tooltip("草の下端を、土の上端より何ピクセル下げて重ねるか")]
    public int grassSinkPx = 2;

    [Header("Extension Point")]
    [Tooltip("あとから地上の要素を置く親。空なら自動で作られる")]
    public Transform contentRoot;

    DirtTerrain terrain;

    /// <summary>地上エリアのワールド矩形</summary>
    public Rect Rect
    {
        get
        {
            Rect r = terrain.WorldRect;
            return new Rect(r.xMin, terrain.TopY, r.width, heightUnits);
        }
    }

    /// <summary>土の矩形に地上の分を足した矩形(カメラの移動範囲用)</summary>
    public static Rect Expand(Rect soil)
    {
        if (Instance == null) return soil;
        return new Rect(soil.xMin, soil.yMin, soil.width, soil.height + Instance.heightUnits);
    }

    void Awake()
    {
        Instance = this;
        terrain = FindFirstObjectByType<DirtTerrain>();
        if (terrain == null) return;

        BuildSky();
        if (showFarForest) BuildFarForest();
        if (showGrassTufts) BuildGrassTufts();
        BuildContentRoot();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildContentRoot()
    {
        if (contentRoot == null)
        {
            var go = new GameObject("SurfaceContent");
            go.transform.SetParent(transform, false);
            contentRoot = go.transform;
        }
        Rect r = terrain.WorldRect;
        contentRoot.position = new Vector3(r.center.x, terrain.TopY, 0f);
    }

    void BuildSky()
    {
        const int n = 64;
        var t = new Texture2D(1, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < n; y++) t.SetPixel(0, y, Color.Lerp(skyBottom, skyTop, y / (n - 1f)));
        t.Apply(false);

        Rect r = terrain.WorldRect;
        var go = new GameObject("Sky");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(t, new Rect(0, 0, 1, n), Vector2.zero, 1f);
        sr.sortingOrder = -20;
        go.transform.position = new Vector3(r.xMin, terrain.TopY, 0f);
        go.transform.localScale = new Vector3(r.width, heightUnits / n, 1f);
    }

    void BuildFarForest()
    {
        int w = terrain.widthPx;
        int h = Mathf.Max(8, forestHeightPx);
        var px = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            float hill = 0.45f + 0.15f * Mathf.Sin(x * 0.013f) + 0.10f * Mathf.Sin(x * 0.041f + 2f);
            float tri = Mathf.Abs(((x % 14) / 14f) * 2f - 1f); // 木のとがり
            float jitter = 0.5f + Hash(x / 14, 77, randomSeed);
            int top = Mathf.Clamp(Mathf.RoundToInt(hill * h * 0.7f + (1f - tri) * 12f * jitter), 4, h - 1);
            for (int y = 0; y < top; y++)
            {
                float shade = Mathf.Lerp(0.78f, 1.05f, y / (float)h);
                Color c = forestColor * shade;
                px[y * w + x] = new Color32(
                    (byte)Mathf.Clamp(c.r * 255f, 0, 255),
                    (byte)Mathf.Clamp(c.g * 255f, 0, 255),
                    (byte)Mathf.Clamp(c.b * 255f, 0, 255), 255);
            }
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        t.SetPixels32(px);
        t.Apply(false);

        Rect r = terrain.WorldRect;
        var go = new GameObject("FarForest");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(t, new Rect(0, 0, w, h), Vector2.zero, terrain.pixelsPerUnit);
        sr.sortingOrder = -15;
        go.transform.position = new Vector3(r.xMin, terrain.TopY, 0f);
    }

    void BuildGrassTufts()
    {
        if (HasAnySprite(grassSprites)) { BuildGrassFromSprites(); return; }
        int w = terrain.widthPx;
        const int h = 12;
        const int baseRows = 2; // 土に重なる帯
        var px = new Color32[w * h];
        var light = new Color32(150, 196, 56, 255);
        var mid = new Color32(104, 156, 44, 255);
        var dark = new Color32(60, 106, 34, 255);

        for (int x = 0; x < w; x++)
        {
            int cell = x / 2;
            float a = Hash(cell, 31, randomSeed);
            float b = Hash(cell / 3, 57, randomSeed);
            int blade = baseRows + 1 + Mathf.FloorToInt(a * 5f) + (b > 0.7f ? 4 : 0); // 3〜11
            blade = Mathf.Min(blade, h);
            for (int y = 0; y < blade; y++)
            {
                Color32 c = y >= blade - 2 ? light : (y >= baseRows ? mid : dark);
                px[y * w + x] = c;
            }
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        t.SetPixels32(px);
        t.Apply(false);

        Rect r = terrain.WorldRect;
        var go = new GameObject("GrassTufts");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(t, new Rect(0, 0, w, h), Vector2.zero, terrain.pixelsPerUnit);
        sr.sortingOrder = 2;
        // 土の上端に少し重ねる
        go.transform.position = new Vector3(r.xMin, terrain.TopY - baseRows / terrain.pixelsPerUnit, 0f);
    }

static bool HasAnySprite(Sprite[] a)
    {
        if (a == null) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != null) return true;
        return false;
    }

    // 草の絵を、左から右へ並べて貼る(絵の左下を基準にする。絵の幅は自由)
    void BuildGrassFromSprites()
    {
        float ppu = terrain.pixelsPerUnit;
        Rect r = terrain.WorldRect;
        float baseY = terrain.TopY - grassSinkPx / ppu;

        var tiles = new System.Collections.Generic.List<Sprite>();
        for (int i = 0; i < grassSprites.Length; i++) if (grassSprites[i] != null) tiles.Add(grassSprites[i]);
        var decors = new System.Collections.Generic.List<Sprite>();
        if (grassDecorSprites != null)
            for (int i = 0; i < grassDecorSprites.Length; i++) if (grassDecorSprites[i] != null) decors.Add(grassDecorSprites[i]);

        var root = new GameObject("GrassTufts");
        root.transform.SetParent(transform, false);

        float x = r.xMin;
        int n = 0, last = -1;
        while (x < r.xMax && n < 4000)
        {
            int k = Mathf.Min(tiles.Count - 1, Mathf.FloorToInt(Hash(n, 7, randomSeed) * tiles.Count));
            if (tiles.Count > 1 && k == last) k = (k + 1) % tiles.Count; // 同じ絵が続かない
            last = k;
            var s = tiles[k];
            float wUnits = Mathf.Max(0.05f, s.bounds.size.x);

            var go = new GameObject("Grass_" + n);
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = 2;
            go.transform.position = new Vector3(x - s.bounds.min.x, baseY - s.bounds.min.y, 0f);

            if (decors.Count > 0 && Hash(n, 19, randomSeed) < grassDecorChance)
            {
                var d = decors[Mathf.Min(decors.Count - 1, Mathf.FloorToInt(Hash(n, 23, randomSeed) * decors.Count))];
                var dgo = new GameObject("GrassDecor_" + n);
                dgo.transform.SetParent(root.transform, false);
                var dsr = dgo.AddComponent<SpriteRenderer>();
                dsr.sprite = d;
                dsr.sortingOrder = 3;
                float cx = x + wUnits * 0.5f - d.bounds.center.x;
                dgo.transform.position = new Vector3(cx, baseY - d.bounds.min.y, 0f);
            }

            x += wUnits;
            n++;
        }
    }


    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint hh = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            hh = (hh ^ (hh >> 13)) * 1274126177u;
            hh ^= hh >> 16;
            return (hh & 0xFFFFFF) / (float)0x1000000;
        }
    }

    void OnDrawGizmosSelected()
    {
        var t = terrain != null ? terrain : FindFirstObjectByType<DirtTerrain>();
        if (t == null) return;
        Rect r = t.WorldRect;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(r.center.x, t.TopY + heightUnits * 0.5f, 0f), new Vector3(r.width, heightUnits, 0f));
    }
}
