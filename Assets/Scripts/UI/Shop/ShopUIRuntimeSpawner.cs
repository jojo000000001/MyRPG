using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShopUIRuntimeSpawner : MonoBehaviour
{
    [SerializeField] private ShopUI shopUIPrefab;
    [SerializeField] private bool spawnOnAwake = true;

    private ShopUI runtimeInstance;

    public ShopUI RuntimeInstance => runtimeInstance;

    private void Awake()
    {
        if (spawnOnAwake)
            Spawn();
    }

    public ShopUI Spawn()
    {
        if (runtimeInstance != null)
            return runtimeInstance;

        runtimeInstance = GetComponentInChildren<ShopUI>(true);
        if (runtimeInstance != null)
            return runtimeInstance;

        if (shopUIPrefab == null)
        {
            Debug.LogWarning("Shop UI prefab is missing.", this);
            return null;
        }

        runtimeInstance = Instantiate(shopUIPrefab, transform);
        runtimeInstance.name = "ShopUIRoot";
        Stretch(runtimeInstance.GetComponent<RectTransform>());
        return runtimeInstance;
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
