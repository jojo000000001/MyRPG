/// <summary>
/// 可承受伤害的统一接口，攻击盒只需要依赖它即可打到不同类型的目标。
/// </summary>
public interface IDamageable
{
    bool TryTakeDamage(DamageInfo damage);
}
