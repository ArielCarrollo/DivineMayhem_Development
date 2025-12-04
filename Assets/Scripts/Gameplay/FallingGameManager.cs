using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FallingGameManager : MonoBehaviour
{
    public static FallingGameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private FallingGameHUD topHUD;

    // Opcional: Referencia al generador de caídas para aumentar dificultad
    [SerializeField] private FallingLineManager lineManager;

    // Listas de jugadores
    private List<CharacterBase> activePlayers = new List<CharacterBase>();
    private List<CharacterBase> allPlayers = new List<CharacterBase>();

    private bool gameEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    // Se llama automáticamente desde CharacterBase
    public void RegisterPlayer(CharacterBase player)
    {
        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);
            activePlayers.Add(player);

            if (topHUD != null)
                topHUD.RegisterPlayer(player.PlayerIndex, player.name);
        }
    }

    // Se llama desde DeathZone o por Inactividad
    public void OnPlayerDied(CharacterBase player)
    {
        if (gameEnded) return;
        if (!activePlayers.Contains(player)) return;

        Debug.Log($"¡CAÍDA! {player.name} eliminado.");

        // 1. Efectos visuales muerte (Si no se ha desactivado ya)
        player.Kill();

        // 2. Actualizar Listas
        activePlayers.Remove(player);

        // 3. Actualizar HUD
        if (topHUD != null)
            topHUD.OnPlayerEliminated(player.PlayerIndex);

        // 4. Chequear Victoria
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        // Si queda 1 vivo (o 0 si cayeron juntos)
        if (activePlayers.Count <= 1)
        {
            gameEnded = true;

            int winnerIndex = -1;
            if (activePlayers.Count == 1)
            {
                winnerIndex = activePlayers[0].PlayerIndex;
            }

            EndGame(winnerIndex);
        }
    }

    private void EndGame(int winnerIndex)
    {
        Debug.Log($"Ganador Bloques: P{winnerIndex + 1}");

        if (GameManager.Instance != null)
        {
            // Ganador: 10 pts
            if (winnerIndex != -1)
                GameManager.Instance.AddMatchScore(winnerIndex, 10);

            // Sobrevivientes (Perdedores): 5 pts
            foreach (var p in allPlayers)
            {
                if (p.PlayerIndex != winnerIndex)
                    GameManager.Instance.AddMatchScore(p.PlayerIndex, 5);
            }

            StartCoroutine(EndSequence());
        }
    }

    private IEnumerator EndSequence()
    {
        yield return new WaitForSeconds(3f);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithFade("Intermission");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Intermission");
    }
}