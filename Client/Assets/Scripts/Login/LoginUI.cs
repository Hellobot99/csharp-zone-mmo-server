using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private TMP_Text errorText;

    private LoginHandler _loginHandler;
    private RegisterHandler _registerHandler;

    private void Awake()
    {
        _loginHandler    = GetComponent<LoginHandler>();
        _registerHandler = GetComponent<RegisterHandler>();
        errorText.gameObject.SetActive(false);
        loginButton.onClick.AddListener(OnLoginButtonClicked);
        registerButton.onClick.AddListener(OnRegisterButtonClicked);
    }

    private void OnLoginButtonClicked()
    {
        string username = usernameInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Username and password are required.");
            return;
        }

        errorText.gameObject.SetActive(false);
        StartCoroutine(_loginHandler.LoginAsync(username, password));
    }

    private void OnRegisterButtonClicked()
    {
        string username = usernameInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Username and password are required.");
            return;
        }

        errorText.gameObject.SetActive(false);
        StartCoroutine(_registerHandler.RegisterAsync(username, password));
    }

    public void ShowError(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }

    public void ShowMessage(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }
}
