using UnityEngine;

/// <summary>
/// モグラの移動・掘削・スタミナ管理。
/// - 8方向/360度移動(斜めは正規化)
/// - 土に当たったとき、掘削キーを押していれば円形ブラシで削りながら前進
/// - 新規に削ったピクセル数に応じてスタミナ消費(既存トンネルは消費なし)
/// - 壁に当たったときは壁沿いに滑る(角度を振って空いている方向を探す)
/// </summary>
public class MoleController : MonoBehaviour
{
    [Header("References")]
    public DirtTerrain terrain;

    [Header("Movement (units/sec)")]
    public float moveSpeed = 5f;
    public float digSpeed = 2.5f;

    [Header("Tunnel")]
    [Tooltip("トンネル幅(px)。仕様書: 24〜32pxから検証")]
    public float tunnelWidthPx = 24f;
    [Tooltip("モグラの当たり判定半径 = ブラシ半径 - この値(px)。大きいほど壁に引っかかりにくい")]
    public float bodyClearancePx = 4f;
    [Tooltip("開始時にモグラの周囲へ作る小さな空間の半径(px)")]
    public float startCavityRadiusPx = 36f;

    [Header("Wall Sliding")]
    [Tooltip("壁沿いに滑るとき、進行方向から最大何度まで曲げて探すか")]
    public float maxSlideAngle = 80f;
    public float slideAngleStep = 10f;

    [Header("Obstacles")]
    [Tooltip("見えない障害物にぶつかったとき、その障害物を見せる時間(秒)。0で無効")]
    public float bumpRevealSeconds = 1.5f;

    [Header("Stamina")]
    public float maxStamina = 200f;
    [Tooltip("新たに削った1ピクセルあたりの消費量")]
    public float staminaPerPixel = 0.003f;
    [Tooltip("掘っていないときの回復量/秒(0で回復なし)")]
    public float regenPerSecond = 8f;
    public float regenDelay = 0.5f;
    [Tooltip("スタミナ切れ後、この量まで回復するまで掘れない(0で無効)。ガクガク進むのを防ぐ")]
    public float exhaustRecoverThreshold = 20f;

    [Header("Hardness (地層の硬さ)")]
    [Range(0f, 1f)]
    [Tooltip("硬さが掘る速さに与える影響。0=影響なし、1=硬さ2倍で速さが半分")]
    public float hardnessSpeedInfluence = 0.6f;
    [Range(0f, 1f)]
    [Tooltip("硬さがスタミナ消費に与える影響。0=影響なし、1=硬さ2倍で消費も2倍")]
    public float hardnessStaminaInfluence = 1f;

    [Header("Visual")]
    public Sprite sprite;
    public float visualScale = 1f;

    public float Stamina { get; private set; }
    public bool Exhausted { get; private set; }

    // ---- 一時的な最大スタミナ増加(★アイテム) ----
    class StaminaBoost { public float amount; public float remaining; }
    readonly System.Collections.Generic.List<StaminaBoost> boosts = new System.Collections.Generic.List<StaminaBoost>();

    /// <summary>現在の最大スタミナ(基本値 + ブースト)</summary>
    public float MaxStaminaCurrent
    {
        get
        {
            float m = maxStamina;
            for (int i = 0; i < boosts.Count; i++) m += boosts[i].amount;
            return m;
        }
    }

    /// <summary>ブースト中の最大スタミナ増加量(合計)</summary>
    public float BoostBonus => MaxStaminaCurrent - maxStamina;

    /// <summary>ブーストの残り時間(最長のもの)</summary>
    public float BoostTimeRemaining
    {
        get
        {
            float t = 0f;
            for (int i = 0; i < boosts.Count; i++) t = Mathf.Max(t, boosts[i].remaining);
            return t;
        }
    }

    /// <summary>スタミナ回復(●アイテム)</summary>
    public void Heal(float amount)
    {
        Stamina = Mathf.Min(MaxStaminaCurrent, Stamina + amount);
    }

    /// <summary>最大スタミナを一時的に増やす(★アイテム)。alsoFill=trueで増えた分だけ現在値も増やす。</summary>
    public void AddMaxStaminaBoost(float amount, float duration, bool alsoFill)
    {
        boosts.Add(new StaminaBoost { amount = amount, remaining = duration });
        if (alsoFill) Stamina = Mathf.Min(MaxStaminaCurrent, Stamina + amount);
    }

    const float MaxStepPx = 3f;

    // ---- 感情のかけら(EmotionEffects)による補正。付いていなければ全て等倍 ----
    EmotionEffects effects;
    EmotionEffects Fx { get { if (effects == null) effects = GetComponent<EmotionEffects>(); return effects; } }
    float MoveMul { get { return Fx != null ? Fx.MoveSpeedMul : 1f; } }
    float DigMul { get { return Fx != null ? Fx.DigSpeedMul : 1f; } }
    float CostMul { get { return Fx != null ? Fx.StaminaCostMul : 1f; } }
    float RegenBonus { get { return Fx != null ? Fx.RegenBonusPerSecond : 0f; } }
    /// <summary>掘削力。基本は1、「怒り」で+1。地層の Min Dig Power 以上でないと掘れない。</summary>
    public float DigPower { get { return 1f + (Fx != null ? Fx.DigPowerBonus : 0f); } }
    /// <summary>硬すぎて掘れなかった時刻(画面の通知用)</summary>
    public float HardBlockedUntil { get; private set; } // 1回の判定で進む最大距離(px)。フレーム落ち時のすり抜け防止

