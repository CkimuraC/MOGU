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
    public float exhaustRecoverThreshold = 5f;

    [Header("Fatigue (疲れ)")]
    [Tooltip("この量以上のスタミナがあれば、ふつうの速さで掘れる。「掘れるようになる量」からこの量までは、スタミナが少ないほど、ゆっくり掘る")]
    public float tiredFullSpeedStamina = 20f;
    [Range(0.05f, 1f)]
    [Tooltip("スタミナが「掘れるようになる量」のとき、ふつうの何倍の速さで掘るか(0.2=5分の1)")]
    public float tiredMinDigSpeedMul = 0.2f;
    [Tooltip("疲れて掘れないとき、むりに掘ろうとした回数がこれになると、フリーズする")]
    public int freezeAfterAttempts = 3;
    [Tooltip("フリーズする時間(秒)。動けない・掘れない")]
    public float freezeSeconds = 4f;
    [Tooltip("むりに掘ろうとした回数を、数え続ける時間(秒)。これより間があくと、0に戻る")]
    public float attemptWindowSeconds = 4f;
    [Tooltip("掘るボタンを押しっぱなしのとき、何秒ごとに「もう1回掘ろうとした」と数えるか")]
    public float attemptRepeatSeconds = 1f;

    float frozenUntil;
    float lastAttemptTime = -999f;
    int attempts;
    bool prevDigHeld;

    /// <summary>疲れて、フリーズしている最中か(動けない・掘れない)</summary>
    public bool Frozen { get { return Time.time < frozenUntil; } }
    /// <summary>フリーズの残り時間(秒)</summary>
    public float FreezeRemaining { get { return Mathf.Max(0f, frozenUntil - Time.time); } }
    /// <summary>むりに掘ろうとした回数(数え続けている間)</summary>
    public int DigAttempts { get { return Time.time - lastAttemptTime > attemptWindowSeconds ? 0 : attempts; } }
    /// <summary>最後に、疲れて掘れなかった時刻(画面の説明用)</summary>
    public float LastFailedDigTime { get; private set; } = -999f;

    /// <summary>疲れによる、掘る速さの倍率。「掘れるようになる量」で最小、「ふつうの速さで掘れる量」で1。</summary>
    public float TiredDigMul
    {
        get
        {
            if (tiredFullSpeedStamina <= exhaustRecoverThreshold) return 1f;
            float t = Mathf.InverseLerp(exhaustRecoverThreshold, tiredFullSpeedStamina, Stamina);
            return Mathf.Lerp(tiredMinDigSpeedMul, 1f, t);
        }
    }

    [Header("Recovery Penalty (スタミナ切れのペナルティ)")]
    [Tooltip("スタミナが切れた回数ごとの、回復の速さの倍率。1番目=1回目に切れたあと、2番目=2回目…。足りない分は最後の値を使う")]
    public float[] regenMulByDepletion = { 1f, 0.6f, 0.3f, 0.05f };
    [Tooltip("true: 地上に戻ると、スタミナが切れた回数をリセットする")]
    public bool resetDepletionOnSurface = true;

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

    /// <summary>スタミナが切れた回数(切れるたびに、回復がだんだん遅くなる)</summary>
    public int DepletionCount { get; private set; }
    /// <summary>スタミナが切れたあと、満タンに戻るまでの「回復中」か</summary>
    public bool Recovering { get; private set; }
    /// <summary>最後にスタミナが切れた時刻(画面の通知用)</summary>
    public float LastDepletedTime { get; private set; } = -999f;

    /// <summary>n回目に切れたあとの、回復の速さの倍率</summary>
    public float RegenMultiplierFor(int n)
    {
        if (n <= 0 || regenMulByDepletion == null || regenMulByDepletion.Length == 0) return 1f;
        int i = Mathf.Clamp(n - 1, 0, regenMulByDepletion.Length - 1);
        return Mathf.Max(0f, regenMulByDepletion[i]);
    }

    /// <summary>今の回復の速さの倍率。スタミナ切れからの回復中だけ、切れた回数に応じて遅くなる。</summary>
    public float RegenMultiplier { get { return Recovering ? RegenMultiplierFor(DepletionCount) : 1f; } }

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
        if (Frozen) { input = Vector2.zero; digHeld = false; } // フリーズ中は、動けない・掘れない
        if (MoleInput.RefillPressed()) { Stamina = MaxStaminaCurrent; Exhausted = false; } // デバッグ用

        if (Exhausted && Stamina >= exhaustRecoverThreshold) Exhausted = false;

        // 疲れて掘れないのに、土に向かって何度も掘ろうとすると、フリーズする
        bool digEdge = digHeld && !prevDigHeld;
        prevDigHeld = digHeld;
        if (!Frozen && Exhausted && digHeld && input.sqrMagnitude > 0.0001f)
        {
            Vector2 ahead = (Vector2)transform.position + input.normalized * 0.12f;
            if (Blocked(ahead) && (digEdge || Time.time - lastAttemptTime >= attemptRepeatSeconds)) RegisterDigAttempt();
        }

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
                Stamina = Mathf.Min(MaxStaminaCurrent, Stamina + regenPerSecond * RegenMultiplier * dt);
        }

        // 感情(安心)による継続回復(掘っていても回復する)
        float bonus = RegenBonus;
        if (bonus > 0f) Stamina = Mathf.Min(MaxStaminaCurrent, Stamina + bonus * dt);

        // 満タンに戻ったら、スタミナ切れからの「回復中」を終える
        if (Recovering && Stamina >= MaxStaminaCurrent - 0.01f) Recovering = false;

        // 地上に戻ったら、スタミナが切れた回数をリセットする
        if (resetDepletionOnSurface && DepletionCount > 0 && transform.position.y > terrain.TopY) ResetDepletion();
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

