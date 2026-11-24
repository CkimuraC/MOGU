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

    [Header("Surface (地表)")]
    [Tooltip("true: 土の上端が開いていて、掘り抜くと地上に出られる")]
    public bool topOpen = true;
    [Tooltip("土の最上部を草の色にする厚み(px)。0で無効")]
    public int turfDepthPx = 10;
    public Color32 turfLight = new Color32(120, 168, 52, 255);
    public Color32 turfDark = new Color32(58, 100, 36, 255);

    [Header("Layers (地層)")]
    [Tooltip("土の上端からの深さで分けた地層。上から下へ並べる。空なら従来の1色グラデーション")]
    public System.Collections.Generic.List<SoilLayer> layers = new System.Collections.Generic.List<SoilLayer>
    {
        new SoilLayer("Topsoil", 0f,  new Color32(136, 90, 48, 255),  new Color32(114, 74, 40, 255), 1.0f, 1.0f),
        new SoilLayer("Clay",    6f,  new Color32(158, 92, 54, 255),  new Color32(128, 72, 44, 255), 1.4f, 1.0f),
        new SoilLayer("Gravel",  12f, new Color32(132, 108, 84, 255), new Color32(104, 84, 66, 255), 1.9f, 3.0f),
        new SoilLayer("Hard",    18f, new Color32(96, 82, 74, 255),   new Color32(72, 62, 58, 255),  2.6f, 2.0f),
        new SoilLayer("Bedrock", 25f, new Color32(74, 76, 90, 255),   new Color32(48, 50, 64, 255),  3.5f, 2.5f) { minDigPower = 1.5f }
    };
    [Tooltip("層の境目の波打ち(ユニット)。0でまっすぐ")]
    public float layerWobble = 0.35f;
    [Tooltip("層の境目のギザギザ(ユニット)")]
    public float layerEdgeJitter = 0.25f;

    Texture2D tex;
    bool[] dug; // true = 掘削済み(空間)
    bool dirty;
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    public Vector2 WorldSize => new Vector2(widthPx, heightPx) / pixelsPerUnit;
    public Vector2 MinWorld => (Vector2)transform.position - WorldSize * 0.5f;
    public Rect WorldRect => new Rect(MinWorld, WorldSize);
    /// <summary>土の上端のワールドY(ここから上が地上)</summary>
    public float TopY => MinWorld.y + WorldSize.y;

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
        bool useLayers = layers != null && layers.Count > 0;
        float totalDepth = heightPx / pixelsPerUnit;

        // 層の境目の波打ち(列ごと)
        var wob = new float[widthPx];
        for (int x = 0; x < widthPx; x++) wob[x] = Wobble((x + 0.5f) / pixelsPerUnit);

        for (int y = 0; y < heightPx; y++)
        {
            float depthRow = 1f - (float)y / (heightPx - 1); // 上=0, 下=1
            int fromTop = heightPx - 1 - y;
            for (int x = 0; x < widthPx; x++)
            {
                float n = Hash(x / cell, y / cell, randomSeed);
                float shade = 0.88f + 0.24f * n;
                float speck = Hash(x / cell, y / cell, randomSeed + 7);

                Color col;
                float speckK = 1f;
                if (useLayers)
                {
                    float depthU = (fromTop + 0.5f) / pixelsPerUnit + wob[x]
                                 + (Hash(x / 2, y / 2, randomSeed + 99) - 0.5f) * layerEdgeJitter;
                    int li = LayerIndexAtDepth(depthU);
                    var L = layers[li];
                    float end = li + 1 < layers.Count ? layers[li + 1].startDepth : totalDepth;
                    float t = Mathf.Clamp01((depthU - L.startDepth) / Mathf.Max(0.01f, end - L.startDepth));
                    col = Color.Lerp(L.colorLight, L.colorDark, t);
                    speckK = Mathf.Max(0f, L.speckle);
                }
                else
                {
                    col = Color.Lerp(dirtLight, dirtDark, depthRow);
                }

                if (speck > 1f - 0.025f * speckK) shade *= 0.6f;       // 暗い粒
                else if (speck < 0.015f * speckK) shade *= 1.25f;      // 明るい粒

                // 最上部は草(縁は凸凹)
                if (turfDepthPx > 0)
                {
                    int depthHere = turfDepthPx - Mathf.FloorToInt(Hash(x / 3, 1234, randomSeed) * turfDepthPx * 0.5f);
                    if (fromTop < depthHere)
                        col = Color.Lerp(turfLight, turfDark, fromTop / (float)Mathf.Max(1, turfDepthPx));
                }

                Color c = col * shade;
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
        if (x < 0 || y < 0 || x >= widthPx) return true;
        if (y >= heightPx) return !topOpen; // 土の上端より上は空(topOpenのとき)
        return !dug[y * widthPx + x];
    }

/// <summary>層の境目の波打ち(ユニット)。xは地形の左端からの距離(ユニット)。</summary>
    public float Wobble(float xFromLeftUnits)
    {
        if (layerWobble <= 0f) return 0f;
        float s = randomSeed * 0.001f;
        return layerWobble * (0.6f * Mathf.Sin(xFromLeftUnits * 1.0f + s) + 0.4f * Mathf.Sin(xFromLeftUnits * 2.4f + 1.3f + s * 2f));
    }

    /// <summary>深さ(土の上端からのユニット)がどの層に当たるか。</summary>
    public int LayerIndexAtDepth(float depthUnits)
    {
        if (layers == null || layers.Count == 0) return 0;
        for (int i = layers.Count - 1; i >= 0; i--)
            if (depthUnits >= layers[i].startDepth) return i;
        return 0;
    }

    /// <summary>ワールド座標の場所がどの層か(層が未設定なら-1)。</summary>
    public int LayerIndexAt(Vector2 world)
    {
        if (layers == null || layers.Count == 0) return -1;
        float depth = TopY - world.y + Wobble(world.x - MinWorld.x);
        return LayerIndexAtDepth(depth);
    }

    /// <summary>ワールド座標の場所の土の硬さ(層が未設定なら1)。</summary>
    public float HardnessAt(Vector2 world)
    {
        int i = LayerIndexAt(world);
        return i < 0 ? 1f : Mathf.Max(0.01f, layers[i].hardness);
    }

    /// <summary>ワールド座標の場所の層の名前(層が未設定なら空文字)。</summary>
    public string LayerNameAt(Vector2 world)
    {
        int i = LayerIndexAt(world);
        return i < 0 ? "" : layers[i].name;
    }

/// <summary>ワールド座標の場所を掘るのに必要な掘削力(層が未設定なら0)。</summary>
    public float MinDigPowerAt(Vector2 world)
    {
        int i = LayerIndexAt(world);
        return i < 0 ? 0f : layers[i].minDigPower;
    }



    /// <summary>円形ブラシで土を削る。新たに削れたピクセル数を返す。</summary>
    public int Dig(Vector2 world, float radiusPx)
    {
        Vector2 p = (world - MinWorld) * pixelsPerUnit;
        int b = undiggableBorderPx;
        int x0 = Mathf.Max(b, Mathf.FloorToInt(p.x - radiusPx));
        int x1 = Mathf.Min(widthPx - 1 - b, Mathf.CeilToInt(p.x + radiusPx));
        int y0 = Mathf.Max(b, Mathf.FloorToInt(p.y - radiusPx));
        int y1 = Mathf.Min(heightPx - 1 - (topOpen ? 0 : b), Mathf.CeilToInt(p.y + radiusPx)); // topOpenなら上端も掘れる
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
