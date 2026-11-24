using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 探知(スキャン)に反応する物体。
/// hiddenUntilScanned=true の間は見えず、スキャンの波が当たると一定時間だけ現れて、フェードアウトする。
/// </summary>
public class Detectable : MonoBehaviour
{
    [Tooltip("true: スキャンするまで見えない")]
    public bool hiddenUntilScanned = true;
    [Tooltip("画面外にいるときの方向マーカーの色")]
    public Color markerColor = Color.white;
    [Tooltip("見つかったとき、画面外にいたら画面の端に方向マーカーを出す")]
    public bool showMarker = true;
    [Tooltip("消える前にフェードアウトする時間(秒)")]
    public float fadeOutTime = 1f;
    [Tooltip("見つかった瞬間の拡大演出")]
    public float popTime = 0.3f;
    public float popScale = 1.5f;

    static readonly List<Detectable> all = new List<Detectable>();
    public static IReadOnlyList<Detectable> All => all;

    SpriteRenderer sr;
    Vector3 baseScale;
    float remaining;
    float total;

    /// <summary>スキャンで見つかっている最中か</summary>
    public bool IsPinged => remaining > 0f;
    /// <summary>現在の見え方(0〜1)</summary>
    public float Alpha { get; private set; }

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        Alpha = hiddenUntilScanned ? 0f : 1f;
        ApplyVisual(1f);
    }

    public void Reveal(float duration)
    {
        remaining = duration;
        total = duration;
    }

/// <summary>短時間だけ姿を見せる(ぶつかったとき用)。すでに見えている間は、最初の拡大演出をやり直さない。</summary>
    public void Flash(float duration)
    {
        if (remaining <= 0f)
        {
            remaining = duration;
            total = duration;
        }
        else if (remaining < duration)
        {
            total += duration - remaining;
            remaining = duration;
        }
    }


    void Update()
    {
        if (remaining > 0f) remaining -= Time.deltaTime;

        float a;
        if (!hiddenUntilScanned) a = 1f;
        else if (remaining <= 0f) a = 0f;
        else a = remaining > fadeOutTime ? 1f : remaining / Mathf.Max(0.01f, fadeOutTime);
        Alpha = a;

        float scale = 1f;
        if (remaining > 0f)
        {
            float age = total - remaining;
            if (age < popTime) scale = Mathf.Lerp(popScale, 1f, age / Mathf.Max(0.01f, popTime));
        }
        ApplyVisual(scale);
    }

    void ApplyVisual(float scale)
    {
        transform.localScale = baseScale * scale;
        if (sr == null) return;
        var c = sr.color;
        c.a = Alpha;
        sr.color = c;
        sr.enabled = Alpha > 0.001f;
    }
}
