using UnityEngine;
using UnityEngine.InputSystem;

public class LocalPlayerContext : MonoBehaviour
{
    public static System.Action<LocalPlayerContext> OnPlayerJoinedContext;
    public static System.Action<LocalPlayerContext> OnPlayerLeftContext;

    [Header("Referencias")]
    [SerializeField] private GameObject cursorPrefab;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true; // Actívalo en el inspector

    public LocalPlayerData Data { get; private set; }
    public PlayerInput Input { get; private set; }
    public GamepadVirtualCursor MyCursor { get; private set; }

    private void Awake()
    {
        Input = GetComponent<PlayerInput>();
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // DEBUG: Confirmación de conexión
        if (showDebugLogs)
        {
            string devName = Input.devices.Count > 0 ? Input.devices[0].displayName : "Desconocido";
            Debug.Log($"<color=green>[CONEXIÓN]</color> ¡Jugador {Input.playerIndex} se ha unido! Usando: {devName}");
        }

        // 1. Inicializar Datos
        int index = Input.playerIndex;
        Data = new LocalPlayerData
        {
            PlayerIndex = index,
            Username = $"Jugador {index + 1}",
            IsReady = false,
            Level = 1
        };

        // 2. Crear Cursor
        SpawnCursor();

        // 3. Suscribirse a eventos de input para DEBUG
        // Esto disparará un log cada vez que hagas CUALQUIER cosa con el mando
        if (showDebugLogs)
        {
            Input.onActionTriggered += HandleDebugInput;
        }

        OnPlayerJoinedContext?.Invoke(this);
    }

    private void HandleDebugInput(InputAction.CallbackContext ctx)
    {
        // Solo mostramos logs cuando se "realiza" la acción (botón presionado o stick movido)
        // para no saturar la consola.
        if (ctx.performed)
        {
            Debug.Log($"[INPUT P{Input.playerIndex}] Acción: <b>{ctx.action.name}</b> | Valor: {ctx.ReadValueAsObject()}");
        }
    }

    private void SpawnCursor()
    {
        GameObject canvasObj = GameObject.FindGameObjectWithTag("MainCanvas");
        Canvas canvas = canvasObj != null ? canvasObj.GetComponent<Canvas>() : FindFirstObjectByType<Canvas>();

        if (canvas != null && cursorPrefab != null)
        {
            GameObject cursorObj = Instantiate(cursorPrefab, canvas.transform);
            MyCursor = cursorObj.GetComponent<GamepadVirtualCursor>();

            RectTransform cursorRect = cursorObj.GetComponent<RectTransform>();
            cursorRect.anchoredPosition = Vector2.zero;
            cursorObj.transform.SetAsLastSibling();

            Color pColor = GetColorForIndex(Input.playerIndex);

            // Pasamos el índice también para el debug visual
            MyCursor.Initialize(Input, pColor, Input.playerIndex);
        }
        else
        {
            Debug.LogError("¡LocalPlayerContext no encontró un Canvas!");
        }
    }

    private void OnDestroy()
    {
        if (showDebugLogs)
        {
            Debug.Log($"<color=red>[DESCONEXIÓN]</color> Jugador {Input.playerIndex} ha salido.");
            if (Input != null) Input.onActionTriggered -= HandleDebugInput;
        }

        OnPlayerLeftContext?.Invoke(this);
        if (MyCursor != null) Destroy(MyCursor.gameObject);
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