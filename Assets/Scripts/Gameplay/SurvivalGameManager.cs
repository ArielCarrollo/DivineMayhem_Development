using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class SurvivalGameManager : NetworkBehaviour
{
    public static SurvivalGameManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private IntermissionUI intermissionPanel; // Tu panel de resultados
    [SerializeField] private string nextSceneName = "Minigame3_Ball"; // El siguiente juego

    // Puntos según el orden de muerte: 
    // Posición 0 = 1er muerto (ej. 10 pts)
    // Posición 1 = 2do muerto (ej. 30 pts)
    // ... Último = Ganador (ej. 100 pts)
    [SerializeField] private int[] pointsByRank = new int[] { 10, 30, 60, 100 };

    // Lista de jugadores VIVOS
    private List<CharacterBase> alivePlayers = new List<CharacterBase>();

    // Cuántos han muerto ya (para saber qué puntaje dar)
    private int deadCount = 0;
    private bool isGameEnded = false;

    // Variables para el inicio (similar al otro manager)
    private NetworkVariable<int> playersReadyCount = new NetworkVariable<int>(0);

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        // Nos suscribimos al contador de "Listos" para el final
        playersReadyCount.OnValueChanged += OnReadyCountChanged;
    }

    // --- 1. REGISTRO DE JUGADORES ---
    public void RegisterPlayer(CharacterBase player)
    {
        if (!IsServer) return;

        if (!alivePlayers.Contains(player))
        {
            alivePlayers.Add(player);
            Debug.Log($"SurvivalManager: Player {player.OwnerClientId} listo para sobrevivir.");
        }
    }

    // (Nota: No necesitamos Unregister aquí, porque si se desconecta cuenta como muerte)

    // --- 2. LÓGICA DE MUERTE ---
    public void OnPlayerDied(CharacterBase player)
    {
        if (!IsServer || isGameEnded) return;

        if (alivePlayers.Contains(player))
        {
            // A. Lo sacamos de la lista de vivos
            alivePlayers.Remove(player);

            // B. "Matamos" al jugador visualmente (RPC)
            player.KillPlayerClientRpc();

            // C. Le damos puntos según su orden de muerte
            // Si es el 1er muerto, deadCount es 0 -> pointsByRank[0]
            int pointsToGive = 0;
            if (deadCount < pointsByRank.Length - 1) // -1 porque el último es el ganador
            {
                pointsToGive = pointsByRank[deadCount];
            }

            // Guardamos en el Global
            if (GlobalGameManager.Instance != null)
            {
                GlobalGameManager.Instance.AddPointsToGlobal(player.OwnerClientId, pointsToGive);
            }
            Debug.Log($"Player {player.OwnerClientId} eliminado. Ganó {pointsToGive} pts.");

            // D. Aumentamos el contador de muertos
            deadCount++;

            // E. COMPROBAR VICTORIA
            CheckWinner();
        }
    }

    private void CheckWinner()
    {
        // El juego termina si queda 1 solo jugador (o 0 si se cayeron juntos)
        if (alivePlayers.Count <= 1)
        {
            EndSurvivalGame();
        }
    }

    private void EndSurvivalGame()
    {
        isGameEnded = true;

        // Si queda alguien vivo, es el GANADOR FINAL
        if (alivePlayers.Count > 0)
        {
            CharacterBase winner = alivePlayers[0];
            int winnerPoints = pointsByRank[pointsByRank.Length - 1]; // El puntaje más alto (100)

            if (GlobalGameManager.Instance != null)
            {
                GlobalGameManager.Instance.AddPointsToGlobal(winner.OwnerClientId, winnerPoints);
            }
            Debug.Log($"¡GANADOR DE SUPERVIVENCIA: Player {winner.OwnerClientId}!");

            // Opcional: Matarlo visualmente o dejarlo celebrar
            winner.SetInputActive(false);
        }

        // Mostrar Panel de Resultados
        ShowIntermissionClientRpc();
    }

    // --- 3. LÓGICA DE INTERMISSION (Igual que el otro manager) ---
    [ClientRpc]
    private void ShowIntermissionClientRpc()
    {
        if (intermissionPanel == null) intermissionPanel = FindObjectOfType<IntermissionUI>(true);
        if (intermissionPanel != null) intermissionPanel.gameObject.SetActive(true);
        if (UIManager.Instance != null) UIManager.Instance.SetGameHUDActive(false);
    }

    public void ClientIsReady() { PlayerReadyServerRpc(); }

    [ServerRpc(RequireOwnership = false)]
    private void PlayerReadyServerRpc()
    {
        playersReadyCount.Value++;
        if (playersReadyCount.Value >= NetworkManager.Singleton.ConnectedClients.Count)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnReadyCountChanged(int prev, int current)
    {
        if (intermissionPanel == null) intermissionPanel = FindObjectOfType<IntermissionUI>(true);
        if (intermissionPanel != null && intermissionPanel.gameObject.activeSelf)
        {
            intermissionPanel.UpdateReadyCount(current, NetworkManager.Singleton.ConnectedClients.Count);
        }
    }
}