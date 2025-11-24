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
        // 1. INTENTO DE RECUPERACIÓN: Si la referencia se perdió, búscala.
        if (intermissionPanel == null)
        {
            // El 'true' dentro del paréntesis es vital: significa "busca incluso si está desactivado"
            intermissionPanel = FindObjectOfType<IntermissionUI>(true);
        }

        // 2. VERIFICACIÓN Y ACTIVACIÓN
        if (intermissionPanel != null)
        {
            Debug.Log("CLIENTE: ¡Panel encontrado y activado!");
            intermissionPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("CLIENTE: ¡SOCORRO! No encuentro el 'PanelResultados' en la escena.");
            return; // Si no hay panel, no podemos seguir
        }

        // B. Ocultar HUD del Juego
        if (UIManager.Instance != null) UIManager.Instance.SetGameHUDActive(false);

        // C. Congelar al Jugador Local
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
            Debug.Log("¡Todos listos! Cargando siguiente nivel...");
            NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
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