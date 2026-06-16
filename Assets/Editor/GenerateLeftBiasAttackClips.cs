using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Bakes combat-adjusted copies of the three sword attack clips and assigns them on the player animator.
/// Attack01: light left-bias. Attack02: left-bias + weapon diagonal arc. Attack03: original FBX.
/// </summary>
public static class GenerateLeftBiasAttackClips
{
    private const string OutputFolder = "Assets/Animations/Player";
    private const string ControllerPath = "Assets/RPG Tiny Hero Duo/Animator/Player 1.controller";

    private enum BakeStyle
    {
        LeftBias,
        /// <summary>Left-bias for hit reliability plus weapon_r arc (top-right to bottom-left).</summary>
        LeftBiasWithWeaponArc,
        /// <summary>Keep the FBX clip unchanged.</summary>
        Original,
    }

    private readonly struct AttackSource
    {
        public readonly string FbxPath;
        public readonly string ClipName;
        public readonly string StateName;
        public readonly float Strength;
        public readonly float InOutStretchMul;
        public readonly float FrontBackMul;
        public readonly BakeStyle Style;

        public AttackSource(
            string fbxPath,
            string clipName,
            string stateName,
            float strength,
            BakeStyle style = BakeStyle.LeftBias,
            float inOutStretchMul = 1f,
            float frontBackMul = 1f)
        {
            FbxPath = fbxPath;
            ClipName = clipName;
            StateName = stateName;
            Strength = strength;
            Style = style;
            InOutStretchMul = inOutStretchMul;
            FrontBackMul = frontBackMul;
        }
    }

