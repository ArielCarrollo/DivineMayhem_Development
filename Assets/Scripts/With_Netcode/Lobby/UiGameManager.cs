using UnityEngine;
using UnityEngine.UI;

public class UiGameManager : MonoBehaviour
{
    public static UiGameManager Instance { get; private set; }

    [Header("Paneles")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel; // El panel donde está el LocalLobbyManager

    [Header("Botones Menu")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Configuración inicial
        ShowMainMenu();

        playButton.onClick.AddListener(OnPlayClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnPlayClicked()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        // Al activar el lobbyPanel, el LocalLobbyManager (que debe estar ahí) empezará a escuchar inputs
    }

    private void OnQuitClicked()
    {
        Application.Quit();
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }
}