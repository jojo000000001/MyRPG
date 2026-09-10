using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attack hitbox: opens only during attack active frames, then forwards DamageInfo to Hurtbox.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    [Header("伤害")]
    [Tooltip("固定伤害值；启用「使用攻击者攻击力」时作为备用。")]
    [SerializeField] private int damage = 10;
    [Tooltip("伤害类型：物理/魔法/真实。")]
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("是否使用攻击者的攻击力作为基础伤害。")]
    [SerializeField] private bool useSourceAttackPower = true;
    
    [Header("Impact")]
    [SerializeField] private bool playImpactFeedback = true;
    [SerializeField] private float hitStopSeconds = 0.045f;
    [SerializeField, Range(0.01f, 1f)] private float hitStopTimeScale = 0.08f;
    [SerializeField] private float cameraShakeSeconds = 0.12f;
    [SerializeField] private float cameraShakeStrength = 0.075f;
    [SerializeField] private float cameraShakeFrequency = 38f;

    [Header("Audio")]
    [SerializeField] private bool playHitSound = true;
    [SerializeField] private AudioClip hitSoundClip;
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 0.85f;
    [SerializeField] private Vector2 hitSoundPitchRange = new Vector2(0.96f, 1.04f);

    [Header("Timing")]
    [SerializeField] private float activeSeconds = 0.12f;
    [SerializeField] private bool closeAfterFirstHit;
    [Tooltip("动画 Close 后，若此时间内没有新的 Open/攻击续接，再关闭判定盒。")]
    [SerializeField] private float idleCloseSeconds = 0.3f;

    [Header("Facing")]
    [SerializeField] private bool requireForwardArc = true;
    [SerializeField, Range(1f, 360f)] private float forwardArcDegrees = 150f;

    [Header("Debug")]
    [SerializeField] private bool drawActiveGizmo = true;

    // 运行时缓存触发器，并用 active 控制本次攻击是否还能命中。
    private Collider col;
    private bool active;

    private readonly HashSet<Hurtbox> hitTargets = new HashSet<Hurtbox>();
    private Coroutine disableRoutine;
    private Coroutine idleCloseRoutine;
    private bool impactPlayedThisSwing;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        activeSeconds = Mathf.Max(0.01f, activeSeconds);
        idleCloseSeconds = Mathf.Max(0f, idleCloseSeconds);
        forwardArcDegrees = Mathf.Clamp(forwardArcDegrees, 1f, 360f);
        hitStopSeconds = Mathf.Max(0f, hitStopSeconds);
        hitStopTimeScale = Mathf.Clamp(hitStopTimeScale, 0.01f, 1f);
        cameraShakeSeconds = Mathf.Max(0f, cameraShakeSeconds);
        cameraShakeStrength = Mathf.Max(0f, cameraShakeStrength);
        cameraShakeFrequency = Mathf.Max(1f, cameraShakeFrequency);
        hitSoundVolume = Mathf.Clamp01(hitSoundVolume);
        hitSoundPitchRange.x = Mathf.Max(0.1f, hitSoundPitchRange.x);
        hitSoundPitchRange.y = Mathf.Max(hitSoundPitchRange.x, hitSoundPitchRange.y);
    }


    /// <summary>
    /// 开启一次短暂的攻击判定窗口，通常由攻击动画或输入触发。
    /// </summary>
    public void ActivateOnce()
    {
        ActivateForSeconds(activeSeconds);
    }

    public void ActivateForSeconds(float seconds)
    {
        OpenHitbox();

        if (disableRoutine != null)
            StopCoroutine(disableRoutine);

        disableRoutine = StartCoroutine(DisableAfter(Mathf.Max(0.01f, seconds)));
    }

    public void OpenHitbox()
    {
        if (col == null)
            col = GetComponent<Collider>();

        CancelIdleClose();

        if (disableRoutine != null)
        {
            StopCoroutine(disableRoutine);
            disableRoutine = null;
        }

        active = true;
        impactPlayedThisSwing = false;
        hitTargets.Clear();
        col.enabled = true;
        ProcessOverlappingHurtboxes();
    }

    /// <summary>动画挥砍结束：不立刻关盒，等待 idle 时间且没有续攻再关。</summary>
    public void ScheduleIdleClose(float seconds = -1f)
    {
        if (!active)
            return;

        if (seconds < 0f)
            seconds = idleCloseSeconds;

        if (seconds <= 0f)
        {
            CloseHitbox();
            return;
        }

        CancelIdleClose();
        idleCloseRoutine = StartCoroutine(IdleCloseAfter(seconds));
    }

    public void CancelIdleClose()
    {
        if (idleCloseRoutine == null)
            return;

        StopCoroutine(idleCloseRoutine);
        idleCloseRoutine = null;
    }

    public void CloseHitbox()
    {
        CancelIdleClose();

        if (disableRoutine != null)
        {
            StopCoroutine(disableRoutine);
            disableRoutine = null;
        }

        SetColliderActive(false);
    }

    private IEnumerator IdleCloseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        idleCloseRoutine = null;
        SetColliderActive(false);
    }


    private IEnumerator DisableAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        disableRoutine = null;
        SetColliderActive(false);
    }

    private void SetColliderActive(bool enabled)
    {
        if (col == null)
            col = GetComponent<Collider>();

        active = enabled;
        col.enabled = enabled;

        if (!enabled)
            hitTargets.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHitCollider(other);
    }

    private void ProcessOverlappingHurtboxes()
    {
        if (!active || col == null)
            return;

        Bounds bounds = col.bounds;
        Collider[] overlaps = Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            col.transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlaps.Length; i++)
            TryHitCollider(overlaps[i]);
    }

    private void TryHitCollider(Collider other)
    {
        if (!active || other == null)
            return;

        if (other == col || other.transform.root == transform.root)
            return;

        Hurtbox hurtbox = other.GetComponent<Hurtbox>() ?? other.GetComponentInParent<Hurtbox>();
        if (hurtbox == null || hitTargets.Contains(hurtbox))
            return;

        if (!IsInsideForwardArc(hurtbox.transform.position))
            return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 direction = hurtbox.transform.position - transform.root.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = transform.root.forward;

        int baseDamage = GetBaseDamageAmount();
        bool isCritical = false;
        IDamageable target = hurtbox.GetDamageable();
        int targetDefense = CombatDamageFormulas.GetTargetDefense(target, damageType);

        Player player = transform.root.GetComponent<Player>();
        int finalDamage = player != null
            ? player.ResolveOutgoingDamage(baseDamage, targetDefense, damageType, out isCritical)
            : Mathf.Max(0, baseDamage);

        DamageInfo damageInfo = new DamageInfo(
            finalDamage,
            transform.root.gameObject,
            hitPoint,
            direction,
            damageType,
            isCritical);
        if (!hurtbox.ApplyDamage(damageInfo))
            return;

        hitTargets.Add(hurtbox);
        PlayImpactOnce();
        TryApplyLifeSteal(finalDamage);
        transform.root.GetComponent<Player>()?.NotifyCombat();

        if (closeAfterFirstHit)
            CloseHitbox();
    }

    private int GetBaseDamageAmount()
    {
        if (useSourceAttackPower)
        {
            var player = transform.root.GetComponent<Player>();
            if (player != null)
                return player.AttackPower;
        }

        return Mathf.Max(0, damage);
    }

    private void TryApplyLifeSteal(int damageDealt)
    {
        if (damageDealt <= 0)
            return;

        transform.root.GetComponent<Player>()?.TryApplyLifeStealFromAttack(damageDealt);
    }


    private bool IsInsideForwardArc(Vector3 targetPosition)
    {
        if (!requireForwardArc || forwardArcDegrees >= 359f)
            return true;

        Vector3 toTarget = targetPosition - transform.root.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return true;

        Vector3 forward = transform.root.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return true;

        float angle = Vector3.Angle(forward, toTarget);
        return angle <= forwardArcDegrees * 0.5f;
    }

    private void PlayImpactOnce()
    {
        if (impactPlayedThisSwing)
            return;

        impactPlayedThisSwing = true;
        PlayHitSound();

        if (!playImpactFeedback)
            return;

        HitImpactManager.PlayImpact(
            hitStopSeconds,
            hitStopTimeScale,
            cameraShakeSeconds,
            cameraShakeStrength,
            cameraShakeFrequency);
    }

    private void PlayHitSound()
    {
        if (!playHitSound || hitSoundClip == null)
            return;

        Vector3 position = transform.root.position + Vector3.up * 1.2f;
        float pitch = Random.Range(hitSoundPitchRange.x, hitSoundPitchRange.y);

        var temp = new GameObject("AttackHitSfx");
        temp.transform.position = position;

        AudioSource source = temp.AddComponent<AudioSource>();
        source.clip = hitSoundClip;
        source.volume = hitSoundVolume;
        source.pitch = pitch;
        source.spatialBlend = 0.35f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 24f;
        source.Play();

        Destroy(temp, hitSoundClip.length / Mathf.Max(0.01f, pitch) + 0.05f);
    }

    private void OnDrawGizmos()
    {
        if (!drawActiveGizmo || !Application.isPlaying || !active || col == null)
            return;

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
        Bounds bounds = col.bounds;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawCube(bounds.center, bounds.size);
    }
}