    Transform visual;
    int facing = 1;
    float currentAngle;
    float idleTimer;
    Vector2[] sampleOffsets;

    float BrushRadiusPx => tunnelWidthPx * 0.5f;
    float BodyRadiusUnits => Mathf.Max(1f, BrushRadiusPx - bodyClearancePx) / terrain.pixelsPerUnit;

    void Awake()
    {
        if (terrain == null) terrain = FindFirstObjectByType<DirtTerrain>();
        Stamina = maxStamina;
        BuildVisual();
    }

    void Start()
    {
        // 当たり判定サンプル点(中心+円周16点)
        float r = BodyRadiusUnits;
        const int n = 16;
        sampleOffsets = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            sampleOffsets[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        // 初期位置に小さな空間を作る
        terrain.Dig(transform.position, startCavityRadiusPx);
    }

    void BuildVisual()
    {
        var go = new GameObject("Visual");
        go.transform.SetParent(transform, false);
        visual = go.transform;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : BuildPlaceholderSprite();
        sr.sortingOrder = 10;
        visual.localScale = Vector3.one * visualScale;
    }

bool Blocked(Vector2 center)
    {
        if (ObstacleBlocked(center)) return true;
        if (terrain.IsSolid(center)) return true;
        for (int i = 0; i < sampleOffsets.Length; i++)
            if (terrain.IsSolid(center + sampleOffsets[i])) return true;
        return false;
    }

Vector2 ClampToTerrain(Vector2 p)
    {
        Rect r = terrain.WorldRect;
        float m = (terrain.undiggableBorderPx + BrushRadiusPx) / terrain.pixelsPerUnit;
        // 上端が開いていて地上があるなら、地面の少し上まで出られる
        float top = (terrain.topOpen && SurfaceArea.Instance != null)
            ? r.yMax + SurfaceArea.Instance.moleCeilingAboveGround
            : r.yMax - m;
        p.x = Mathf.Clamp(p.x, r.xMin + m, r.xMax - m);
        p.y = Mathf.Clamp(p.y, r.yMin + m, top);
        return p;
    }

void Update()
    {
        float dt = Time.deltaTime;
        TickBoosts(dt);

        Vector2 input = MoleInput.Move();
        bool digHeld = MoleInput.DigHeld();
        if (MoleInput.RefillPressed()) { Stamina = MaxStaminaCurrent; Exhausted = false; } // デバッグ用

        if (Exhausted && Stamina >= exhaustRecoverThreshold) Exhausted = false;

        bool dugThisFrame = false;

        if (input.sqrMagnitude > 0.0001f)
        {
            Vector2 dir = input.normalized; // 斜めも同じ速さ
            Vector2 pos = transform.position;

            // 1フレームの移動を細かく分割して判定(すり抜け・引っかかり対策)
            float maxSpeed = Mathf.Max(moveSpeed * MoveMul, digSpeed * DigMul);
            int steps = Mathf.Max(1, Mathf.CeilToInt(maxSpeed * dt * terrain.pixelsPerUnit / MaxStepPx));
            float sdt = dt / steps;
            for (int i = 0; i < steps; i++)
                pos = Step(pos, dir, sdt, digHeld, ref dugThisFrame);

            transform.position = ClampToTerrain(pos);
            UpdateVisual(dir);
        }

        // スタミナ回復(掘っていない間)
        if (dugThisFrame) idleTimer = 0f;
        else
        {
            idleTimer += dt;
            if (idleTimer >= regenDelay && regenPerSecond > 0f)
                Stamina = Mathf.Min(MaxStaminaCurrent, Stamina + regenPerSecond * dt);
        }

        // 感情(安心)による継続回復(掘っていても回復する)
        float bonus = RegenBonus;
        if (bonus > 0f) Stamina = Mathf.Min(MaxStaminaCurrent, Stamina + bonus * dt);
    }

    void TickBoosts(float dt)
    {
        bool expired = false;
        for (int i = boosts.Count - 1; i >= 0; i--)
        {
            boosts[i].remaining -= dt;
            if (boosts[i].remaining <= 0f) { boosts.RemoveAt(i); expired = true; }
        }
        if (expired) Stamina = Mathf.Min(Stamina, MaxStaminaCurrent);
    }

Vector2 Step(Vector2 pos, Vector2 dir, float dt, bool digHeld, ref bool dugThisFrame)
    {
        // 空いていれば通常速度で進む(既存トンネル内:スタミナ消費なし)
        float move = moveSpeed * MoveMul;
        Vector2 free = pos + dir * move * dt;
        if (!Blocked(free)) return free;

        // 土に当たった:掘削ボタン+スタミナがあれば削って進む(通常より遅い)
        if (digHeld && !Exhausted && Stamina > 0f)
        {
            // 掘り進む先(ブラシの先端)の層
            Vector2 front = pos + dir * (BrushRadiusPx / terrain.pixelsPerUnit);

            if (terrain.MinDigPowerAt(front) > DigPower + 0.001f)
            {
                // 掘削力が足りず、硬すぎて掘れない
                HardBlockedUntil = Time.time + 0.3f;
            }
            else
            {
                float hard = terrain.HardnessAt(front);
                float speedMul = DigMul / Mathf.Lerp(1f, hard, hardnessSpeedInfluence);
                float costMul = Mathf.Lerp(1f, hard, hardnessStaminaInfluence) * CostMul;

                Vector2 target = pos + dir * digSpeed * speedMul * dt;
                if (!ObstacleBlocked(target)) // 障害物の中へは掘り進めない
                {
                    int newPixels = terrain.Dig(target, BrushRadiusPx);
                    Stamina = Mathf.Max(0f, Stamina - newPixels * staminaPerPixel * costMul);
                    if (newPixels > 0) dugThisFrame = true;
                    if (Stamina <= 0f && exhaustRecoverThreshold > 0f) Exhausted = true;
                    return target;
                }
            }
        }

        // 掘れない/障害物:壁沿いに滑る
        return Slide(pos, dir, move * dt);
    }

    /// <summary>進行方向を左右に少しずつ曲げて、空いている方向へ進む。</summary>
    Vector2 Slide(Vector2 pos, Vector2 dir, float dist)
    {
        int tries = Mathf.Max(1, Mathf.FloorToInt(maxSlideAngle / Mathf.Max(1f, slideAngleStep)));
        for (int i = 1; i <= tries; i++)
        {
            float a = i * slideAngleStep;
            float factor = Mathf.Max(0.3f, Mathf.Cos(a * Mathf.Deg2Rad));
            for (int s = 0; s < 2; s++)
            {
                float signedA = s == 0 ? a : -a;
                Vector2 d = Quaternion.Euler(0f, 0f, signedA) * dir;
                Vector2 cand = pos + d * dist * factor;
                if (!Blocked(cand)) return cand;
            }
        }
        return pos;
    }

    void UpdateVisual(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > 0.1f) facing = dir.x > 0f ? 1 : -1;
        float target = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;
        float signed = facing > 0 ? target : -target;
        currentAngle = Mathf.LerpAngle(currentAngle, signed, 1f - Mathf.Exp(-18f * Time.deltaTime));
        visual.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        visual.localScale = new Vector3(facing * visualScale, visualScale, 1f);
    }

