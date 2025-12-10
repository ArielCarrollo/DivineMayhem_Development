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

    [Header("Selección de Clase")]
    [SerializeField] private TextMeshProUGUI classText;
    [SerializeField] public Button BtnNextClass; // Público para linkear navegación
    [SerializeField] public Button BtnPrevClass;
    [SerializeField] private Image classImage;

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
        myInput = input;
        myEventSystem = input.GetComponent<EventSystem>();

        // Auto-Referencias
        if (nameText == null) nameText = transform.Find("name")?.GetComponent<TextMeshProUGUI>();
        if (ReadyButton == null) ReadyButton = transform.Find("ReadyButton")?.GetComponent<Button>();

        // Busca los botones de clase si no están asignados (asegúrate de ponerles nombre en el prefab)
        if (BtnNextClass == null) BtnNextClass = transform.Find("BtnNext")?.GetComponent<Button>();
        if (BtnPrevClass == null) BtnPrevClass = transform.Find("BtnPrev")?.GetComponent<Button>();
        if (classText == null) classText = transform.Find("ClassText")?.GetComponent<TextMeshProUGUI>();
        if (classImage == null) classImage = transform.Find("ClassImage")?.GetComponent<Image>();

        if (nameText != null)
        {
            nameText.text = data.Username;
            nameText.color = playerColor;
        }

        // Listeners
        if (ReadyButton != null)
        {
            ReadyButton.onClick.RemoveAllListeners();
            ReadyButton.onClick.AddListener(() => ToggleReady());
        }

        if (BtnNextClass != null)
        {
            BtnNextClass.onClick.RemoveAllListeners();
            BtnNextClass.onClick.AddListener(() => manager.ChangePlayerClass(myData, 1));
        }
        if (BtnPrevClass != null)
        {
            BtnPrevClass.onClick.RemoveAllListeners();
            BtnPrevClass.onClick.AddListener(() => manager.ChangePlayerClass(myData, -1));
        }

        // Navegación Local (Para P2, P3...)
        // Si no es el P1 (que se maneja en el Manager), configuramos navegación interna básica
        if (input.playerIndex != 0)
        {
            SetupInternalNavigation();
        }

        // Input Físico "A"
        InputAction submitAction = myInput.actions.FindAction("Submit");
        if (submitAction == null) submitAction = myInput.actions.FindAction("Jump");
        if (submitAction != null) submitAction.performed += OnHardwareSubmit;

        UpdateUI();
        UpdateClassUI();
    }

    private void SetupInternalNavigation()
    {
        if (ReadyButton == null || BtnNextClass == null || BtnPrevClass == null) return;

        // 1. Configurar Botón PREV (<)
        Navigation prevNav = new Navigation { mode = Navigation.Mode.Explicit };
        prevNav.selectOnRight = BtnNextClass; // Derecha -> Next
        prevNav.selectOnDown = ReadyButton;   // Abajo -> Ready
        prevNav.selectOnUp = BtnNextClass;    // Arriba -> Loop a Next (opcional) o nada
        // Bloqueo izquierdo para no salirse de la tarjeta
        prevNav.selectOnLeft = BtnNextClass;  // Loop cíclico (Izquierda va al otro extremo)
        BtnPrevClass.navigation = prevNav;

        // 2. Configurar Botón NEXT (>)
        Navigation nextNav = new Navigation { mode = Navigation.Mode.Explicit };
        nextNav.selectOnLeft = BtnPrevClass;  // Izquierda -> Prev
        nextNav.selectOnDown = ReadyButton;   // Abajo -> Ready
        nextNav.selectOnUp = BtnPrevClass;    // Arriba -> Loop
        // Bloqueo derecho
        nextNav.selectOnRight = BtnPrevClass; // Loop cíclico
        BtnNextClass.navigation = nextNav;

        // 3. Configurar Botón READY (Listo)
        Navigation readyNav = new Navigation { mode = Navigation.Mode.Explicit };
        readyNav.selectOnUp = BtnNextClass;   // Subir -> Next (por defecto)

        // Atajos laterales desde Ready:
        readyNav.selectOnLeft = BtnPrevClass; // Izquierda -> Sube a Prev
        readyNav.selectOnRight = BtnNextClass;// Derecha -> Sube a Next

        // Bloqueo abajo
        readyNav.selectOnDown = BtnNextClass; // Loop vertical

        ReadyButton.navigation = readyNav;
    }

    public void UpdateClassUI()
    {
        if (classText != null && GameManager.Instance != null)
        {
            classText.text = GameManager.Instance.GetPantheonName(myData.PantheonIndex);
        }
        if (classImage != null)
            classImage.sprite = GameManager.Instance.GetPantheonIcon(myData.PantheonIndex);
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

        bool isReady = myData.IsReady;

        if (isReady)
        {
            if (readyButtonText) readyButtonText.text = "¡LISTO!";
            if (ReadyButton) ReadyButton.image.color = readyStateColor;
        }
        else
        {
            if (readyButtonText) readyButtonText.text = "No Listo";
            if (ReadyButton) ReadyButton.image.color = notReadyColor;
        }

        // Desactivar botones de cambio si está listo
        if (BtnNextClass) BtnNextClass.interactable = !isReady;
        if (BtnPrevClass) BtnPrevClass.interactable = !isReady;
    }
}