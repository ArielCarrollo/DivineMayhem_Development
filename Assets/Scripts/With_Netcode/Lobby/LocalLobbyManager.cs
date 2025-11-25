using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor de lobby local para multijugador en la misma máquina.
/// Este componente permite detectar la entrada de varios mandos mediante
/// PlayerInputManager y asigna un cursor único a cada jugador. Cada vez
/// que se detecta un nuevo jugador se genera un cursor con un color diferente.
/// Cuando un jugador se desconecta se elimina su cursor.
/// </summary>
public class LocalLobbyManager : MonoBehaviour
{
    [Header("Configuración de Player Input")]
    [Tooltip("Referencia al PlayerInputManager que gestionará las uniones de jugadores.")]
    public PlayerInputManager playerInputManager;

    [Header("Prefabs y UI")]
    [Tooltip("Prefab del cursor que se instanciará por cada jugador. Este prefab debe tener el componente GamepadVirtualCursor.")]
    public GameObject cursorPrefab;
    [Tooltip("Transform padre donde se instanciarán los cursores (por ejemplo, el Canvas principal).")]
    public RectTransform cursorParent;

    [Header("Colores de Jugador")]
    [Tooltip("Colores que se asignarán a los cursores en orden. Si hay más jugadores que colores, los colores se repetirán.")]
    public Color[] playerColors = new Color[]
    {
        new Color(0.9f, 0.3f, 0.3f), // rojo claro
        new Color(0.3f, 0.6f, 0.9f), // azul claro
        new Color(0.3f, 0.9f, 0.4f), // verde
        new Color(0.9f, 0.8f, 0.3f), // amarillo
        new Color(0.8f, 0.3f, 0.9f), // violeta
    };

    // Listado de jugadores y sus cursores
    private readonly List<PlayerInput> joinedPlayers = new List<PlayerInput>();
    private readonly Dictionary<PlayerInput, GamepadVirtualCursor> cursors = new Dictionary<PlayerInput, GamepadVirtualCursor>();

    // --- Ready UI (para lobby offline) ---
    [Header("Ready UI")]
    [Tooltip("Prefab del panel de ready para cada jugador local.")]
    public GameObject readyPanelPrefab;
    [Tooltip("Contenedor donde se instanciarán los panels de ready.")]
    public RectTransform readyPanelParent;
    [Tooltip("Texto global donde se mostrará el estado de listos y la cuenta atrás.")]
    public TMPro.TextMeshProUGUI readyStatusText;

    // Diccionarios para manejar el estado "listo" de cada jugador y sus panels
    private readonly Dictionary<PlayerInput, bool> readyStates = new Dictionary<PlayerInput, bool>();
    private readonly Dictionary<PlayerInput, LocalReadyPanel> readyPanels = new Dictionary<PlayerInput, LocalReadyPanel>();
    // Corutina para la cuenta atrás cuando todos están listos
    private Coroutine countdownCoroutine;
    private const float countdownSeconds = 5f;

    private void Awake()
    {
        // Si no se asigna el PlayerInputManager en el inspector, intentamos encontrarlo en la escena
        if (playerInputManager == null)
        {
            playerInputManager = PlayerInputManager.instance;
        }
        if (playerInputManager == null)
        {
            Debug.LogError("LocalLobbyManager: No se encontró ningún PlayerInputManager en la escena. Para el modo local es necesario uno.");
        }
    }

    private void OnEnable()
    {
        if (playerInputManager != null)
        {
            // Aseguramos que se permite unirse al lobby cuando se presione un botón
            // Configurar el comportamiento de unión usando el enum PlayerJoinBehavior
            playerInputManager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
            playerInputManager.EnableJoining();
            playerInputManager.onPlayerJoined += OnPlayerJoined;
            playerInputManager.onPlayerLeft += OnPlayerLeft;
        }
    }

    private void OnDisable()
    {
        if (playerInputManager != null)
        {
            playerInputManager.onPlayerJoined -= OnPlayerJoined;
            playerInputManager.onPlayerLeft -= OnPlayerLeft;
        }
    }

