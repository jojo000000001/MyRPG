using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Bakes left-biased copies of the three sword attack clips and assigns them on the player animator.
/// Adjusts arm humanoid muscle curves only (no spine/chest/head), not hitbox transforms.
/// </summary>
public static class GenerateLeftBiasAttackClips
{
    private const string OutputFolder = "Assets/Animations/Player";
    private const string ControllerPath = "Assets/RPG Tiny Hero Duo/Animator/Player 1.controller";

    private readonly struct AttackSource
    {
        public readonly string FbxPath;
        public readonly string ClipName;
        public readonly string StateName;
        public readonly float Strength;

        public AttackSource(string fbxPath, string clipName, string stateName, float strength)
        {
            FbxPath = fbxPath;
            ClipName = clipName;
            StateName = stateName;
            Strength = strength;
        }
    }

    private static readonly AttackSource[] Sources =
    {
        new(
            "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/Attack01_SwordAndShiled.fbx",
            "Attack01_SwordAndShiled",
            "Attack01_SwordAndShiled",
            1.15f),
        new(
            "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/Attack02_SwordAndShiled.fbx",
            "Attack02_SwordAndShiled",
            "Attack02_SwordAndShiled",
            1.25f),
        new(
            "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/Attack03_SwordAndShiled.fbx",
            "Attack03_SwordAndShiled",
            "Attack03_SwordAndShiled",
            1.35f),
    };

    [MenuItem("Tools/Combat/Generate Left-Bias Attack Clips")]
    public static void Generate()
    {
        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        foreach (AttackSource source in Sources)
        {
            AnimationClip original = LoadClip(source.FbxPath, source.ClipName);
            if (original == null)
            {
                Debug.LogError($"Could not load clip '{source.ClipName}' from {source.FbxPath}");
                continue;
            }

            string outputName = source.ClipName + "_LeftBias";
            string outputPath = $"{OutputFolder}/{outputName}.anim";

            AnimationClip baked = Object.Instantiate(original);
            baked.name = outputName;
            ApplyLeftBias(baked, source.Strength);

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (existing != null)
                EditorUtility.CopySerialized(baked, existing);
            else
                AssetDatabase.CreateAsset(baked, outputPath);

            Object.DestroyImmediate(baked);
            Debug.Log($"Generated left-bias attack clip: {outputPath}", AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath));
        }

        AssignClipsToController();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName);
    }

    private static void ApplyLeftBias(AnimationClip clip, float strength)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        float length = Mathf.Max(clip.length, 0.0001f);

        foreach (EditorCurveBinding binding in bindings)
        {
            if (ShouldSkipBinding(binding))
                continue;

            float muscleBias = GetMuscleBias(binding.propertyName);
            if (Mathf.Approximately(muscleBias, 0f))
                continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length == 0)
                continue;

            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                float normalizedTime = keys[i].time / length;
                float weight = AttackPhaseWeight(normalizedTime);
                keys[i].value += muscleBias * strength * weight;
            }

            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        EditorUtility.SetDirty(clip);
    }

    /// <summary>Strongest during the active swing, faded at start/end of clip.</summary>
    private static float AttackPhaseWeight(float normalizedTime)
    {
        if (normalizedTime < 0.06f || normalizedTime > 0.94f)
            return 0f;

        if (normalizedTime < 0.18f)
            return Mathf.InverseLerp(0.06f, 0.18f, normalizedTime);

        if (normalizedTime > 0.78f)
            return Mathf.InverseLerp(0.94f, 0.78f, normalizedTime);

        return 1f;
    }

    private static bool ShouldSkipBinding(EditorCurveBinding binding)
    {
        if (!string.IsNullOrEmpty(binding.propertyName) && binding.propertyName.StartsWith("Head"))
            return true;

        return ShouldSkipBonePath(binding.path);
    }

    private static bool ShouldSkipBonePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string lowerPath = path.ToLowerInvariant();
        return lowerPath.Contains("head") || lowerPath.Contains("neck");
    }

    private static float GetMuscleBias(string propertyName)
    {
        // Arms only: torso/head muscles are untouched so the head stays upright.
        switch (propertyName)
        {
            case "Right Arm Down-Up":
                return -0.22f;
            case "Right Arm Front-Back":
                return -0.38f;
            case "Right Arm In-Out-Stretch":
                return 0.62f;
            case "Right Arm Twist In-Out":
                return 0.28f;
            case "Right Forearm Stretch":
                return 0.2f;
            case "Right Forearm Twist":
                return 0.16f;
            default:
                return 0f;
        }
    }

    private static void AssignClipsToController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found: {ControllerPath}");
            return;
        }

        foreach (AttackSource source in Sources)
        {
            string clipPath = $"{OutputFolder}/{source.ClipName}_LeftBias.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
                continue;

            ChildAnimatorState? state = FindState(controller, source.StateName);
            if (state == null)
            {
                Debug.LogWarning($"Animator state not found: {source.StateName}");
                continue;
            }

            state.Value.state.motion = clip;
            EditorUtility.SetDirty(controller);
        }
    }

    private static ChildAnimatorState? FindState(AnimatorController controller, string stateName)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            ChildAnimatorState? found = FindStateInMachine(layer.stateMachine, stateName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static ChildAnimatorState? FindStateInMachine(AnimatorStateMachine machine, string stateName)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == stateName)
                return child;
        }

        foreach (ChildAnimatorStateMachine childMachine in machine.stateMachines)
        {
            if (childMachine.stateMachine == null)
                continue;

            ChildAnimatorState? found = FindStateInMachine(childMachine.stateMachine, stateName);
            if (found != null)
                return found;
        }

        return null;
    }
}
