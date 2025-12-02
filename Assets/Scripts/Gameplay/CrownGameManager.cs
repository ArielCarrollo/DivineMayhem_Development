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
    private List<CharacterBase> activePlayers = new List<CharacterBase>();
    private bool gameEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (!activePlayers.Contains(player))
        {
            activePlayers.Add(player);

            // Inicializar timer
            if (!playerCrownTimers.ContainsKey(player.PlayerIndex))
            {
                playerCrownTimers.Add(player.PlayerIndex, 0f);

                // --- CORRECCIÓN: Asegurar que el HUD existe antes de llamar ---
                if (topHUD != null)
                {
                    topHUD.RegisterPlayer(player.PlayerIndex);
                    // Actualizar visualmente a 0
                    topHUD.UpdateScore(player.PlayerIndex, 0, secondsToWin);
                }
                else
                {
                    Debug.LogWarning("CrownGameManager: No hay TopHUD asignado.");
                }
            }

            // Si no hay rey, el primero la tiene
            if (currentKing == null) TransferCrown(player);
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

        if (currentKing != null)
        {
            int kIndex = currentKing.PlayerIndex;

            // Sumar tiempo
            playerCrownTimers[kIndex] += Time.deltaTime;

            // Actualizar UI
            if (topHUD) topHUD.UpdateScore(kIndex, playerCrownTimers[kIndex], secondsToWin);

            // Chequear Victoria
            if (playerCrownTimers[kIndex] >= secondsToWin)
            {
                EndGame(kIndex);
            }
        }
    }

    private void EndGame(int winnerIndex)
    {
        gameEnded = true;
        Debug.Log($"¡GANADOR P{winnerIndex + 1}!");

        if (GameManager.Instance != null)
        {
            // 1. Dar puntos al Ganador (10 pts)
            GameManager.Instance.AddMatchScore(winnerIndex, 10);

            // 2. Dar puntos de consolación a los demás (5 pts)
            foreach (var p in activePlayers)
            {
                if (p.PlayerIndex != winnerIndex)
                {
                    GameManager.Instance.AddMatchScore(p.PlayerIndex, 5);
                }
            }

            // 3. Mostrar pantalla de resultados o siguiente juego
            StartCoroutine(EndSequence(winnerIndex));
        }
    }

    private IEnumerator EndSequence(int winnerIndex)
    {
        // Aquí podrías mostrar un panel de "¡VICTORIA P1!" por unos segundos
        yield return new WaitForSeconds(3f);

        // Cargar escena intermedia de puntuaciones o siguiente juego
        // Para simplificar, vamos directo al siguiente juego por ahora
        // Pero idealmente cargarías "Scene_Intermission"
        GameManager.Instance.LoadNextMinigame();
    }
}