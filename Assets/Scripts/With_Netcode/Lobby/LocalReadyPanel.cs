using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class LocalReadyPanel : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] public Button ReadyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;

    [Header("Configuración Visual")]
    [SerializeField] private Color notReadyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color readyStateColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    private LocalLobbyManager manager;
    private LocalPlayerData myData;
    private EventSystem myEventSystem;
    private PlayerInput myInput;

    public void Initialize(LocalLobbyManager lobbyManager, LocalPlayerData data, PlayerInput input, Color playerColor)
    {
        manager = lobbyManager;
        myData = data;
        myInput = input; // Guardamos referencia al input
        myEventSystem = input.GetComponent<EventSystem>();

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

        // 3. SUSCRIPCIÓN DIRECTA A HARDWARE (La solución "Fuerza Bruta")
        // Buscamos la acción 'Submit' en el mapa 'UI' o 'Player'
        InputAction submitAction = myInput.actions.FindAction("Submit"); // Mapa UI
        if (submitAction == null) submitAction = myInput.actions.FindAction("Jump"); // Fallback si no tienes mapa UI (Botón Sur)

        if (submitAction != null)
        {
            submitAction.performed += OnHardwareSubmit;
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
        // Limpieza obligatoria para no dejar eventos colgados
        if (myInput != null)
        {
            InputAction submitAction = myInput.actions.FindAction("Submit");
            if (submitAction == null) submitAction = myInput.actions.FindAction("Jump");

            if (submitAction != null) submitAction.performed -= OnHardwareSubmit;
        }
    }

    // Esta función se ejecuta SIEMPRE que aprietes "A", sin importar dónde estés
    private void OnHardwareSubmit(InputAction.CallbackContext ctx)
    {
        // FILTRO DE SEGURIDAD:
        // Solo hacemos caso al botón "A" si el selector del jugador está ENCIMA de este botón.
        // Esto permite que el P2 funcione (siempre está encima) 
        // y que el P1 NO se ponga listo si está tocando los botones de configuración.
        if (IsMyButtonSelected())
        {
            ToggleReady();
        }
    }

    private bool IsMyButtonSelected()
    {
        if (myEventSystem == null || ReadyButton == null) return false;
        return myEventSystem.currentSelectedGameObject == ReadyButton.gameObject;
    }

    private void ToggleReady()
    {
        myData.IsReady = !myData.IsReady;
        manager.OnPlayerReadyChange();
        UpdateUI();
    }

    // El Update se queda como guardián por si se pierde la selección visual
    private void Update()
    {
        if (myEventSystem != null && ReadyButton != null)
        {
            if (myEventSystem.currentSelectedGameObject == null && myEventSystem.gameObject.activeInHierarchy)
            {
                myEventSystem.SetSelectedGameObject(ReadyButton.gameObject);
            }
        }
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