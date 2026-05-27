using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public sealed class PlayerEquipmentVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform weaponSocket;

    [Header("Attachment")]
    [SerializeField] private HumanBodyBones attachmentBone = HumanBodyBones.RightHand;
    [SerializeField] private string socketName = "weapon_r";
    [SerializeField] private Vector3 weaponLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 weaponLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 weaponLocalScale = Vector3.one;

    private readonly List<GameObject> defaultSocketChildren = new List<GameObject>();
    private GameObject equippedWeaponVisual;
    private ItemSO currentWeapon;
    private bool capturedDefaultSocketChildren;

    private void Awake()
    {
        ResolveReferences();
        EnsureWeaponSocket();
        RefreshWeaponVisual();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (player != null)
            player.EquipmentChanged += RefreshWeaponVisual;

        RefreshWeaponVisual();
    }

    private void OnDisable()
    {
        if (player != null)
            player.EquipmentChanged -= RefreshWeaponVisual;
    }

public void RefreshWeaponVisual()
    {
        ResolveReferences();
        EnsureWeaponSocket();
        CaptureSocketDefaultChildren();

        ItemSO equippedWeapon = player != null ? player.EquippedWeapon : null;
        if (currentWeapon == equippedWeapon && equippedWeaponVisual != null)
        {
            SetSocketDefaultChildrenVisible(false);
            return;
        }

        ClearWeaponVisual();
        currentWeapon = equippedWeapon;
        SetSocketDefaultChildrenVisible(false);

        if (equippedWeapon == null || equippedWeapon.prefab == null || weaponSocket == null)
            return;

        equippedWeaponVisual = Instantiate(equippedWeapon.prefab, weaponSocket);
        equippedWeaponVisual.name = equippedWeapon.prefab.name + "_Equipped";

        Transform visualTransform = equippedWeaponVisual.transform;
        visualTransform.localPosition = weaponLocalPosition;
        visualTransform.localRotation = Quaternion.Euler(weaponLocalEulerAngles);
        visualTransform.localScale = weaponLocalScale;

        PrepareEquippedVisual(equippedWeaponVisual);
    }

    private void ResolveReferences()
    {
        if (player == null)
            player = GetComponent<Player>();

        if (animator == null)
            animator = GetComponent<Animator>();
    }

private void EnsureWeaponSocket()
    {
        if (weaponSocket != null)
            return;

        Transform parent = ResolveAttachmentParent();
        if (parent == null)
            parent = transform;

        Transform existingSocket = parent.Find(socketName);
        if (existingSocket == null)
            existingSocket = FindChildRecursive(transform, socketName);

        if (existingSocket != null)
        {
            weaponSocket = existingSocket;
            return;
        }

        GameObject socketObject = new GameObject(socketName);
        weaponSocket = socketObject.transform;
        weaponSocket.SetParent(parent, false);
        weaponSocket.localPosition = Vector3.zero;
        weaponSocket.localRotation = Quaternion.identity;
        weaponSocket.localScale = Vector3.one;
    }

private void CaptureSocketDefaultChildren()
    {
        if (capturedDefaultSocketChildren || weaponSocket == null)
            return;

        defaultSocketChildren.Clear();
        for (int i = 0; i < weaponSocket.childCount; i++)
        {
            Transform child = weaponSocket.GetChild(i);
            if (child != null)
                defaultSocketChildren.Add(child.gameObject);
        }

        capturedDefaultSocketChildren = true;
    }

    private void SetSocketDefaultChildrenVisible(bool visible)
    {
        for (int i = 0; i < defaultSocketChildren.Count; i++)
        {
            GameObject child = defaultSocketChildren[i];
            if (child != null)
                child.SetActive(visible);
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }


    private Transform ResolveAttachmentParent()
    {
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(attachmentBone);
            if (bone != null)
                return bone;
        }

        return transform;
    }

    private void ClearWeaponVisual()
    {
        if (equippedWeaponVisual == null)
            return;

        Destroy(equippedWeaponVisual);
        equippedWeaponVisual = null;
    }

    private static void PrepareEquippedVisual(GameObject visualRoot)
    {
        if (visualRoot == null)
            return;

        ConsumablePickup pickup = visualRoot.GetComponentInChildren<ConsumablePickup>(true);
        if (pickup != null)
            pickup.enabled = false;

        Collider[] colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] rigidbodies = visualRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }
    }
}
