using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AreaBgmZone))]
public sealed class AreaBgmZoneEditor : Editor
{
    private static readonly Color ForestFill = new Color(0.25f, 0.95f, 0.35f, 0.18f);
    private static readonly Color ForestWire = new Color(0.15f, 0.85f, 0.2f, 0.95f);
    private static readonly Color VillageFill = new Color(0.3f, 0.8f, 1f, 0.16f);
    private static readonly Color VillageWire = new Color(0.2f, 0.7f, 1f, 0.95f);

    private SerializedProperty areaKindProp;
    private SerializedProperty zoneCenterProp;
    private SerializedProperty zoneRadiusProp;
    private SerializedProperty zoneHalfExtentsProp;

    private void OnEnable()
    {
        areaKindProp = serializedObject.FindProperty("areaKind");
        zoneCenterProp = serializedObject.FindProperty("zoneCenter");
        zoneRadiusProp = serializedObject.FindProperty("zoneRadius");
        zoneHalfExtentsProp = serializedObject.FindProperty("zoneHalfExtents");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var zone = (AreaBgmZone)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "在 Scene 视图中选中此对象即可拖动调整（无需运行游戏）。调整后 Ctrl+S 保存场景。",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("聚焦到区域"))
                FrameZone(zone);

            if (GUILayout.Button("对齐到 Player"))
                SnapCenterToPlayer(zone);
        }

        if (GUI.changed)
            EditorUtility.SetDirty(zone);
    }

    private void OnSceneGUI()
    {
        var zone = (AreaBgmZone)target;
        if (zone == null)
            return;

        // 仅编辑 BGM 区域，避免误触场景里的 CameraRig / Main Camera。
        Tools.current = Tool.None;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        serializedObject.Update();

        bool isForest = areaKindProp.enumValueIndex == (int)AreaBgmZone.AreaKind.Forest;
        if (isForest)
            DrawRectangleSceneGui(zone);
        else
            DrawCircleSceneGui(zone);

        if (serializedObject.ApplyModifiedProperties())
            MarkDirty(zone);
    }

    private void DrawCircleSceneGui(AreaBgmZone zone)
    {
        DrawCircle(zone);

        EditorGUI.BeginChangeCheck();

        Vector3 center = zoneCenterProp.vector3Value;
        float radius = zoneRadiusProp.floatValue;

        Handles.color = VillageWire;
        center = Handles.PositionHandle(center, Quaternion.identity);

        Vector3 radiusHandle = center + Vector3.right * radius;
        Handles.color = Color.yellow;
        radiusHandle = Handles.FreeMoveHandle(
            radiusHandle,
            HandleUtility.GetHandleSize(radiusHandle) * 0.12f,
            Vector3.zero,
            Handles.DotHandleCap);

        Vector3 flatHandle = new Vector3(radiusHandle.x, center.y, radiusHandle.z);
        radius = Vector3.Distance(
            new Vector3(center.x, 0f, center.z),
            new Vector3(flatHandle.x, 0f, flatHandle.z));
        radius = Mathf.Max(1f, radius);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(zone, "Edit BGM Zone");
            zoneCenterProp.vector3Value = center;
            zoneRadiusProp.floatValue = radius;
        }

        Handles.Label(center + Vector3.up * 1.5f, $"{zone.Kind} BGM  |  半径 {radius:0.#}m");
    }

    private void DrawRectangleSceneGui(AreaBgmZone zone)
    {
        DrawRectangle(zone);

        EditorGUI.BeginChangeCheck();

        Vector3 center = zoneCenterProp.vector3Value;
        Vector2 halfExtents = zoneHalfExtentsProp.vector2Value;

        Handles.color = ForestWire;
        center = Handles.PositionHandle(center, Quaternion.identity);

        Vector3 xHandle = center + Vector3.right * halfExtents.x;
        Handles.color = Color.yellow;
        xHandle = Handles.FreeMoveHandle(
            xHandle,
            HandleUtility.GetHandleSize(xHandle) * 0.12f,
            Vector3.zero,
            Handles.DotHandleCap);
        halfExtents.x = Mathf.Max(0.5f, Mathf.Abs(xHandle.x - center.x));

        Vector3 zHandle = center + Vector3.forward * halfExtents.y;
        Handles.color = Color.cyan;
        zHandle = Handles.FreeMoveHandle(
            zHandle,
            HandleUtility.GetHandleSize(zHandle) * 0.12f,
            Vector3.zero,
            Handles.DotHandleCap);
        halfExtents.y = Mathf.Max(0.5f, Mathf.Abs(zHandle.z - center.z));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(zone, "Edit BGM Zone");
            zoneCenterProp.vector3Value = center;
            zoneHalfExtentsProp.vector2Value = halfExtents;
        }

        Handles.Label(
            center + Vector3.up * 1.5f,
            $"{zone.Kind} BGM  |  {halfExtents.x * 2f:0.#} x {halfExtents.y * 2f:0.#} m");
    }

    private static void DrawCircle(AreaBgmZone zone)
    {
        Handles.color = VillageFill;
        Handles.DrawSolidDisc(zone.ZoneCenter, Vector3.up, zone.ZoneRadius);
        Handles.color = VillageWire;
        Handles.DrawWireDisc(zone.ZoneCenter, Vector3.up, zone.ZoneRadius);
    }

    private static void DrawRectangle(AreaBgmZone zone)
    {
        Vector3[] corners = zone.GetRectangleCorners();
        Handles.color = ForestFill;
        Handles.DrawAAConvexPolygon(corners);
        Handles.color = ForestWire;
        Handles.DrawLine(corners[0], corners[1]);
        Handles.DrawLine(corners[1], corners[2]);
        Handles.DrawLine(corners[2], corners[3]);
        Handles.DrawLine(corners[3], corners[0]);
    }

    private static void FrameZone(AreaBgmZone zone)
    {
        if (zone == null)
            return;

        Selection.activeGameObject = zone.gameObject;
        SceneView.lastActiveSceneView?.Frame(zone.GetZoneBounds(), false);
        SceneView.lastActiveSceneView?.Repaint();
    }

    private static void SnapCenterToPlayer(AreaBgmZone zone)
    {
        Player player = Player.Resolve();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Area BGM Zone", "场景里找不到 Player。", "OK");
            return;
        }

        Undo.RecordObject(zone, "Snap BGM Zone To Player");
        Vector3 position = player.transform.position;
        zone.ZoneCenter = new Vector3(position.x, zone.ZoneCenter.y, position.z);
        MarkDirty(zone);
    }

    private static void MarkDirty(AreaBgmZone zone)
    {
        EditorUtility.SetDirty(zone);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
    }
}

