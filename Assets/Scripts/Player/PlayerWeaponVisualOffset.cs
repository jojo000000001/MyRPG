using UnityEngine;

/// <summary>
/// Offsets only the weapon visual pivot during attacks. Hitbox stays on weapon_r.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponVisualOffset : MonoBehaviour
{
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualPivot;

    [Header("Attack Visual Offset (local space)")]
    [SerializeField] private AttackVisualProfile attack1 = new AttackVisualProfile(
        new Vector3(-0.42f, 0.06f, 0f),
        new Vector3(0f, -18f, 6f));

    [SerializeField] private AttackVisualProfile attack2 = new AttackVisualProfile(
        new Vector3(-0.36f, 0.1f, 0f),
        new Vector3(0f, -14f, 8f));

    [SerializeField] private AttackVisualProfile attack3 = new AttackVisualProfile(
        new Vector3(-0.48f, 0.02f, 0f),
        new Vector3(0f, -22f, 10f));

    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;

    [System.Serializable]
    private struct AttackVisualProfile
    {
        public Vector3 localPosition;
        public Vector3 localEulerAngles;

        public AttackVisualProfile(Vector3 localPosition, Vector3 localEulerAngles)
        {
            this.localPosition = localPosition;
            this.localEulerAngles = localEulerAngles;
        }
    }

    private void Awake()
    {
        if (visualPivot == null)
            visualPivot = transform;

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        defaultLocalPosition = visualPivot.localPosition;
        defaultLocalRotation = visualPivot.localRotation;
    }

    private void LateUpdate()
    {
        if (animator == null || visualPivot == null)
            return;

        if (!animator.GetBool(IsAttackingHash))
        {
            ResetVisualPivot();
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Attack01_SwordAndShiled"))
            ApplyProfile(attack1);
        else if (state.IsName("Attack02_SwordAndShiled"))
            ApplyProfile(attack2);
        else if (state.IsName("Attack03_SwordAndShiled"))
            ApplyProfile(attack3);
        else
            ResetVisualPivot();
    }

    private void ApplyProfile(AttackVisualProfile profile)
    {
        visualPivot.localPosition = defaultLocalPosition + profile.localPosition;
        visualPivot.localRotation = defaultLocalRotation * Quaternion.Euler(profile.localEulerAngles);
    }

    private void ResetVisualPivot()
    {
        visualPivot.localPosition = defaultLocalPosition;
        visualPivot.localRotation = defaultLocalRotation;
    }
}
