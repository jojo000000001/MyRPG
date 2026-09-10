using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 为哥布林增加第二套帽子头槌攻击，并接到 Gob1_Combat。
/// </summary>
public static class SetupGoblinSecondAttack
{
    private const string ControllerPath = "Assets/Gob_1/Gob1_Combat.controller";
    private const string SmashClipPath = "Assets/Gob_1/Animations/RigGob1_AttackSmash.anim";
    private const string IdleStateName = "RigGob1_Idle";
    private const string SlashStateName = "RigGob1_Attack";
    private const string SmashStateName = "RigGob1_AttackSmash";

    [MenuItem("Tools/Combat/Setup Goblin Second Attack")]
    public static void Setup()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("SetupGoblinSecondAttack: missing " + ControllerPath);
            return;
        }

        AnimationClip smashClip = CreateOrUpdateSmashClip();
        EnsureParameter(controller, "AttackIndex", AnimatorControllerParameterType.Int);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorState idle = FindState(root, IdleStateName);
        AnimatorState slash = FindState(root, SlashStateName);
        if (idle == null || slash == null)
        {
            Debug.LogError("SetupGoblinSecondAttack: missing idle or slash state.");
            return;
        }

        AnimatorState smash = FindState(root, SmashStateName) ?? root.AddState(SmashStateName, new Vector3(640f, -40f, 0f));
        smash.motion = smashClip;

        EnsureExitToIdle(smash, idle);
        RestrictExistingAttackTransition(root, slash);
        EnsureAnyStateAttack(root, slash, 0);
        EnsureAnyStateAttack(root, smash, 1);

        EditorUtility.SetDirty(smashClip);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("SetupGoblinSecondAttack: wired slash (index 0) and hat smash (index 1).");
    }

    private static AnimationClip CreateOrUpdateSmashClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SmashClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "RigGob1_AttackSmash", frameRate = 30f };
            AssetDatabase.CreateAsset(clip, SmashClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;

        const float Crouch = 0.28f;
        const float Coil = 0.55f;
        const float Impact = 0.68f;
        const float Follow = 0.90f;
        const float Recover = 1.12f;
        const float End = 1.32f;

        const string Root = "RigGob1";
        const string Spine2 = "RigGob1/spine/spine.001/spine.002";
        const string Spine3 = "RigGob1/spine/spine.001/spine.002/spine.003";
        const string Head = "RigGob1/spine/spine.001/spine.002/spine.003/Head";
        const string UpperR = "RigGob1/spine/spine.001/spine.002/spine.003/shoulder.R/upper_arm.R";
        const string ForeR = "RigGob1/spine/spine.001/spine.002/spine.003/shoulder.R/upper_arm.R/forearm.R";
        const string HandR = "RigGob1/spine/spine.001/spine.002/spine.003/shoulder.R/upper_arm.R/forearm.R/hand.R";
        const string UpperL = "RigGob1/spine/spine.001/spine.002/spine.003/shoulder.L/upper_arm.L";
        const string ForeL = "RigGob1/spine/spine.001/spine.002/spine.003/shoulder.L/upper_arm.L/forearm.L";
        const string HandL = "RigGob1/spine/spine.001/spine.002/spine.003/shoulder.L/upper_arm.L/forearm.L/hand.L";

        SetPosition(clip, Root,
            Curve(0f, 0f, Crouch, 0f, Coil, 0f, Impact, 0f, Follow, 0f, Recover, 0f, End, 0f),
            Curve(0f, 0f, Crouch, -0.06f, Coil, -0.08f, Impact, -0.02f, Follow, 0.01f, Recover, -0.02f, End, 0f),
            Curve(0f, 0f, Crouch, -0.10f, Coil, -0.14f, Impact, 0.12f, Follow, 0.04f, Recover, -0.04f, End, 0f));

        SetEuler(clip, Root,
            Curve(0f, 0f, Crouch, -6f, Coil, -8f, Impact, 12f, Follow, 6f, Recover, 2f, End, 0f),
            Zero(End),
            Zero(End));

        SetEuler(clip, Spine2,
            Curve(0f, 0f, Crouch, -18f, Coil, -24f, Impact, 28f, Follow, 12f, Recover, 4f, End, 0f),
            Curve(0f, 0f, Crouch, 4f, Coil, 6f, Impact, -6f, Follow, -2f, Recover, 0f, End, 0f),
            Zero(End));

        SetEuler(clip, Spine3,
            Curve(0f, 0f, Crouch, -12f, Coil, -16f, Impact, 18f, Follow, 8f, Recover, 2f, End, 0f),
            Curve(0f, 0f, Crouch, 3f, Coil, 4f, Impact, -4f, Follow, -1f, Recover, 0f, End, 0f),
            Zero(End));

        SetEuler(clip, Head,
            Curve(0f, 0f, Crouch, -32f, Coil, -42f, Impact, 58f, Follow, 22f, Recover, 8f, End, 0f),
            Zero(End),
            Curve(0f, 0f, Crouch, 6f, Coil, 8f, Impact, -10f, Follow, -4f, Recover, 0f, End, 0f));

        SetEuler(clip, UpperR,
            Curve(0f, 0f, Crouch, -88f, Coil, -102f, Impact, 28f, Follow, -18f, Recover, -36f, End, 0f),
            Curve(0f, 0f, Crouch, 28f, Coil, 34f, Impact, -8f, Follow, 6f, Recover, 10f, End, 0f),
            Curve(0f, 0f, Crouch, 36f, Coil, 42f, Impact, -12f, Follow, 8f, Recover, 12f, End, 0f));

        SetEuler(clip, ForeR,
            Curve(0f, 0f, Crouch, -42f, Coil, -52f, Impact, 18f, Follow, -8f, Recover, -16f, End, 0f),
            Zero(End),
            Curve(0f, 0f, Crouch, 16f, Coil, 20f, Impact, -10f, Follow, 4f, Recover, 6f, End, 0f));

        SetEuler(clip, HandR,
            Curve(0f, 0f, Crouch, 18f, Coil, 24f, Impact, -8f, Follow, 6f, Recover, 10f, End, 0f),
            Zero(End),
            Curve(0f, 0f, Crouch, 12f, Coil, 16f, Impact, -6f, Follow, 4f, Recover, 6f, End, 0f));

        SetEuler(clip, UpperL,
            Curve(0f, 0f, Crouch, -82f, Coil, -96f, Impact, 24f, Follow, -14f, Recover, -32f, End, 0f),
            Curve(0f, 0f, Crouch, -24f, Coil, -30f, Impact, 8f, Follow, -4f, Recover, -8f, End, 0f),
            Curve(0f, 0f, Crouch, -32f, Coil, -38f, Impact, 10f, Follow, -6f, Recover, -10f, End, 0f));

        SetEuler(clip, ForeL,
            Curve(0f, 0f, Crouch, -38f, Coil, -48f, Impact, 16f, Follow, -6f, Recover, -14f, End, 0f),
            Zero(End),
            Curve(0f, 0f, Crouch, -14f, Coil, -18f, Impact, 8f, Follow, -4f, Recover, -6f, End, 0f));

        SetEuler(clip, HandL,
            Curve(0f, 0f, Crouch, 16f, Coil, 22f, Impact, -6f, Follow, 4f, Recover, 8f, End, 0f),
            Zero(End),
            Curve(0f, 0f, Crouch, -10f, Coil, -14f, Impact, 6f, Follow, -3f, Recover, -4f, End, 0f));

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void RestrictExistingAttackTransition(AnimatorStateMachine root, AnimatorState slash)
    {
        AnimatorStateTransition[] transitions = root.anyStateTransitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition = transitions[i];
            if (transition == null || transition.destinationState != slash)
                continue;

            EnsureCondition(transition, AnimatorConditionMode.Equals, "AttackIndex", 0f);
        }
    }

    private static void EnsureAnyStateAttack(AnimatorStateMachine root, AnimatorState destination, int attackIndex)
    {
        AnimatorStateTransition[] transitions = root.anyStateTransitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition existing = transitions[i];
            if (existing == null || existing.destinationState != destination)
                continue;
            if (!HasCondition(existing, AnimatorConditionMode.If, "Attack")
                || !HasCondition(existing, AnimatorConditionMode.Equals, "AttackIndex"))
                continue;

            EnsureCondition(existing, AnimatorConditionMode.Equals, "AttackIndex", attackIndex);
            return;
        }

        AnimatorStateTransition transition = root.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        transition.AddCondition(AnimatorConditionMode.Equals, attackIndex, "AttackIndex");
    }

    private static void EnsureExitToIdle(AnimatorState from, AnimatorState idle)
    {
        AnimatorStateTransition[] transitions = from.transitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition existing = transitions[i];
            if (existing == null || existing.destinationState != idle)
                continue;

            existing.hasExitTime = true;
            existing.exitTime = 0.94f;
            existing.duration = 0.1f;
            existing.hasFixedDuration = true;
            return;
        }

        AnimatorStateTransition exit = from.AddTransition(idle);
        exit.hasExitTime = true;
        exit.exitTime = 0.94f;
        exit.duration = 0.1f;
        exit.hasFixedDuration = true;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
                return;
        }

        controller.AddParameter(name, type);
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == name)
                return states[i].state;
        }

        return null;
    }

    private static void EnsureCondition(
        AnimatorStateTransition transition,
        AnimatorConditionMode mode,
        string param,
        float threshold)
    {
        AnimatorCondition[] conditions = transition.conditions;
        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].mode != mode || conditions[i].parameter != param)
                continue;
            if (!Mathf.Approximately(conditions[i].threshold, threshold))
            {
                transition.RemoveCondition(conditions[i]);
                transition.AddCondition(mode, threshold, param);
            }

            return;
        }

        transition.AddCondition(mode, threshold, param);
    }

    private static bool HasCondition(AnimatorStateTransition transition, AnimatorConditionMode mode, string param)
    {
        AnimatorCondition[] conditions = transition.conditions;
        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].mode == mode && conditions[i].parameter == param)
                return true;
        }

        return false;
    }

    private static void SetEuler(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y, AnimationCurve z)
    {
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.x", x);
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.y", y);
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", z);
    }

    private static void SetPosition(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y, AnimationCurve z)
    {
        clip.SetCurve(path, typeof(Transform), "localPosition.x", x);
        clip.SetCurve(path, typeof(Transform), "localPosition.y", y);
        clip.SetCurve(path, typeof(Transform), "localPosition.z", z);
    }

    private static AnimationCurve Zero(float endTime = 1.32f)
    {
        return Curve(0f, 0f, endTime, 0f);
    }

    private static AnimationCurve Curve(params float[] timeValuePairs)
    {
        var curve = new AnimationCurve();
        for (int i = 0; i + 1 < timeValuePairs.Length; i += 2)
            curve.AddKey(new Keyframe(timeValuePairs[i], timeValuePairs[i + 1]));

        for (int i = 0; i < curve.length; i++)
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);

        for (int i = 0; i < curve.length; i++)
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);

        return curve;
    }
}
