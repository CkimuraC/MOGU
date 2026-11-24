using UnityEngine;

public enum ItemKind
{
    Heal, // ● スタミナ回復
    Star, // ★ 最大スタミナを一時的に増加
    Shard // ◆ 感情のかけら(持ち物に入る)
}

/// <summary>
/// 拾えるアイテム。モグラが近づくと効果を発動して消える。
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public ItemKind kind = ItemKind.Heal;
    [Tooltip("拾える距離(ユニット)")]
    public float pickupRadius = 0.5f;

    [Header("Heal (●)")]
    public float healAmount = 60f;

    [Header("Star (★)")]
    public float boostAmount = 100f;
    public float boostDuration = 20f;
    [Tooltip("true: 増えた最大値の分だけ現在のスタミナも増やす")]
    public bool boostAlsoFills = true;

    [Header("Shard (感情のかけら)")]
    public EmotionType emotion;

    MoleController mole;

    void Start()
    {
        mole = FindFirstObjectByType<MoleController>();
    }

    void Update()
    {
        if (mole == null) return;
        Vector2 d = (Vector2)mole.transform.position - (Vector2)transform.position;
        if (d.sqrMagnitude <= pickupRadius * pickupRadius) Collect();
    }

void Collect()
    {
        switch (kind)
        {
            case ItemKind.Heal:
                mole.Heal(healAmount);
                break;
            case ItemKind.Star:
                mole.AddMaxStaminaBoost(boostAmount, boostDuration, boostAlsoFills);
                break;
            case ItemKind.Shard:
                {
                    var inv = mole.GetComponent<ShardInventory>();
                    if (inv != null) inv.Add(emotion);
                    break;
                }
        }
        Destroy(gameObject);
    }
}
