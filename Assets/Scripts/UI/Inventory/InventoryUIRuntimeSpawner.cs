using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryUIRuntimeSpawner : MonoBehaviour
{
    [SerializeField] private InventoryUI inventoryUIPrefab;
    [SerializeField] private Player player;
    [SerializeField] private Inventory inventory;
    [SerializeField] private bool spawnOnAwake = true;

    private InventoryUI runtimeInstance;

    public InventoryUI RuntimeInstance => runtimeInstance;

    public void BindPlayer(Player target)
    {
        if (target == null)
            return;

        player = target;
        inventory = target.GetComponent<Inventory>();
        if (runtimeInstance != null)
            runtimeInstance.Initialize(inventory, player);
    }

    private void Awake()
    {
        if (spawnOnAwake)
            Spawn();
    }

    public InventoryUI Spawn()
    {
        if (runtimeInstance != null)
            return runtimeInstance;

        ResolveTargets();

        if (inventoryUIPrefab == null)
        {
            Debug.LogWarning("Inventory UI prefab is missing.", this);
            return null;
        }

        runtimeInstance = Instantiate(inventoryUIPrefab, transform);
        runtimeInstance.name = "InventoryUIRoot";
        Stretch(runtimeInstance.GetComponent<RectTransform>());
        runtimeInstance.Initialize(inventory, player);
        return runtimeInstance;
    }

    private void ResolveTargets()
    {
        if (player == null)
            player = Player.Resolve();

        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>();
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
