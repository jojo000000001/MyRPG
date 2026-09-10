using UnityEngine;

/// <summary>
/// 探索区域 BGM 触发器（村庄圆形 / 森林矩形）。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class AreaBgmZone : MonoBehaviour
{
    public enum AreaKind
    {
        Village,
        Forest
    }

    [SerializeField] private AreaKind areaKind = AreaKind.Village;
    [SerializeField] private Vector3 zoneCenter = new Vector3(22f, 1f, -35f);
    [SerializeField] private float zoneRadius = 30f;
    [SerializeField] private Vector2 zoneHalfExtents = new Vector2(28f, 26f);
    [SerializeField] private float checkInterval = 0.2f;
    [SerializeField] private bool alwaysDrawGizmo = true;

    [SerializeField] private bool playerInside;

    private float nextCheckTime;

    public AreaKind Kind
    {
        get => areaKind;
        set => areaKind = value;
    }

    public bool UsesRectangle => areaKind == AreaKind.Forest;

    public Vector3 ZoneCenter
    {
        get => zoneCenter;
        set => zoneCenter = value;
    }

    public float ZoneRadius
    {
        get => zoneRadius;
        set => zoneRadius = Mathf.Max(1f, value);
    }

    public Vector2 ZoneHalfExtents
    {
        get => zoneHalfExtents;
        set => zoneHalfExtents = new Vector2(Mathf.Max(0.5f, value.x), Mathf.Max(0.5f, value.y));
    }

    public bool AlwaysDrawGizmo
    {
        get => alwaysDrawGizmo;
        set => alwaysDrawGizmo = value;
    }

    private void OnValidate()
    {
        zoneHalfExtents = new Vector2(Mathf.Max(0.5f, zoneHalfExtents.x), Mathf.Max(0.5f, zoneHalfExtents.y));
        zoneRadius = Mathf.Max(1f, zoneRadius);

        if (areaKind == AreaKind.Forest && zoneHalfExtents.x <= 0.5f && zoneRadius > 1f)
            zoneHalfExtents = new Vector2(zoneRadius, zoneRadius);
    }

    private void Update()
    {
        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;
        UpdatePlayerPresence();
    }

    private void OnEnable()
    {
        nextCheckTime = 0f;
        playerInside = false;
    }

    private void Start()
    {
        playerInside = false;
        UpdatePlayerPresence();
    }

    public void UpdatePlayerPresence()
    {
        Player player = ResolvePlayer();
        if (player == null || player.IsDead)
        {
            if (playerInside)
                ExitArea();
            return;
        }

        bool inside = IsInsideZone(player.transform.position);
        if (inside && !playerInside)
            EnterArea();
        else if (!inside && playerInside)
            ExitArea();
    }

    public bool IsInsideZone(Vector3 worldPosition)
    {
        if (UsesRectangle)
        {
            float dx = Mathf.Abs(worldPosition.x - zoneCenter.x);
            float dz = Mathf.Abs(worldPosition.z - zoneCenter.z);
            return dx <= zoneHalfExtents.x && dz <= zoneHalfExtents.y;
        }

        float ox = worldPosition.x - zoneCenter.x;
        float oz = worldPosition.z - zoneCenter.z;
        return ox * ox + oz * oz <= zoneRadius * zoneRadius;
    }

    public Bounds GetZoneBounds()
    {
        if (UsesRectangle)
        {
            return new Bounds(
                zoneCenter,
                new Vector3(zoneHalfExtents.x * 2f, 4f, zoneHalfExtents.y * 2f));
        }

        float diameter = zoneRadius * 2f;
        return new Bounds(zoneCenter, new Vector3(diameter, 4f, diameter));
    }

    private void EnterArea()
    {
        if (playerInside)
            return;

        playerInside = true;
        if (!Application.isPlaying)
            return;

        BgmManager.NotifyAreaEntered(areaKind);
    }

    private void ExitArea()
    {
        if (!playerInside)
            return;

        playerInside = false;
        if (!Application.isPlaying)
            return;

        BgmManager.NotifyAreaExited(areaKind);
    }

    private static Player ResolvePlayer()
    {
        return Player.Resolve();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!alwaysDrawGizmo && !UnityEditor.Selection.Contains(gameObject))
            return;

        DrawZoneGizmo(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawZoneGizmo(true);
    }

    private void DrawZoneGizmo(bool selected)
    {
        Color fill = areaKind switch
        {
            AreaKind.Village => new Color(0.3f, 0.8f, 1f, selected ? 0.22f : 0.12f),
            AreaKind.Forest => new Color(0.25f, 0.95f, 0.35f, selected ? 0.28f : 0.16f),
            _ => new Color(1f, 1f, 1f, 0.12f)
        };

        Color wire = areaKind switch
        {
            AreaKind.Village => new Color(0.2f, 0.7f, 1f, selected ? 1f : 0.75f),
            AreaKind.Forest => new Color(0.2f, 0.9f, 0.25f, selected ? 1f : 0.85f),
            _ => Color.white
        };

        if (UsesRectangle)
            DrawRectangleGizmo(fill, wire, selected);
        else
            DrawCircleGizmo(fill, wire, selected);
    }

    private void DrawCircleGizmo(Color fill, Color wire, bool selected)
    {
        UnityEditor.Handles.color = fill;
        UnityEditor.Handles.DrawSolidDisc(zoneCenter, Vector3.up, zoneRadius);

        UnityEditor.Handles.color = wire;
        UnityEditor.Handles.DrawWireDisc(zoneCenter, Vector3.up, zoneRadius);

        if (selected)
        {
            UnityEditor.Handles.DrawLine(zoneCenter, zoneCenter + Vector3.right * zoneRadius);
            UnityEditor.Handles.Label(zoneCenter + Vector3.up * 2f, $"{areaKind} BGM\nR={zoneRadius:0.#}");
        }
    }

    private void DrawRectangleGizmo(Color fill, Color wire, bool selected)
    {
        Vector3[] corners = GetRectangleCorners();
        UnityEditor.Handles.color = fill;
        UnityEditor.Handles.DrawAAConvexPolygon(corners);

        UnityEditor.Handles.color = wire;
        UnityEditor.Handles.DrawLine(corners[0], corners[1]);
        UnityEditor.Handles.DrawLine(corners[1], corners[2]);
        UnityEditor.Handles.DrawLine(corners[2], corners[3]);
        UnityEditor.Handles.DrawLine(corners[3], corners[0]);

        if (selected)
        {
            UnityEditor.Handles.Label(
                zoneCenter + Vector3.up * 2f,
                $"{areaKind} BGM\n{zoneHalfExtents.x * 2f:0.#} x {zoneHalfExtents.y * 2f:0.#}");
        }
    }
#endif

    /// <summary>
    /// 区域边界上朝向指定点的位置，用于在森林边缘放怪。
    /// </summary>
    public Vector3 GetPerimeterPointToward(Vector3 worldPosition)
    {
        Vector3 toTarget = worldPosition - zoneCenter;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            toTarget = Vector3.back;

        Vector3 point;
        if (UsesRectangle)
        {
            float scaleX = zoneHalfExtents.x / Mathf.Max(0.001f, Mathf.Abs(toTarget.x));
            float scaleZ = zoneHalfExtents.y / Mathf.Max(0.001f, Mathf.Abs(toTarget.z));
            point = zoneCenter + toTarget * Mathf.Min(scaleX, scaleZ);
        }
        else
        {
            point = zoneCenter + toTarget.normalized * zoneRadius;
        }

        point.y = zoneCenter.y;
        return point;
    }

#if UNITY_EDITOR
    public Vector3[] GetRectangleCorners()
    {
        float hx = zoneHalfExtents.x;
        float hz = zoneHalfExtents.y;
        float y = zoneCenter.y;

        return new[]
        {
            new Vector3(zoneCenter.x - hx, y, zoneCenter.z - hz),
            new Vector3(zoneCenter.x + hx, y, zoneCenter.z - hz),
            new Vector3(zoneCenter.x + hx, y, zoneCenter.z + hz),
            new Vector3(zoneCenter.x - hx, y, zoneCenter.z + hz),
        };
    }
#endif
}
