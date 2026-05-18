using UnityEngine;

/// <summary>
/// 受击盒：负责把碰撞命中转发给父级或指定对象上的 IDamageable。
/// </summary>
public class Hurtbox : MonoBehaviour
{
    // 可手动指定承伤对象；未指定时会沿父级自动查找。
    [SerializeField] private MonoBehaviour damageableTarget;

    private IDamageable damageable;

    private void Awake()
    {
        damageable = damageableTarget as IDamageable;
        if (damageable == null)
            damageable = FindDamageableInParents();
    }

    // 沿父物体查找实现 IDamageable 的脚本，让模型子节点也能作为受击区域。
    private IDamageable FindDamageableInParents()
    {
        var behaviours = GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            var candidate = behaviours[i] as IDamageable;
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// 对外入口：攻击盒把伤害交给 Hurtbox，再由 Hurtbox 转给承伤逻辑。
    /// </summary>
    public bool ApplyDamage(DamageInfo damage)
    {
        if (damageable == null)
            damageable = FindDamageableInParents();

        if (damageable == null) return false;
        return damageable.TryTakeDamage(damage);
    }

    public bool ApplyDamage(int amount)
    {
        return ApplyDamage(new DamageInfo(amount, null, transform.position, Vector3.zero));
    }
}
