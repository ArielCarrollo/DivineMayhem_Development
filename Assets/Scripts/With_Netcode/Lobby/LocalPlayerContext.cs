using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI; // Necesario para MultiplayerEventSystem

public class LocalPlayerContext : MonoBehaviour
{
    public static System.Action<LocalPlayerContext> OnPlayerJoinedContext;
    public static System.Action<LocalPlayerContext> OnPlayerLeftContext;

    [Header("Referencias")]
    // Arrastra aquí el PREFAB del marco (una imagen con borde transparente)
    [SerializeField] private GameObject selectorFramePrefab;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public LocalPlayerData Data { get; private set; }
    public PlayerInput Input { get; private set; }

    // Referencia al objeto visual del marco
    private MultiplayerSelectorVisual mySelectorVisual;

    private void Awake()
    {
        Input = GetComponent<PlayerInput>();
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 1. Configurar Datos
        int index = Input.playerIndex;
        Data = new LocalPlayerData
        {
            PlayerIndex = index,
            Username = $"Jugador {index + 1}",
            IsReady = false,
            Level = 1
        };

        // 2. Configurar la UI para Multijugador
        // El PlayerInput ya debería tener "UI Input Module" si usas el prefab por defecto,
        // pero nos aseguramos de asignar el root de la UI.
        ConfigureMultiplayerUI();

        // 3. Crear el Marco Visual (Selector)
        SpawnSelector();

        if (showDebugLogs) Debug.Log($"[CONEXIÓN] Jugador {index} listo.");
        OnPlayerJoinedContext?.Invoke(this);
    }

    private void ConfigureMultiplayerUI()
    {
        // 1. Asegurar que el mapa UI está habilitado
        if (Input.actions.FindActionMap("UI") != null)
        {
            Input.actions.FindActionMap("UI").Enable();
        }

        // 2. Configurar el módulo UI
        var uiModule = GetComponent<InputSystemUIInputModule>();
        if (uiModule != null)
        {
            // Asignar acciones por código si se pierden las referencias del inspector
            // (Esto es un "seguro de vida", idealmente hazlo en el Inspector del prefab)
            uiModule.move = InputActionReference.Create(Input.actions["UI/Navigate"]);
            uiModule.submit = InputActionReference.Create(Input.actions["UI/Submit"]);
            uiModule.cancel = InputActionReference.Create(Input.actions["UI/Cancel"]);
        }

        // 3. Seleccionar primer botón (del script anterior)
        FindFirstButton();
    }

    private void FindFirstButton()
    {
        // Buscamos un botón activo en el canvas para seleccionarlo al inicio
        // Nota: Mejor tener un "FirstSelected" en tu GameManager, esto es un fallback.
        var btn = FindFirstObjectByType<UnityEngine.UI.Button>();
        if (btn != null)
        {
            // Forzamos la selección en el EventSystem de ESTE jugador
            var eventSystem = GetComponent<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(btn.gameObject);
            }
        }
    }

    private void SpawnSelector()
    {
        // Buscamos el Canvas
        GameObject canvasObj = GameObject.FindGameObjectWithTag("MainCanvas");
        if (canvasObj != null && selectorFramePrefab != null)
        {
            GameObject selectorObj = Instantiate(selectorFramePrefab, canvasObj.transform);

            // Script visual
            mySelectorVisual = selectorObj.GetComponent<MultiplayerSelectorVisual>();

            // Obtenemos el EventSystem local de este jugador
            var myEventSystem = GetComponent<UnityEngine.EventSystems.EventSystem>();

            // Inicializamos el visual con mi EventSystem y mi color
            Color pColor = GetColorForIndex(Input.playerIndex);
            mySelectorVisual.Initialize(myEventSystem, pColor, Input.playerIndex);
        }
    }

    private void OnDestroy()
    {
        OnPlayerLeftContext?.Invoke(this);
        if (mySelectorVisual != null) Destroy(mySelectorVisual.gameObject);
    }

    private Color GetColorForIndex(int index)
    {
        switch (index)
        {
            case 0: return Color.blue;
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }
}