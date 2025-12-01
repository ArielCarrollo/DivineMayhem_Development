using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Cinemachine;
using System.Threading.Tasks;
using System.Linq;
using System;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs & Scenes")]
    [SerializeField] private Transform playerPrefab;
    private const string GameSceneName = "Game";

    [Header("Game Settings")]
    private const int MaxPlayers = 5;

    public NetworkList<PlayerData> PlayersInLobby = new NetworkList<PlayerData>();

    private CinemachineImpulseSource impulseSource;

    [Header("Point & Map System")]
    // Lista de nombres de escenas que representan los mapas disponibles. Se puede configurar desde el Inspector.
    [SerializeField] private List<string> availableMapNames = new List<string> { "Game" };
    // Variable de red para replicar el índice del mapa inicial seleccionado por el host.
    public NetworkVariable<int> currentMapIndex = new NetworkVariable<int>(0);

    [SerializeField] private int baseXpToLevelUp = 100; // obsoleto
    [SerializeField] private float xpMultiplierPerLevel = 1.2f; // obsoleto

    private Dictionary<ulong, bool> clientLoadedLobbyUI = new Dictionary<ulong, bool>();

    // Evento que se dispara cuando cambia el índice del mapa inicial.
    public event Action<int> OnMapIndexChanged;

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
        // Muy importante: soltar la referencia estática
        if (Instance == this)
        {
            Instance = null;
        }

        // Por si quedó algo en memoria
        if (PlayersInLobby != null)
            PlayersInLobby.Clear();

        if (clientLoadedLobbyUI != null)
            clientLoadedLobbyUI.Clear();
    }

    // En GameManager.cs

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            currentMapIndex.OnValueChanged += (prev, cur) => { OnMapIndexChanged?.Invoke(cur); };
            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        currentMapIndex.OnValueChanged += (prev, cur) => { OnMapIndexChanged?.Invoke(cur); };

        // AGREGAR ESTO: Suscribirse permanentemente para detectar CUALQUIER cambio de escena
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnClientSceneLoaded;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;

        if (clientLoadedLobbyUI.ContainsKey(clientId))
        {
            clientLoadedLobbyUI.Remove(clientId);
        }

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                Debug.Log($"Servidor: Eliminando jugador {PlayersInLobby[i].Username.ToString()} (ID: {clientId}) de la lista.");
                PlayersInLobby.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// Lista de nombres de mapas disponibles. Expone el campo serializado.
    /// </summary>
    public List<string> AvailableMapNames => availableMapNames;

    /// <summary>
    /// Devuelve el índice del mapa actualmente seleccionado.
    /// </summary>
    public int CurrentMapIndex => currentMapIndex.Value;

    // --- Lógica de Autenticación y Carga de Lobby ---

    [Rpc(SendTo.Server)]
    public void OnPlayerAuthenticatedServerRpc(PlayerData playerData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // 👇 SANITIZAR AQUÍ TAMBIÉN
        string name = playerData.Username.ToString();
        if (!string.IsNullOrEmpty(name))
            name = name.Replace("\0", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            name = $"Player_{clientId}";
        playerData.Username = new Unity.Collections.FixedString64Bytes(name);

        bool isAlreadyConnected = false;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                isAlreadyConnected = true;
                break;
            }
        }

        if (isAlreadyConnected)
        {
            Debug.LogWarning($"El cliente {clientId} ya está en la lista. Sincronizando UI.");
        }
        else
        {
            if (PlayersInLobby.Count >= 5)
            {
                Debug.LogWarning($"El cliente {clientId} intentó unirse pero el lobby está lleno.");
                return;
            }

            playerData.ClientId = clientId;
            playerData.IsReady = false;
            PlayersInLobby.Add(playerData);
            clientLoadedLobbyUI[clientId] = false;
            Debug.Log($"Servidor: Jugador {playerData.Username.ToString()} (ID: {clientId}) añadido a la lista.");
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
        LoginSuccessClientRpc(clientRpcParams);
    }


    [ClientRpc]
    public void LoginSuccessClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Cliente: Recibida señal LoginSuccessClientRpc. Cargando UI de Lobby...");
        if (UiGameManager.Instance != null)
        {
            UiGameManager.Instance.GoToLobby();
        }
        else
        {
            Debug.LogError("Error: UiGameManager.Instance es nulo en el cliente.");
        }

        ConfirmLobbyUILoadedServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void ConfirmLobbyUILoadedServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (clientLoadedLobbyUI.ContainsKey(clientId))
        {
            clientLoadedLobbyUI[clientId] = true;
            Debug.Log($"Servidor: Cliente {clientId} ha confirmado la carga de la UI del Lobby.");
        }

        ForceSyncPlayerToClient(clientId);
    }

    private void ForceSyncPlayerToClient(ulong targetClientId)
    {
        PlayerData playerData = new PlayerData();
        bool found = false;
        foreach (var p in PlayersInLobby)
        {
            if (p.ClientId == targetClientId)
            {
                playerData = p;
                found = true;
                break;
            }
        }

        if (found)
        {

        }
    }

    /// <summary>
    /// Permite que el host establezca el índice del mapa inicial. Este RPC debe ser llamado sólo por el host.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void SetStartingMapServerRpc(int index, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        // Validamos que el índice esté dentro de la lista de mapas disponibles
        if (availableMapNames == null || availableMapNames.Count == 0) return;
        if (index < 0 || index >= availableMapNames.Count) return;
        currentMapIndex.Value = index;
    }

    /// <summary>
    /// Selecciona un mapa aleatorio distinto al actual y lo asigna como siguiente mapa.
    /// </summary>
    public void SelectRandomNextMap()
    {
        if (!IsServer) return;
        if (availableMapNames == null || availableMapNames.Count == 0) return;
        int count = availableMapNames.Count;
        if (count == 1)
        {
            return; // no hay alternancia
        }
        int newIndex;
        do
        {
            newIndex = UnityEngine.Random.Range(0, count);
        } while (newIndex == currentMapIndex.Value);
        currentMapIndex.Value = newIndex;
    }


    // --- Lógica de la Sala de Espera (Ready, Customization) ---

    [Rpc(SendTo.Server)]
    public void ToggleReadyServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.IsReady = !updatedPlayer.IsReady;
                PlayersInLobby[i] = updatedPlayer;
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdatePlayerAppearanceServerRpc(PlayerData customData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.BodyIndex = customData.BodyIndex;
                updatedPlayer.EyesIndex = customData.EyesIndex;
                updatedPlayer.GlovesIndex = customData.GlovesIndex;
                PlayersInLobby[i] = updatedPlayer;
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
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.Username = new FixedString64Bytes(newName);
                PlayersInLobby[i] = updatedPlayer;
                break;
            }
        }

        // Actualizar también el Nickname NetworkVariable en el objeto del jugador para que la UI se sincronice.
        try
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
            {
                var nicknameUI = client.PlayerObject.GetComponentInChildren<PlayerNicknameUI>();
                if (nicknameUI != null)
                {
                    nicknameUI.Nickname.Value = new FixedString64Bytes(newName);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al actualizar Nickname NetworkVariable: {ex}");
        }
    }

    /// <summary>
    /// Actualiza en el servidor los datos de perfil (descripción, fecha, estado e imagen predefinida) para el jugador que realiza la llamada.
    /// Esto hará que se sincronicen con todos los clientes a través de la NetworkList.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void UpdatePlayerProfileDataServerRpc(string description, string birthDate, string status, string profileImageKey, string profileImageBase64, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                if (description != null)
                    updatedPlayer.Description = new FixedString512Bytes(description);
                if (birthDate != null)
                    updatedPlayer.BirthDate = new FixedString32Bytes(birthDate);
                if (status != null)
                    updatedPlayer.Status = new FixedString128Bytes(status);
                if (profileImageKey != null)
                    updatedPlayer.ProfileImageKey = new FixedString64Bytes(profileImageKey);
                // Actualizar la imagen de perfil personalizada en base64
                if (profileImageBase64 != null)
                    updatedPlayer.ProfileImageBase64 = new FixedString4096Bytes(profileImageBase64);
                else
                    updatedPlayer.ProfileImageBase64 = new FixedString4096Bytes("");
                // Al editar perfil asumimos que ya no es anónimo
                updatedPlayer.IsAnonymous = false;
                PlayersInLobby[i] = updatedPlayer;
                break;
            }
        }
    }

    // --- Expulsar jugador (solo host) ---
    [Rpc(SendTo.Server)]
    public void KickPlayerServerRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            return;

        if (targetClientId == NetworkManager.ServerClientId)
            return;

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == targetClientId)
            {
                Debug.Log($"Servidor: expulsando al jugador {PlayersInLobby[i].Username.ToString()} ({targetClientId})");
                PlayersInLobby.RemoveAt(i);
                break;
            }
        }

        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId))
        {
            NetworkManager.Singleton.DisconnectClient(targetClientId);
        }
    }

    [Rpc(SendTo.Server)]
    public void CloseLobbyServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            return;

        var connectedIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        foreach (var id in connectedIds)
        {
            if (id == NetworkManager.ServerClientId)
                continue;

            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(id))
            {
                NetworkManager.Singleton.DisconnectClient(id);
            }
        }

        PlayersInLobby.Clear();

        CloseLobbyOnClientRpc();

        NetworkManager.Singleton.Shutdown();
        Destroy(gameObject);
    }

    [ClientRpc]
    private void CloseLobbyOnClientRpc(ClientRpcParams clientRpcParams = default)
    {
        var relay = FindObjectOfType<RelayLobbyConnector>();
        if (relay != null)
        {
            // vuelve al panel de selección
            relay.ClearCurrentLobby();
            relay.ShowJoiningPanel();
        }

        if (UiGameManager.Instance != null)
        {
            //UiGameManager.Instance.ShowLobbySelection();
        }
    }

    // --- Lógica de Inicio de Juego ---

    [Rpc(SendTo.Server)]
    public void StartGameServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        if (!AllPlayersReady()) return;

        // Utilizamos la escena correspondiente al índice de mapa actual
        string sceneToLoad = GameSceneName;
        if (availableMapNames != null && availableMapNames.Count > 0)
        {
            int idx = currentMapIndex.Value;
            if (idx >= 0 && idx < availableMapNames.Count)
            {
                sceneToLoad = availableMapNames[idx];
            }
        }
        NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }

    private bool AllPlayersReady()
    {
        if (PlayersInLobby.Count == 0) return false;
        foreach (var player in PlayersInLobby)
        {
            if (!player.IsReady) return false;
        }
        return true;
    }

        private void OnClientSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            // Comprobamos si la escena cargada es la principal o una de las escenas definidas en availableMapNames
            bool isMainGameScene = sceneName == GameSceneName;
            bool isDefinedMiniGameScene = false;
            if (availableMapNames != null && availableMapNames.Count > 0)
            {
                isDefinedMiniGameScene = availableMapNames.Contains(sceneName);
            }
            if (!isMainGameScene && !isDefinedMiniGameScene) return;

        var allConnectedClientIds = NetworkManager.Singleton.ConnectedClientsIds.ToList();

        if (NetworkManager.Singleton.IsHost)
        {
            allConnectedClientIds.Remove(NetworkManager.Singleton.LocalClientId);
        }

        bool allClientsLoaded = true;
        foreach (var clientId in allConnectedClientIds)
        {
            if (!clientsCompleted.Contains(clientId))
            {
                allClientsLoaded = false;
                break;
            }
        }

        if (NetworkManager.Singleton.IsHost && !clientsCompleted.Contains(NetworkManager.Singleton.LocalClientId))
        {
            allClientsLoaded = false;
        }


        if (allClientsLoaded)
        {
            Debug.Log("Servidor: Todos los clientes han cargado la escena 'Game'. Spawneando jugadores...");
            for (int i = 0; i < PlayersInLobby.Count; i++)
            {
                // Pasamos el índice 'i' a la función de spawn
                SpawnPlayerForClient(PlayersInLobby[i], i);
            }
        }
    }

    private void SpawnPlayerForClient(PlayerData playerData, int spawnIndex)
    {
        // 1. Buscamos el MapSettings de la escena actual
        MapSettings mapSettings = FindObjectOfType<MapSettings>();

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        // 2. Si existe el settings, pedimos la posición
        if (mapSettings != null)
        {
            Transform targetPoint = mapSettings.GetSpawnPoint(spawnIndex);
            spawnPos = targetPoint.position;
            spawnRot = targetPoint.rotation;
        }
        else
        {
            Debug.LogWarning("No se encontró MapSettings en esta escena. Usando (0,0,0).");
            // Fallback: Intentar elevarlo un poco para que no caiga al vacío
            spawnPos = new Vector3(0, 2, 0);
        }

        // 3. Instanciamos YA en la posición correcta (más limpio que moverlo después)
        Transform playerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);

        // 4. Lógica de Netcode normal
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(playerData.ClientId, true);

        PlayerAppearance appearance = playerInstance.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.PlayerCustomData.Value = playerData;
        }

        PlayerNicknameUI nicknameUI = playerInstance.GetComponentInChildren<PlayerNicknameUI>();
        if (nicknameUI != null)
        {
            nicknameUI.Nickname.Value = playerData.Username;
            // Asignamos los puntos acumulados al componente de UI
            nicknameUI.Points.Value = playerData.Points;
        }
    }


    // --- RPCs y Misceláneos ---

    [ClientRpc]
    private void NotifyClientOfFailureClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (UiGameManager.Instance != null)
        {
            UiGameManager.Instance.ShowError(message);
        }
    }

    public void TriggerCameraShake()
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
    }

    // --- Sistema de Experiencia ---

    public int GetXpForLevel(int level)
    {
        return Mathf.FloorToInt(baseXpToLevelUp * Mathf.Pow(xpMultiplierPerLevel, level));
    }

    [Rpc(SendTo.Server)]
    public void AddXpToPlayerServerRpc(int amount, RpcParams rpcParams = default)
    {
        // Método obsoleto. Para el nuevo sistema de puntos, utilice AddPointsToPlayerServerRpc.
        AddPointsToPlayerServerRpc(amount, rpcParams);
    }

    /// <summary>
    /// Suma puntos al jugador que realiza la llamada. Se usa para el sistema de minijuegos.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void AddPointsToPlayerServerRpc(int amount, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.Points += amount;
                PlayersInLobby[i] = updatedPlayer;
                // Persistimos el progreso del jugador
                SavePlayerProgress(updatedPlayer, clientId);
                break;
            }
        }
    }
    /// <summary>
    /// Añade puntos al jugador identificado por su clientId. Este método debe ejecutarse en el servidor.
    /// Busca al jugador en la NetworkList y actualiza su campo Points, luego persiste el progreso.
    /// </summary>
    /// <param name="clientId">Id de cliente del jugador al que se sumarán los puntos.</param>
    /// <param name="amount">Cantidad de puntos a sumar.</param>
    public void AddPointsToPlayerById(ulong clientId, int amount)
    {
        if (!IsServer) return;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.Points += amount;
                PlayersInLobby[i] = updatedPlayer;
                SavePlayerProgress(updatedPlayer, clientId);
                break;
            }
        }
    }
    private void SavePlayerProgress(PlayerData data, ulong clientId)
    {
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
        RequestClientSaveProgressClientRpc(data, clientRpcParams);
    }

    [ClientRpc]
    private void RequestClientSaveProgressClientRpc(PlayerData data, ClientRpcParams clientRpcParams)
    {
        // Se registra la recepción de la solicitud de guardado de progreso con el nuevo sistema de puntos
        Debug.Log($"Cliente: Recibida solicitud para guardar progreso. Puntos: {data.Points}");
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.UpdateLocalData(data);
            _ = CloudAuthManager.Instance.SavePlayerProgress();
        }
    }
}