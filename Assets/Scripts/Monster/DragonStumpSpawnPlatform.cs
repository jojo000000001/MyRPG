using UnityEngine;

/// <summary>
/// Marks the flat collider the dragon boss should stand on.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class DragonStumpSpawnPlatform : MonoBehaviour
{
    [SerializeField] private float standClearance = 0.05f;

    public float StandClearance => standClearance;

    public float GetStandSurfaceY()
    {
        Collider collider = GetComponent<Collider>();
        return collider != null ? collider.bounds.max.y + standClearance : transform.position.y;
    }

    public bool ContainsPlanar(Vector3 worldPosition)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            Collider collider = GetComponent<Collider>();
            if (collider == null)
                return false;

            Bounds bounds = collider.bounds;
            return Mathf.Abs(worldPosition.x - bounds.center.x) <= bounds.extents.x
                && Mathf.Abs(worldPosition.z - bounds.center.z) <= bounds.extents.z;
        }

        Vector3 local = transform.InverseTransformPoint(worldPosition);
        Vector3 half = box.size * 0.5f;
        Vector3 center = box.center;
        return Mathf.Abs(local.x - center.x) <= half.x
            && Mathf.Abs(local.z - center.z) <= half.z;
    }
}