/// <summary>スタミナが0になった(回数を数え、回復中にする)</summary>
    void OnDepleted()
    {
        DepletionCount++;
        Recovering = true;
        LastDepletedTime = Time.time;
    }

    /// <summary>スタミナが切れた回数を0に戻す(回復の速さも元に戻る)</summary>
    public void ResetDepletion()
    {
        DepletionCount = 0;
        Recovering = false;
    }

/// <summary>疲れて掘れないのに、掘ろうとした。回数を数えて、たまるとフリーズする。</summary>
    void RegisterDigAttempt()
    {
        float now = Time.time;
        if (now - lastAttemptTime > attemptWindowSeconds) attempts = 0; // 間があいたので、数え直す
        attempts++;
        lastAttemptTime = now;
        LastFailedDigTime = now;
        if (attempts >= freezeAfterAttempts) StartFreeze();
    }

    /// <summary>疲れて動けなくなる。freezeSeconds の間、動けない・掘れない。(回復は進む)</summary>
    void StartFreeze()
    {
        frozenUntil = Time.time + freezeSeconds;
        attempts = 0;
        var thought = GetComponent<MoleThought>();
        if (thought != null) thought.Say("はぁ…はぁ…", freezeSeconds); // 息が上がっている吹き出し
    }

    /// <summary>デバッグ用: いますぐフリーズさせる(確認用)</summary>
    public void DebugFreezeNow() { StartFreeze(); }


    /// <summary>デバッグ用: スタミナを全回復する(切れた回数はそのまま)</summary>
public void DebugRefill()
    {
        Stamina = MaxStaminaCurrent;
        Exhausted = false;
        Recovering = false;
        frozenUntil = 0f; // フリーズも解く
        attempts = 0;
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
                float speedMul = DigMul / Mathf.Lerp(1f, hard, hardnessSpeedInfluence) * TiredDigMul; // 疲れているほど、ゆっくり掘る
                float costMul = Mathf.Lerp(1f, hard, hardnessStaminaInfluence) * CostMul;

                Vector2 target = pos + dir * digSpeed * speedMul * dt;
                if (!ObstacleBlocked(target)) // 障害物の中へは掘り進めない
                {
                    int newPixels = terrain.Dig(target, BrushRadiusPx);
                    Stamina = Mathf.Max(0f, Stamina - newPixels * staminaPerPixel * costMul);
                    if (newPixels > 0) dugThisFrame = true;
                    if (Stamina <= 0f)
                    {
                        OnDepleted(); // スタミナが切れた回数を数える
                        if (exhaustRecoverThreshold > 0f) Exhausted = true;
                    }
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
