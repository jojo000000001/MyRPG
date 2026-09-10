using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把剑盾举盾 / 格挡受击动画接到玩家 BodyLayer。
/// </summary>
public static class SetupPlayerShieldAnimator
{
    private const string ControllerPath = "Assets/RPG Tiny Hero Duo/Animator/Player 1.controller";
    private const string DefendFbx = "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/Defend_SwordAndShield.fbx";
    private const string DefendHitFbx = "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/DefendHit_SwordAndShield.fbx";

    [MenuItem("Tools/Combat/Setup Player Shield Animator")]
    public static void Setup()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("SetupPlayerShieldAnimator: missing " + ControllerPath);
            return;
        }

        AnimationClip defendClip = LoadClip(DefendFbx, "Defend_SwordAndShield");
        AnimationClip defendHitClip = LoadClip(DefendHitFbx, "DefendHit_SwordAndShield");
        if (defendClip == null || defendHitClip == null)
        {
            Debug.LogError("SetupPlayerShieldAnimator: missing Defend clips.");
            return;
        }

        EnsureParameter(controller, "IsGuarding", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "GuardHit", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine body = controller.layers[1].stateMachine;
        AnimatorState nullState = FindState(body, "Null");
        AnimatorState attack1 = FindState(body, "Attack01_SwordAndShiled");
        AnimatorState attack2 = FindState(body, "Attack02_SwordAndShiled");
        AnimatorState attack3 = FindState(body, "Attack03_SwordAndShiled");
        if (nullState == null)
        {
            Debug.LogError("SetupPlayerShieldAnimator: BodyLayer missing Null state.");
            return;
        }

        AnimatorState defend = FindState(body, "Defend") ?? body.AddState("Defend", new Vector3(280f, 20f, 0f));
        defend.motion = defendClip;

        AnimatorState defendHit = FindState(body, "DefendHit") ?? body.AddState("DefendHit", new Vector3(530f, 20f, 0f));
        defendHit.motion = defendHitClip;

        EnsureBoolTransition(nullState, defend, "IsGuarding", true, 0.08f);
        EnsureBoolTransition(defend, nullState, "IsGuarding", false, 0.1f);
        EnsureTriggerTransition(defend, defendHit, "GuardHit", 0.04f);
        EnsureExitBoolTransition(defendHit, defend, "IsGuarding", true, 0.55f, 0.08f);
        EnsureBoolTransition(defendHit, nullState, "IsGuarding", false, 0.08f);

        if (attack1 != null)
            EnsureBoolTransition(attack1, defend, "IsGuarding", true, 0.08f);
        if (attack2 != null)
            EnsureBoolTransition(attack2, defend, "IsGuarding", true, 0.08f);
        if (attack3 != null)
            EnsureBoolTransition(attack3, defend, "IsGuarding", true, 0.08f);

        BlockAttacksWhileGuarding(body);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("SetupPlayerShieldAnimator: Defend / DefendHit wired on BodyLayer.");
    }

    private static void BlockAttacksWhileGuarding(AnimatorStateMachine body)
    {
        AnimatorStateTransition[] anyStates = body.anyStateTransitions;
        for (int i = 0; i < anyStates.Length; i++)
        {
            AnimatorStateTransition transition = anyStates[i];
            if (transition == null || transition.destinationState == null)
                continue;
            if (!transition.destinationState.name.StartsWith("Attack"))
                continue;
            EnsureCondition(transition, AnimatorConditionMode.IfNot, "IsGuarding");
        }
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

    private static void EnsureBoolTransition(AnimatorState from, AnimatorState to, string param, bool value, float duration)
    {
        if (HasTransition(from, to, param, value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, false))
            return;

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    private static void EnsureTriggerTransition(AnimatorState from, AnimatorState to, string param, float duration)
    {
        if (HasTransition(from, to, param, AnimatorConditionMode.If, false))
            return;

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(AnimatorConditionMode.If, 0f, param);
    }

    private static void EnsureExitBoolTransition(
        AnimatorState from,
        AnimatorState to,
        string param,
        bool value,
        float exitTime,
        float duration)
    {
        if (HasTransition(from, to, param, value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, true))
            return;

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    private static bool HasTransition(
        AnimatorState from,
        AnimatorState to,
        string param,
        AnimatorConditionMode mode,
        bool hasExitTime)
    {
        AnimatorStateTransition[] transitions = from.transitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition = transitions[i];
            if (transition.destinationState != to || transition.hasExitTime != hasExitTime)
                continue;
            if (HasCondition(transition, mode, param))
                return true;
        }

        return false;
    }

    private static void EnsureCondition(AnimatorStateTransition transition, AnimatorConditionMode mode, string param)
    {
        if (HasCondition(transition, mode, param))
            return;
        transition.AddCondition(mode, 0f, param);
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

    private static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip.name == clipName);
    }
}
