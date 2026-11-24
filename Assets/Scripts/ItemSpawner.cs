using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 仮アイテムを地形内にランダム配置する。
/// ●=スタミナ回復 / ✕=障害物 / ★=最大スタミナ一時増加
/// 画像は仮(コードで描画)。本番画像は下の Sprite 欄に入れると差し替わる。
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    enum Kind { Heal, Star, Obstacle }

    [Header("References (空なら自動検索)")]
    public DirtTerrain terrain;
    public MoleController mole;

    [Header("Counts")]
    public int healCount = 12;
    public int starCount = 5;
    public int obstacleCount = 14;
    [Tooltip("各種類の1個目を、モグラの近く(3〜6ユニット)に置く。動作確認用")]
    public bool placeOneNearStart = true;
    public int seed = 777;

    [Header("Placement")]
    public float minDistanceFromMole = 4f;
    public float minSpacing = 2.2f;
    public float edgeMargin = 2f;

    [Header("Effects")]
    public float healAmount = 60f;
    public float starBoostAmount = 100f;
    public float starDuration = 20f;
    public bool starAlsoFills = true;
    public float obstacleRadius = 0.4f;

    [Header("Detection (スキャン)")]
    [Tooltip("●★をスキャンするまで隠す")]
    public bool hideItemsUntilScanned = true;
    [Tooltip("✕もスキャンするまで隠す(見えない障害物にぶつかることになる)")]
    public bool hideObstaclesUntilScanned = true;

    [Header("Sprites (空なら仮画像)")]
    public Sprite healSprite;
    public Sprite starSprite;
    public Sprite obstacleSprite;

    void Start()
    {
        if (terrain == null) terrain = FindFirstObjectByType<DirtTerrain>();
        if (mole == null) mole = FindFirstObjectByType<MoleController>();
        if (terrain == null || mole == null) return;

        if (healSprite == null) healSprite = MakeHeal(terrain.pixelsPerUnit);
        if (starSprite == null) starSprite = MakeStar(terrain.pixelsPerUnit);
        if (obstacleSprite == null) obstacleSprite = MakeObstacle(terrain.pixelsPerUnit);

        var rng = new System.Random(seed);
        var placed = new List<Vector2>();
        Spawn(Kind.Heal, healCount, rng, placed);
        Spawn(Kind.Star, starCount, rng, placed);
        Spawn(Kind.Obstacle, obstacleCount, rng, placed);
    }

    void Spawn(Kind kind, int count, System.Random rng, List<Vector2> placed)
    {
        for (int i = 0; i < count; i++)
        {
            bool near = placeOneNearStart && i == 0;
            if (!FindSpot(rng, near, placed, out Vector2 p)) continue;
            placed.Add(p);
            Create(kind, p);
        }
    }

    bool FindSpot(System.Random rng, bool near, List<Vector2> placed, out Vector2 pos)
    {
        Vector2 origin = mole.transform.position;
        Rect r = terrain.WorldRect;
        float minMole = near ? 2.5f : minDistanceFromMole;

        for (int t = 0; t < 80; t++)
        {
            Vector2 p;
            if (near)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float d = 3f + (float)rng.NextDouble() * 3f;
                p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            }
            else
            {
                p = new Vector2(
                    Mathf.Lerp(r.xMin + edgeMargin, r.xMax - edgeMargin, (float)rng.NextDouble()),
                    Mathf.Lerp(r.yMin + edgeMargin, r.yMax - edgeMargin, (float)rng.NextDouble()));
            }

            if (p.x < r.xMin + edgeMargin || p.x > r.xMax - edgeMargin ||
                p.y < r.yMin + edgeMargin || p.y > r.yMax - edgeMargin) continue;
            if ((p - origin).magnitude < minMole) continue;

            bool ok = true;
            for (int i = 0; i < placed.Count; i++)
                if ((placed[i] - p).magnitude < minSpacing) { ok = false; break; }
            if (!ok) continue;

            pos = p;
            return true;
        }
        pos = default;
        return false;
    }

void Create(Kind kind, Vector2 p)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(transform, false);
        go.transform.position = p;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 9; // 暗闇(8)の上・モグラ(10)の下。見つかったものは暗闇の中でもはっきり見える

        Color marker = Color.white;
        switch (kind)
        {
            case Kind.Heal:
                sr.sprite = healSprite;
                var h = go.AddComponent<ItemPickup>();
                h.kind = ItemKind.Heal;
                h.healAmount = healAmount;
                marker = new Color(0.4f, 0.95f, 0.45f);
                break;
            case Kind.Star:
                sr.sprite = starSprite;
                var s = go.AddComponent<ItemPickup>();
                s.kind = ItemKind.Star;
                s.boostAmount = starBoostAmount;
                s.boostDuration = starDuration;
                s.boostAlsoFills = starAlsoFills;
                marker = new Color(1f, 0.88f, 0.25f);
                break;
            case Kind.Obstacle:
                sr.sprite = obstacleSprite;
                var o = go.AddComponent<Obstacle>();
                o.radius = obstacleRadius;
                marker = new Color(1f, 0.35f, 0.3f);
                break;
        }

        // 探知に反応させる(スプライト設定後に追加する)
        var det = go.AddComponent<Detectable>();
        det.hiddenUntilScanned = kind == Kind.Obstacle ? hideObstaclesUntilScanned : hideItemsUntilScanned;
        det.markerColor = marker;
        det.showMarker = kind != Kind.Obstacle; // 画面外の方向マーカーはアイテムだけ
    }

    // ---------------- 仮画像の生成 ----------------

    static Sprite MakeHeal(float ppu)
    {
        // ● 緑の丸
        return MakeSprite(16, 16, ppu,
            (dx, dy) => dx * dx + dy * dy <= 6f * 6f,
            new Color32(96, 216, 100, 255), new Color32(20, 80, 28, 255));
    }

    static Sprite MakeStar(float ppu)
    {
        // ★ 黄色い星
        var verts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = (90f + i * 36f) * Mathf.Deg2Rad;
            float rad = i % 2 == 0 ? 6.8f : 2.9f;
            verts[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
        }
        return MakeSprite(16, 16, ppu,
            (dx, dy) => InPolygon(verts, dx, dy),
            new Color32(252, 218, 64, 255), new Color32(128, 84, 10, 255));
    }

    static Sprite MakeObstacle(float ppu)
    {
        // ✕ 赤いバツ印
        return MakeSprite(24, 24, ppu,
            (dx, dy) => Mathf.Abs(dx) <= 8.5f && Mathf.Abs(dy) <= 8.5f &&
                        (Mathf.Abs(dx - dy) <= 2.2f || Mathf.Abs(dx + dy) <= 2.2f),
            new Color32(216, 64, 52, 255), new Color32(62, 14, 10, 255));
    }

    static bool InPolygon(Vector2[] v, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if ((v[i].y > y) != (v[j].y > y) &&
                x < (v[j].x - v[i].x) * (y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>図形判定関数から、縁取り付きのドット絵スプライトを作る。</summary>
    static Sprite MakeSprite(int w, int h, float ppu,
        System.Func<float, float, bool> inside, Color32 fill, Color32 outline)
    {
        var fillMask = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                fillMask[y * w + x] = inside(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (fillMask[i]) { px[i] = fill; continue; }
                bool edge =
                    (x > 0 && fillMask[i - 1]) || (x < w - 1 && fillMask[i + 1]) ||
                    (y > 0 && fillMask[i - w]) || (y < h - 1 && fillMask[i + w]);
                if (edge) px[i] = outline;
            }
        }

        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        t.SetPixels32(px);
        t.Apply(false);
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }
}
