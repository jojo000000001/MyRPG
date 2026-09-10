using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 为 Usurper 龙生成 Boss 战斗 Animator。
/// </summary>
public static class GenerateDragonBossAnimator
{
    private const string OutputPath = "Assets/Animators/DragonBoss_Usurper.controller";

    [MenuItem("Tools/Combat/Generate Dragon Boss Animator")]
    public static void Generate()
    {
        EnsureFolder("Assets/Animators");

        AnimationClip idle = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/idle01.fbx", "Idle01");
        AnimationClip walk = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/Walk.fbx", "Walk");
        AnimationClip run = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/Run.fbx", "Run");
        AnimationClip claw = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/attackHand.fbx", "Claw Attack");
        AnimationClip flame = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/attackFlame.fbx", "Flame Attack");
        AnimationClip hit = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/getHit.fbx", "Get Hit");
        AnimationClip die = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/Die.fbx", "Die");
        AnimationClip fly = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/FlyIdle.fbx", "Fly Float");
        AnimationClip land = LoadClip("Assets/FourEvilDragonsHP/Animations/DragonUsurper/Land.fbx", "Land");

        if (idle == null || walk == null || run == null || claw == null || flame == null || hit == null || die == null || fly == null || land == null)
        {
            Debug.LogError("GenerateDragonBossAnimator: missing animation clips.");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        ClearController(controller);
        AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "AttackIndex", AnimatorControllerParameterType.Int);
        AddParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "HitBool", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Flying", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Land", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AnimatorState idleState = root.AddState("Idle", new Vector3(300f, 0f, 0f));
        idleState.motion = idle;

        AnimatorState walkState = root.AddState("Walk", new Vector3(300f, 80f, 0f));
        walkState.motion = walk;

        AnimatorState runState = root.AddState("Run", new Vector3(300f, 160f, 0f));
        runState.motion = run;

        AnimatorState clawState = root.AddState("ClawAttack", new Vector3(600f, -80f, 0f));
        clawState.motion = claw;

        AnimatorState flameState = root.AddState("FlameAttack", new Vector3(600f, -160f, 0f));
        flameState.motion = flame;

        AnimatorState hitState = root.AddState("GetHit", new Vector3(600f, 80f, 0f));
        hitState.motion = hit;
        hitState.speed = 1.75f;

        AnimatorState dieState = root.AddState("Die", new Vector3(600f, 240f, 0f));
        dieState.motion = die;

        AnimatorState flyState = root.AddState("FlyIdle", new Vector3(0f, -80f, 0f));
        flyState.motion = fly;

        AnimatorState landState = root.AddState("Land", new Vector3(0f, -160f, 0f));
        landState.motion = land;

        root.defaultState = idleState;

        AddFloatTransition(idleState, walkState, AnimatorConditionMode.Greater, 0.12f, 0.2f);
        AddFloatTransition(walkState, idleState, AnimatorConditionMode.Less, 0.05f, 0.2f);
        AddFloatTransition(walkState, runState, AnimatorConditionMode.Greater, 0.7f, 0.2f);
        AddFloatTransition(runState, walkState, AnimatorConditionMode.Less, 0.6f, 0.2f);
        AddFloatTransition(runState, idleState, AnimatorConditionMode.Less, 0.05f, 0.25f);

        AddExitTransition(clawState, idleState, 0.88f, 0.15f);
        AddExitTransition(flameState, idleState, 0.92f, 0.1f);
        AddHitExitTransition(hitState, idleState);
        AddExitTransition(landState, idleState, 0.82f, 0.12f);

        AddAnyStateTriggerAttack(root, clawState, 0);
        AddAnyStateTriggerAttack(root, flameState, 1);
        AddAnyStateBool(root, hitState, "HitBool", true, 0.06f, canTransitionToSelf: false);
        AddAnyStateBool(root, dieState, "Dead", true, 0.08f, canTransitionToSelf: false);
        AddAnyStateFlying(root, flyState);
        AddAnyStateLand(root, landState);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Generated dragon boss animator at {OutputPath}", controller);
    }

    private static void ClearController(AnimatorController controller)
    {
        while (controller.parameters.Length > 0)
            controller.RemoveParameter(0);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in root.states.ToArray())
            root.RemoveState(child.state);

        foreach (AnimatorStateTransition transition in root.anyStateTransitions.ToArray())
            root.RemoveAnyStateTransition(transition);
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        controller.AddParameter(name, type);
    }

    private static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName);
    }

    private static void AddFloatTransition(
        AnimatorState from,
        AnimatorState to,
        AnimatorConditionMode mode,
        float threshold,
        float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(mode, threshold, "Speed");
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
    }

    private static void AddAnyStateTriggerAttack(AnimatorStateMachine root, AnimatorState destination, int attackIndex)
    {
        AnimatorStateTransition transition = root.AddAnyStateTransition(destination);
        transition.canTransitionToSelf = false;
        transition.duration = 0.12f;
        transition.hasExitTime = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        transition.AddCondition(AnimatorConditionMode.Equals, attackIndex, "AttackIndex");
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
    }

    private static void AddAnyStateFlying(AnimatorStateMachine root, AnimatorState destination)
    {
        AnimatorStateTransition transition = root.AddAnyStateTransition(destination);
        transition.canTransitionToSelf = false;
        transition.duration = 0.18f;
        transition.hasExitTime = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Flying");
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
    }

    private static void AddAnyStateLand(AnimatorStateMachine root, AnimatorState destination)
    {
        AnimatorStateTransition transition = root.AddAnyStateTransition(destination);
        transition.canTransitionToSelf = false;
        transition.duration = 0.12f;
        transition.hasExitTime = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Land");
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
    }

    private static void AddHitExitTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition timed = from.AddTransition(to);
        timed.hasExitTime = true;
        timed.exitTime = 0.22f;
        timed.duration = 0.06f;

        AnimatorStateTransition interrupt = from.AddTransition(to);
        interrupt.hasExitTime = false;
        interrupt.duration = 0.06f;
        interrupt.AddCondition(AnimatorConditionMode.IfNot, 0f, "HitBool");
    }

    private static void AddAnyStateBool(
        AnimatorStateMachine root,
        AnimatorState destination,
        string boolParam,
        bool value,
        float duration,
        bool canTransitionToSelf)
    {
        AnimatorStateTransition transition = root.AddAnyStateTransition(destination);
        transition.canTransitionToSelf = canTransitionToSelf;
        transition.duration = duration;
        transition.hasExitTime = false;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, boolParam);
        if (boolParam != "Dead")
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
