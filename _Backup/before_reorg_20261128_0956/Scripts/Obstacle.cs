using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 障害物(✕)。円形の通行止め。掘削中でも通れない(掘って迂回する)。
/// MoleController が Obstacle.All を参照して判定する。
/// </summary>
public class Obstacle : MonoBehaviour
{
    [Tooltip("通行止めの半径(ユニット)。モグラの体の半径はこれに加算される")]
    public float radius = 0.4f;

    static readonly List<Obstacle> all = new List<Obstacle>();
    public static IReadOnlyList<Obstacle> All => all;

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    Detectable detectable;

    /// <summary>ぶつかったときに一時的に姿を見せる(隠れている障害物用)。</summary>
    public void Bump(float seconds)
    {
        if (detectable == null) detectable = GetComponent<Detectable>();
        if (detectable != null && detectable.hiddenUntilScanned) detectable.Flash(seconds);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
