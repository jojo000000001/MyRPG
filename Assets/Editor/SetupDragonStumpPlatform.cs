using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupDragonStumpPlatform
{
    private const string StumpName = "Stump_01";
    private const string PlatformName = "DragonStumpPlatform";
    private const string DragonName = "DragonBoss";
    private const float StandClearance = 0.05f;
    private const float PlatformThickness = 0.35f;
    private const float TopSurfaceInset = 0.96f;
    private const float TargetHeightToWidthRatio = 0.72f;

    [MenuItem("Tools/Combat/Setup Dragon Stump Platform")]
    public static void Setup()
    {
        GameObject stump = GameObject.Find(StumpName);
        if (stump == null)
        {
            Debug.LogError($"SetupDragonStumpPlatform: Could not find '{StumpName}' in the active scene.");
            return;
        }

        FixStumpProportions(stump.transform);

        Bounds stumpBounds = CalculateWorldBounds(stump.transform);
        EnsureTopPlatformCollider(stump.transform, stumpBounds);
        SnapDragonToPlatformTop(stumpBounds);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"SetupDragonStumpPlatform: Fixed stump proportions, rebuilt platform (topY={stumpBounds.max.y:F2}), aligned DragonBoss.");
    }

    [MenuItem("Tools/Combat/Fix Stump Proportions")]
    public static void FixProportionsOnly()
    {
        GameObject stump = GameObject.Find(StumpName);
        if (stump == null)
        {
            Debug.LogError($"SetupDragonStumpPlatform: Could not find '{StumpName}' in the active scene.");
            return;
        }

        FixStumpProportions(stump.transform);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Bounds stumpBounds = CalculateWorldBounds(stump.transform);
        Debug.Log($"FixStumpProportions: stump height={stumpBounds.size.y:F2}, topY={stumpBounds.max.y:F2}, scaleY={stump.transform.localScale.y:F2}");
    }

    private static void FixStumpProportions(Transform stumpRoot)
    {
        Vector3 scale = stumpRoot.localScale;
        float avgHorizontalScale = (scale.x + scale.z) * 0.5f;
        float targetScaleY = avgHorizontalScale * TargetHeightToWidthRatio;
        if (targetScaleY <= scale.y * 1.02f)
            return;

        Bounds boundsBefore = CalculateWorldBounds(stumpRoot);
        float bottomY = boundsBefore.min.y;

        stumpRoot.localScale = new Vector3(scale.x, targetScaleY, scale.z);

        Bounds boundsAfter = CalculateWorldBounds(stumpRoot);
        stumpRoot.position += Vector3.up * (bottomY - boundsAfter.min.y);
    }

    private static void EnsureTopPlatformCollider(Transform stumpRoot, Bounds stumpWorldBounds)
    {
        GameObject platformObject = GameObject.Find(PlatformName);
        if (platformObject == null)
            platformObject = new GameObject(PlatformName);

        Bounds localBounds = CalculateLocalRendererBounds(stumpRoot);
        float topLocalY = localBounds.max.y;
        Vector3 localCenter = new Vector3(
            localBounds.center.x,
            topLocalY - PlatformThickness * 0.5f,
            localBounds.center.z);

        platformObject.transform.SetParent(stumpRoot, false);
        platformObject.transform.localPosition = localCenter;
        platformObject.transform.localRotation = Quaternion.identity;
        platformObject.transform.localScale = Vector3.one;

        BoxCollider collider = platformObject.GetComponent<BoxCollider>();
        if (collider == null)
            collider = platformObject.AddComponent<BoxCollider>();

        collider.isTrigger = false;
        collider.center = Vector3.zero;
        collider.size = new Vector3(
            Mathf.Max(0.5f, localBounds.size.x * TopSurfaceInset),
            PlatformThickness,
            Mathf.Max(0.5f, localBounds.size.z * TopSurfaceInset));

        DragonStumpSpawnPlatform marker = platformObject.GetComponent<DragonStumpSpawnPlatform>();
        if (marker == null)
            marker = platformObject.AddComponent<DragonStumpSpawnPlatform>();

        SerializedObject serializedMarker = new SerializedObject(marker);
        SerializedProperty clearanceProperty = serializedMarker.FindProperty("standClearance");
        if (clearanceProperty != null)
        {
            clearanceProperty.floatValue = StandClearance;
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
        }

        DisableStumpBodyColliders(stumpRoot, platformObject.transform);
    }

    private static void DisableStumpBodyColliders(Transform stumpRoot, Transform platformTransform)
    {
        Collider[] colliders = stumpRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.transform == platformTransform)
                continue;

            collider.enabled = false;
        }
    }

    private static void SnapDragonToPlatformTop(Bounds stumpBounds)
    {
        GameObject dragon = GameObject.Find(DragonName);
        if (dragon == null)
            return;

        DragonBoss dragonBoss = dragon.GetComponent<DragonBoss>();
        if (dragonBoss != null)
        {
            SerializedObject serializedDragon = new SerializedObject(dragonBoss);
            GameObject platformObject = GameObject.Find(PlatformName);
            DragonStumpSpawnPlatform marker = platformObject != null
                ? platformObject.GetComponent<DragonStumpSpawnPlatform>()
                : null;

            SerializedProperty platformProperty = serializedDragon.FindProperty("spawnPlatform");
            if (platformProperty != null)
            {
                platformProperty.objectReferenceValue = marker;
                serializedDragon.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        CharacterController controller = dragon.GetComponent<CharacterController>();
        if (controller == null)
            return;

        GameObject platformObjectForTop = GameObject.Find(PlatformName);
        Collider platformCollider = platformObjectForTop != null
            ? platformObjectForTop.GetComponent<Collider>()
            : null;

        float topY = platformCollider != null
            ? platformCollider.bounds.max.y + StandClearance
            : stumpBounds.max.y + StandClearance;

        float visualFeetOffset = GetVisualFeetOffsetFromTransform(dragon.transform, controller);
        Vector3 position = dragon.transform.position;
        position.y = topY - visualFeetOffset;
        dragon.transform.position = position;
    }

    private static float GetVisualFeetOffsetFromTransform(Transform root, CharacterController controller)
    {
        float lowestY = float.PositiveInfinity;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (renderer.bounds.min.y < lowestY)
                lowestY = renderer.bounds.min.y;
        }

        if (float.IsPositiveInfinity(lowestY))
            return controller.center.y - controller.height * 0.5f;

        return lowestY - root.position.y;
    }

    private static Bounds CalculateWorldBounds(Transform stumpRoot)
    {
        Renderer[] renderers = stumpRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(stumpRoot.position, Vector3.one * 4f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static Bounds CalculateLocalRendererBounds(Transform stumpRoot)
    {
        Renderer[] renderers = stumpRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        bool hasBounds = false;
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            EncapsulateWorldBoundsCorners(stumpRoot, renderer.bounds, ref min, ref max);
            hasBounds = true;
        }

        if (!hasBounds)
            return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static void EncapsulateWorldBoundsCorners(Transform root, Bounds worldBounds, ref Vector3 min, ref Vector3 max)
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int xi = -1; xi <= 1; xi += 2)
        {
            for (int yi = -1; yi <= 1; yi += 2)
            {
                for (int zi = -1; zi <= 1; zi += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                    Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                    min = Vector3.Min(min, localCorner);
                    max = Vector3.Max(max, localCorner);
                }
            }
        }
    }
}
