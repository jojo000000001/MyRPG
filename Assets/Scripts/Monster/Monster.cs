using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物基类，统一处理生命值、受击无敌、受击动画和死亡入口。
/// </summary>
public abstract class Monster : MonoBehaviour, IDamageable
{
    private static readonly List<Monster> ActiveInstances = new List<Monster>();

    [Header("生命值")]
    [Tooltip("最大生命值（基础 HP）。")]
    [SerializeField] protected int maxHp = 30;
    [Tooltip("物理护甲，减伤倍率 = 100 / (100 + 护甲)。")]
    [SerializeField] protected int armor;
    [Tooltip("魔法抗性，减伤倍率 = 100 / (100 + 魔抗)。")]
    [SerializeField] protected int magicResistance;
    [Tooltip("受击后的无敌时间（秒），期间不会再次扣血。")]
    [SerializeField] protected float invulnSeconds = 0.15f;

    [Header("等级与战斗")]
    [Tooltip("怪物等级。")]
    [SerializeField] protected int level = 1;
    [Tooltip("死亡时是否给玩家发放经验。")]
    [SerializeField] protected bool grantsExperienceOnDeath;
    [Tooltip("死亡时给予玩家的经验值。")]
    [SerializeField] protected int xpRewardOnDeath = 20;
    [Tooltip("死亡时是否给玩家发放卢比。")]
    [SerializeField] protected bool grantsRupeesOnDeath = true;
    [Tooltip("死亡时给予玩家的卢比。")]
    [SerializeField] protected int rupeeRewardOnDeath = 40;
    [Tooltip("额外伤害百分比，10 表示 +10%。")]
    [SerializeField] protected float damageBonusPercent;
    [Tooltip("暴击率（0.05 = 5%）。")]
    [SerializeField, Range(0f, 1f)] protected float critChance;
    [Tooltip("暴击伤害倍率（1.5 = 150% 伤害）。")]
    [SerializeField] protected float critDamageMultiplier = 1.5f;
    [Tooltip("攻击吸血比例（按造成伤害回血，0.05 = 5%）。")]
    [SerializeField, Range(0f, 1f)] protected float lifeStealPercent;

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
    private int prefabMaxHp;
    private bool prefabGrantsRupeesOnDeath;
    private int prefabRupeeRewardOnDeath;

    /// <summary>当前生命值。</summary>
    public int CurrentHp => hp;
    /// <summary>最大生命值。</summary>
    public int MaxHp => maxHp;
    /// <summary>是否已死亡。</summary>
    public bool IsDead => hp <= 0;

    public void SetMaxHp(int value)
    {
        maxHp = Mathf.Max(1, value);
        hp = maxHp;
    }

    public void SetRupeeRewardOnDeath(int amount)
    {
        rupeeRewardOnDeath = Mathf.Max(0, amount);
        grantsRupeesOnDeath = rupeeRewardOnDeath > 0;
    }

    /// <summary>物理护甲。</summary>
    public int Armor => armor;
    /// <summary>魔法抗性。</summary>
    public int MagicResistance => magicResistance;

