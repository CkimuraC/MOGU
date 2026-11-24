using UnityEngine;

/// <summary>
/// 暗闇の演出。地形の上に「黒い膜」をかぶせ、
/// モグラの周囲と、スキャンで照らされた範囲だけ膜を薄くして見えるようにする。
/// ・低解像度(既定:地形4pxにつき1マス)のアルファ画像を毎フレーム更新する方式。
///   Bilinearで拡大するので、縁がなめらかにぼける。URP/Built-inどちらでも動く。
/// ・darknessEnabled をオフにすると、膜ごと消えて元の見た目に戻る(開発用)。
/// </summary>
public class DarknessOverlay : MonoBehaviour
{
    [Header("On/Off (開発用)")]
    [Tooltip("暗闇のオンオフ。再生中にチェックを切り替えてもOK")]
    public bool darknessEnabled = true;
    [Tooltip("再生中にLキーでオンオフを切り替える")]
    public bool allowHotkey = true;
    [Tooltip("true: スキャンしたとき周囲の暗闇も一緒に晴れる。false(既定): 暗闇はそのままで、隠れているものだけが見える")]
    public bool scanBrightensArea = false;

    [Header("Mole Light (モグラの周囲)")]
    [Tooltip("この半径までは完全に見える(ユニット)")]
    public float lightInnerRadius = 1.0f;
    [Tooltip("この半径で真っ暗になる(ユニット)。画面の高さは約12ユニット")]
    public float lightOuterRadius = 3.0f;

    [Header("Scan Light (スキャンで照らされる範囲)")]
    [Tooltip("照らされた範囲の縁のぼかし幅(ユニット)")]
    public float scanEdgeSoftness = 1.5f;
    [Tooltip("見える時間の最後にゆっくり暗くなる時間(秒)")]
    public float scanFadeTime = 1.5f;

    [Header("Look")]
    [Range(0f, 1f)]
    [Tooltip("暗闇の中の最低限の明るさ。0=真っ暗。開発中に少し見たいときは0.1〜0.2")]
    public float ambient = 0f;
    [Tooltip("暗闇画像の1マスが地形の何pxか。大きいほど軽いが粗くなる")]
    public int cellPx = 4;
    public Color darkColor = Color.black;
    public int sortingOrder = 8; // 地形(0)・アイテム(5)の上、モグラ(10)の下

    DirtTerrain terrain;
    MoleController mole;
    ScanPulse scan;

    Texture2D tex;
    Color32[] pixels;
    int w, h;
    float cellWorld;
    Vector2 minWorld;
    SpriteRenderer sr;

    bool hasLast;
    int lx0, ly0, lx1, ly1; // 前フレームに更新した範囲

    void Start()
    {
        terrain = FindFirstObjectByType<DirtTerrain>();
        mole = FindFirstObjectByType<MoleController>();
        if (mole != null) scan = mole.GetComponent<ScanPulse>();
        if (terrain == null) return;

        cellPx = Mathf.Max(1, cellPx);
        cellWorld = cellPx / terrain.pixelsPerUnit;
        minWorld = terrain.MinWorld;
        w = Mathf.CeilToInt(terrain.widthPx / (float)cellPx);
        h = Mathf.CeilToInt(terrain.heightPx / (float)cellPx);

        var dark = (Color32)darkColor;
        dark.a = 255;
        pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = dark;

        tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DarknessMask"
        };
        tex.SetPixels32(pixels);
        tex.Apply(false);

        var go = new GameObject("DarknessSprite");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(minWorld.x, minWorld.y, 0f);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero,
            terrain.pixelsPerUnit / cellPx, 0, SpriteMeshType.FullRect);
        sr.sortingOrder = sortingOrder;
        sr.enabled = darknessEnabled;
    }

    void LateUpdate()
    {
        if (tex == null || mole == null) return;

        if (allowHotkey && MoleInput.DarkTogglePressed()) darknessEnabled = !darknessEnabled;
        sr.enabled = darknessEnabled;
        sr.sortingOrder = sortingOrder;
        if (!darknessEnabled) return;

        Vector2 mp = mole.transform.position;
        var lights = (scanBrightensArea && scan != null) ? scan.Lights : null;

        // 今回更新する範囲 = モグラの光 + スキャンの光 + 前フレームの範囲
        int x0, y0, x1, y1;
        CellRect(mp, lightOuterRadius, out x0, out y0, out x1, out y1);
        if (lights != null)
        {
            for (int i = 0; i < lights.Count; i++)
            {
                CellRect(lights[i].origin, lights[i].radius + scanEdgeSoftness, out int a0, out int b0, out int a1, out int b1);
                x0 = Mathf.Min(x0, a0); y0 = Mathf.Min(y0, b0);
                x1 = Mathf.Max(x1, a1); y1 = Mathf.Max(y1, b1);
            }
        }
        if (hasLast)
        {
            x0 = Mathf.Min(x0, lx0); y0 = Mathf.Min(y0, ly0);
            x1 = Mathf.Max(x1, lx1); y1 = Mathf.Max(y1, ly1);
        }
        x0 = Mathf.Clamp(x0, 0, w - 1); x1 = Mathf.Clamp(x1, 0, w - 1);
        y0 = Mathf.Clamp(y0, 0, h - 1); y1 = Mathf.Clamp(y1, 0, h - 1);

        byte r = ((Color32)darkColor).r, g = ((Color32)darkColor).g, b = ((Color32)darkColor).b;
        float reveal = scan != null ? scan.revealDuration : 0f;

        for (int y = y0; y <= y1; y++)
        {
            float py = minWorld.y + (y + 0.5f) * cellWorld;
            for (int x = x0; x <= x1; x++)
            {
                float px = minWorld.x + (x + 0.5f) * cellWorld;
                float vis = ambient;

                // モグラの光
                float dm = Mathf.Sqrt((px - mp.x) * (px - mp.x) + (py - mp.y) * (py - mp.y));
                vis = Mathf.Max(vis, 1f - Smooth(lightInnerRadius, lightOuterRadius, dm));

                // スキャンの光(広がる円。時間とともにゆっくり暗くなる)
                if (lights != null)
                {
                    for (int i = 0; i < lights.Count; i++)
                    {
                        var l = lights[i];
                        float dx = px - l.origin.x, dy = py - l.origin.y;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float lit = 1f - Smooth(l.radius - scanEdgeSoftness, l.radius, d);
                        float remaining = reveal - l.age;
                        float inten = Mathf.Clamp01(remaining / Mathf.Max(0.01f, scanFadeTime));
                        vis = Mathf.Max(vis, lit * inten);
                    }
                }

                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt((1f - vis) * 255f), 0, 255);
                pixels[y * w + x] = new Color32(r, g, b, a);
            }
        }

        hasLast = true;
        lx0 = x0; ly0 = y0; lx1 = x1; ly1 = y1;

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    void CellRect(Vector2 center, float radius, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Mathf.FloorToInt((center.x - radius - minWorld.x) / cellWorld) - 1;
        y0 = Mathf.FloorToInt((center.y - radius - minWorld.y) / cellWorld) - 1;
        x1 = Mathf.CeilToInt((center.x + radius - minWorld.x) / cellWorld) + 1;
        y1 = Mathf.CeilToInt((center.y + radius - minWorld.y) / cellWorld) + 1;
    }

    static float Smooth(float a, float b, float v)
    {
        if (b <= a) return v < a ? 0f : 1f;
        float t = Mathf.Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
}
