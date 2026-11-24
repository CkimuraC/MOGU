using UnityEngine;

/// <summary>
/// ピクセルマスク方式の掘削可能な地形。
/// Texture2Dのアルファを0にすることで土を削り、トンネルを形成する。
/// 当たり判定はコライダーを使わず、マスクを直接サンプリングして行う。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DirtTerrain : MonoBehaviour
{
    [Header("Size")]
    public int widthPx = 1536;
    public int heightPx = 1024;
    public float pixelsPerUnit = 32f;
    [Tooltip("外周の掘れない壁の厚み(px)")]
    public int undiggableBorderPx = 6;

    [Header("Look")]
    public Color32 dirtLight = new Color32(133, 86, 46, 255);
    public Color32 dirtDark = new Color32(66, 40, 22, 255);
    public Color tunnelBackColor = new Color(0.14f, 0.08f, 0.045f, 1f);
    [Tooltip("土の粒の大きさ(px)")]
    public int noiseCellPx = 2;
    public int randomSeed = 12345;

    Texture2D tex;
    bool[] dug; // true = 掘削済み(空間)
    bool dirty;
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    public Vector2 WorldSize => new Vector2(widthPx, heightPx) / pixelsPerUnit;
    public Vector2 MinWorld => (Vector2)transform.position - WorldSize * 0.5f;
    public Rect WorldRect => new Rect(MinWorld, WorldSize);

    void Awake()
    {
        BuildTexture();
        BuildBackground();
    }

    void BuildTexture()
    {
        dug = new bool[widthPx * heightPx];
        tex = new Texture2D(widthPx, heightPx, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "DirtMask"
        };

        var pixels = new Color32[widthPx * heightPx];
        int cell = Mathf.Max(1, noiseCellPx);
        for (int y = 0; y < heightPx; y++)
        {
            float depth = 1f - (float)y / (heightPx - 1); // 上=0, 下=1
            Color baseCol = Color.Lerp(dirtLight, dirtDark, depth);
            for (int x = 0; x < widthPx; x++)
            {
                float n = Hash(x / cell, y / cell, randomSeed);
                float shade = 0.88f + 0.24f * n;
                float speck = Hash(x / cell, y / cell, randomSeed + 7);
                if (speck > 0.975f) shade *= 0.6f;      // 暗い粒
                else if (speck < 0.015f) shade *= 1.25f; // 明るい粒
                Color c = baseCol * shade;
                pixels[y * widthPx + x] = new Color32(
                    (byte)Mathf.Clamp(c.r * 255f, 0, 255),
                    (byte)Mathf.Clamp(c.g * 255f, 0, 255),
                    (byte)Mathf.Clamp(c.b * 255f, 0, 255), 255);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);

        var sr = GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, widthPx, heightPx),
            new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sr.sortingOrder = 0;
    }

    void BuildBackground()
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, Color.white);
        t.Apply(false);

        var go = new GameObject("TunnelBackground");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        sr.color = tunnelBackColor;
        sr.sortingOrder = -10;
        go.transform.localScale = new Vector3(WorldSize.x, WorldSize.y, 1f);
    }

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

    /// <summary>ワールド座標が土(固体)かどうか。範囲外は固体扱い。</summary>
    public bool IsSolid(Vector2 world)
    {
        Vector2 p = (world - MinWorld) * pixelsPerUnit;
        int x = Mathf.FloorToInt(p.x);
        int y = Mathf.FloorToInt(p.y);
        if (x < 0 || y < 0 || x >= widthPx || y >= heightPx) return true;
        return !dug[y * widthPx + x];
    }

    /// <summary>円形ブラシで土を削る。新たに削れたピクセル数を返す。</summary>
    public int Dig(Vector2 world, float radiusPx)
    {
        Vector2 p = (world - MinWorld) * pixelsPerUnit;
        int b = undiggableBorderPx;
        int x0 = Mathf.Max(b, Mathf.FloorToInt(p.x - radiusPx));
        int x1 = Mathf.Min(widthPx - 1 - b, Mathf.CeilToInt(p.x + radiusPx));
        int y0 = Mathf.Max(b, Mathf.FloorToInt(p.y - radiusPx));
        int y1 = Mathf.Min(heightPx - 1 - b, Mathf.CeilToInt(p.y + radiusPx));
        if (x0 > x1 || y0 > y1) return 0;

        float r2 = radiusPx * radiusPx;
        var data = tex.GetRawTextureData<Color32>();
        int count = 0;
        for (int y = y0; y <= y1; y++)
        {
            float dy = y + 0.5f - p.y;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - p.x;
                if (dx * dx + dy * dy > r2) continue;
                int i = y * widthPx + x;
                if (dug[i]) continue;
                dug[i] = true;
                data[i] = Clear;
                count++;
            }
        }
        if (count > 0) dirty = true;
        return count;
    }

    void LateUpdate()
    {
        if (dirty)
        {
            tex.Apply(false);
            dirty = false;
        }
    }
}
