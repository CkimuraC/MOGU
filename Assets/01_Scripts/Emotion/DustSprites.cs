using UnityEngine;

/// <summary>
/// 感情の塵の仮画像(コードで描画)。細かい粒が数個、ふわっと集まった見た目。色は EmotionInfo で決まる。
/// 本番画像は ItemSpawner の Dust Sprite Overrides に入れると差し替わる。
/// </summary>
public static class DustSprites
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
        const int size = 16;
        var px = new Color32[size * size];

        // うっすらした光のにじみ
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= 7f)
                {
                    float a = Mathf.Clamp01(1f - d / 7f) * 0.35f;
                    px[y * size + x] = new Color(baseCol.r, baseCol.g, baseCol.b, a);
                }
            }
        }

        // 細かい粒(1〜2px)
        int[,] grains = { { 8, 8 }, { 5, 9 }, { 10, 6 }, { 7, 4 }, { 11, 10 }, { 4, 6 }, { 9, 12 }, { 6, 11 } };
        for (int g = 0; g < grains.GetLength(0); g++)
        {
            int gx = grains[g, 0], gy = grains[g, 1];
            Color c = baseCol * (g % 2 == 0 ? 1.25f : 0.95f);
            c.a = 1f;
            px[gy * size + gx] = c;
            if (g < 3) px[gy * size + gx + 1] = c; // 少し大きい粒
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
}
