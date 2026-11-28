using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 掘ったときの、土のかけらのエフェクト。
/// モグラが土を掘ると、掘った場所から、小さな土のかけらが少しだけ飛び出して、落ちて、消える。
/// 色は、掘った場所の土(地層)の色。かけらが土に当たると、そこで消える。
/// 調整はデバッグパネル(F2)の「掘ったときのエフェクト」で行う。
/// </summary>
public class DigEffect : MonoBehaviour
{
    [Header("On/Off")]
    public bool effectEnabled = true;

    [Header("Amount (量)")]
    [Tooltip("掘った画素1つにつき、かけらを何個出すか(0.05=20画素で1個)")]
    public float particlesPerPixel = 0.05f;
    [Tooltip("1回の掘りで出すかけらの上限")]
    public int maxPerEvent = 4;
    [Tooltip("同時に飛んでいるかけらの上限")]
    public int maxAlive = 80;
    [Tooltip("1回で、これより多く削れたとき(拠点づくりなど)は、出さない")]
    public int ignoreIfPixelsOver = 300;

    [Header("Motion (動き)")]
    public float speedMin = 1.0f;
    public float speedMax = 2.6f;
    [Tooltip("落ちる強さ")]
    public float gravity = 9f;
    public float lifeMin = 0.25f;
    public float lifeMax = 0.55f;
    [Tooltip("飛ぶ向きのばらつき(度)")]
    public float spreadDegrees = 55f;

    [Header("Look (見た目)")]
    public int sizeMinPx = 2;
    public int sizeMaxPx = 3;
    [Tooltip("かけらを、土の色より少し明るくする割合")]
    public float brighten = 0.12f;
    [Tooltip("描画の順番。暗闇(8)より下にすると、暗闇の中ではかけらも暗くなる")]
    public int sortingOrder = 7;

    class P
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 pos, vel;
        public float life, age;
        public Color col;
        public bool active;
    }

    readonly List<P> pool = new List<P>();
    DirtTerrain terrain;
    MoleController mole;
    Sprite dot;
    float budget;
    int alive;

    void Start()
    {
        terrain = FindFirstObjectByType<DirtTerrain>();
        mole = FindFirstObjectByType<MoleController>();
        if (terrain != null) terrain.DugWorld += OnDug;
    }

    void OnDestroy()
    {
        if (terrain != null) terrain.DugWorld -= OnDug;
    }

    void OnDug(Vector2 at, int pixels, Color32 soil)
    {
        if (!effectEnabled || pixels <= 0 || pixels > ignoreIfPixelsOver) return;
        budget += pixels * Mathf.Max(0f, particlesPerPixel);
        int n = Mathf.Min(maxPerEvent, Mathf.FloorToInt(budget));
        if (n <= 0) return;
        budget -= n;

        Vector2 molePos = mole != null ? (Vector2)mole.transform.position : at - Vector2.right;
        Vector2 back = molePos - at;
        Vector2 baseDir = back.sqrMagnitude > 0.0001f ? back.normalized : Vector2.left;
        baseDir = (baseDir * 0.6f + Vector2.up * 0.8f).normalized; // 後ろ上へ飛び出す

        for (int i = 0; i < n; i++)
        {
            if (alive >= maxAlive) return;
            var p = Get();
            float ang = Random.Range(-spreadDegrees, spreadDegrees) * Mathf.Deg2Rad;
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            Vector2 d = new Vector2(baseDir.x * c - baseDir.y * s, baseDir.x * s + baseDir.y * c);
            p.pos = at + Random.insideUnitCircle * 0.12f;
            p.vel = d * Random.Range(speedMin, speedMax);
            p.life = Random.Range(lifeMin, lifeMax);
            p.age = 0f;
            float k = 1f + Random.Range(-0.12f, brighten + 0.1f);
            p.col = new Color(soil.r / 255f * k, soil.g / 255f * k, soil.b / 255f * k, 1f);
            int px = Random.Range(sizeMinPx, sizeMaxPx + 1);
            p.t.localScale = Vector3.one * (px / terrain.pixelsPerUnit);
            p.sr.color = p.col;
            p.t.position = new Vector3(p.pos.x, p.pos.y, 0f);
            p.active = true;
            p.t.gameObject.SetActive(true);
            alive++;
        }
    }

    P Get()
    {
        for (int i = 0; i < pool.Count; i++) if (!pool[i].active) return pool[i];
        if (dot == null)
        {
            var tx = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tx.SetPixel(0, 0, Color.white);
            tx.Apply(false);
            dot = Sprite.Create(tx, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        var go = new GameObject("DigCrumb");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dot;
        sr.sortingOrder = sortingOrder;
        var np = new P { t = go.transform, sr = sr };
        pool.Add(np);
        return np;
    }

    void Update()
    {
        if (alive == 0 || terrain == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < pool.Count; i++)
        {
            var p = pool[i];
            if (!p.active) continue;
            p.age += dt;
            p.vel.y -= gravity * dt;
            p.pos += p.vel * dt;
            // 土に当たる(穴の外へ出る)か、寿命が来たら消える
            if (p.age >= p.life || (p.age > 0.05f && terrain.IsSolid(p.pos)))
            {
                p.active = false;
                p.t.gameObject.SetActive(false);
                alive--;
                continue;
            }
            p.t.position = new Vector3(p.pos.x, p.pos.y, 0f);
            float u = p.age / p.life;
            var c = p.col; c.a = u < 0.6f ? 1f : Mathf.Clamp01((1f - u) / 0.4f);
            p.sr.color = c;
        }
    }
}
