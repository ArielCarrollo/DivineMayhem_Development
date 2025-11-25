using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement; // Necesario para cambiar escenas

public class GlobalGameManager : NetworkBehaviour
{
    public static GlobalGameManager Instance { get; private set; }

    // Aquí guardamos la suma total de TODOS los minijuegos
    public NetworkList<PlayerScore> GlobalScores = new NetworkList<PlayerScore>();
    [Header("Configuración de Escenas")]
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // Pon aquí el nombre exacto de tu escena de menú
    [SerializeField] private string firstMinigameSceneName = "GameVegui";
    private void Awake()
    {
        // 1. Lógica Singleton clásica: Solo puede haber uno
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // ¡Sobrevive al cambio de escena!
    }

   private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    public override void OnNetworkSpawn()
    {
        // Este código se ejecuta justo DESPUÉS de que presionas "Start Host".
        // Aquí IsServer YA es verdadero.

        CheckAndClearScores();
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
        // Solo el servidor puede limpiar la lista
        if (!IsServer) return;

        string currentScene = SceneManager.GetActiveScene().name;

        // --- DEBUG PARA ENCONTRAR EL ERROR ---
        Debug.Log($"[GLOBAL MANAGER] Estoy en escena: '{currentScene}'. Espero: '{firstMinigameSceneName}'");

        // Si estamos en la escena del primer minijuego...
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
    public void AddPointsToGlobal(ulong playerId, int pointsToAdd)
    {
        if (!IsServer) return;

        bool found = false;
        for (int i = 0; i < GlobalScores.Count; i++)
        {
            if (GlobalScores[i].PlayerId == playerId)
            {
                PlayerScore current = GlobalScores[i];
                current.Score += pointsToAdd; // Sumamos al acumulado
                GlobalScores[i] = current;
                found = true;
                break;
            }
        }

        if (!found)
        {
            GlobalScores.Add(new PlayerScore { PlayerId = playerId, Score = pointsToAdd });
        }
    }


    public void LoadNextMinigame(string sceneName)
    {
        if (!IsServer) return;

        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
    public int GetGlobalScore(ulong playerId)
    {
        foreach (var score in GlobalScores)
        {
            if (score.PlayerId == playerId) return score.Score;
        }
        return 0;
    }
}