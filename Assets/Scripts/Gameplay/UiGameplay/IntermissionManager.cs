using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class IntermissionUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI rankingText;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI buttonText;

    private bool isReady = false;

    private void OnEnable()
    {
        UpdateRankingDisplay();

        readyButton.interactable = true;
        readyButton.onClick.AddListener(OnReadyClicked);
        isReady = false;
        if (buttonText) buttonText.text = "¡LISTO!";
        if (statusText) statusText.text = "¡Presiona LISTO para continuar!";
        if (GlobalGameManager.Instance != null)
        {
            // 1. Mostrar lo que haya inmediatamente (por si ya llegó la data)
            UpdateRankingDisplay();

            // 2. Suscribirse para actualizar si la data llega después
            // Nos suscribimos al evento OnListChanged de la lista global
            GlobalGameManager.Instance.GlobalScores.OnListChanged += OnGlobalScoresChanged;
        }
    }

    private void OnDisable()
    {
        readyButton.onClick.RemoveListener(OnReadyClicked);
        if (GlobalGameManager.Instance != null)
        {
            GlobalGameManager.Instance.GlobalScores.OnListChanged -= OnGlobalScoresChanged;
        }

    }
    private void OnGlobalScoresChanged(NetworkListEvent<PlayerScore> changeEvent)
    {
        UpdateRankingDisplay();
    }
    private void UpdateRankingDisplay()
    {
        if (GlobalGameManager.Instance == null || rankingText == null) return;

        string ranking = "RANKING GLOBAL:\n\n";

        if (GlobalGameManager.Instance.GlobalScores.Count == 0)
        {
            ranking += "Cargando puntajes...";
        }
        else
        {
            // Recorremos la lista y la mostramos
            foreach (var score in GlobalGameManager.Instance.GlobalScores)
            {
                ranking += $"Player {score.PlayerId}: {score.Score} Pts\n";
            }
        }

        rankingText.text = ranking;
        rankingText.SetAllDirty();
    }
    public void UpdateReadyCount(int current, int total)
    {
        if (buttonText != null)
        {
            // Ejemplo: "Esperando... (1/3)"
            buttonText.text = $"Esperando... ({current}/{total})";
        }
    }
    private void OnReadyClicked()
    {
        if (isReady) return;

        isReady = true;
        readyButton.interactable = false; // Desactivar para no spamear
        if (statusText) statusText.text = "Esperando a los demás...";
        if (buttonText) buttonText.text = "Enviando...";
        // Avisamos al MinigameManager que estamos listos
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.ClientIsReady();
        }
    }
}