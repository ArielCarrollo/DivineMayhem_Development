using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private int pointsToWinRound = 50;
    [SerializeField] private float pointsPerSecond = 5f;

    // Lista local de jugadores vivos en la escena
    private List<CharacterBase> activePlayers = new List<CharacterBase>();
    private CharacterBase currentKing;

    public System.Action OnPlayerPointsChanged; // Evento para la UI

    private bool roundEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(GameLoop());
    }

    // Llamado automáticamente por CharacterBase al iniciar
    public void RegisterPlayer(CharacterBase player)
    {
        if (!activePlayers.Contains(player))
        {
            activePlayers.Add(player);

            // Si no hay rey, el primero que entra es el rey (o hazlo aleatorio)
            if (currentKing == null)
            {
                TransferCrown(player);
            }
        }
    }

    public void TransferCrown(CharacterBase newKing)
    {
        if (currentKing != null) currentKing.SetKing(false);

        currentKing = newKing;
        if (currentKing != null) currentKing.SetKing(true);
    }

    private IEnumerator GameLoop()
    {
        // 1. Esperar a que spawneen los jugadores
        yield return new WaitForSeconds(1f);

        // 2. Bucle de puntos
        while (!roundEnded)
        {
            if (currentKing != null)
            {
                // Sumar puntos en el GameManager (que es persistente)
                // Buscamos el PlayerData correspondiente
                if (GameManager.Instance != null)
                {
                    // Nota: Aquí sumamos score TEMPORAL de la ronda o directo al total.
                    // Para simplificar, usaremos un método en GameManager para sumar "puntos de ronda"
                    // O implementamos un contador local y al final lo enviamos.

                    // Hagamos un contador local simple en CharacterBase o GameManager?
                    // Usemos GameManager para centralizar.
                    GameManager.Instance.AddMatchScore(currentKing.PlayerIndex, (int)pointsPerSecond);

                    // Chequear victoria
                    int currentScore = GameManager.Instance.GetScore(currentKing.PlayerIndex);
                    if (currentScore >= pointsToWinRound)
                    {
                        EndRound(currentKing.PlayerIndex);
                    }

                    OnPlayerPointsChanged?.Invoke(); // Actualizar UI
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private void EndRound(int winnerIndex)
    {
        if (roundEnded) return;
        roundEnded = true;

        Debug.Log($"¡Ronda terminada! Ganador P{winnerIndex + 1}");

        // Reportar al GameManager para que cargue la siguiente escena
        if (GameManager.Instance != null)
        {
            // Puntos extra por ganar la ronda
            GameManager.Instance.EndMinigame(winnerIndex, 20);
        }
    }

    // Método helper para la UI Portrait
    public int GetPlayerScore(CharacterBase player)
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.GetScore(player.PlayerIndex);
        return 0;
    }
}
