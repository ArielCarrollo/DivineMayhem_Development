using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class LocalReadyPanel : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] public Button ReadyButton; // Público para que el Manager acceda
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private Image panelBackground;

    [Header("Configuración Visual")]
    [SerializeField] private Color notReadyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color readyStateColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    private LocalLobbyManager manager;
    private LocalPlayerData myData;

    // Ya no necesitamos guardar el input aquí para eventos, solo para info si hiciera falta
    private PlayerInput myInput;

    public void Initialize(LocalLobbyManager lobbyManager, LocalPlayerData data, PlayerInput input, Color playerColor)
    {
        manager = lobbyManager;
        myData = data;
        myInput = input;

        // 1. AUTO-BUSCAR REFERENCIAS
        if (nameText == null) nameText = transform.Find("name")?.GetComponent<TextMeshProUGUI>();
        if (ReadyButton == null) ReadyButton = transform.Find("ReadyButton")?.GetComponent<Button>();
        if (ReadyButton != null && readyButtonText == null) readyButtonText = ReadyButton.GetComponentInChildren<TextMeshProUGUI>();

        // 2. APLICAR DATOS
        if (nameText != null)
        {
            nameText.text = data.Username;
            nameText.color = playerColor;
        }

        // 3. CONFIGURAR BOTÓN (Solo UI)
        if (ReadyButton != null)
        {
            // Limpiamos listeners previos para evitar duplicados
            ReadyButton.onClick.RemoveAllListeners();
            // Asignamos la función que se ejecuta SOLO al hacer clic en este botón
            ReadyButton.onClick.AddListener(OnSubmitUI);
        }

        // --- CORRECCIÓN ---
        // HEMOS ELIMINADO la suscripción a myInput.actions["Submit"].performed.
        // Ahora dependemos 100% del MultiplayerEventSystem y el botón de UI.

        UpdateUI();
    }

    // Este método solo se llama si el MultiplayerEventSystem "hace clic" en el botón ReadyButton
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
            if (ReadyButton) ReadyButton.image.color = readyStateColor;
        }
        else
        {
            if (readyButtonText) readyButtonText.text = "No Listo";
            if (ReadyButton) ReadyButton.image.color = notReadyColor;
        }
    }
}