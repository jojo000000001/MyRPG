using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SaveGameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemCatalog itemCatalog;

    [Header("Input")]
    [SerializeField] private KeyCode saveKey = KeyCode.F5;
    [SerializeField] private KeyCode loadKey = KeyCode.F9;
    [SerializeField] private bool autoLoadOnStart;

    private bool isLoading;

    private void Awake()
    {
        ResolveReferences();
        if (itemCatalog != null)
            ItemCatalog.RegisterRuntimeInstance(itemCatalog);
    }

    private void Start()
    {
        if (autoLoadOnStart && SaveSystem.HasSave)
            StartCoroutine(LoadRoutine());
    }

    private void Update()
    {
        if (isLoading)
            return;

        if (Input.GetKeyDown(saveKey))
            Save();

        if (Input.GetKeyDown(loadKey))
            StartCoroutine(LoadRoutine());
    }

    public void Save()
    {
        ResolveReferences();
        SaveSystem.Save(player, inventory);
    }

    public void Load()
    {
        if (!isLoading)
            StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        if (isLoading)
            yield break;

        isLoading = true;

        if (!SaveSystem.TryRead(out SaveData data))
        {
            isLoading = false;
            yield break;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != activeScene)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(data.sceneName);
            while (loadOperation != null && !loadOperation.isDone)
                yield return null;

            yield return null;
            ResolveReferences();
        }

        ResolveReferences();
        if (player == null || inventory == null)
        {
            Debug.LogWarning("SaveGameController: Player or Inventory not found after load.");
            isLoading = false;
            yield break;
        }

        if (!SaveSystem.Apply(data, player, inventory, itemCatalog))
            Debug.LogWarning("SaveGameController: Load failed.");

        isLoading = false;
    }

    private void ResolveReferences()
    {
        if (player == null)
            player = FindObjectOfType<Player>();

        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>();

        if (itemCatalog == null)
            itemCatalog = ItemCatalog.EnsureAvailable();
    }
}
