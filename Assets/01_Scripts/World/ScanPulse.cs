using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 探知(スキャン)。ボタンを押すとモグラの位置から波が広がり、
/// 波が当たった Detectable を一定時間だけ見えるようにする。
/// 画面外で見つかっているものは、画面の端に方向マーカーを出す。
/// </summary>
public class ScanPulse : MonoBehaviour
{
    [Header("Scan")]
    [Tooltip("探知範囲(ユニット)。画面の高さは約12ユニット")]
    public float maxRadius = 10f;
    [Tooltip("波が広がる速さ(ユニット/秒)")]
    public float expandSpeed = 14f;
    [Tooltip("次に使えるまでの時間(秒)")]
    public float cooldown = 3f;
    [Tooltip("見つかったものが見え続ける時間(秒)")]
    public float revealDuration = 8f;

    [Header("Look")]
    public Color ringColor = new Color(0.45f, 0.9f, 1f, 1f);
    public bool showOffscreenMarkers = true;

    public float CooldownLeft { get; private set; }
    public float CooldownRatio => cooldown > 0f ? Mathf.Clamp01(CooldownLeft / cooldown) : 0f;
    public bool Ready => !active && CooldownLeft <= 0f;

    /// <summary>スキャンで照らされた範囲(暗闇表示用)。波と同じ速さで広がり、見える時間が過ぎると消える。</summary>
    public class ScanLight
    {
        public Vector2 origin;
        public float radius;
        public float age;
        public float maxRadius;
    }
    readonly List<ScanLight> lights = new List<ScanLight>();
    public IReadOnlyList<ScanLight> Lights => lights;

    const int RingTexSize = 256;
    const float RingPx = 124f;
    const float RingFrac = RingPx / RingTexSize; // スケール1のときのリング半径(ユニット)

    static Sprite ringSprite;

    Transform ring;
    SpriteRenderer ringSr;
    Camera cam;
    bool active;
    float radius;
    Vector2 origin;
    float activeMax; // 今回のスキャンの最大半径(感情の効果込み)
    EmotionEffects fx;
    EmotionEffects Fx { get { if (fx == null) fx = GetComponent<EmotionEffects>(); return fx; } }
    readonly HashSet<Detectable> hit = new HashSet<Detectable>();

    void Awake()
    {
        cam = Camera.main;
        BuildRing();
    }

    void BuildRing()
    {
        if (ringSprite == null) ringSprite = MakeRingSprite();
        var go = new GameObject("ScanRing"); // モグラの子にしない(波は世界に残る)
        ring = go.transform;
        ringSr = go.AddComponent<SpriteRenderer>();
        ringSr.sprite = ringSprite;
        ringSr.sortingOrder = 20;
        go.SetActive(false);
    }

    void OnDestroy()
    {
        if (ring != null) Destroy(ring.gameObject);
    }

void Update()
    {
        float dt = Time.deltaTime;
        if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
        if (MoleInput.ScanPressed()) TryScan();

        // 照らされた範囲の更新(波と同じ速さで広がり、見える時間が過ぎたら消える)
        for (int i = lights.Count - 1; i >= 0; i--)
        {
            var l = lights[i];
            l.age += dt;
            l.radius = Mathf.Min(l.maxRadius, l.radius + expandSpeed * dt);
            if (l.age >= revealDuration) lights.RemoveAt(i);
        }

        if (!active) return;

        radius = Mathf.Min(activeMax, radius + expandSpeed * dt);

        // 波が届いたものを見えるようにする(特殊な空間は好奇心で遠くまで届く)
        float specialMul = Fx != null ? Fx.SpecialSenseMul : 1f;
        var list = Detectable.All;
        float r2 = radius * radius;
        float sr = radius * specialMul;
        float sr2 = sr * sr;
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null || hit.Contains(d)) continue;
            float d2 = ((Vector2)d.transform.position - origin).sqrMagnitude;
            if (d2 <= (d.isSpecialSpace ? sr2 : r2))
            {
                hit.Add(d);
                d.Reveal(revealDuration);
            }
        }

        // 波の見た目
        float t = radius / Mathf.Max(0.01f, activeMax);
        ring.position = new Vector3(origin.x, origin.y, 0f);
        float s = Mathf.Max(0.02f, radius) / RingFrac;
        ring.localScale = new Vector3(s, s, 1f);
        var c = ringColor;
        c.a = Mathf.Lerp(1f, 0f, Mathf.Pow(t, 1.5f));
        ringSr.color = c;

        if (radius >= activeMax)
        {
            active = false;
            ring.gameObject.SetActive(false);
        }
    }

public bool TryScan()
    {
        if (active || CooldownLeft > 0f) return false;
        origin = transform.position;
        radius = 0f;
        activeMax = maxRadius * (Fx != null ? Fx.ScanRangeMul : 1f); // 恐れで範囲アップ
        hit.Clear();
        active = true;
        CooldownLeft = cooldown;
        ring.gameObject.SetActive(true);
        lights.Add(new ScanLight { origin = origin, radius = 0f, age = 0f, maxRadius = activeMax });
        return true;
    }

    void OnGUI()
    {
        if (!showOffscreenMarkers) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var list = Detectable.All;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        const float margin = 22f;
        var prev = GUI.color;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null || !d.hiddenUntilScanned || !d.showMarker || !d.IsPinged) continue;

            Vector3 sp = cam.WorldToScreenPoint(d.transform.position);
            if (sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height) continue; // 画面内なら本体が見える

            Vector2 dir = new Vector2(sp.x, sp.y) - center;
            float sx = (Screen.width * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
            float sy = (Screen.height * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
            Vector2 p = center + dir * Mathf.Min(sx, sy);
            float gy = Screen.height - p.y; // GUIはY軸が逆

            float a = d.Alpha;
            GUI.color = new Color(0f, 0f, 0f, 0.7f * a);
            GUI.DrawTexture(new Rect(p.x - 9, gy - 9, 18, 18), Texture2D.whiteTexture);
            GUI.color = new Color(d.markerColor.r, d.markerColor.g, d.markerColor.b, a);
            GUI.DrawTexture(new Rect(p.x - 6, gy - 6, 12, 12), Texture2D.whiteTexture);
        }
        GUI.color = prev;
    }

    /// <summary>細いリング + 内側に尾を引く光。白で作り、色はSpriteRendererで付ける。</summary>
    static Sprite MakeRingSprite()
    {
        int s = RingTexSize;
        var px = new Color32[s * s];
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - RingPx) / 3.5f); // リング本体
                if (d < RingPx)
                {
                    float glow = Mathf.Clamp01((d - 88f) / (RingPx - 88f));
                    a = Mathf.Max(a, glow * glow * 0.3f); // 内側の尾
                }
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
            }
        }
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        t.SetPixels32(px);
        t.Apply(false);
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }
}
