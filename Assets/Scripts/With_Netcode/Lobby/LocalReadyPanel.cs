using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class LocalReadyPanel : MonoBehaviour
{
    [Header("Referencias UI (Automáticas o Manuales)")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText; // El texto dentro del botón
    [SerializeField] private Image panelBackground;

    [Header("Configuración Visual")]
    [SerializeField] private Color notReadyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color readyStateColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    private LocalLobbyManager manager;
    private LocalPlayerData myData;
    private PlayerInput myInput;

    public void Initialize(LocalLobbyManager lobbyManager, LocalPlayerData data, PlayerInput input, Color playerColor)
    {
        manager = lobbyManager;
        myData = data;
        myInput = input;

        // 1. AUTO-BUSCAR REFERENCIAS si no están asignadas
        // Busca un hijo llamado "name" (como pediste)
        if (nameText == null)
            nameText = transform.Find("name")?.GetComponent<TextMeshProUGUI>();

        // Busca un hijo llamado "ReadyButton"
        if (readyButton == null)
            readyButton = transform.Find("ReadyButton")?.GetComponent<Button>();

        // Busca el texto dentro del botón (opcional)
        if (readyButton != null && readyButtonText == null)
            readyButtonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();

        // 2. APLICAR COLOR DE JUGADOR
        if (nameText != null)
        {
            nameText.text = data.Username;
            nameText.color = playerColor; // <--- AQUÍ APLICAMOS EL COLOR DEL PUNTERO
        }

        // 3. CONFIGURAR BOTÓN DE UI (Para clic con cursor virtual)
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnSubmitUI);
        }

        // 4. CONFIGURAR INPUT DE MANDO (Para botón físico 'A' o 'X')
        // Usamos el action map "UI" o "Player" según tengas configurado
        if (myInput.actions.FindAction("Submit") != null)
            myInput.actions["Submit"].performed += OnSubmitPressed;

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (myInput != null && myInput.actions.FindAction("Submit") != null)
        {
            myInput.actions["Submit"].performed -= OnSubmitPressed;
        }
    }

    // Se llama si pulsas el botón físico del mando
    private void OnSubmitPressed(InputAction.CallbackContext ctx)
    {
        ToggleReady();
    }

    // Se llama si haces clic con el puntero virtual en el botón "ReadyButton"
    private void OnSubmitUI()
    {
        ToggleReady();
    }

    private void ToggleReady()
    {
        myData.IsReady = !myData.IsReady;
        manager.OnPlayerReadyChange();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (nameText != null) nameText.text = myData.Username;

        if (myData.IsReady)
        {
            if (readyButtonText) readyButtonText.text = "¡LISTO!";
            // Cambiamos el color del botón o del fondo
            if (readyButton) readyButton.image.color = readyStateColor;
        }
        else
        {
            if (readyButtonText) readyButtonText.text = "No Listo";
            if (readyButton) readyButton.image.color = notReadyColor;
        }
    }
}