using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class MinigameScoreEntry
{
    public CharacterBase player;
    public int score;
}

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

    [Header("Condiciones de Victoria")]
    [SerializeField] private int scoreToWin = 100;
    [SerializeField] private string nextSceneName = "SurvivalLava";
    [SerializeField] private IntermissionUI intermissionPanel;

    [SerializeField, Tooltip("Puntos por segundo por tener la corona")]
    private int pointsPerSecond = 1;

    [Header("Sistema de Mapas (Sin cambio de escena)")]
    [SerializeField] private GameObject mapCorona;
    [SerializeField] private GameObject mapLava;
    [SerializeField] private Transform[] lavaSpawnPoints;

    public bool isGameEnded = false;

    private List<CharacterBase> playerList = new List<CharacterBase>();
    public List<MinigameScoreEntry> PlayerPoints = new List<MinigameScoreEntry>();

    public System.Action OnPlayerPointsChanged;

    private CharacterBase currentKing;
    private int playersReadyCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        StartCoroutine(PointAwardCoroutine());
    }

    /// <summary>
    /// Llama esto desde donde instancias / activas a los players en el minijuego de la corona.
    /// </summary>
    public void RegisterPlayer(CharacterBase player)
    {
        if (player == null) return;

        if (!playerList.Contains(player))
        {
            playerList.Add(player);
            PlayerPoints.Add(new MinigameScoreEntry { player = player, score = 0 });
            Debug.Log($"MinigameManager: Player {player.name} registrado.");

            // Si aún no hay rey, este se vuelve rey
            if (currentKing == null)
            {
                TransferCrown(player);
            }
        }
    }

    public void UnregisterPlayer(CharacterBase player)
    {
        if (player == null) return;

        if (playerList.Contains(player))
            playerList.Remove(player);

        PlayerPoints.RemoveAll(p => p.player == player);

        if (currentKing == player)
        {
            currentKing.IsKing = false;
            currentKing = null;
        }
    }

    public void TransferCrown(CharacterBase newKing)
    {
        if (newKing == null) return;

        if (currentKing != null)
            currentKing.IsKing = false;

        currentKing = newKing;
        currentKing.IsKing = true;

        Debug.Log($"MinigameManager: ¡La corona pasa a {newKing.name}!");
    }

    private IEnumerator PointAwardCoroutine()
    {
        Debug.Log("MinigameManager: Esperando a que haya al menos 1 jugador...");

        while (playerList.Count == 0)
        {
            yield return new WaitForSeconds(1.0f);
        }

        Debug.Log("MinigameManager: ¡Jugadores listos! Comienza la puntuación.");

        while (!isGameEnded)
        {
            yield return new WaitForSeconds(1.0f);

            if (currentKing == null)
                continue;

            var entry = PlayerPoints.Find(e => e.player == currentKing);
            if (entry != null)
            {
                entry.score += pointsPerSecond;
                OnPlayerPointsChanged?.Invoke();

                if (entry.score >= scoreToWin)
                {
                    EndMinigame(currentKing);
                }
            }
        }
    }

    private void EndMinigame(CharacterBase winner)
    {
        if (isGameEnded) return;
        isGameEnded = true;

        Debug.Log($"¡JUEGO TERMINADO! Ganador: {winner.name}");

        if (GlobalGameManager.Instance != null)
        {
            foreach (var localScore in PlayerPoints)
            {
                GlobalGameManager.Instance.AddPointsToGlobal(localScore.player, localScore.score);
            }
        }

        ShowIntermission();
    }

    private void ShowIntermission()
    {
        if (intermissionPanel == null)
        {
            intermissionPanel = FindObjectOfType<IntermissionUI>(true);
        }

        if (intermissionPanel != null)
        {
            intermissionPanel.gameObject.SetActive(true);
        }

        if (UIManager.Instance != null)
            UIManager.Instance.SetGameHUDActive(false);

        // congelar a todos
        foreach (var p in playerList)
        {
            if (p != null)
                p.SetInputActive(false);
        }
    }

    public void ClientIsReady()
    {
        playersReadyCount++;

        if (intermissionPanel != null)
        {
            intermissionPanel.UpdateReadyCount(playersReadyCount, playerList.Count);
        }

        if (playersReadyCount >= playerList.Count && playerList.Count > 0)
        {
            Debug.Log("Todos listos. Cambiando al mapa de Lava...");
            SwitchToLavaMap();
        }
    }

    private void SwitchToLavaMap()
    {
        // Esconder panel de resultados
        if (intermissionPanel != null)
            intermissionPanel.gameObject.SetActive(false);

        // Mostrar HUD
        if (UIManager.Instance != null)
            UIManager.Instance.SetGameHUDActive(true);

        // Cambiar mapas
        if (mapCorona != null) mapCorona.SetActive(false);
        if (mapLava != null) mapLava.SetActive(true);

        // Teletransportar y reactivar jugadores
        for (int i = 0; i < playerList.Count; i++)
        {
            var player = playerList[i];
            if (player == null) continue;

            Vector3 spawnPos = (lavaSpawnPoints != null && lavaSpawnPoints.Length > 0)
                ? lavaSpawnPoints[i % lavaSpawnPoints.Length].position
                : player.transform.position;

            player.ServerTeleport(spawnPos);  // usa el ServerTeleport local que te di en CharacterBase
            player.SetInputActive(true);
        }

        // Registrar jugadores en SurvivalGameManager (si está en escena)
        var survival = FindObjectOfType<SurvivalGameManager>();
        if (survival != null)
        {
            foreach (var p in playerList)
            {
                survival.RegisterPlayer(p);
            }
        }
    }

    public int GetPlayerScore(CharacterBase player)
    {
        var entry = PlayerPoints.Find(e => e.player == player);
        return entry != null ? entry.score : 0;
    }
}
