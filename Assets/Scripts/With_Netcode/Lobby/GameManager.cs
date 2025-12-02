using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using System.Linq;

[System.Serializable]
public class LocalPlayerData
{
    public int PlayerIndex;
    public string Username;
    public int Level;
    public int CurrentXP;
    public bool IsReady;
    public int MatchScore;
    public int BodyIndex;
    public int EyesIndex;
    public int GlovesIndex;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuración de Partida")]
    [Tooltip("Lista EXACTA de nombres de escenas de tus minijuegos. ¡DEBEN ESTAR EN BUILD SETTINGS!")]
    public List<string> MinigameScenes;

    [Header("Estado Actual")]
    public int TotalRounds = 5;
    public int CurrentRound = 0;

    private Queue<string> minigameQueue = new Queue<string>();
    public List<LocalPlayerData> LocalPlayers { get; private set; } = new List<LocalPlayerData>();
    private CinemachineImpulseSource impulseSource;

    private void Awake()
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

    public void SetPlayers(List<LocalPlayerData> players)
    {
        LocalPlayers = new List<LocalPlayerData>(players);
        foreach (var p in LocalPlayers) p.MatchScore = 0;
        Debug.Log($"[GameManager] Jugadores establecidos: {LocalPlayers.Count}");
    }

    public void ConfigureMatch(int rounds, int startMinigameIndex)
    {
        Debug.Log($"[GameManager] Configurando partida... Rondas: {rounds}, StartIndex: {startMinigameIndex}");

        // 1. VALIDACIÓN CRÍTICA
        if (MinigameScenes == null || MinigameScenes.Count == 0)
        {
            Debug.LogError("[GameManager] ¡ERROR FATAL! La lista 'MinigameScenes' está vacía en el Inspector. No se puede iniciar.");
            return;
        }

        TotalRounds = rounds;
        CurrentRound = 0;
        minigameQueue.Clear();

        List<string> gamesPool = new List<string>(MinigameScenes);

        // Si se eligió un juego específico
        if (startMinigameIndex > 0)
        {
            int realIndex = Mathf.Clamp(startMinigameIndex - 1, 0, MinigameScenes.Count - 1);
            string firstGame = MinigameScenes[realIndex];
            minigameQueue.Enqueue(firstGame);
            Debug.Log($"[GameManager] Primer juego forzado: {firstGame}");
        }

        System.Random rng = new System.Random();

        // Rellenar cola
        while (minigameQueue.Count < TotalRounds)
        {
            // Recargar pool si se vacía
            if (gamesPool.Count == 0)
            {
                gamesPool = new List<string>(MinigameScenes);
            }

            int rnd = rng.Next(gamesPool.Count);
            minigameQueue.Enqueue(gamesPool[rnd]);
            gamesPool.RemoveAt(rnd);
        }

        Debug.Log($"[GameManager] Cola de juegos generada ({minigameQueue.Count} items): {string.Join(", ", minigameQueue)}");
    }

    public void StartGameSequence()
    {
        Debug.Log("[GameManager] Iniciando secuencia de juego...");
        LoadNextMinigame();
    }

    public void LoadNextMinigame()
    {
        if (minigameQueue.Count > 0)
        {
            CurrentRound++;
            string nextScene = minigameQueue.Dequeue();
            Debug.Log($"[GameManager] Cargando siguiente minijuego ({CurrentRound}/{TotalRounds}): {nextScene}");

            // --- USO DEL SCENE TRANSITION MANAGER ---
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadSceneWithFade(nextScene);
            }
            else
            {
                Debug.LogWarning("[GameManager] SceneTransitionManager no encontrado, cargando directo.");
                SceneManager.LoadScene(nextScene);
            }
        }
        else
        {
            Debug.Log("[GameManager] Partida Terminada. Volviendo al menú.");
            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadSceneWithFade("IntroLogo"); // O MainMenu
            else
                SceneManager.LoadScene("IntroLogo");
        }
    }

    // ... (El resto de métodos de Score y XP se mantienen igual) ...
    public void EndMinigame(int winnerPlayerIndex, int pointsAwarded)
    {
        var winner = LocalPlayers.Find(p => p.PlayerIndex == winnerPlayerIndex);
        if (winner != null)
        {
            winner.MatchScore += pointsAwarded;
            AddXp(winnerPlayerIndex, 25);
        }
        foreach (var p in LocalPlayers) AddXp(p.PlayerIndex, 10);

        LoadNextMinigame();
    }

    public void AddMatchScore(int playerIndex, int amount)
    {
        var p = LocalPlayers.Find(x => x.PlayerIndex == playerIndex);
        if (p != null) p.MatchScore += amount;
    }

    public int GetScore(int playerIndex)
    {
        var p = LocalPlayers.Find(x => x.PlayerIndex == playerIndex);
        return p != null ? p.MatchScore : 0;
    }

    public void AddXp(int playerIndex, int amount)
    {
        var p = LocalPlayers.Find(x => x.PlayerIndex == playerIndex);
        if (p != null)
        {
            p.CurrentXP += amount;
            if (p.CurrentXP >= 100) { p.CurrentXP = 0; p.Level++; }
        }
    }

    public void TriggerCameraShake()
    {
        if (impulseSource != null) impulseSource.GenerateImpulse();
        else
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource != null) impulseSource.GenerateImpulse();
        }
    }
}