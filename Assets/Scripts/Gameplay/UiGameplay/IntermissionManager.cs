using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; // Necesario para ordenar listas

public class IntermissionUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI rankingText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI roundsText;

    // Solo el P1 debería poder dar a continuar, o hacerlo automático
    private bool isReady = false;

    private void Start()
    {
        // 1. Mostrar Rondas Restantes
        if (GameManager.Instance != null && roundsText != null)
        {
            roundsText.text = $"Ronda {GameManager.Instance.CurrentRound} / {GameManager.Instance.TotalRounds}";
        }

        // 2. Mostrar Ranking
        UpdateRankingDisplay();

        // 3. Configurar Botón (Solo P1 o Automático)
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
            // Opcional: Seleccionarlo automáticamente para que el P1 pueda pulsarlo con mando
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        // Opcional: Auto-continuar después de 5 segundos
        // StartCoroutine(AutoContinueRoutine());
    }

    private void UpdateRankingDisplay()
    {
        if (rankingText == null || GameManager.Instance == null) return;

        // Obtenemos la lista de jugadores y la ordenamos por puntuación (Descendente)
        List<LocalPlayerData> sortedPlayers = GameManager.Instance.LocalPlayers
            .OrderByDescending(p => p.MatchScore)
            .ToList();

        string ranking = "<size=120%>RANKING GLOBAL</size>\n\n";

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            var p = sortedPlayers[i];
            string colorHex = GetColorHex(p.PlayerIndex);

            // Ejemplo: "1. [P1] Jugador 1: 150 pts"
            ranking += $"{i + 1}. <color={colorHex}>{p.Username}</color>: <b>{p.MatchScore} pts</b>\n";
        }

        rankingText.text = ranking;
    }

    private void OnContinueClicked()
    {
        if (isReady) return;
        isReady = true;

        if (statusText) statusText.text = "Cargando siguiente juego...";

        // Llamamos al GameManager para que saque el siguiente juego de la cola
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadNextMinigame();
        }
    }

    private string GetColorHex(int index)
    {
        switch (index)
        {
            case 0: return "#0000FF"; // Azul
            case 1: return "#FF0000"; // Rojo
            case 2: return "#00FF00"; // Verde
            case 3: return "#FFFF00"; // Amarillo
            default: return "#FFFFFF";
        }
    }
}