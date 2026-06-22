using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Demo 竖切片流程：追踪击杀、玩家死亡、胜负与重开。
/// </summary>
[DisallowMultipleComponent]
public sealed class DemoGameManager : MonoBehaviour
{
    public static DemoGameManager Instance { get; private set; }

    [SerializeField] private Player player;
    [SerializeField] private DemoFlowUI flowUI;
    [SerializeField] private int killsToWin = 3;
    [SerializeField] private float defeatDelaySeconds = 0.6f;

    private int enemiesTotal;
    private int enemiesKilled;
    private bool gameEnded;

    public bool IsPlaying => !gameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (player == null)
            player = Object.FindObjectOfType<Player>();

        if (flowUI == null)
            flowUI = GetComponent<DemoFlowUI>();

        if (player != null)
            player.Died += OnPlayerDied;

        flowUI?.ShowIntro(killsToWin);
        flowUiUpdateProgress();
    }

    /// <summary>
    /// 由 GoblinSpawner 在生成怪物后调用。
    /// </summary>
    public void RegisterEnemy(Monster enemy)
    {
        if (enemy == null || gameEnded || enemy.IsDead)
            return;

        enemiesTotal++;
        enemy.Died += OnEnemyDied;
        flowUiUpdateProgress();
    }

    private void OnEnemyDied()
    {
        if (gameEnded)
            return;

        enemiesKilled++;
        flowUiUpdateProgress();

        int goal = Mathf.Max(killsToWin, enemiesTotal);
        if (enemiesKilled >= goal)
            Win();
    }

    private void OnPlayerDied()
    {
        if (gameEnded)
            return;

        Invoke(nameof(ShowDefeat), defeatDelaySeconds);
    }

    private void ShowDefeat()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        Time.timeScale = 0f;
        flowUI?.ShowDefeat(Restart);
    }

    private void Win()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        Time.timeScale = 0f;
        flowUI?.ShowVictory(Restart);
    }

    private void flowUiUpdateProgress()
    {
        int target = Mathf.Max(killsToWin, enemiesTotal);
        flowUI?.UpdateProgress(enemiesKilled, target);
    }

    public void NotifyBossDefeated()
    {
        if (gameEnded)
            return;

        Win();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
