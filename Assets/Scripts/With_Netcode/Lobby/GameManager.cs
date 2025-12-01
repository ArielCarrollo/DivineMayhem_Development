using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// Estructura simplificada para datos locales (ya no necesita tipos de Netcode)
[System.Serializable]
public class LocalPlayerData
{
    public int PlayerIndex;
    public string Username;
    public int Level;
    public int CurrentXP;
    public bool IsReady;

    // Datos de personalización
    public int BodyIndex;
    public int EyesIndex;
    public int GlovesIndex;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    public string GameSceneName = "Game";

    // Lista persistente de jugadores para pasar del Lobby al Juego
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
    }

    // Método para recibir la lista final desde el LocalLobbyManager antes de iniciar
    public void SetPlayers(List<LocalPlayerData> players)
    {
        LocalPlayers = new List<LocalPlayerData>(players);
    }

    public void StartLocalGame()
    {
        SceneManager.LoadScene(GameSceneName);
    }

    // Sistema simple de XP (opcional)
    public void AddXp(int playerIndex, int amount)
    {
        var player = LocalPlayers.Find(p => p.PlayerIndex == playerIndex);
        if (player != null)
        {
            player.CurrentXP += amount;
            // Lógica simple de nivel
            if (player.CurrentXP >= 100)
            {
                player.CurrentXP = 0;
                player.Level++;
            }
        }
    }
    public void TriggerCameraShake()
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
        else
        {
            // Intenta buscarlo de nuevo por si se perdió la referencia o no estaba en Awake
            impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource != null) impulseSource.GenerateImpulse();
        }
    }
}