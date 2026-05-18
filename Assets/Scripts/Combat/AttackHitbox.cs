using System.Collections;
using UnityEngine;

/// <summary>
/// 攻击判定盒：只在攻击窗口内短暂开启触发器，命中 Hurtbox 后传递 DamageInfo。
/// </summary>
[RequireComponent(typeof(Collider))]
/// <summary>
/// 攻击判定盒：只在攻击窗口内短暂开启触发器，命中 Hurtbox 后传递 DamageInfo。
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    // 没有攻击者属性时使用的兜底伤害。
    [SerializeField] private int damage = 10;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [SerializeField] private bool useSourceAttackPower = true;
    [SerializeField] private float activeSeconds = 0.12f;

    // 运行时缓存触发器，并用 active 控制本次攻击是否还能命中。
    private Collider col;
    private bool active;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;
    }

    /// <summary>
    /// 开启一次短暂的攻击判定窗口，通常由攻击动画或输入触发。
    /// </summary>
    public void ActivateOnce()
    {
        if (active) return;

        active = true;
        col.enabled = true;
        StartCoroutine(DisableAfter(activeSeconds));
    }

    private IEnumerator DisableAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        col.enabled = false;
        active = false;
    }

    // 触发命中时，优先寻找对方身上的 Hurtbox，再把伤害数据交给真正的 IDamageable。
    private void OnTriggerEnter(Collider other)
    {
        if (!active) return;
        if (other.transform.root == transform.root) return;

        var hb = other.GetComponent<Hurtbox>() ?? other.GetComponentInParent<Hurtbox>();
        if (hb == null) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 direction = other.transform.position - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = transform.forward;

        var damageInfo = new DamageInfo(GetDamageAmount(), transform.root.gameObject, hitPoint, direction, damageType);
        if (hb.ApplyDamage(damageInfo))
        {
            col.enabled = false;
            active = false;
        }
    }

    private int GetDamageAmount()
    {
        if (useSourceAttackPower)
        {
            var player = transform.root.GetComponent<Player>();
            if (player != null)
                return player.AttackPower;
        }

        return Mathf.Max(0, damage);
    }
}