    private const string WeaponPath =
        "root/pelvis/spine_01/spine_02/spine_03/clavicle_r/upperarm_r/lowerarm_r/hand_r/weapon_r";

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
            1.34f,
            BakeStyle.LeftBiasWithWeaponArc,
            inOutStretchMul: 1.08f,
            frontBackMul: 1.14f),
        new(
            "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/Attack03_SwordAndShiled.fbx",
            "Attack03_SwordAndShiled",
            "Attack03_SwordAndShiled",
            0f,
            BakeStyle.Original),
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
            ApplyCombatBias(baked, source);

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(baked, existing);
                if (source.Style == BakeStyle.LeftBiasWithWeaponArc)
                    SetHitboxEvents(existing, 0.24f, 0.5f);
            }
            else
            {
                if (source.Style == BakeStyle.LeftBiasWithWeaponArc)
                    SetHitboxEvents(baked, 0.24f, 0.5f);
                AssetDatabase.CreateAsset(baked, outputPath);
            }

            Object.DestroyImmediate(baked);
            Debug.Log($"Generated combat attack clip: {outputPath}", AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath));
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

    private static void ApplyCombatBias(AnimationClip clip, AttackSource source)
    {
        switch (source.Style)
        {
            case BakeStyle.LeftBiasWithWeaponArc:
                ApplyLeftBias(clip, source);
                ApplyAttack02WindupTame(clip);
                ApplyAttack02SlashExtend(clip);
                ApplyWeaponSlashReach(clip);
                ApplyWeaponDiagonalArc(clip, 1.05f);
                break;
            case BakeStyle.LeftBias:
                ApplyLeftBias(clip, source);
                break;
            case BakeStyle.Original:
                break;
        }
    }

    private static void ApplyLeftBias(AnimationClip clip, AttackSource source)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        float length = Mathf.Max(clip.length, 0.0001f);

        foreach (EditorCurveBinding binding in bindings)
        {
            if (ShouldSkipBinding(binding))
                continue;

            float muscleBias = GetLeftBiasMuscle(binding.propertyName, source);
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
                keys[i].value += muscleBias * source.Strength * weight;
            }

            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        EditorUtility.SetDirty(clip);
    }

    /// <summary>Rotate weapon_r during the swing so the blade travels top-right to bottom-left.</summary>
    private static void ApplyWeaponDiagonalArc(AnimationClip clip, float strength)
    {
        AnimationCurve curveX = GetTransformCurve(clip, WeaponPath, "m_LocalRotation.x");
        AnimationCurve curveY = GetTransformCurve(clip, WeaponPath, "m_LocalRotation.y");
        AnimationCurve curveZ = GetTransformCurve(clip, WeaponPath, "m_LocalRotation.z");
        AnimationCurve curveW = GetTransformCurve(clip, WeaponPath, "m_LocalRotation.w");
        if (curveX == null || curveY == null || curveZ == null || curveW == null)
        {
            Debug.LogWarning($"Weapon diagonal arc skipped: no rotation curves on {WeaponPath}");
            return;
        }

        float length = Mathf.Max(clip.length, 0.0001f);
        float[] times = CollectKeyframeTimes(curveX, curveY, curveZ, curveW);
        if (times.Length == 0)
            return;

        foreach (float time in times)
        {
            float normalizedTime = time / length;
            Quaternion rotation = SampleRotation(curveX, curveY, curveZ, curveW, time);
            Vector3 euler = rotation.eulerAngles;

            float windup = WindupTopRightWeight(normalizedTime);
            float slash = SlashBottomLeftWeight(normalizedTime);
            float fade = ClipEndFade(normalizedTime);

            euler.x += fade * strength * (windup * -10f + slash * 40f);
            euler.y += fade * strength * (windup * 4f + slash * -26f);
            euler.z += fade * strength * (windup * 3f + slash * -10f);

            rotation = Quaternion.Euler(euler);
            SetRotationKey(curveX, time, rotation.x);
            SetRotationKey(curveY, time, rotation.y);
            SetRotationKey(curveZ, time, rotation.z);
            SetRotationKey(curveW, time, rotation.w);
        }

        SetTransformCurve(clip, WeaponPath, "m_LocalRotation.x", curveX);
        SetTransformCurve(clip, WeaponPath, "m_LocalRotation.y", curveY);
        SetTransformCurve(clip, WeaponPath, "m_LocalRotation.z", curveZ);
        SetTransformCurve(clip, WeaponPath, "m_LocalRotation.w", curveW);
        EditorUtility.SetDirty(clip);
    }

    private static AnimationCurve GetTransformCurve(AnimationClip clip, string path, string propertyName)
    {
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
        return AnimationUtility.GetEditorCurve(clip, binding);
    }

    private static void SetTransformCurve(AnimationClip clip, string path, string propertyName, AnimationCurve curve)
    {
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static float[] CollectKeyframeTimes(params AnimationCurve[] curves)
    {
        return curves
            .Where(curve => curve != null)
            .SelectMany(curve => curve.keys)
            .Select(key => key.time)
            .Distinct()
            .OrderBy(time => time)
            .ToArray();
    }

    private static Quaternion SampleRotation(
        AnimationCurve curveX,
        AnimationCurve curveY,
        AnimationCurve curveZ,
        AnimationCurve curveW,
        float time)
    {
        return new Quaternion(
            curveX.Evaluate(time),
            curveY.Evaluate(time),
            curveZ.Evaluate(time),
            curveW.Evaluate(time)).normalized;
    }

    private static void SetRotationKey(AnimationCurve curve, float time, float value)
    {
        int index = FindKeyframeIndex(curve, time);
        if (index < 0)
        {
            curve.AddKey(time, value);
            return;
        }

        Keyframe key = curve.keys[index];
        key.value = value;
        curve.MoveKey(index, key);
    }

    private static int FindKeyframeIndex(AnimationCurve curve, float time)
    {
        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            if (Mathf.Approximately(keys[i].time, time))
                return i;
        }

        return -1;
    }

    /// <summary>Lowers and centers the opening pose so the wind-up is not too high or far right.</summary>
    private static void ApplyAttack02WindupTame(AnimationClip clip)
    {
        float length = Mathf.Max(clip.length, 0.0001f);
        ApplyMuscleWindupDelta(clip, "Right Arm Down-Up", length, -0.16f);
        ApplyMuscleWindupDelta(clip, "Right Hand Down-Up", length, -0.12f);
        ApplyMuscleWindupDelta(clip, "Right Hand In-Out", length, -0.18f);
    }

    /// <summary>Pushes the slash finish a bit further down and to the lower-left.</summary>
    private static void ApplyAttack02SlashExtend(AnimationClip clip)
    {
        float length = Mathf.Max(clip.length, 0.0001f);
        ApplyMuscleSlashDelta(clip, "Right Arm Down-Up", length, -0.18f);
        ApplyMuscleSlashDelta(clip, "Right Arm Front-Back", length, -0.2f);
        ApplyMuscleSlashDelta(clip, "Right Hand Down-Up", length, -0.1f);
        ApplyMuscleSlashDelta(clip, "Right Hand In-Out", length, -0.05f);
        ApplyMuscleSlashDelta(clip, "Right Forearm Stretch", length, 0.12f);
    }

    /// <summary>Reach forward during the slash so the blade stays on target while finishing lower-left.</summary>
    private static void ApplyWeaponSlashReach(AnimationClip clip)
    {
        float length = Mathf.Max(clip.length, 0.0001f);
        const float defaultX = 0.094f;
        const float defaultY = -0.0073f;
        const float defaultZ = -0.0036f;

        ApplyPositionSlashDelta(clip, "m_LocalPosition.x", length, -0.012f, defaultX);
        ApplyPositionSlashDelta(clip, "m_LocalPosition.y", length, -0.012f, defaultY);
        ApplyPositionSlashDelta(clip, "m_LocalPosition.z", length, 0.07f, defaultZ);
    }

    private static void SetHitboxEvents(AnimationClip clip, float openTime, float closeTime)
    {
        float length = Mathf.Max(clip.length, 0.0001f);
        openTime = Mathf.Clamp(openTime, 0f, length);
        closeTime = Mathf.Clamp(closeTime, openTime, length);

        clip.events = new[]
        {
            new AnimationEvent
            {
                time = openTime,
                functionName = "AttackHitboxOpen",
            },
            new AnimationEvent
            {
                time = closeTime,
                functionName = "AttackHitboxClose",
            },
        };

        AnimationUtility.SetAnimationEvents(clip, clip.events);
        EditorUtility.SetDirty(clip);
    }

    private static void ApplyPositionSlashDelta(
        AnimationClip clip,
        string propertyName,
        float length,
        float delta,
        float defaultValue)
    {
        AnimationCurve curve = GetTransformCurve(clip, WeaponPath, propertyName);
        if (curve == null || curve.length == 0)
            curve = AnimationCurve.Constant(0f, length, defaultValue);

        if (curve.length <= 2)
        {
            float[] sampleTimes =
            {
                0f,
                length * 0.28f,
                length * 0.42f,
                length * 0.56f,
                length,
            };

            var rebuilt = new AnimationCurve();
            foreach (float time in sampleTimes)
            {
                float normalizedTime = time / length;
                float weight = SlashBottomLeftWeight(normalizedTime) * ClipEndFade(normalizedTime);
                rebuilt.AddKey(time, defaultValue + delta * weight);
            }

            curve = rebuilt;
        }
        else
        {
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                float normalizedTime = keys[i].time / length;
                float weight = SlashBottomLeftWeight(normalizedTime) * ClipEndFade(normalizedTime);
                keys[i].value += delta * weight;
            }

            curve.keys = keys;
        }

        SetTransformCurve(clip, WeaponPath, propertyName, curve);
        EditorUtility.SetDirty(clip);
    }

    private static void ApplyMuscleSlashDelta(AnimationClip clip, string muscleName, float length, float delta)
    {
        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(Animator),
            propertyName = muscleName,
        };

        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
        if (curve == null || curve.length == 0)
            return;

        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            float normalizedTime = keys[i].time / length;
            float weight = SlashBottomLeftWeight(normalizedTime) * ClipEndFade(normalizedTime);
            keys[i].value += delta * weight;
        }

        curve.keys = keys;
        AnimationUtility.SetEditorCurve(clip, binding, curve);
        EditorUtility.SetDirty(clip);
    }

    private static void ApplyMuscleWindupDelta(AnimationClip clip, string muscleName, float length, float delta)
    {
        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(Animator),
            propertyName = muscleName,
        };

        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
        if (curve == null || curve.length == 0)
            return;

        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            float normalizedTime = keys[i].time / length;
            float weight = WindupTopRightWeight(normalizedTime) * ClipEndFade(normalizedTime);
            keys[i].value += delta * weight;
        }

        curve.keys = keys;
        AnimationUtility.SetEditorCurve(clip, binding, curve);
        EditorUtility.SetDirty(clip);
    }

    private static float WindupTopRightWeight(float t)
    {
        if (t < 0.04f || t > 0.3f)
            return 0f;

        if (t < 0.12f)
            return Mathf.SmoothStep(0f, 1f, (t - 0.04f) / 0.08f);

        if (t > 0.22f)
            return Mathf.SmoothStep(0f, 1f, (0.3f - t) / 0.08f);

        return 1f;
    }

    private static float SlashBottomLeftWeight(float t)
    {
        if (t < 0.2f || t > 0.7f)
            return 0f;

        if (t < 0.3f)
            return Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.1f);

        if (t > 0.52f)
            return Mathf.SmoothStep(0f, 1f, (0.7f - t) / 0.18f);

        return 1f;
    }

    private static float ClipEndFade(float normalizedTime)
    {
        if (normalizedTime < 0.03f)
            return Mathf.InverseLerp(0f, 0.03f, normalizedTime);

        if (normalizedTime > 0.92f)
            return Mathf.InverseLerp(1f, 0.92f, normalizedTime);

        return 1f;
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

    private static float GetLeftBiasMuscle(string propertyName, AttackSource source)
    {
        switch (propertyName)
        {
            case "Right Arm Down-Up":
                return -0.22f;
            case "Right Arm Front-Back":
                return -0.38f * source.FrontBackMul;
            case "Right Arm In-Out-Stretch":
                return 0.62f * source.InOutStretchMul;
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