    /// <summary>
    /// Callback que se ejecuta cuando un nuevo jugador entra en el lobby local.
    /// Se asigna un color, se instancia su cursor y se añade a la lista de jugadores.
    /// </summary>
    /// <param name="playerInput">PlayerInput recién creado por PlayerInputManager</param>
    private void OnPlayerJoined(PlayerInput playerInput)
    {
        if (playerInput == null)
            return;

        // Añadir a la lista de jugadores
        joinedPlayers.Add(playerInput);
        int index = joinedPlayers.Count - 1;

        // Seleccionar un color para el cursor y panel
        Color color = playerColors.Length > 0 ? playerColors[index % playerColors.Length] : Color.white;

        // Instanciar el cursor y configurarlo
        if (cursorPrefab != null && cursorParent != null)
        {
            GameObject cursorGO = Instantiate(cursorPrefab, cursorParent);
            var gvc = cursorGO.GetComponent<GamepadVirtualCursor>();
            if (gvc != null)
            {
                gvc.Initialize(playerInput, color);
                cursors[playerInput] = gvc;
            }
            else
            {
                Debug.LogWarning("LocalLobbyManager: el prefab del cursor no contiene un componente GamepadVirtualCursor.");
            }
        }
        else
        {
            Debug.LogWarning("LocalLobbyManager: cursorPrefab o cursorParent no asignados.");
        }

        // Crear el panel de ready para este jugador
        if (readyPanelPrefab != null && readyPanelParent != null)
        {
            GameObject panelGO = Instantiate(readyPanelPrefab, readyPanelParent);
            var readyPanel = panelGO.GetComponent<LocalReadyPanel>();
            if (readyPanel != null)
            {
                string playerName = $"Player {index + 1}";
                readyPanel.Setup(playerInput, this, playerName);
                readyPanels[playerInput] = readyPanel;
                readyStates[playerInput] = false;
            }
            else
            {
                Debug.LogWarning("LocalLobbyManager: el prefab del ready panel no contiene LocalReadyPanel.");
                readyStates[playerInput] = false;
            }
        }
        else
        {
            readyStates[playerInput] = false;
        }

        // Añadir al GameManager en modo offline para que la UI del lobby lo pinte
        if (GameManager.Instance != null && GameManager.Instance.IsOfflineMode)
        {
            try
            {
                // Crear datos básicos para el jugador local
                PlayerData pd = new PlayerData();
                pd.ClientId = (ulong)index;
                pd.IsReady = false;
                pd.Level = 0;
                pd.Username = new Unity.Collections.FixedString64Bytes($"Player {index + 1}");

                bool already = false;
                for (int i = 0; i < GameManager.Instance.PlayersInLobby.Count; i++)
                {
                    if (GameManager.Instance.PlayersInLobby[i].ClientId == pd.ClientId)
                    {
                        already = true;
                        break;
                    }
                }
                if (!already)
                {
                    GameManager.Instance.PlayersInLobby.Add(pd);
                }
            }
            catch
            {
                // Ignorar fallos al manipular la NetworkList en modo offline
            }
        }

        // Actualizar texto de estado de ready
        UpdateReadyStatusText();
    }

    /// <summary>
    /// Callback que se ejecuta cuando un jugador abandona el lobby local (por ejemplo, desconecta su mando).
    /// Elimina su cursor y lo borra de las listas.
    /// </summary>
    /// <param name="playerInput">PlayerInput que abandonó</param>
    private void OnPlayerLeft(PlayerInput playerInput)
    {
        if (playerInput == null)
            return;

        // Eliminar de la lista de jugadores
        joinedPlayers.Remove(playerInput);

        // Destruir su cursor
        if (cursors.TryGetValue(playerInput, out var cursor))
        {
            if (cursor != null)
            {
                Destroy(cursor.gameObject);
            }
            cursors.Remove(playerInput);
        }

        // Eliminar de la lista del GameManager en offline, si corresponde
        if (GameManager.Instance != null && GameManager.Instance.IsOfflineMode)
        {
            try
            {
                ulong clientId = 0;
                // Encontrar el index de este playerInput en joinedPlayers para determinar el clientId
                // Nota: si hay salidas en medio, las ids podrían no coincidir exactamente.
                // Para simplificar, buscamos por nombre (Jugador X)
                string name = $"Player {joinedPlayers.Count + 1}";
                for (int i = 0; i < GameManager.Instance.PlayersInLobby.Count; i++)
                {
                    if (GameManager.Instance.PlayersInLobby[i].Username.ToString() == name)
                    {
                        GameManager.Instance.PlayersInLobby.RemoveAt(i);
                        break;
                    }
                }
            }
            catch
            {
                // ignorar posibles errores
            }
        }

        // Quitar panel de ready y estado
        if (readyPanels.TryGetValue(playerInput, out var panel))
        {
            if (panel != null)
                Destroy(panel.gameObject);
            readyPanels.Remove(playerInput);
        }
        if (readyStates.ContainsKey(playerInput))
        {
            readyStates.Remove(playerInput);
        }

        // Cancelar cuenta atrás si estaba activa
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        // Actualizar texto de ready
        UpdateReadyStatusText();
    }