    public int GetDefense(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Physical:
                return Mathf.Max(0, armor);
            case DamageType.Magic:
                return Mathf.Max(0, magicResistance);
            default:
                return 0;
        }
    }
    /// <summary>怪物等级。</summary>
    public int Level => Mathf.Max(1, level);
    /// <summary>额外伤害百分比。</summary>
    public float DamageBonusPercent => damageBonusPercent;
    /// <summary>暴击率（0~1）。</summary>
    public float CritChance => critChance;
    /// <summary>暴击伤害倍率。</summary>
    public float CritDamageMultiplier => critDamageMultiplier;
    /// <summary>攻击吸血比例（0~1）。</summary>
    public float LifeStealPercent => lifeStealPercent;

    public event Action Died;

    public virtual Vector3 GetAttackAssistFacingPoint() => transform.position;

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
        prefabMaxHp = Mathf.Max(1, maxHp);
        hp = prefabMaxHp;
        prefabGrantsRupeesOnDeath = grantsRupeesOnDeath;
        prefabRupeeRewardOnDeath = Mathf.Max(0, rupeeRewardOnDeath);
        animator = GetComponent<Animator>();
        hitFeedback = GetComponent<HitFeedback>();
        if (hitFeedback == null)
            hitFeedback = gameObject.AddComponent<HitFeedback>();
    }

    protected void RestorePrefabVitals()
    {
        int restoredMaxHp = prefabMaxHp > 0 ? prefabMaxHp : Mathf.Max(1, maxHp);
        maxHp = restoredMaxHp;
        hp = restoredMaxHp;
        grantsRupeesOnDeath = prefabGrantsRupeesOnDeath;
        rupeeRewardOnDeath = Mathf.Max(0, prefabRupeeRewardOnDeath);
    }

    protected virtual void OnEnable()
    {
        if (!ActiveInstances.Contains(this))
            ActiveInstances.Add(this);

        ApplyMonsterCollisionLayer();
    }

    private void ApplyMonsterCollisionLayer()
    {
        ApplyMonsterCollisionLayer(this);
    }

    protected virtual void OnDisable()
    {
        ActiveInstances.Remove(this);
    }

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

    public virtual bool ShouldShowHealthBar => !IsDead;

    /// <summary>读档还原存活单位的当前生命。下限为 1，死亡走 RemoveForSaveRestore。</summary>
    public void SetCurrentHp(int value)
    {
        hp = Mathf.Clamp(value, 1, Mathf.Max(1, maxHp));
    }

    /// <summary>
    /// 读档时清掉已死亡单位：不发奖励、不触发 Died。默认只关掉物体。
    /// </summary>
    public virtual void RemoveForSaveRestore()
    {
        UnregisterWithoutDeath();
        gameObject.SetActive(false);
    }

    /// <summary>从活动列表移除并标为死亡，不走掉落和任务回调。</summary>
    protected void UnregisterWithoutDeath()
    {
        hp = 0;
        ActiveInstances.Remove(this);
    }

    public virtual bool TryTakeDamage(int damage)
    {
        return TryTakeDamage(new DamageInfo(damage, null, transform.position, Vector3.zero));
    }

    public virtual bool TryTakeDamage(DamageInfo damage)
    {
        if (IsDead) return false;
        if (Time.time - lastHitTime < invulnSeconds) return false;

        int appliedDamage = Mathf.Max(0, damage.amount);
        if (appliedDamage == 0) return false;

        lastHitTime = Time.time;
        hp = Mathf.Max(0, hp - appliedDamage);

        PlayHitFeedback(damage);
        DamageNumberSpawner.Show(damage, transform.position);
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
        GrantExperienceToPlayer();
        GrantRupeesToPlayer();
        ActiveInstances.Remove(this);
        Died?.Invoke();
    }

    protected int ResolveOutgoingDamage(int baseDamage, int targetDefense, DamageType damageType, out bool isCritical)
    {
        int defense = damageType == DamageType.TrueDamage ? 0 : targetDefense;
        return CombatDamageFormulas.Calculate(
            baseDamage,
            damageBonusPercent,
            critChance,
            critDamageMultiplier,
            defense,
            out isCritical);
    }

    protected void TryApplyAttackLifeSteal(int damageDealt)
    {
        if (damageDealt <= 0 || lifeStealPercent <= 0f)
            return;

        int healAmount = Mathf.Max(0, Mathf.RoundToInt(damageDealt * lifeStealPercent));
        if (healAmount <= 0)
            return;

        hp = Mathf.Min(maxHp, hp + healAmount);
    }

    private void GrantExperienceToPlayer()
    {
        if (!grantsExperienceOnDeath || xpRewardOnDeath <= 0)
            return;

        Player player = Player.Resolve();

        player?.TryGainExperience(xpRewardOnDeath);
    }

    private void GrantRupeesToPlayer()
    {
        if (!grantsRupeesOnDeath || rupeeRewardOnDeath <= 0)
            return;

        Player player = Player.Resolve();
        player?.AddRupees(rupeeRewardOnDeath);
    }

    private void PlayHitFeedback(DamageInfo damage)
    {
        if (hitFeedback == null)
            return;

        string animatorBool = hitFlagSeconds > 0f ? hitBoolParam : string.Empty;
        hitFeedback.Play(animator, hitTriggerParam, animatorBool, hitFlagSeconds, damage);
    }

    public static IReadOnlyList<Monster> Active => ActiveInstances;

    private static void ApplyMonsterCollisionLayer(Monster monster)
    {
        int layer = LayerMask.NameToLayer("Monster");
        if (layer < 0 || monster == null)
            return;

        Collider[] colliders = monster.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (collider.bounds.extents.x > 20f
                || collider.bounds.extents.y > 20f
                || collider.bounds.extents.z > 20f)
                continue;

            collider.gameObject.layer = layer;
        }
    }

    protected virtual void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        armor = Mathf.Max(0, armor);
        magicResistance = Mathf.Max(0, magicResistance);
        level = Mathf.Max(1, level);
        critChance = Mathf.Clamp01(critChance);
        lifeStealPercent = Mathf.Clamp01(lifeStealPercent);
        critDamageMultiplier = Mathf.Max(1f, critDamageMultiplier);
        xpRewardOnDeath = Mathf.Max(0, xpRewardOnDeath);
        rupeeRewardOnDeath = Mathf.Max(0, rupeeRewardOnDeath);
    }
}
