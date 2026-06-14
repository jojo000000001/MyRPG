using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 登录界面逻辑：引用场景中预先摆好的 UI 控件。
/// </summary>
[DisallowMultipleComponent]
public sealed class LoginUI : MonoBehaviour
{
    private const string LastUsernameKey = "LastUsername";

    [Header("Flow")]
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private int minUsernameLength = 1;
    [SerializeField] private int minPasswordLength = 1;

    [Header("UI")]
    [SerializeField] private InputField usernameField;
    [SerializeField] private InputField passwordField;
    [SerializeField] private Text statusText;
    [SerializeField] private Button loginButton;

    private bool isLoading;

    private void Awake()
    {
        GameplayCursor.UnlockForUI();
    }

    private void OnEnable()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(TryLogin);

        if (passwordField != null)
            passwordField.onEndEdit.AddListener(OnPasswordEndEdit);
    }

    private void OnDisable()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(TryLogin);

        if (passwordField != null)
            passwordField.onEndEdit.RemoveListener(OnPasswordEndEdit);
    }

    private void Start()
    {
        string savedUsername = PlayerPrefs.GetString(LastUsernameKey, string.Empty);
        if (usernameField != null && !string.IsNullOrEmpty(savedUsername))
            usernameField.text = savedUsername;

        usernameField?.ActivateInputField();
    }

    private void OnPasswordEndEdit(string _)
    {
        TryLogin();
    }

    private void TryLogin()
    {
        if (isLoading)
            return;

        string username = usernameField != null ? usernameField.text.Trim() : string.Empty;
        string password = passwordField != null ? passwordField.text : string.Empty;

        if (username.Length < minUsernameLength)
        {
            SetStatus("请输入账号");
            return;
        }

        if (password.Length < minPasswordLength)
        {
            SetStatus("请输入密码");
            return;
        }

        isLoading = true;
        if (loginButton != null)
            loginButton.interactable = false;

        SetStatus(string.Empty);
        GameSession.SetUsername(username);
        PlayerPrefs.SetString(LastUsernameKey, username);
        PlayerPrefs.Save();

        GameplayCursor.LockForGameplay();

        LoadGameplayScene();
    }

    private void LoadGameplayScene()
    {
        int buildIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{gameSceneName}.scene");
        if (buildIndex < 0)
            buildIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{gameSceneName}.unity");

        if (buildIndex >= 0)
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