    /// <summary>
    /// Llama a este método desde un botón de UI para iniciar la partida local.
    /// Carga la escena de juego configurada en el GameManager y spawnea los jugadores.
    /// </summary>
    public void StartLocalGame()
    {
        // Guardar el número de jugadores a transferir a la escena de juego
        int count = joinedPlayers.Count;
        if (count == 0)
        {
            Debug.LogWarning("LocalLobbyManager: No hay jugadores para iniciar la partida.");
            return;
        }
        // Puedes almacenar el número de jugadores en un objeto que persista entre escenas
        // o en un static temporal para usar al cargar la escena. Aquí usamos GameManager.
        if (GameManager.Instance != null)
        {
            // Habilitar modo offline a través del método público
            GameManager.Instance.SetOfflineMode(true);
        }

        // Mantener los PlayerInput al cambiar de escena para que persistan en el juego
        foreach (var pi in joinedPlayers)
        {
            if (pi != null)
                DontDestroyOnLoad(pi.gameObject);
        }
        // Cargar la escena de juego localmente
        string sceneName = "Game";
        try
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        catch
        {
            Debug.LogError($"LocalLobbyManager: no se pudo cargar la escena {sceneName}. Asegúrate de que esté añadida en la build.");
        }
    }

    /// <summary>
    /// Establece el estado listo/no listo de un jugador local y gestiona la lógica
    /// de cuenta atrás cuando todos están listos.
    /// </summary>
    /// <param name="pi">PlayerInput del jugador</param>
    /// <param name="isReady">Nuevo estado</param>
    public void SetReadyState(PlayerInput pi, bool isReady)
    {
        if (pi == null)
            return;
        readyStates[pi] = isReady;
        UpdateReadyStatusText();

        // Comprobar si todos los jugadores están listos
        bool allReady = true;
        if (joinedPlayers.Count == 0) allReady = false;
        foreach (var kv in readyStates)
        {
            if (!kv.Value)
            {
                allReady = false;
                break;
            }
        }
        if (allReady)
        {
            if (countdownCoroutine == null)
            {
                countdownCoroutine = StartCoroutine(CountdownAndStart());
            }
        }
        else
        {
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }
        }
    }

    /// <summary>
    /// Actualiza el texto global de estado de listos. Si hay una cuenta atrás,
    /// ésta se mostrará en la corutina de cuenta atrás.
    /// </summary>
    private void UpdateReadyStatusText()
    {
        if (readyStatusText == null)
            return;
        // Si hay cuenta atrás, no actualizar aquí
        if (countdownCoroutine != null)
            return;
        int readyCount = 0;
        foreach (var kv in readyStates)
        {
            if (kv.Value) readyCount++;
        }
        int total = readyStates.Count;
        readyStatusText.text = $"Listos: {readyCount}/{total}";
    }

    /// <summary>
    /// Corutina que realiza una cuenta atrás de 5 segundos antes de iniciar el juego.
    /// Si algún jugador deja de estar listo durante la cuenta atrás, ésta se cancela.
    /// </summary>
    private IEnumerator CountdownAndStart()
    {
        float remaining = countdownSeconds;
        while (remaining > 0f)
        {
            // Cancelar si algún jugador ya no está listo
            foreach (var kv in readyStates)
            {
                if (!kv.Value)
                {
                    UpdateReadyStatusText();
                    countdownCoroutine = null;
                    yield break;
                }
            }
            if (readyStatusText != null)
            {
                readyStatusText.text = $"Empezando en {Mathf.CeilToInt(remaining)}...";
            }
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        // Verificar que todos siguen listos
        foreach (var kv in readyStates)
        {
            if (!kv.Value)
            {
                UpdateReadyStatusText();
                countdownCoroutine = null;
                yield break;
            }
        }
        // Iniciar partida
        StartLocalGame();
        countdownCoroutine = null;
    }
}