/// <summary>
/// 森林 BGM 矩形范围可视化与编辑工具。
/// </summary>
public static class ForestBgmZoneTool
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ForestZoneName = "ForestBgmZone";
    private static readonly Vector3 DefaultForestCenter = new Vector3(-8f, 1f, 8f);
    private static readonly Vector2 DefaultForestHalfExtents = new Vector2(28f, 26f);

    [MenuItem("Tools/MyRPG/Forest BGM Zone Tool")]
    public static void OpenForestZoneTool()
    {
        if (!Application.isPlaying)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        AreaBgmZone forestZone = EnsureForestZone();
        if (forestZone == null)
            return;

        forestZone.AlwaysDrawGizmo = true;
        Selection.activeGameObject = forestZone.gameObject;
        EditorGUIUtility.PingObject(forestZone.gameObject);

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneView.drawGizmos = true;
            sceneView.Frame(forestZone.GetZoneBounds(), false);
            sceneView.ShowNotification(new GUIContent("森林 BGM：绿色矩形。黄点调宽，青点调深。"), 3f);
            sceneView.Repaint();
        }

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(forestZone.gameObject.scene);
    }

    [MenuItem("Tools/MyRPG/Save Forest BGM Zone To Scene")]
    public static void SaveForestZoneToScene()
    {
        AreaBgmZone forestZone = FindForestZone();
        if (forestZone == null)
        {
            EditorUtility.DisplayDialog("Forest BGM Zone", "请先创建或选中 ForestBgmZone。", "OK");
            return;
        }

        EditorUtility.SetDirty(forestZone);
        EditorSceneManager.MarkSceneDirty(forestZone.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Vector2 size = forestZone.ZoneHalfExtents * 2f;
        EditorUtility.DisplayDialog(
            "Forest BGM Zone",
            $"已保存森林 BGM 范围：\n圆心 {forestZone.ZoneCenter}\n尺寸 {size.x:0.#} x {size.y:0.#}",
            "OK");
    }

    private static AreaBgmZone EnsureForestZone()
    {
        AreaBgmZone zone = FindForestZone();
        if (zone == null)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Forest BGM Zone", "请先打开 SampleScene。", "OK");
                return null;
            }

            GameObject zoneObject = new GameObject(ForestZoneName);
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create Forest BGM Zone");
            EditorSceneManager.MoveGameObjectToScene(zoneObject, scene);
            zone = Undo.AddComponent<AreaBgmZone>(zoneObject);
        }

        Undo.RecordObject(zone, "Configure Forest BGM Zone");
        zone.Kind = AreaBgmZone.AreaKind.Forest;
        zone.AlwaysDrawGizmo = true;

        if (zone.ZoneHalfExtents.x <= 0.5f || zone.ZoneHalfExtents.y <= 0.5f)
        {
            zone.ZoneCenter = DefaultForestCenter;
            zone.ZoneHalfExtents = DefaultForestHalfExtents;
        }

        EditorUtility.SetDirty(zone);
        EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
        return zone;
    }

    private static AreaBgmZone FindForestZone()
    {
        GameObject named = GameObject.Find(ForestZoneName);
        if (named != null)
        {
            AreaBgmZone zone = named.GetComponent<AreaBgmZone>();
            if (zone != null)
                return zone;
        }

        AreaBgmZone[] zones = Object.FindObjectsOfType<AreaBgmZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Kind == AreaBgmZone.AreaKind.Forest)
                return zones[i];
        }

        return null;
    }
}