    /// <summary>仮のモグラ(32x24px・右向き)をコードで生成する。</summary>
    Sprite BuildPlaceholderSprite()
    {
        const int w = 32, h = 24;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[w * h];
        var body = new Color32(78, 56, 46, 255);
        var bodyLight = new Color32(104, 78, 62, 255);
        var nose = new Color32(236, 160, 172, 255);
        var claw = new Color32(240, 200, 176, 255);
        var eye = new Color32(18, 12, 10, 255);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float ex = (x + 0.5f - 14f) / 13f;
                float ey = (y + 0.5f - 11.5f) / 9.5f;
                if (ex * ex + ey * ey <= 1f) px[y * w + x] = ey > 0.3f ? bodyLight : body;

                float sx = (x + 0.5f - 26f) / 5f;
                float sy = (y + 0.5f - 11f) / 3.5f;
                if (sx * sx + sy * sy <= 1f) px[y * w + x] = body;

                float nx = x + 0.5f - 29.5f;
                float ny = y + 0.5f - 11f;
                if (nx * nx + ny * ny <= 4.8f) px[y * w + x] = nose;

                float cx = (x + 0.5f - 24f) / 4.5f;
                float cy = (y + 0.5f - 5f) / 2.5f;
                if (cx * cx + cy * cy <= 1f) px[y * w + x] = claw;
            }
        }
        px[14 * w + 23] = eye;
        px[14 * w + 24] = eye;

        t.SetPixels32(px);
        t.Apply(false);
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), terrain.pixelsPerUnit);
    }


/// <summary>障害物(✕)に重なるか。掘削中でも通れない。</summary>
bool ObstacleBlocked(Vector2 center)
    {
        var list = Obstacle.All;
        float br = BodyRadiusUnits;
        for (int i = 0; i < list.Count; i++)
        {
            var o = list[i];
            if (o == null) continue;
            float rr = o.radius + br;
            if (((Vector2)o.transform.position - center).sqrMagnitude < rr * rr)
            {
                if (bumpRevealSeconds > 0f) o.Bump(bumpRevealSeconds); // 隠れていてもぶつかったら見せる
                return true;
            }
        }
        return false;
    }
}
