using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public struct PlayerScore : INetworkSerializable, System.IEquatable<PlayerScore>
{
    public ulong PlayerId;
    public int Score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref Score);
    }

    public bool Equals(PlayerScore other)
    {
        
        return PlayerId == other.PlayerId && Score == other.Score;
    }
}
public class MinigameManager : NetworkBehaviour
{
    public static MinigameManager Instance { get; private set; }

    [Header("Condiciones de Victoria")]
    [SerializeField] private int scoreToWin = 100; // Meta de puntos
    [SerializeField] private string nextSceneName = "SurvivalLava";
    [SerializeField] private IntermissionUI intermissionPanel;

    [SerializeField, Tooltip("Puntos por segundo por tener la corona")]
    private int pointsPerSecond = 1;

    public bool isGameEnded = false;
    public NetworkVariable<ulong> CurrentKingId = new NetworkVariable<ulong>(999);
    public NetworkList<PlayerScore> PlayerPoints = new NetworkList<PlayerScore>();
    private NetworkVariable<int> playersReadyCount = new NetworkVariable<int>(0);

    private Dictionary<ulong, CharacterBase> playerList = new Dictionary<ulong, CharacterBase>();
    [Header("Sistema de Mapas (Sin cambio de escena)")]
    [SerializeField] private GameObject mapCorona; // Arrastra el objeto Map_Corona
    [SerializeField] private GameObject mapLava;   // Arrastra el objeto Map_Lava
    [SerializeField] private Transform[] lavaSpawnPoints;
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); }
        else { Instance = this; }
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (!IsServer) return;

        ulong playerID = player.OwnerClientId;

        if (!playerList.ContainsKey(playerID))
        {
            playerList.Add(playerID, player);
            Debug.Log($"MinigameManager: Player {playerID} registrado.");

            PlayerPoints.Add(new PlayerScore { PlayerId = playerID, Score = 0 });
        }

        if (CurrentKingId.Value == 999 || !playerList.ContainsKey(CurrentKingId.Value))
        {
            Debug.Log("MinigameManager: No había rey. ¡El nuevo jugador es el Rey!");
            TransferCrown(player);
        }
    }

    public void UnregisterPlayer(CharacterBase player)
    {
        if (!IsServer) return;

        ulong playerID = player.OwnerClientId;
        if (playerList.ContainsKey(playerID))
        {
            playerList.Remove(playerID);
        }

        if (CurrentKingId.Value == playerID)
        {
            CurrentKingId.Value = 999;
        }
    }

    public override void OnNetworkSpawn()
    {

        Debug.Log("MinigameManager: ¡OnNetworkSpawn EJECUTADO! Soy Servidor? " + IsServer);

        if (!IsServer) return;

        StartCoroutine(PointAwardCoroutine());
        playersReadyCount.OnValueChanged += OnReadyCountChanged;
    }
   
    private IEnumerator StartMinigameDelay()
    {
        yield return new WaitForSeconds(3.0f);

        if (playerList.Count > 0)
        {
            List<CharacterBase> players = new List<CharacterBase>(playerList.Values);
            CharacterBase firstKing = players[0];

            if (firstKing != null)
            {
                TransferCrown(firstKing);
            }
        }
        else
        {
            Debug.LogError("MinigameManager: ¡No hay jugadores registrados! No se puede asignar la corona.");
        }

        StartCoroutine(PointAwardCoroutine());
    }

    public void TransferCrown(CharacterBase newKing)
    {
        if (!IsServer) return;
        if (newKing == null) return;

        ulong newKingId = newKing.OwnerClientId;

        // Apaga la corona del rey anterior
        if (playerList.TryGetValue(CurrentKingId.Value, out CharacterBase oldKing))
        {
            if (oldKing != null)
            {
                oldKing.IsKing.Value = false;
            }
        }

        // Enciende la corona del nuevo rey
        newKing.IsKing.Value = true;

        // Actualiza el ID del rey
        CurrentKingId.Value = newKingId;
        Debug.Log($"Servidor: ¡La corona pasa a Player {newKingId}!");
    }


    private IEnumerator PointAwardCoroutine()
    {
        Debug.Log("MinigameManager: Esperando a que haya al menos 2 jugadores...");

        // Mientras haya menos de 2 clientes conectados, esperamos.
        while (NetworkManager.Singleton.ConnectedClients.Count < 2)
        {
            yield return new WaitForSeconds(1.0f); // Revisa cada segundo
        }

        Debug.Log("MinigameManager: ¡Jugadores listos! Comienza la puntuación.");

        while (!isGameEnded)
        {
            yield return new WaitForSeconds(1.0f);

            ulong kingId = CurrentKingId.Value;

            // Si no hay rey valido (999), esperamos
            if (kingId == 999) continue;

            bool scoreUpdated = false;

            // Buscamos al rey en la lista de puntajes
            for (int i = 0; i < PlayerPoints.Count; i++)
            {
                if (PlayerPoints[i].PlayerId == kingId)
                {
                    PlayerScore score = PlayerPoints[i];
                    score.Score += pointsPerSecond;
                    PlayerPoints[i] = score; // Esto actualiza la UI
                    scoreUpdated = true;

                    if (score.Score >= scoreToWin)
                    {
                        EndMinigame(kingId); // ¡Alguien ganó!
                    }

                    break;
                }
            }

            if (!scoreUpdated)
            {
                Debug.LogWarning($"Auto-Repair: Creando puntaje para el Rey {kingId}");
                PlayerPoints.Add(new PlayerScore { PlayerId = kingId, Score = pointsPerSecond });
            }
        }
    }
    private void EndMinigame(ulong winnerId)
    {
        if (!IsServer) return;
        if (isGameEnded) return;

        isGameEnded = true;
        Debug.Log($"¡JUEGO TERMINADO! Ganador: Player {winnerId}");

        
        if (GlobalGameManager.Instance != null)
        {
            foreach (var localScore in PlayerPoints)
            {
                GlobalGameManager.Instance.AddPointsToGlobal(localScore.PlayerId, localScore.Score);
            }
        }
        else
        {
            Debug.LogError("¡ERROR CRÍTICO! No existe GlobalGameManager.");
        }
        ShowIntermissionClientRpc();
    }

    [ClientRpc]
    private void ShowIntermissionClientRpc()
    {
        if (intermissionPanel == null)
        {
            intermissionPanel = FindObjectOfType<IntermissionUI>(true);
        }

        if (intermissionPanel != null)
        {
            Debug.Log("CLIENTE: ¡Panel encontrado y activado!");
            intermissionPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("CLIENTE: ¡SOCORRO! No encuentro el 'PanelResultados' en la escena.");
            return; 
        }

        if (UIManager.Instance != null) UIManager.Instance.SetGameHUDActive(false);

        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (localPlayer != null && localPlayer.TryGetComponent<CharacterBase>(out var character))
        {
            character.SetInputActive(false); // ¡Congelado!
        }
    }
    public void ClientIsReady()
    {
        PlayerReadyServerRpc();
    }
    [ServerRpc(RequireOwnership = false)]
    private void PlayerReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        playersReadyCount.Value++;
        Debug.Log($"Jugadores listos: {playersReadyCount}");

        int totalPlayers = NetworkManager.Singleton.ConnectedClients.Count;

        if (playersReadyCount.Value >= totalPlayers)
        {
            Debug.Log("¡Todos listos! Cambiando al mapa de Lava...");

            // EN LUGAR DE LOAD SCENE, LLAMAMOS A ESTO:
            SwitchToLavaMap();
        }
    }
    private void SwitchToLavaMap()
    {
        // 1. Apagar Mapa Viejo / Prender Nuevo (RPC para que todos lo vean)
        ToggleMapsClientRpc();

        // 2. Teletransportar a los Jugadores
        int index = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<CharacterBase>(out var playerScript))
            {
                // Usamos el spawn point correspondiente (o el 0 si faltan)
                Vector3 spawnPos = lavaSpawnPoints[index % lavaSpawnPoints.Length].position;

                // Llamamos a la función nueva del CharacterBase
                playerScript.ServerTeleport(spawnPos);

                index++;
            }
        }

        // 3. Opcional: Iniciar la lógica del juego de Lava
        // Aquí podrías activar el 'SurvivalGameManager' si lo tienes en la escena pero apagado.
        // O simplemente cambiar el estado de este mismo Manager.
    }

    [ClientRpc]
    private void ToggleMapsClientRpc()
    {
        // Esconder UI de resultados
        if (intermissionPanel != null) intermissionPanel.gameObject.SetActive(false);

        // Mostrar HUD de juego
        if (UIManager.Instance != null) UIManager.Instance.SetGameHUDActive(true);

        // Cambiar los mapas visualmente
        if (mapCorona != null) mapCorona.SetActive(false);
        if (mapLava != null) mapLava.SetActive(true);
    }

    public int GetPlayerScore(ulong playerId)
    {
        // Busca al jugador en la lista de puntajes
        foreach (PlayerScore scoreEntry in PlayerPoints)
        {
            if (scoreEntry.PlayerId == playerId)
            {
                return scoreEntry.Score; // Devuelve su puntaje
            }
        }

        // Si no lo encuentra, devuelve 0
        return 0;
    }
    private void OnReadyCountChanged(int previous, int current)
    {
        if (intermissionPanel != null && intermissionPanel.gameObject.activeSelf)
        {
            int total = NetworkManager.Singleton.ConnectedClients.Count;
            // Actualizamos el texto del botón
            intermissionPanel.UpdateReadyCount(current, total);
        }
    }

    public override void OnNetworkDespawn()
    {
        playersReadyCount.OnValueChanged -= OnReadyCountChanged;
    }
}