using UnityEngine;
using System.Collections.Generic;

public class SurvivalGameManager : MonoBehaviour
{
    public static SurvivalGameManager Instance { get; private set; }

    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float damageZoneY = -10f; // Altura de muerte

    private List<CharacterBase> alivePlayers = new List<CharacterBase>();
    private int totalStartedPlayers = 0;
    private bool roundEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (!alivePlayers.Contains(player))
        {
            alivePlayers.Add(player);
            totalStartedPlayers++;

            // Asignar posición de spawn aleatoria si es necesario
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int spawnIndex = player.PlayerIndex % spawnPoints.Length;
                player.Teleport(spawnPoints[spawnIndex].position);
            }
        }
    }

    private void Update()
    {
        if (roundEnded) return;

        // Chequear muerte por caída
        for (int i = alivePlayers.Count - 1; i >= 0; i--)
        {
            var p = alivePlayers[i];
            if (p.transform.position.y < damageZoneY)
            {
                OnPlayerDied(p);
            }
        }
    }

    public void OnPlayerDied(CharacterBase player)
    {
        if (!alivePlayers.Contains(player)) return;

        alivePlayers.Remove(player);
        player.gameObject.SetActive(false); // Ocultar visualmente

        Debug.Log($"P{player.PlayerIndex + 1} eliminado.");

        // Dar puntos basados en cuanto duró (Ranking inverso)
        // Ejemplo: Si hay 4 jugadores, el primero en morir gana 0, el siguiente 10...
        int points = (totalStartedPlayers - alivePlayers.Count) * 10;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMatchScore(player.PlayerIndex, points);
        }

        CheckWinner();
    }

    private void CheckWinner()
    {
        // Si solo queda 1 (o 0 si se cayeron a la vez)
        if (alivePlayers.Count <= 1)
        {
            roundEnded = true;

            int winnerIndex = -1;
            if (alivePlayers.Count == 1)
            {
                winnerIndex = alivePlayers[0].PlayerIndex;
                Debug.Log($"¡Ganador Supervivencia: P{winnerIndex + 1}!");
            }
            else
            {
                Debug.Log("Empate (todos murieron).");
            }

            // Avisar al GameManager
            if (GameManager.Instance != null)
            {
                // Si hubo ganador, le damos bonus de victoria + pasamos de ronda
                // Si no (empate), pasamos -1
                GameManager.Instance.EndMinigame(winnerIndex, 50);
            }
        }
    }
}