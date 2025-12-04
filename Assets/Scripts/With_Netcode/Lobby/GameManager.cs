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
    public bool IsReady;

    // --- NUEVO: Selección de Clase ---
    public int PantheonIndex; // 0: Inka, 1: Griego, 2: Sintoísta, 3: Nórdico
    // --------------------------------

    public int MatchScore;
    public int ScoreAtStartOfRound;
    public int Level;
    public int CurrentXP;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuración de Partida")]
    public List<string> MinigameScenes;

    [Header("Configuración de Panteones")]
    // Nombres oficiales de las clases
    public string[] PantheonNames = new string[] { "Panteón Inka", "Panteón Griego", "Panteón Sintoísta", "Panteón Nórdico" };
    [Tooltip("Arrastra aquí las 4 imágenes en el mismo orden (Inka, Griego, Sinto, Nórdico)")]
    public Sprite[] PantheonIcons; 

    [Header("Estado Actual")]
    public int TotalRounds = 5;
    public int CurrentRound = 0;

    private Queue<string> minigameQueue = new Queue<string>();
    public List<LocalPlayerData> LocalPlayers { get; private set; } = new List<LocalPlayerData>();
    private CinemachineImpulseSource impulseSource;

    public bool HasNextMinigame => minigameQueue.Count > 0;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    public string GetPantheonName(int index)
    {
        if (index >= 0 && index < PantheonNames.Length) return PantheonNames[index];
        return "Desconocido";
    }
    public Sprite GetPantheonIcon(int index)
    {
        if (PantheonIcons != null && index >= 0 && index < PantheonIcons.Length)
            return PantheonIcons[index];
        return null;
    }
    public void SetPlayers(List<LocalPlayerData> players)
    {
        LocalPlayers = new List<LocalPlayerData>(players);
        foreach (var p in LocalPlayers) 
        {
            p.MatchScore = 0;
            p.ScoreAtStartOfRound = 0;
        }
    }

    public void ConfigureMatch(int rounds, int startMinigameIndex)
    {
        if (MinigameScenes == null || MinigameScenes.Count == 0) return;

        TotalRounds = rounds;
        CurrentRound = 0;
        minigameQueue.Clear();

        List<string> gamesPool = new List<string>(MinigameScenes);
        System.Random rng = new System.Random();

        // Si se fuerza el primero
        if (startMinigameIndex > 0)
        {
            int realIndex = Mathf.Clamp(startMinigameIndex - 1, 0, MinigameScenes.Count - 1);
            minigameQueue.Enqueue(MinigameScenes[realIndex]);
        }

        while (minigameQueue.Count < TotalRounds)
        {
            if (gamesPool.Count == 0) gamesPool = new List<string>(MinigameScenes);
            int rnd = rng.Next(gamesPool.Count);
            minigameQueue.Enqueue(gamesPool[rnd]);
            gamesPool.RemoveAt(rnd);
        }
    }

    public void StartGameSequence()
    {
        LoadNextMinigame();
    }

    public void LoadNextMinigame()
    {
        if (minigameQueue.Count > 0)
        {
            // 1. Antes de cargar, guardamos el puntaje actual como "Inicio de Ronda"
            //    para la próxima vez que vayamos a Intermission.
            foreach(var p in LocalPlayers)
            {
                p.ScoreAtStartOfRound = p.MatchScore;
            }

            CurrentRound++;
            string nextScene = minigameQueue.Dequeue();
            
            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadSceneWithFade(nextScene);
            else
                SceneManager.LoadScene(nextScene);
        }
    }

    // Llamado cuando termina un minijuego
    public void EndMinigame(int winnerPlayerIndex, int pointsAwarded)
    {
        // Dar puntos al ganador
        var winner = LocalPlayers.Find(p => p.PlayerIndex == winnerPlayerIndex);
        if (winner != null)
        {
            winner.MatchScore += pointsAwarded;
            AddXp(winnerPlayerIndex, 25);
        }
        // Dar XP a todos
        foreach (var p in LocalPlayers) AddXp(p.PlayerIndex, 10);
        
        // --- CAMBIO: Ir a Intermission en lugar del siguiente juego ---
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithFade("Intermission");
        else
            SceneManager.LoadScene("Intermission");
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