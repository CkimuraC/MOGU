using UnityEngine;

/// <summary>
/// 感情のかけらの仮画像(コードで描画)。
/// 暗い丸い台座 + 面ごとに明暗の付いた六角形の結晶。色は EmotionInfo で決まる。
/// 本番画像は ItemSpawner の Shard Sprite Overrides に入れると差し替わる。
/// </summary>
public static class ShardSprites
{
    static readonly Sprite[] cache = new Sprite[EmotionInfo.Count];

    public static Sprite Get(EmotionType t, float ppu)
    {
        int i = (int)t;
        if (cache[i] == null) cache[i] = Build(EmotionInfo.ColorOf(t), ppu);
        return cache[i];
    }

    static Sprite Build(Color baseCol, float ppu)
    {
        const int size = 24;
        float c = size * 0.5f;

        // 結晶の輪郭(中心からの座標、Yが上)
        var poly = new[]
        {
            new Vector2(0f, 9.5f), new Vector2(6.5f, 4.5f), new Vector2(6.5f, -4f),
            new Vector2(0f, -9.5f), new Vector2(-6.5f, -4f), new Vector2(-6.5f, 4.5f)
        };

        var gem = new bool[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                gem[y * size + x] = InPolygon(poly, x + 0.5f - c, y + 0.5f - c);

        var px = new Color32[size * size];
        Color outline = baseCol * 0.4f; outline.a = 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int i = y * size + x;

                // 台座:暗い円 + うっすら色付きの縁
                Color32 col = new Color32(0, 0, 0, 0);
                if (dist <= 10.5f) col = new Color32(18, 14, 12, 210);
                if (Mathf.Abs(dist - 11f) <= 1f)
                {
                    col = new Color32((byte)(baseCol.r * 255f), (byte)(baseCol.g * 255f), (byte)(baseCol.b * 255f), 110);
                }

                if (gem[i])
                {
                    bool edge =
                        !gem[i - 1] || !gem[i + 1] || !gem[i - size] || !gem[i + size];
                    if (edge)
                    {
                        col = outline;
                    }
                    else
                    {
                        // 面ごとの明るさ:左上=明るい、右下=暗い
                        bool left = dx < 0.5f;
                        bool upper = dy > -1f;
                        float shade = left ? (upper ? 1.3f : 1.0f) : (upper ? 0.88f : 0.68f);
                        // 小さなハイライト
                        if (dx > -4.5f && dx < -2.5f && dy > 4f && dy < 7f) shade = 1.55f;
                        Color f = baseCol * shade;
                        col = new Color32(
                            (byte)Mathf.Clamp(f.r * 255f, 0, 255),
                            (byte)Mathf.Clamp(f.g * 255f, 0, 255),
                            (byte)Mathf.Clamp(f.b * 255f, 0, 255), 255);
                    }
                }
                px[i] = col;
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels32(px);
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
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
}
