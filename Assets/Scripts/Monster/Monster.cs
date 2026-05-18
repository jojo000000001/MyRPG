using UnityEngine;

/// <summary>
/// 怪物基类，统一处理生命值、受击无敌、受击动画和死亡入口。
/// </summary>
public abstract class Monster : MonoBehaviour, IDamageable
{
    // 血量和受击间隔由子类共用，避免同一帧或连触发器重复扣血。
    [Header("Health")]
    [SerializeField] protected int maxHp = 30;
    [SerializeField] protected float invulnSeconds = 0.15f;

    [Header("Animation")]
    [SerializeField] private string hitBoolParam = "HitBool";
    [SerializeField] private float hitFlagSeconds = 0.05f;
    [Tooltip("Animator float for locomotion: 0 = idle, 1 = run.")]
    [SerializeField] private string speedFloatParam = "Speed";

    protected int hp;
    protected float lastHitTime = -999f;
    protected Animator animator;

    public int CurrentHp => hp;
    public int MaxHp => maxHp;
    public bool IsDead => hp <= 0;

    protected virtual void Awake()
    {
        hp = Mathf.Max(1, maxHp);
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 兼容只传数值的旧调用，内部转换成完整的 DamageInfo。
    /// </summary>
    public virtual bool TryTakeDamage(int damage)
    {
        return TryTakeDamage(new DamageInfo(damage, null, transform.position, Vector3.zero));
    }

    /// <summary>
    /// 尝试承受一次伤害；返回 false 表示死亡、无敌或无有效伤害。
    /// </summary>
    public virtual bool TryTakeDamage(DamageInfo damage)
    {
        if (IsDead) return false;
        if (Time.time - lastHitTime < invulnSeconds) return false;

        int appliedDamage = Mathf.Max(0, damage.amount);
        if (appliedDamage == 0) return false;

        lastHitTime = Time.time;
        hp = Mathf.Max(0, hp - appliedDamage);

        PlayHitFeedback();
        OnDamaged(appliedDamage);

        if (IsDead)
            OnDeath();

        return true;
    }

    public void SetLocomotionSpeed01(float speed01)
    {
        if (!animator) return;
        animator.SetFloat(speedFloatParam, Mathf.Clamp01(speed01));
    }

    protected virtual void OnDamaged(int damage)
    {
    }

    protected virtual void OnDeath()
    {
        SetLocomotionSpeed01(0f);
    }

    // 用短暂布尔参数驱动受击动画，随后自动复位。
    private void PlayHitFeedback()
    {
        if (!animator) return;

        animator.SetBool(hitBoolParam, true);
        CancelInvoke(nameof(ResetHitFlag));
        Invoke(nameof(ResetHitFlag), hitFlagSeconds);
    }

    private void ResetHitFlag()
    {
        if (animator) animator.SetBool(hitBoolParam, false);
    }
}
