using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUIPortrait : MonoBehaviour
{
    [Header("Componentes de UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;

    private CharacterBase targetPlayer;

    public void Initialize(CharacterBase player)
    {
        targetPlayer = player;

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = player.EstaminaMaxima;
            staminaSlider.value = player.Estamina;
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = player.Vida;
            healthSlider.value = player.Vida;
        }

        if (playerNameText != null)
        {
            playerNameText.text = player.name;
        }

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.OnPlayerPointsChanged += UpdateScoreText;
            UpdateScoreText();
        }
    }

    private void Update()
    {
        if (targetPlayer == null) return;

        if (staminaSlider != null)
            staminaSlider.value = targetPlayer.Estamina;

        if (healthSlider != null)
            healthSlider.value = targetPlayer.Vida;
    }

    private void UpdateScoreText()
    {
        if (MinigameManager.Instance == null || scoreText == null || targetPlayer == null)
            return;

        int currentScore = MinigameManager.Instance.GetPlayerScore(targetPlayer);
        scoreText.text = "Puntos: " + currentScore;
        scoreText.SetAllDirty();
    }

    private void OnDestroy()
    {
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.OnPlayerPointsChanged -= UpdateScoreText;
        }
    }
}
