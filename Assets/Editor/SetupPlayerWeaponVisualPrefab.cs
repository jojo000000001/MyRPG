using UnityEditor;
using UnityEngine;

public static class SetupPlayerWeaponVisualPrefab
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/Combat/Setup Weapon Visual Pivot On Player")]
    public static void Setup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        Transform weaponSocket = FindChildRecursive(root.transform, "weapon_r");
        if (weaponSocket == null)
        {
            Debug.LogError("weapon_r not found on Player prefab.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Transform pivot = weaponSocket.Find("WeaponVisual");
        if (pivot == null)
        {
            GameObject pivotObject = new GameObject("WeaponVisual");
            pivot = pivotObject.transform;
            pivot.SetParent(weaponSocket, false);
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
        }

        if (pivot.GetComponent<PlayerWeaponVisualOffset>() == null)
            pivot.gameObject.AddComponent<PlayerWeaponVisualOffset>();

        for (int i = weaponSocket.childCount - 1; i >= 0; i--)
        {
            Transform child = weaponSocket.GetChild(i);
            if (child == pivot || child.GetComponent<AttackHitbox>() != null)
                continue;

            child.SetParent(pivot, true);
        }

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Player prefab weapon visual pivot setup complete.");
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
