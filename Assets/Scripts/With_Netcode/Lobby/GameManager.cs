using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    #region Configuration & Settings

    [Header("Prefabs & Visuals")]
    [Tooltip("Orden debe coincidir: 0=Inka, 1=Sinto, 2=Griego, 3=Nórdico")]
    [SerializeField] private NetworkObject[] pantheonPrefabs;

    [Header("Scenes Configuration")]
    [SerializeField] private string lobbySceneName = "Login";
    [SerializeField] private string intermissionSceneName = "Intermission";
    [SerializeField] private List<string> availableMapNames = new List<string> { "CrownCatching" };

    [Header("Game Constraints")]
    private const int MaxPlayers = 5;

    #endregion

    #region Network State

    // --- Configuración de Partida ---
    public NetworkVariable<int> TotalRounds = new NetworkVariable<int>(3);
    public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1);
    public NetworkVariable<int> currentMapIndex = new NetworkVariable<int>(0);

    // --- Estado de Jugadores ---
    public NetworkList<PlayerData> PlayersInLobby = new NetworkList<PlayerData>();

    #endregion

    #region Internal State

    private CinemachineImpulseSource impulseSource;
    private Dictionary<ulong, bool> clientLoadedLobbyUI = new Dictionary<ulong, bool>();
    private string currentLobbyId;
    private Coroutine lobbyHeartbeatCoroutine;

    // Propiedades públicas de solo lectura
    public List<string> AvailableMapNames => availableMapNames;
    public int CurrentMapIndex => currentMapIndex.Value;

    // Eventos
    public event Action<int> OnMapIndexChanged;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (PlayersInLobby != null) PlayersInLobby.Clear();
        if (clientLoadedLobbyUI != null) clientLoadedLobbyUI.Clear();
    }

    #endregion

    #region Network Lifecycle & Events

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnClientSceneLoaded;
        }

        currentMapIndex.OnValueChanged += (prev, cur) => { OnMapIndexChanged?.Invoke(cur); };
        SceneManager.sceneLoaded += OnLocalSceneLoaded;
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnLocalSceneLoaded;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnClientSceneLoaded;
        }
        base.OnNetworkDespawn();
    }

    private void OnLocalSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Fade In visual
        if (SceneTransitionManager.Instance != null)
            StartCoroutine(SceneTransitionManager.Instance.FadeInRoutine());

        // Asegurar cursor libre en Lobby
        if (scene.name == lobbySceneName)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;

        if (clientLoadedLobbyUI.ContainsKey(clientId))
            clientLoadedLobbyUI.Remove(clientId);

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                Debug.Log($"Servidor: Eliminando jugador {PlayersInLobby[i].Username} (ID: {clientId})");
                PlayersInLobby.RemoveAt(i);
                break;
            }
        }
    }

    #endregion

    #region Lobby Management (Heartbeat, Close, Leave)

    public void StartLobbyHeartbeat(string lobbyId)
    {
        currentLobbyId = lobbyId;
        if (lobbyHeartbeatCoroutine != null) StopCoroutine(lobbyHeartbeatCoroutine);
        lobbyHeartbeatCoroutine = StartCoroutine(HeartbeatLobbyRoutine());
        Debug.Log($"GameManager: Heartbeat iniciado para Lobby {lobbyId}");
    }

    private IEnumerator HeartbeatLobbyRoutine()
    {
        var wait = new WaitForSeconds(15f);
        while (!string.IsNullOrEmpty(currentLobbyId))
        {
            LobbyService.Instance.SendHeartbeatPingAsync(currentLobbyId);
            yield return wait;
        }
    }

    // --- SALIDA ROBUSTA (Evitar Lobbies Fantasmas) ---

    // 1. Cliente abandona voluntariamente
    public async void LeaveLobby()
    {
        if (!string.IsNullOrEmpty(currentLobbyId))
        {
            try
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                await LobbyService.Instance.RemovePlayerAsync(currentLobbyId, playerId);
            }
            catch (Exception e) { Debug.LogWarning($"Error saliendo del lobby cloud: {e.Message}"); }
        }

        CleanupAndLoadMenu();
    }

    // 2. Host cierra la sala para todos
    [Rpc(SendTo.Server)]
    public void CloseLobbyServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        StartCoroutine(CloseLobbyRoutine());
    }

    private IEnumerator CloseLobbyRoutine()
    {
        // A. Eliminar de la nube
        if (!string.IsNullOrEmpty(currentLobbyId))
        {
            var deleteTask = LobbyService.Instance.DeleteLobbyAsync(currentLobbyId);
            yield return new WaitUntil(() => deleteTask.IsCompleted);
            currentLobbyId = null;
        }

        // B. Echar a clientes (RPC)
        KickAllClientsClientRpc();
        yield return new WaitForSeconds(0.5f);

        // C. Apagar servidor local
        CleanupAndLoadMenu();
    }

    [ClientRpc]
    private void KickAllClientsClientRpc()
    {
        if (IsServer) return; // El host ya se encarga en su rutina
        CleanupAndLoadMenu();
    }

    private void CleanupAndLoadMenu()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("IntroLogo"); // O tu escena de Login
        if (Instance != null) Destroy(Instance.gameObject);
    }

    #endregion

    #region Authentication & Player Registration

    [Rpc(SendTo.Server)]
    public void OnPlayerAuthenticatedServerRpc(PlayerData playerData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        string name = playerData.Username.ToString().Replace("\0", string.Empty);
        if (string.IsNullOrWhiteSpace(name)) name = $"Player_{clientId}";
        playerData.Username = new FixedString64Bytes(name);

        // --- CORRECCIÓN PUNTOS: Resetear a 0 al entrar al Lobby ---
        // Esto ignora cualquier dato "acumulado" que venga de la nube para esta sesión.
        playerData.Points = 0;
        playerData.LastAddedPoints = 0; // Resetear también aquí
        // -----------------------------------------------------------

        bool isAlreadyConnected = false;
        foreach (var p in PlayersInLobby)
        {
            if (p.ClientId == clientId) { isAlreadyConnected = true; break; }
        }

        if (!isAlreadyConnected)
        {
            if (PlayersInLobby.Count >= MaxPlayers)
            {
                Debug.LogWarning("Lobby lleno, no se puede unir.");
                return;
            }

            playerData.ClientId = clientId;
            playerData.IsReady = false;

            // Asignación automática de Clase
            List<int> takenIndices = new List<int>();
            foreach (var p in PlayersInLobby) takenIndices.Add(p.PantheonIndex);

            int assignedIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!takenIndices.Contains(i)) { assignedIndex = i; break; }
            }
            playerData.PantheonIndex = assignedIndex;

            PlayersInLobby.Add(playerData);
            clientLoadedLobbyUI[clientId] = false;
        }

        ClientRpcParams clientParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
        LoginSuccessClientRpc(clientParams);
    }

    [ClientRpc]
    public void LoginSuccessClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (UiGameManager.Instance != null)
            UiGameManager.Instance.GoToLobby();
        else
            Debug.LogError("UiGameManager.Instance es nulo en el cliente.");

        ConfirmLobbyUILoadedServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void ConfirmLobbyUILoadedServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (clientLoadedLobbyUI.ContainsKey(clientId))
            clientLoadedLobbyUI[clientId] = true;
    }

    #endregion

    #region Game Flow Control (Rounds, Maps, Start)

    // --- Configuración Pre-Juego ---

    [Rpc(SendTo.Server)]
    public void SetTotalRoundsServerRpc(int rounds)
    {
        TotalRounds.Value = Mathf.Clamp(rounds, 1, 10);
    }

    [Rpc(SendTo.Server)]
    public void SetStartingMapServerRpc(int index, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (availableMapNames != null && index >= 0 && index < availableMapNames.Count)
            currentMapIndex.Value = index;
    }

    [Rpc(SendTo.Server)]
    public void ToggleReadyServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                var p = PlayersInLobby[i];
                p.IsReady = !p.IsReady;
                PlayersInLobby[i] = p;
                break;
            }
        }
    }

    // --- Inicio de Juego ---

    [Rpc(SendTo.Server)]
    public void StartGameServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        if (!AllPlayersReady()) return;

        CurrentRound.Value = 1; // Reset rondas
        ResetRoundScores();
        StartCoroutine(StartGameSequence());
    }

    private IEnumerator StartGameSequence()
    {
        TriggerFadeOutClientRpc();
        yield return new WaitForSeconds(0.6f);

        string sceneToLoad = "Game";
        if (availableMapNames != null && availableMapNames.Count > 0)
        {
            int idx = currentMapIndex.Value;
            if (idx >= 0 && idx < availableMapNames.Count) sceneToLoad = availableMapNames[idx];
        }

        NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }

    // --- Avance de Rondas y Fin de Partida ---

    public void AdvanceRoundOrFinish()
    {
        if (!IsServer) return;

        if (CurrentRound.Value < TotalRounds.Value)
        {
            // Siguiente Ronda
            CurrentRound.Value++;
            SelectRandomNextMap();
            ResetRoundScores();
            StartCoroutine(LoadNextMapSequence());
        }
        else
        {
            // Fin de Partida -> Volver al Lobby
            StartCoroutine(ReturnToLobbySessionSequence());
        }
    }
    private void ResetRoundScores()
    {
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            var p = PlayersInLobby[i];
            p.LastAddedPoints = 0;
            PlayersInLobby[i] = p;
        }
    }
    public void SelectRandomNextMap()
    {
        if (!IsServer) return;
        if (availableMapNames == null || availableMapNames.Count <= 1)
        {
            currentMapIndex.Value = 0;
            return;
        }

        int newIndex;
        int attempts = 0;
        do
        {
            newIndex = UnityEngine.Random.Range(0, availableMapNames.Count);
            attempts++;
        } while (newIndex == currentMapIndex.Value && attempts < 10);

        currentMapIndex.Value = newIndex;
    }

    private IEnumerator LoadNextMapSequence()
    {
        TriggerFadeOutClientRpc();
        yield return new WaitForSeconds(0.6f);
        string nextScene = availableMapNames[currentMapIndex.Value];
        NetworkManager.Singleton.SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
    }

    private IEnumerator ReturnToLobbySessionSequence()
    {
        TriggerFadeOutClientRpc();
        yield return new WaitForSeconds(0.6f);

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            var p = PlayersInLobby[i];
            p.IsReady = false;
            // Aquí NO reseteamos puntos porque queremos ver quién ganó al final
            // Si quieres reiniciar para la siguiente partida, se hace al volver a entrar
            // o al empezar StartGameServerRpc si prefieres.
            PlayersInLobby[i] = p;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    // --- Cargar Intermission (desde Minijuegos) ---
    public void LoadIntermission()
    {
        if (!IsServer) return;
        StartCoroutine(LoadIntermissionSequence());
    }

    private IEnumerator LoadIntermissionSequence()
    {
        TriggerFadeOutClientRpc();
        yield return new WaitForSeconds(0.6f);
        NetworkManager.Singleton.SceneManager.LoadScene(intermissionSceneName, LoadSceneMode.Single);
    }

    private bool AllPlayersReady()
    {
        if (PlayersInLobby.Count == 0) return false;
        foreach (var p in PlayersInLobby)
        {
            if (!p.IsReady) return false;
        }
        return true;
    }

    #endregion

    #region Scene Loading & Spawning

    private void OnClientSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        // Verificar si la escena cargada es un mapa jugable
        bool isMapScene = availableMapNames != null && availableMapNames.Contains(sceneName);
        // (Podrías agregar más lógica aquí si tienes escenas que no son mapas pero cargan jugadores)

        if (!isMapScene) return;

        // Esperar a que todos carguen
        var allIds = NetworkManager.Singleton.ConnectedClientsIds.ToList();
        if (NetworkManager.Singleton.IsHost)
            allIds.Remove(NetworkManager.Singleton.LocalClientId);

        bool allLoaded = true;
        foreach (var id in allIds)
        {
            if (!clientsCompleted.Contains(id)) { allLoaded = false; break; }
        }
        if (NetworkManager.Singleton.IsHost && !clientsCompleted.Contains(NetworkManager.Singleton.LocalClientId))
            allLoaded = false;

        if (allLoaded)
        {
            Debug.Log("Servidor: Todos cargaron el mapa. Spawneando jugadores.");
            for (int i = 0; i < PlayersInLobby.Count; i++)
            {
                SpawnPlayerForClient(PlayersInLobby[i], i);
            }
        }
    }

    private void SpawnPlayerForClient(PlayerData playerData, int spawnIndex)
    {
        MapSettings mapSettings = FindObjectOfType<MapSettings>();
        Vector3 spawnPos = (mapSettings != null) ? mapSettings.GetSpawnPoint(spawnIndex).position : new Vector3(0, 2, 0);
        Quaternion spawnRot = (mapSettings != null) ? mapSettings.GetSpawnPoint(spawnIndex).rotation : Quaternion.identity;

        int pIndex = playerData.PantheonIndex;
        if (pantheonPrefabs == null || pantheonPrefabs.Length == 0) return;
        if (pIndex < 0 || pIndex >= pantheonPrefabs.Length) pIndex = 0;

        NetworkObject prefabToSpawn = pantheonPrefabs[pIndex];
        NetworkObject playerInstance = Instantiate(prefabToSpawn, spawnPos, spawnRot);
        playerInstance.SpawnAsPlayerObject(playerData.ClientId, true);

        // Configurar Nickname UI
        PlayerNicknameUI nicknameUI = playerInstance.GetComponentInChildren<PlayerNicknameUI>();
        if (nicknameUI != null)
        {
            nicknameUI.Nickname.Value = playerData.Username;
            // No pasamos los puntos aquí para que no se muestren, 
            // aunque el script de UI ya lo ignorará.
        }
    }

    #endregion

    #region Player Data Updates (RPCs)

    [Rpc(SendTo.Server)]
    public void UpdatePlayerAppearanceServerRpc(PlayerData customData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int requestedIndex = customData.PantheonIndex;

        // Verificar disponibilidad
        bool isTaken = false;
        foreach (var p in PlayersInLobby)
        {
            if (p.ClientId != clientId && p.PantheonIndex == requestedIndex)
            {
                isTaken = true; break;
            }
        }
        if (isTaken) return;

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                var p = PlayersInLobby[i];
                p.PantheonIndex = requestedIndex;
                PlayersInLobby[i] = p;
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdatePlayerNameServerRpc(string newName, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                var p = PlayersInLobby[i];
                p.Username = new FixedString64Bytes(newName);
                PlayersInLobby[i] = p;
                break;
            }
        }
        // Actualizar objeto en vivo si existe
        try
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
            {
                var ui = client.PlayerObject.GetComponentInChildren<PlayerNicknameUI>();
                if (ui) ui.Nickname.Value = new FixedString64Bytes(newName);
            }
        }
        catch { }
    }

    [Rpc(SendTo.Server)]
    public void UpdatePlayerProfileDataServerRpc(string description, string birthDate, string status, string profileImageKey, string profileImageBase64, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                var p = PlayersInLobby[i];
                if (description != null) p.Description = new FixedString512Bytes(description);
                if (birthDate != null) p.BirthDate = new FixedString32Bytes(birthDate);
                if (status != null) p.Status = new FixedString128Bytes(status);
                if (profileImageKey != null) p.ProfileImageKey = new FixedString64Bytes(profileImageKey);

                if (profileImageBase64 != null) p.ProfileImageBase64 = new FixedString4096Bytes(profileImageBase64);
                else p.ProfileImageBase64 = new FixedString4096Bytes("");

                p.IsAnonymous = false;
                PlayersInLobby[i] = p;
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void KickPlayerServerRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        if (targetClientId == NetworkManager.ServerClientId) return;

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == targetClientId)
            {
                PlayersInLobby.RemoveAt(i);
                break;
            }
        }
        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId))
            NetworkManager.Singleton.DisconnectClient(targetClientId);
    }

    #endregion

    #region Points & Progress Logic

    [Rpc(SendTo.Server)]
    public void AddPointsToPlayerServerRpc(int amount, RpcParams rpcParams = default)
    {
        AddPointsToPlayerById(rpcParams.Receive.SenderClientId, amount);
    }

    public void AddPointsToPlayerById(ulong clientId, int amount)
    {
        if (!IsServer) return;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                var p = PlayersInLobby[i];
                p.Points += amount;
                p.LastAddedPoints = amount;
                PlayersInLobby[i] = p;
                SavePlayerProgress(p, clientId);
                break;
            }
        }
    }

    private void SavePlayerProgress(PlayerData data, ulong clientId)
    {
        ClientRpcParams p = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
        RequestClientSaveProgressClientRpc(data, p);
    }

    [ClientRpc]
    private void RequestClientSaveProgressClientRpc(PlayerData data, ClientRpcParams clientRpcParams)
    {
        Debug.Log($"Cliente: Guardando progreso. Puntos: {data.Points}");
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.UpdateLocalData(data);
            _ = CloudAuthManager.Instance.SavePlayerProgress();
        }
    }

    #endregion

    #region Utils & Visuals

    public void TriggerCameraShake()
    {
        if (impulseSource != null) impulseSource.GenerateImpulse();
    }

    [ClientRpc]
    private void NotifyClientOfFailureClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (UiGameManager.Instance != null) UiGameManager.Instance.ShowError(message);
    }

    [ClientRpc]
    private void TriggerFadeOutClientRpc()
    {
        if (SceneTransitionManager.Instance != null)
            StartCoroutine(SceneTransitionManager.Instance.FadeOutRoutine());
    }

    #endregion
}