using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        if (readyButton != null)
        {
            readyButton.interactable = true;
            readyButton.onClick.AddListener(OnReadyClicked);
        }

        isReady = false;
        if (buttonText) buttonText.text = "¡LISTO!";
        if (statusText) statusText.text = "¡Presiona LISTO para continuar!";

        UpdateRankingDisplay();
    }

    private void OnDisable()
    {
        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnReadyClicked);
    }

    private void UpdateRankingDisplay()
    {
        if (rankingText == null || GlobalGameManager.Instance == null)
            return;

        var scores = GlobalGameManager.Instance.GetAllScores();
        string ranking = "RANKING GLOBAL:\n\n";

        if (scores == null || scores.Count == 0)
        {
            ranking += "Cargando puntajes...";
        }
        else
        {
            foreach (var entry in scores)
            {
                ranking += $"{entry.playerName}: {entry.score} pts\n";
            }
        }

        rankingText.text = ranking;
        rankingText.SetAllDirty();
    }

    public void UpdateReadyCount(int current, int total)
    {
        if (buttonText != null)
        {
            buttonText.text = $"Esperando... ({current}/{total})";
        }
    }

    private void OnReadyClicked()
    {
        if (isReady) return;

        isReady = true;
        if (readyButton != null)
            readyButton.interactable = false;

        if (statusText) statusText.text = "Esperando a los demás...";
        if (buttonText) buttonText.text = "Enviando...";

        //// Minijuego de corona
        //if (MinigameManager.Instance != null)
        //{
        //    MinigameManager.Instance.ClientIsReady();
        //}
        //// Supervivencia
        //else if (SurvivalGameManager.Instance != null)
        //{
        //    SurvivalGameManager.Instance.ClientIsReady();
        //}
    }
}
