using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物基类，统一处理生命值、受击无敌、受击动画和死亡入口。
/// </summary>
public abstract class Monster : MonoBehaviour, IDamageable
{
    private static readonly List<Monster> ActiveInstances = new List<Monster>();

    // 血量和受击间隔由子类共用，避免同一帧或连触发器重复扣血。
    [Header("Health")]
    [SerializeField] protected int maxHp = 30;
    [SerializeField] protected float invulnSeconds = 0.15f;

    [Header("Animation")]
    [SerializeField] private string hitTriggerParam = "";
    [SerializeField] private string hitBoolParam = "HitBool";
    [SerializeField] private float hitFlagSeconds = 0.25f;
    [Tooltip("Animator float for locomotion: 0 = idle, 1 = run.")]
    [SerializeField] private string speedFloatParam = "Speed";

    protected int hp;
    protected float lastHitTime = -999f;
    protected Animator animator;
    private HitFeedback hitFeedback;

    public int CurrentHp => hp;
    public int MaxHp => maxHp;
    public bool IsDead => hp <= 0;

    public event Action Died;

    /// <summary>
    /// 玩家攻击辅助瞄准点。大体型 Boss 可覆写为头颈附近。
    /// </summary>
    public virtual Vector3 GetAttackAssistFacingPoint() => transform.position;

    /// <summary>
    /// 玩家攻击辅助的额外搜索半径（米）。
    /// </summary>
    public virtual float GetAttackAssistRangeBonus() => 0f;

    protected static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    protected static void RotateYawToward(Transform self, Vector3 worldDirection, float degreesPerSecond, bool snap = false)
    {
        if (worldDirection.sqrMagnitude < 0.0001f)
            return;

        float targetYaw = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
        if (snap)
        {
            self.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            return;
        }

        float yaw = Mathf.MoveTowardsAngle(self.eulerAngles.y, targetYaw, degreesPerSecond * Time.deltaTime);
        self.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    protected virtual void Awake()
    {
        hp = Mathf.Max(1, maxHp);
        animator = GetComponent<Animator>();
        hitFeedback = GetComponent<HitFeedback>();
        if (hitFeedback == null)
            hitFeedback = gameObject.AddComponent<HitFeedback>();
    }

    protected virtual void OnEnable()
    {
        if (!ActiveInstances.Contains(this))
            ActiveInstances.Add(this);
    }

    protected virtual void OnDisable()
    {
        ActiveInstances.Remove(this);
    }

    /// <summary>
    /// 在半径内查找最近的存活怪物，避免每帧 FindObjectsOfType。
    /// </summary>
    public static bool TryFindNearestLiving(Vector3 origin, float maxRadius, out Monster nearest, out float distance)
    {
        nearest = null;
        distance = float.MaxValue;

        float bestSq = float.MaxValue;

        for (int i = ActiveInstances.Count - 1; i >= 0; i--)
        {
            Monster monster = ActiveInstances[i];
            if (monster == null)
            {
                ActiveInstances.RemoveAt(i);
                continue;
            }

            if (monster.IsDead)
                continue;

            float allowedRadius = maxRadius + monster.GetAttackAssistRangeBonus();
            float allowedRadiusSq = allowedRadius * allowedRadius;

            Vector3 anchor = monster.GetAttackAssistFacingPoint();
            Vector3 toOrigin = origin - anchor;
            toOrigin.y = 0f;
            float distSq = toOrigin.sqrMagnitude;
            if (distSq > allowedRadiusSq)
                continue;

            if (distSq > bestSq)
                continue;

            bestSq = distSq;
            nearest = monster;
        }

        if (nearest == null)
            return false;

        distance = Mathf.Sqrt(bestSq);
        return true;
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

        PlayHitFeedback(damage);
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
        ActiveInstances.Remove(this);
        Died?.Invoke();
    }

    // 用短暂布尔参数驱动受击动画，随后自动复位。
private void PlayHitFeedback(DamageInfo damage)
    {
        if (hitFeedback != null)
            hitFeedback.Play(animator, hitTriggerParam, hitBoolParam, hitFlagSeconds, damage);
    }
}
