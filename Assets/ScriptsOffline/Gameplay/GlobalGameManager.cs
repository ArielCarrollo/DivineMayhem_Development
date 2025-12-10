using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[System.Serializable]
public class GlobalScoreEntry
{
    public string playerName;
    public int score;
}

public class GlobalGameManager : MonoBehaviour
{
    public static GlobalGameManager Instance { get; private set; }

    // Suma total de TODOS los minijuegos
    public List<GlobalScoreEntry> GlobalScores = new List<GlobalScoreEntry>();

    [Header("Configuración de Escenas")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string firstMinigameSceneName = "GameVegui";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainMenuSceneName)
        {
            Destroy(gameObject);
            return;
        }

        CheckAndClearScores();
    }

    private void CheckAndClearScores()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        Debug.Log($"[GLOBAL MANAGER] Estoy en escena: '{currentScene}'. Espero: '{firstMinigameSceneName}'");

        if (currentScene == firstMinigameSceneName)
        {
            Debug.Log("✅ ¡NOMBRES COINCIDEN! Limpiando puntajes globales para TODOS.");
            GlobalScores.Clear();
        }
        else
        {
            Debug.LogWarning("❌ NOMBRES NO COINCIDEN. No se borrarán los puntos.");
        }
    }

    public void AddPointsToGlobal(CharacterBase player, int pointsToAdd)
    {
        if (player == null) return;

        string name = player.name;
        if (string.IsNullOrWhiteSpace(name))
            name = "Player";

        var entry = GlobalScores.Find(e => e.playerName == name);
        if (entry != null)
        {
            entry.score += pointsToAdd;
        }
        else
        {
            GlobalScores.Add(new GlobalScoreEntry
            {
                playerName = name,
                score = pointsToAdd
            });
        }
    }

    public int GetGlobalScore(string playerName)
    {
        var entry = GlobalScores.Find(e => e.playerName == playerName);
        return entry != null ? entry.score : 0;
    }

    public IReadOnlyList<GlobalScoreEntry> GetAllScores()
    {
        return GlobalScores;
    }

    public void LoadNextMinigame(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
