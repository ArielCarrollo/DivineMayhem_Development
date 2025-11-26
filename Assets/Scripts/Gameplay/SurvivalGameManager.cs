using UnityEngine;
using System.Collections.Generic;

public class SurvivalGameManager : MonoBehaviour
{
    public static SurvivalGameManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private IntermissionUI intermissionPanel;
    [SerializeField] private string nextSceneName = "Minigame3_Ball";

    // Puntos según orden de muerte
    [SerializeField] private int[] pointsByRank = new int[] { 10, 30, 60, 100 };

    private List<CharacterBase> alivePlayers = new List<CharacterBase>();
    private int deadCount = 0;
    private bool isGameEnded = false;

    private int playersReadyCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (player == null) return;

        if (!alivePlayers.Contains(player))
        {
            alivePlayers.Add(player);
            Debug.Log($"SurvivalManager: Player {player.name} listo para sobrevivir.");
        }
    }

    public void OnPlayerDied(CharacterBase player)
    {
        if (isGameEnded || player == null) return;

        if (alivePlayers.Contains(player))
        {
            alivePlayers.Remove(player);

            // matar visualmente
            player.KillPlayer();

            int pointsToGive = 0;
            if (deadCount < pointsByRank.Length - 1)
            {
                pointsToGive = pointsByRank[deadCount];
            }

            if (GlobalGameManager.Instance != null)
            {
                GlobalGameManager.Instance.AddPointsToGlobal(player, pointsToGive);
            }

            Debug.Log($"Player {player.name} eliminado. Ganó {pointsToGive} pts.");

            deadCount++;
            CheckWinner();
        }
    }

    private void CheckWinner()
    {
        if (alivePlayers.Count <= 1)
        {
            EndSurvivalGame();
        }
    }

    private void EndSurvivalGame()
    {
        isGameEnded = true;

        if (alivePlayers.Count > 0)
        {
            CharacterBase winner = alivePlayers[0];
            int winnerPoints = pointsByRank[pointsByRank.Length - 1];

            if (GlobalGameManager.Instance != null)
            {
                GlobalGameManager.Instance.AddPointsToGlobal(winner, winnerPoints);
            }

            Debug.Log($"¡GANADOR DE SUPERVIVENCIA: {winner.name}!");
            winner.SetInputActive(false);
        }

        ShowIntermission();
    }

    private void ShowIntermission()
    {
        if (intermissionPanel == null)
            intermissionPanel = FindObjectOfType<IntermissionUI>(true);

        if (intermissionPanel != null)
            intermissionPanel.gameObject.SetActive(true);

        if (UIManager.Instance != null)
            UIManager.Instance.SetGameHUDActive(false);

        playersReadyCount = 0;
        int totalPlayers = alivePlayers.Count + deadCount;
        if (intermissionPanel != null)
            intermissionPanel.UpdateReadyCount(0, totalPlayers);
    }

    public void ClientIsReady()
    {
        playersReadyCount++;
        int totalPlayers = alivePlayers.Count + deadCount;

        if (intermissionPanel != null)
            intermissionPanel.UpdateReadyCount(playersReadyCount, totalPlayers);

        if (playersReadyCount >= totalPlayers && !string.IsNullOrEmpty(nextSceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }
    }
}
