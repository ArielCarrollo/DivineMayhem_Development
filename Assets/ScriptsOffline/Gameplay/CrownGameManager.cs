using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CrownGameManager : MonoBehaviour
{
    public static CrownGameManager Instance { get; private set; }

    [Header("Configuración Corona")]
    [Tooltip("Segundos necesarios con la corona para ganar")]
    [SerializeField] private float secondsToWin = 30f;

    [Header("Referencias UI")]
    [SerializeField] private CrownMinigameHUD topHUD;

    // Estado Local
    private Dictionary<int, float> playerCrownTimers = new Dictionary<int, float>();
    private CharacterBase currentKing;

    // Lista de TODOS los que empezaron (para repartir puntos)
    private List<CharacterBase> allPlayers = new List<CharacterBase>();

    // Lista de los que siguen VIVOS
    private List<CharacterBase> alivePlayers = new List<CharacterBase>();

    private bool gameEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);
            alivePlayers.Add(player);

            // Inicializar timer
            if (!playerCrownTimers.ContainsKey(player.PlayerIndex))
            {
                playerCrownTimers.Add(player.PlayerIndex, 0f);
                if (topHUD != null)
                {
                    topHUD.RegisterPlayer(player.PlayerIndex);
                    topHUD.UpdateScore(player.PlayerIndex, 0, secondsToWin);
                }
            }

            // Si no hay rey, el primero la tiene
            if (currentKing == null) TransferCrown(player);
        }
    }

    // Llamado por CharacterBase cuando muere
    public void OnPlayerDied(CharacterBase player)
    {
        if (alivePlayers.Contains(player))
        {
            alivePlayers.Remove(player);

            // Si el que murió tenía la corona, se la quitamos
            if (currentKing == player)
            {
                TransferCrown(null);
                // Opcional: Dársela a otro al azar
                if (alivePlayers.Count > 0)
                {
                    TransferCrown(alivePlayers[Random.Range(0, alivePlayers.Count)]);
                }
            }
        }
    }

    public void TransferCrown(CharacterBase newKing)
    {
        if (currentKing != null) currentKing.SetKing(false);
        currentKing = newKing;
        if (currentKing != null) currentKing.SetKing(true);
    }

    private void Update()
    {
        if (gameEnded) return;

        // 1. CONDICIÓN VICTORIA POR TIEMPO
        if (currentKing != null)
        {
            int kIndex = currentKing.PlayerIndex;
            playerCrownTimers[kIndex] += Time.deltaTime;

            if (topHUD) topHUD.UpdateScore(kIndex, playerCrownTimers[kIndex], secondsToWin);

            if (playerCrownTimers[kIndex] >= secondsToWin)
            {
                EndGame(kIndex);
            }
        }

        // 2. CONDICIÓN VICTORIA POR ELIMINACIÓN (ÚLTIMO EN PIE)
        // Solo verificamos si la partida ya empezó (más de 1 jugador original)
        if (allPlayers.Count > 1 && alivePlayers.Count <= 1)
        {
            // Si queda 1, gana. Si quedan 0 (murieron juntos), empate o gana el último que murió.
            // Asumimos que gana el que queda vivo.
            if (alivePlayers.Count == 1)
            {
                EndGame(alivePlayers[0].PlayerIndex);
            }
            else
            {
                // Todos murieron. Nadie gana o lógica especial.
                // Terminamos el juego sin ganador claro (o el que tenía más tiempo)
                EndGameWithTimeCheck();
            }
        }
    }

    private void EndGameWithTimeCheck()
    {
        // Buscar quién acumuló más tiempo
        int bestPlayer = -1;
        float bestTime = -1f;

        foreach (var kvp in playerCrownTimers)
        {
            if (kvp.Value > bestTime)
            {
                bestTime = kvp.Value;
                bestPlayer = kvp.Key;
            }
        }
        EndGame(bestPlayer);
    }

    private void EndGame(int winnerIndex)
    {
        if (gameEnded) return;
        gameEnded = true;

        Debug.Log($"¡GANADOR RONDA P{winnerIndex + 1}!");

        if (GameManager.Instance != null)
        {
            // Ganador: 10 pts
            if (winnerIndex != -1)
                GameManager.Instance.AddMatchScore(winnerIndex, 10);

            // Perdedores: 5 pts
            foreach (var p in allPlayers)
            {
                if (p.PlayerIndex != winnerIndex)
                {
                    GameManager.Instance.AddMatchScore(p.PlayerIndex, 5);
                }
            }

            StartCoroutine(EndSequence(winnerIndex));
        }
    }

    private IEnumerator EndSequence(int winnerIndex)
    {
        yield return new WaitForSeconds(3f);

        // IR A INTERMISSION
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithFade("Intermission");
        else
            SceneManager.LoadScene("Intermission");
    }
}