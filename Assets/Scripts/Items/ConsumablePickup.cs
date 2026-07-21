using UnityEngine;

[DisallowMultipleComponent]
public sealed class ConsumablePickup : MonoBehaviour, IPoolable
{
    [Header("Item")]
    [SerializeField] private ItemSO item;
    [SerializeField] private bool addToInventory = true;
    [SerializeField] private bool consumeOnPickup = true;
    [SerializeField] private bool destroyOnPickup = true;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private float spinSpeed = 45f;
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobFrequency = 1.6f;

    private Vector3 visualBaseLocalPosition;
    private bool hasVisualBase;
    private bool consumed;

    public ItemSO Item => item;

    public void OnSpawnedFromPool()
    {
        transform.localRotation = Quaternion.identity;
        hasVisualBase = false;
        consumed = false;
        CacheVisualBase();
    }

    public void OnReturnedToPool()
    {
    }

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        CacheVisualBase();
    }

    private void OnValidate()
    {
        spinSpeed = Mathf.Max(0f, spinSpeed);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
    }

    private void LateUpdate()
    {
        if (visualRoot == null)
            return;

        CacheVisualBase();

        float bobOffset = bobAmplitude <= 0f
            ? 0f
            : Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;

        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * bobOffset;

        if (spinSpeed > 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        Camera mainCamera = Camera.main;
        if (faceCamera && mainCamera != null)
            visualRoot.rotation = Quaternion.LookRotation(mainCamera.transform.forward, mainCamera.transform.up);
    }

private void OnTriggerEnter(Collider other)
    {
        TryPickup(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPickup(other);
    }

    private void TryPickup(Collider other)
    {
        if (!consumeOnPickup || item == null || consumed)
            return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead)
            return;

        Inventory inventory = player.GetComponent<Inventory>();
        if (!TryHandlePickup(player, inventory))
            return;

        consumed = true;

        if (destroyOnPickup)
        {
            if (!PooledObject.TryRelease(gameObject))
                Destroy(gameObject);
        }
    }

    private bool TryHandlePickup(Player player, Inventory inventory)
    {
        if (addToInventory && inventory != null && inventory.AddItem(item, 1, insertAtFront: true))
        {
            if (ShouldAutoEquipWeapon(player))
                player.EquipWeapon(item);

            return true;
        }

        if (IsWeaponItem(item) && ShouldAutoEquipWeapon(player))
            return player.EquipWeapon(item);

        if (IsWeaponItem(item))
            return false;

        ApplyTo(player);
        return true;
    }

    private void ApplyTo(Player player)
    {
        ConsumableEffectApplicator.Apply(item, player);
    }

    private void CacheVisualBase()
    {
        if (hasVisualBase || visualRoot == null)
            return;

        visualBaseLocalPosition = visualRoot.localPosition;
        hasVisualBase = true;
    }


private bool ShouldAutoEquipWeapon(Player player)
    {
        return player != null
            && player.EquippedWeapon == null
            && IsWeaponItem(item);
    }

    private static bool IsWeaponItem(ItemSO candidate)
    {
        return candidate != null
            && candidate.itemType == ItemType.Weapon
            && candidate.GetPropertyValue(ItemPropertyType.AttackValue) > 0;
    }
}
