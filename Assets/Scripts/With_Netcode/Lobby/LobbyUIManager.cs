using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbyUIManager : MonoBehaviour
{
    #region UI References

    [Header("Paneles Principales")]
    [SerializeField] private GameObject lobbyPanel;

    [Header("Botones Generales")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI readyCountText;

    [Header("Configuración de Rondas (Host Only)")]
    [SerializeField] private Button decreaseRoundsButton; // Botón [-]
    [SerializeField] private Button increaseRoundsButton; // Botón [+]
    [SerializeField] private TextMeshProUGUI roundsValueText; // Texto "5"

    [Header("Control de Salida")]
    [Tooltip("Botón para que el Host cierre la sala")]
    [SerializeField] private Button closeLobbyButton;
    [Tooltip("Botón para que un Cliente abandone")]
    [SerializeField] private Button leaveLobbyButton;

    [Header("Lista de Jugadores")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerCardPrefab;
    [SerializeField] private PlayerInfoPopup playerInfoPopup;

    [Header("Selección de Mapa (Host)")]
    [SerializeField] private TextMeshProUGUI firstMapText;
    [SerializeField] private Button changeMapButton;

    [Header("Personalización (Clase/Panteón)")]
    [SerializeField] private PlayerAppearance previewPlayer;
    [SerializeField] private Button nextClassButton; // <--- ASEGÚRATE DE ASIGNAR ESTO EN INSPECTOR
    [SerializeField] private Button prevClassButton; // <--- ASEGÚRATE DE ASIGNAR ESTO EN INSPECTOR
    [SerializeField] private TextMeshProUGUI classNameText;
    [SerializeField] private TextMeshProUGUI deityNameText;
    [SerializeField] private Image pantheonIconImage;
    [SerializeField] private Sprite[] pantheonSprites;

    [Header("Nombre de Jugador")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TMP_InputField nameChangeInputField;
    [SerializeField] private Button saveNameButton;

    [Header("Colores Estado")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.white;

    #endregion

    #region Chat References
    [Header("Chat Público")]
    [SerializeField] private GameObject publicChatPanel;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Transform chatMessagesContainer;
    [SerializeField] private GameObject chatMessagePrefab;
    [SerializeField] private ScrollRect chatScroll;

    [Header("Chat Privado")]
    [SerializeField] private GameObject privateChatPanel;
    [SerializeField] private Transform privatePlayersContainer;
    [SerializeField] private GameObject privatePlayerButtonPrefab;
    [SerializeField] private Transform privateMessagesContainer;
    [SerializeField] private GameObject privateMessagePrefab;
    [SerializeField] private TMP_InputField privateChatInputField;
    [SerializeField] private Color privateUnreadColor = Color.yellow;
    #endregion

    #region Voice References
    [Header("Vivox Voice")]
    [SerializeField] private Button micToggleButton;
    [SerializeField] private TextMeshProUGUI micStateText;
    [SerializeField] private Button deafenToggleButton;
    [SerializeField] private TextMeshProUGUI deafenStateText;
    [SerializeField] private Sprite micOnSprite;
    [SerializeField] private Sprite micOffSprite;
    [SerializeField] private Sprite deafenOnSprite;
    [SerializeField] private Sprite deafenOffSprite;
    #endregion

    #region Private Fields
    private bool isInitialized = false;
    private Dictionary<ulong, GameObject> playerCardInstances = new Dictionary<ulong, GameObject>();
    private PlayerData localCustomData;

    // Datos estáticos de Panteones
    private readonly string[] classNames = { "Inca", "Shinto", "Greek", "Norse" };
    private readonly string[] deityNames = { "Viracocha", "Izanagi & Izanami", "Zeus", "Odin" };

    // Variables Chat
    [System.Serializable]
    private class PublicChatMessage { public string sender; public string text; public bool isHost; }
    private List<PublicChatMessage> publicChatHistory = new List<PublicChatMessage>();

    private class PrivateEntry { public string playerId; public ulong clientId; public GameObject go; public Image bg; public bool hasUnread; public Color baseColor; public string displayName; }
    private Dictionary<string, PrivateEntry> privateEntries = new Dictionary<string, PrivateEntry>();

    [System.Serializable]
    private class PrivateChatMessage { public string sender; public string text; }
    private Dictionary<string, List<PrivateChatMessage>> privateChatHistory = new Dictionary<string, List<PrivateChatMessage>>();

    private string currentPrivateTargetPlayerId = null;
    private ulong currentPrivateTargetClientId = 0;
    private float _voiceUiRefreshTimer = 0f;
    private const float VOICE_REFRESH_RATE = 0.1f;
    #endregion

    #region Initialization & Lifecycle

    public void Initialize()
    {
        // Permitimos re-inicialización si la escena se recargó pero el objeto persistió (aunque en tu caso el UI se destruye y crea de nuevo)
        isInitialized = false;
        lobbyPanel.SetActive(true);

        // 1. Suscripciones
        if (GameManager.Instance != null)
        {
            // Primero desuscribir para evitar duplicados si algo raro pasa
            GameManager.Instance.PlayersInLobby.OnListChanged -= HandlePlayerListChanged;
            GameManager.Instance.TotalRounds.OnValueChanged -= OnRoundsNetworkValueChanged;
            GameManager.Instance.OnMapIndexChanged -= HandleMapIndexChanged;

            GameManager.Instance.PlayersInLobby.OnListChanged += HandlePlayerListChanged;
            GameManager.Instance.TotalRounds.OnValueChanged += OnRoundsNetworkValueChanged;
            GameManager.Instance.OnMapIndexChanged += HandleMapIndexChanged;
        }

        if (CloudAuthManager.Instance != null) CloudAuthManager.Instance.OnPlayerNameUpdated += HandlePlayerNameUpdated;
        if (VivoxLobbyChatManager.Instance != null)
        {
            VivoxLobbyChatManager.Instance.OnTextMessage += HandleChatMessage;
            VivoxLobbyChatManager.Instance.OnDirectMessage += HandleDirectMessage;
        }

        // 2. Configurar Botones
        SetupButtons();

        // 3. Configurar UI Inicial
        SetupRoundsUI();
        SetupExitButtons();
        SetupMapSelectionUI();

        LoadLocalPlayerData();
        ShowPublicChatPanel();

        // 4. IMPORTANTE: Marcar como inicializado ANTES de intentar refrescar la lista
        isInitialized = true;

        // 5. Refresco inicial de datos del servidor
        if (GameManager.Instance != null)
        {
            UpdateRoundsText(GameManager.Instance.TotalRounds.Value);

            string myName = localCustomData.Username.ToString();
            if (!string.IsNullOrWhiteSpace(myName))
                GameManager.Instance.UpdatePlayerNameServerRpc(myName);

            // CORRECCIÓN PUNTO 3: Iniciamos el refresco agresivo
            StartCoroutine(InitialListRefresh());
        }
    }
    private void SetupButtons()
    {
        startGameButton.onClick.RemoveAllListeners();
        startGameButton.onClick.AddListener(OnStartGameClicked);
        readyButton.onClick.RemoveAllListeners();
        readyButton.onClick.AddListener(OnReadyClicked);

        if (saveNameButton != null)
        {
            saveNameButton.onClick.RemoveAllListeners();
            saveNameButton.onClick.AddListener(OnSaveNameClicked);
        }
        if (nextClassButton != null)
        {
            nextClassButton.onClick.RemoveAllListeners();
            nextClassButton.onClick.AddListener(() => OnChangeClass(1));
        }
        if (prevClassButton != null)
        {
            prevClassButton.onClick.RemoveAllListeners();
            prevClassButton.onClick.AddListener(() => OnChangeClass(-1));
        }

        if (chatInputField != null) chatInputField.onEndEdit.AddListener(OnChatSubmit);
        if (privateChatInputField != null) privateChatInputField.onEndEdit.AddListener(OnPrivateChatSubmit);
        if (micToggleButton) micToggleButton.onClick.AddListener(OnToggleMicClicked);
        if (deafenToggleButton) deafenToggleButton.onClick.AddListener(OnToggleDeafenClicked);
    }
    private IEnumerator InitialListRefresh()
    {
        // Esperamos un frame para asegurar que la escena esté lista
        yield return null;

        // --- LÓGICA DE REINTENTOS AGRESIVA ---
        // Al volver de una partida, la lista ya tiene datos, pero el evento OnListChanged
        // ocurrió antes de que esta escena cargara. Por eso debemos "pollear" (consultar)
        // los datos manualmente varias veces.

        float retryDuration = 2.0f; // Intentar durante 2 segundos
        float interval = 0.2f;      // Cada 0.2 segundos

        while (retryDuration > 0)
        {
            // Forzamos el redibujado
            HandlePlayerListChanged(new NetworkListEvent<PlayerData>());

            // También forzamos actualización de rondas y mapa por si acaso
            if (GameManager.Instance != null)
            {
                UpdateRoundsText(GameManager.Instance.TotalRounds.Value);
                UpdateFirstMapText();
            }

            yield return new WaitForSeconds(interval);
            retryDuration -= interval;
        }
    }
    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.PlayersInLobby != null)
                GameManager.Instance.PlayersInLobby.OnListChanged -= HandlePlayerListChanged;

            GameManager.Instance.TotalRounds.OnValueChanged -= OnRoundsNetworkValueChanged;
            GameManager.Instance.OnMapIndexChanged -= HandleMapIndexChanged;
        }

        if (CloudAuthManager.Instance != null) CloudAuthManager.Instance.OnPlayerNameUpdated -= HandlePlayerNameUpdated;

        if (VivoxLobbyChatManager.Instance != null)
        {
            VivoxLobbyChatManager.Instance.OnTextMessage -= HandleChatMessage;
            VivoxLobbyChatManager.Instance.OnDirectMessage -= HandleDirectMessage;
        }

        foreach (var card in playerCardInstances.Values) Destroy(card);
        playerCardInstances.Clear();
        privateEntries.Clear();

        isInitialized = false;
    }

    private void Update()
    {
        if (privateEntries.Count > 0 && privateChatPanel != null && privateChatPanel.activeSelf)
        {
            float t = Mathf.PingPong(Time.unscaledTime * 3.5f, 1f);
            foreach (var kvp in privateEntries)
            {
                var entry = kvp.Value;
                if (entry.hasUnread && entry.bg != null)
                    entry.bg.color = Color.Lerp(entry.baseColor, privateUnreadColor, t);
            }
        }

        _voiceUiRefreshTimer += Time.unscaledDeltaTime;
        if (_voiceUiRefreshTimer >= VOICE_REFRESH_RATE)
        {
            _voiceUiRefreshTimer = 0f;
            RefreshVoiceUI();
        }
    }
    #endregion

    #region Class Selection Logic (Panteones)

    private void OnChangeClass(int direction)
    {
        if (GameManager.Instance == null) return;

        // Cuántas clases hay (normalmente 4)
        int maxClasses = 4;
        if (previewPlayer != null) maxClasses = previewPlayer.GetPantheonCount();
        if (maxClasses == 0) maxClasses = 4;

        int currentIndex = localCustomData.PantheonIndex;
        int attempts = 0;
        int potentialIndex = currentIndex;

        // Buscamos el siguiente índice que NO esté ocupado por otro jugador
        do
        {
            // Algoritmo circular para sumar o restar
            potentialIndex = (potentialIndex + direction) % maxClasses;
            if (potentialIndex < 0) potentialIndex += maxClasses;

            attempts++;

        } while (IsClassTaken(potentialIndex) && attempts < maxClasses);

        // Si encontramos uno libre (o dimos la vuelta y nos quedamos con el mismo)
        localCustomData.PantheonIndex = potentialIndex;

        // 1. Actualizar visuales locales inmediatamente (Predicción)
        if (previewPlayer != null) previewPlayer.ApplyAppearance(localCustomData);
        UpdateClassAndDeityUI(localCustomData.PantheonIndex);

        // 2. Enviar al servidor para que actualice la NetworkList y avise a los demás
        GameManager.Instance.UpdatePlayerAppearanceServerRpc(localCustomData);

        // 3. Guardar localmente
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.UpdateLocalData(localCustomData);
        }
    }

    private bool IsClassTaken(int indexToCheck)
    {
        if (GameManager.Instance == null) return false;

        ulong myId = NetworkManager.Singleton.LocalClientId;

        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            // Ignorarnos a nosotros mismos
            if (player.ClientId == myId) continue;

            if (player.PantheonIndex == indexToCheck)
            {
                return true; // Está ocupada
            }
        }
        return false;
    }

    private void UpdateClassAndDeityUI(int index)
    {
        if (index >= 0 && index < classNames.Length)
        {
            if (classNameText != null) classNameText.text = classNames[index];
            if (deityNameText != null) deityNameText.text = deityNames[index];
        }
        else
        {
            if (classNameText != null) classNameText.text = "Unknown";
            if (deityNameText != null) deityNameText.text = "---";
        }

        if (pantheonIconImage != null && pantheonSprites != null)
        {
            if (index >= 0 && index < pantheonSprites.Length)
            {
                pantheonIconImage.sprite = pantheonSprites[index];
            }
        }
    }

    #endregion

    #region Game Settings Logic (Rounds & Maps & Exit)

    private void SetupRoundsUI()
    {
        bool isHost = NetworkManager.Singleton.IsHost;

        if (decreaseRoundsButton) decreaseRoundsButton.gameObject.SetActive(isHost);
        if (increaseRoundsButton) increaseRoundsButton.gameObject.SetActive(isHost);
        if (roundsValueText) roundsValueText.gameObject.SetActive(true);

        if (isHost)
        {
            decreaseRoundsButton.onClick.RemoveAllListeners();
            increaseRoundsButton.onClick.RemoveAllListeners();

            decreaseRoundsButton.onClick.AddListener(() => ChangeRoundsAmount(-1));
            increaseRoundsButton.onClick.AddListener(() => ChangeRoundsAmount(1));
        }
    }

    private void ChangeRoundsAmount(int change)
    {
        if (GameManager.Instance == null) return;
        int current = GameManager.Instance.TotalRounds.Value;
        int nextValue = Mathf.Clamp(current + change, 1, 10);

        if (nextValue != current)
            GameManager.Instance.SetTotalRoundsServerRpc(nextValue);
    }

    private void OnRoundsNetworkValueChanged(int previous, int current)
    {
        UpdateRoundsText(current);
    }

    private void UpdateRoundsText(int value)
    {
        if (roundsValueText != null) roundsValueText.text = value.ToString();
    }

    private void SetupExitButtons()
    {
        bool isHost = NetworkManager.Singleton.IsHost;

        if (closeLobbyButton)
        {
            closeLobbyButton.gameObject.SetActive(isHost);
            closeLobbyButton.onClick.RemoveAllListeners();
            closeLobbyButton.onClick.AddListener(OnCloseLobbyClicked);
        }

        if (leaveLobbyButton)
        {
            leaveLobbyButton.gameObject.SetActive(!isHost);
            leaveLobbyButton.onClick.RemoveAllListeners();
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        }
    }

    private void OnCloseLobbyClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.CloseLobbyServerRpc();
    }

    private void OnLeaveLobbyClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.LeaveLobby();
    }

    private void SetupMapSelectionUI()
    {
        bool isHost = NetworkManager.Singleton.IsHost;
        if (changeMapButton != null)
        {
            changeMapButton.gameObject.SetActive(isHost);
            changeMapButton.onClick.RemoveAllListeners();
            if (isHost) changeMapButton.onClick.AddListener(OnChangeMapClicked);
        }
        UpdateFirstMapText();
    }

    private void OnChangeMapClicked()
    {
        if (GameManager.Instance == null) return;
        var maps = GameManager.Instance.AvailableMapNames;
        if (maps == null || maps.Count == 0) return;

        int nextIndex = (GameManager.Instance.CurrentMapIndex + 1) % maps.Count;
        GameManager.Instance.SetStartingMapServerRpc(nextIndex);
    }

    private void HandleMapIndexChanged(int newIndex) => UpdateFirstMapText();

    public void UpdateFirstMapText()
    {
        if (firstMapText == null || GameManager.Instance == null) return;
        var maps = GameManager.Instance.AvailableMapNames;
        if (maps != null && maps.Count > 0)
        {
            int index = GameManager.Instance.CurrentMapIndex;
            if (index >= 0 && index < maps.Count)
                firstMapText.text = $"First Game: {maps[index]}";
        }
    }
    #endregion

    #region Player List Logic

    private void HandlePlayerListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        // Esta comprobación era la que fallaba antes. Ahora isInitialized es true al llegar aquí.
        if (GameManager.Instance == null || !isInitialized) return;

        List<ulong> currentIds = new List<ulong>();
        foreach (var p in GameManager.Instance.PlayersInLobby) currentIds.Add(p.ClientId);

        List<ulong> toRemove = new List<ulong>();
        foreach (var kvp in playerCardInstances)
        {
            if (!currentIds.Contains(kvp.Key))
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var id in toRemove) playerCardInstances.Remove(id);

        int readyCount = 0;
        bool localFound = false;
        ulong myId = NetworkManager.Singleton.LocalClientId;
        PlayerData localData = default;

        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            if (!playerCardInstances.TryGetValue(player.ClientId, out var card))
            {
                card = Instantiate(playerCardPrefab, playerListContent);
                playerCardInstances[player.ClientId] = card;
            }

            UpdatePlayerCard(card, player);

            if (player.IsReady) readyCount++;
            if (player.ClientId == myId)
            {
                localFound = true;
                localData = player;

                // Sincronizar datos locales con el servidor (importante al volver de partida)
                if (localCustomData.PantheonIndex != player.PantheonIndex)
                {
                    localCustomData = player;
                    if (previewPlayer != null) previewPlayer.ApplyAppearance(localCustomData);
                    UpdateClassAndDeityUI(localCustomData.PantheonIndex);
                }
            }
        }

        UpdateLobbyControls(localData, readyCount, localFound);
        if (privateChatPanel != null && privateChatPanel.activeSelf) RefreshPrivatePlayersList();
    }

    private void UpdatePlayerCard(GameObject card, PlayerData player)
    {
        var nameTxt = card.transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        if (nameTxt == null) nameTxt = card.GetComponentInChildren<TextMeshProUGUI>(true);
        if (nameTxt != null) nameTxt.text = SanitizeName(player.Username, player.ClientId);

        var img = card.GetComponent<Image>();
        if (img != null) img.color = player.IsReady ? readyColor : notReadyColor;

        // --- AÑADIDO: Actualizar el icono del panteón ---
        // Buscamos una imagen llamada "PantheonIcon" dentro de la tarjeta
        var pantheonImg = card.transform.Find("PantheonIcon")?.GetComponent<Image>();
        if (pantheonImg != null && pantheonSprites != null)
        {
            if (player.PantheonIndex >= 0 && player.PantheonIndex < pantheonSprites.Length)
            {
                pantheonImg.sprite = pantheonSprites[player.PantheonIndex];
                pantheonImg.gameObject.SetActive(true);
            }
            else
            {
                pantheonImg.gameObject.SetActive(false);
            }
        }
        // ------------------------------------------------

        var hostIcon = card.transform.Find("HostIcon")?.gameObject;
        if (hostIcon != null) hostIcon.SetActive(player.ClientId == NetworkManager.ServerClientId);

        var kickBtn = card.transform.Find("KickButton")?.GetComponent<Button>();
        if (kickBtn != null)
        {
            bool amHost = NetworkManager.Singleton.IsHost;
            bool isTargetHost = (player.ClientId == NetworkManager.ServerClientId);
            kickBtn.gameObject.SetActive(amHost && !isTargetHost);
            kickBtn.onClick.RemoveAllListeners();
            if (amHost && !isTargetHost)
            {
                ulong target = player.ClientId;
                kickBtn.onClick.AddListener(() => OnKickPlayerClicked(target));
            }
        }

        var profileBtn = card.transform.Find("ProfileButton")?.GetComponent<Button>();
        if (profileBtn == null) profileBtn = card.GetComponent<Button>();
        if (profileBtn != null)
        {
            profileBtn.onClick.RemoveAllListeners();
            PlayerData capture = player;
            profileBtn.onClick.AddListener(() => OnViewPlayerProfileClicked(capture));
        }
    }

    private void UpdateLobbyControls(PlayerData localPlayer, int readyCount, bool found)
    {
        if (GameManager.Instance == null) return;
        int total = GameManager.Instance.PlayersInLobby.Count;
        readyCountText.text = $"{readyCount} / {total}";

        if (found) readyButtonText.text = localPlayer.IsReady ? "Not yet" : "Ready";
        else readyButtonText.text = "Ready";

        bool allReady = (readyCount == total && total > 0);
        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        startGameButton.interactable = allReady;
    }

    private void OnKickPlayerClicked(ulong id)
    {
        if (GameManager.Instance != null && NetworkManager.Singleton.IsHost)
            GameManager.Instance.KickPlayerServerRpc(id);
    }

    private void OnViewPlayerProfileClicked(PlayerData player)
    {
        if (playerInfoPopup != null) playerInfoPopup.Show(player);
    }
    #endregion

    #region Local Player Data & Customization

    private void LoadLocalPlayerData()
    {
        if (CloudAuthManager.Instance != null) localCustomData = CloudAuthManager.Instance.LocalPlayerData;
        else localCustomData = new PlayerData(0, "Player");

        UpdatePlayerInfoUI(localCustomData);

        // Si ya existen datos en GameManager (ej. volviendo de partida), usarlos
        if (GameManager.Instance != null)
        {
            foreach (var p in GameManager.Instance.PlayersInLobby)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId)
                {
                    localCustomData.PantheonIndex = p.PantheonIndex;
                    break;
                }
            }
        }

        if (previewPlayer != null) previewPlayer.ApplyAppearance(localCustomData);
        UpdateClassAndDeityUI(localCustomData.PantheonIndex);
    }

    private void UpdatePlayerInfoUI(PlayerData data)
    {
        string nick = SanitizeName(data.Username, data.ClientId);
        playerNameText.text = nick;
        nameChangeInputField.text = nick;
    }

    private string SanitizeName(FixedString64Bytes fs, ulong id)
    {
        string s = fs.ToString().Replace("\0", "");
        return string.IsNullOrWhiteSpace(s) ? $"Player_{id}" : s;
    }

    private void OnStartGameClicked()
    {
        if (GameManager.Instance != null && NetworkManager.Singleton.IsHost)
            GameManager.Instance.StartGameServerRpc();
    }

    private void OnReadyClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.ToggleReadyServerRpc();
    }

    private async void OnSaveNameClicked()
    {
        string newName = nameChangeInputField.text;
        if (string.IsNullOrWhiteSpace(newName) || newName.Length < 3) return;

        saveNameButton.interactable = false;
        await CloudAuthManager.Instance.UpdatePlayerNameAsync(newName);
        if (GameManager.Instance != null) GameManager.Instance.UpdatePlayerNameServerRpc(newName);
        saveNameButton.interactable = true;
    }

    private void HandlePlayerNameUpdated(string newName)
    {
        playerNameText.text = newName;
        localCustomData.Username = new FixedString64Bytes(newName);
        CloudAuthManager.Instance.UpdateLocalData(localCustomData);
    }
    #endregion

    #region Chat System

    private void OnChatSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        SendChat(text);
        chatInputField.text = "";
        chatInputField.ActivateInputField();
    }

    private async void SendChat(string text)
    {
        if (VivoxLobbyChatManager.Instance != null)
            await VivoxLobbyChatManager.Instance.SendTextMessage(text);
        else
            HandleChatMessage("Local", text, NetworkManager.Singleton.IsHost);
    }

    private void HandleChatMessage(string sender, string message, bool isHost)
    {
        publicChatHistory.Add(new PublicChatMessage { sender = sender, text = message, isHost = isHost });
        if (publicChatPanel != null && publicChatPanel.activeSelf)
            AddPublicMessageToUI(sender, message, isHost);
    }

    private void AddPublicMessageToUI(string sender, string message, bool isHost)
    {
        if (chatMessagePrefab == null || chatMessagesContainer == null) return;
        var go = Instantiate(chatMessagePrefab, chatMessagesContainer);
        var txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = isHost ? $"<b>{sender} [HOST]:</b> {message}" : $"<b>{sender}:</b> {message}";

        if (chatScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    public void ShowPublicChatPanel()
    {
        if (publicChatPanel) publicChatPanel.SetActive(true);
        if (privateChatPanel) privateChatPanel.SetActive(false);
        foreach (Transform child in chatMessagesContainer) Destroy(child.gameObject);
        foreach (var m in publicChatHistory) AddPublicMessageToUI(m.sender, m.text, m.isHost);
    }

    public void ShowPrivateChatPanel()
    {
        if (publicChatPanel) publicChatPanel.SetActive(false);
        if (privateChatPanel) privateChatPanel.SetActive(true);
        RefreshPrivatePlayersList();
    }

    private void RefreshPrivatePlayersList() { StartCoroutine(DoRefreshPrivatePlayersList()); }

    private IEnumerator DoRefreshPrivatePlayersList()
    {
        if (privatePlayersContainer == null) yield break;
        foreach (Transform child in privatePlayersContainer) Destroy(child.gameObject);
        privateEntries.Clear();

        var relay = FindObjectOfType<RelayLobbyConnector>();
        Lobby lobby = null;

        if (relay != null && relay.CurrentLobby != null)
        {
            var task = LobbyService.Instance.GetLobbyAsync(relay.CurrentLobby.Id);
            yield return new WaitUntil(() => task.IsCompleted);
            lobby = !task.IsFaulted ? task.Result : relay.CurrentLobby;
        }

        string myPid = CloudAuthManager.Instance != null ? CloudAuthManager.Instance.GetPlayerId() : AuthenticationService.Instance.PlayerId;

        if (lobby != null && lobby.Players != null)
        {
            foreach (var p in lobby.Players)
            {
                if (p.Id == myPid) continue;
                CreatePrivateButton(p.Id, 0, GetLobbyPlayerDisplayName(p));
            }
        }
        else if (GameManager.Instance != null)
        {
            foreach (var p in GameManager.Instance.PlayersInLobby)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) continue;
                CreatePrivateButton(null, p.ClientId, SanitizeName(p.Username, p.ClientId));
            }
        }
    }

    private void CreatePrivateButton(string pid, ulong cid, string displayName)
    {
        if (privatePlayerButtonPrefab == null) return;
        var go = Instantiate(privatePlayerButtonPrefab, privatePlayersContainer);
        var txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = displayName;

        var entry = new PrivateEntry
        {
            playerId = pid,
            clientId = cid,
            go = go,
            bg = go.GetComponent<Image>(),
            hasUnread = false,
            baseColor = go.GetComponent<Image>().color,
            displayName = displayName
        };

        string key = pid ?? cid.ToString();
        privateEntries[key] = entry;

        go.GetComponent<Button>().onClick.AddListener(() => OpenPrivateChannel(pid, cid));
    }

    private string GetLobbyPlayerDisplayName(Unity.Services.Lobbies.Models.Player p)
    {
        if (p.Data != null && p.Data.TryGetValue("PlayerName", out var d)) return d.Value;
        return p.Id;
    }

    private void OpenPrivateChannel(string pid, ulong cid)
    {
        currentPrivateTargetPlayerId = pid;
        currentPrivateTargetClientId = cid;

        string key = pid ?? cid.ToString();
        if (privateEntries.TryGetValue(key, out var e))
        {
            e.hasUnread = false;
            if (e.bg != null) e.bg.color = e.baseColor;
        }
        RenderPrivateConversation();
    }

    private void RenderPrivateConversation()
    {
        if (privateMessagesContainer == null) return;
        foreach (Transform child in privateMessagesContainer) Destroy(child.gameObject);

        string key = GetConversationKey(currentPrivateTargetPlayerId, currentPrivateTargetClientId);
        if (key != null && privateChatHistory.TryGetValue(key, out var list))
        {
            foreach (var m in list) AddPrivateMessageToUI(m.sender, m.text);
        }
    }

    private void OnPrivateChatSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        SendPrivateChat(text);
        privateChatInputField.text = "";
        privateChatInputField.ActivateInputField();
    }

    private async void SendPrivateChat(string text)
    {
        string key = GetConversationKey(currentPrivateTargetPlayerId, currentPrivateTargetClientId);
        if (key != null)
        {
            if (!privateChatHistory.ContainsKey(key)) privateChatHistory[key] = new List<PrivateChatMessage>();
            privateChatHistory[key].Add(new PrivateChatMessage { sender = "Me", text = text });
            AddPrivateMessageToUI("Me", text);
        }

        if (!string.IsNullOrEmpty(currentPrivateTargetPlayerId) && VivoxLobbyChatManager.Instance != null)
            await VivoxLobbyChatManager.Instance.SendDirectMessage(currentPrivateTargetPlayerId, text);
    }

    private void HandleDirectMessage(string senderName, string senderId, string message)
    {
        string key = GetConversationKey(senderId, 0);
        if (key != null)
        {
            if (!privateChatHistory.ContainsKey(key)) privateChatHistory[key] = new List<PrivateChatMessage>();
            privateChatHistory[key].Add(new PrivateChatMessage { sender = senderName, text = message });
        }

        if (currentPrivateTargetPlayerId == senderId)
            AddPrivateMessageToUI(senderName, message);
        else if (privateEntries.TryGetValue(senderId, out var e))
            e.hasUnread = true;
    }

    private void AddPrivateMessageToUI(string sender, string msg)
    {
        var go = Instantiate(privateMessagePrefab, privateMessagesContainer);
        go.GetComponentInChildren<TextMeshProUGUI>().text = $"<b>{sender}:</b> {msg}";
    }

    private string GetConversationKey(string pid, ulong cid)
    {
        if (!string.IsNullOrEmpty(pid)) return "pid:" + pid;
        if (cid != 0) return "cid:" + cid;
        return null;
    }
    #endregion

    #region Voice Logic
    private void OnToggleMicClicked() { if (VivoxLobbyChatManager.Instance) { VivoxLobbyChatManager.Instance.ToggleMicMute(); } }
    private void OnToggleDeafenClicked() { if (VivoxLobbyChatManager.Instance) { VivoxLobbyChatManager.Instance.ToggleDeafen(); } }

    private void RefreshVoiceUI()
    {
        var v = VivoxLobbyChatManager.Instance;

        // 1. Verificar si el Manager existe y si estamos Logueados en Vivox
        bool isReady = v != null && v.IsLoggedIn;

        // 2. Controlar interactividad de los botones
        if (micToggleButton) micToggleButton.interactable = isReady;
        if (deafenToggleButton) deafenToggleButton.interactable = isReady;

        if (!isReady)
        {
            if (micStateText) micStateText.text = "Mic: ...";
            if (deafenStateText) deafenStateText.text = "Audio: ...";

            // Opcional: Poner iconos en gris o estado default
            return;
        }

        // 3. Obtener estados reales
        bool muted = v.IsMicMuted;
        bool deaf = v.IsDeafened;

        if (micStateText) micStateText.text = muted ? "Mic: Muted" : "Mic: ON";
        if (deafenStateText) deafenStateText.text = deaf ? "Audio: Deaf" : "Audio: ON";

        UpdateVoiceIcon(micToggleButton, muted ? micOffSprite : micOnSprite);
        UpdateVoiceIcon(deafenToggleButton, deaf ? deafenOnSprite : deafenOffSprite);
    }

    private void UpdateVoiceIcon(Button btn, Sprite sprite)
    {
        if (btn == null || sprite == null) return;
        var icon = btn.transform.Find("Icon")?.GetComponent<Image>();
        if (icon == null) icon = btn.GetComponentInChildren<Image>();
        if (icon != null && icon.gameObject != btn.gameObject) icon.sprite = sprite;
    }
    #endregion
}