using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntermissionPlayerRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI addedPointsText;
    [SerializeField] private CanvasGroup rowCanvasGroup;
    [SerializeField] private TextMeshProUGUI pantheonText;

    public void Setup(string playerName, int finalScore, int addedPoints, Color color, string pantheonName)
    {
        nameText.text = playerName;
        nameText.color = color;

        // Si no sabemos los puntos añadidos, simplemente empezamos en el score final
        int startDisplayScore = (addedPoints > 0) ? finalScore - addedPoints : 0;
        scoreText.text = startDisplayScore.ToString();

        if (addedPoints > 0)
        {
            addedPointsText.text = $"+{addedPoints}";
            addedPointsText.gameObject.SetActive(true);
        }
        else
        {
            addedPointsText.gameObject.SetActive(false);
        }

        if (rowCanvasGroup) rowCanvasGroup.alpha = 0f;
        transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        if (pantheonText != null)
        {
            pantheonText.text = pantheonName;
            pantheonText.color = new Color(1, 1, 1, 0.7f);
        }
    }

    public void AnimateEntrance(float delay)
    {
        if (rowCanvasGroup) rowCanvasGroup.DOFade(1f, 0.4f).SetDelay(delay);
        transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
    }

    public void AnimateScore(int startScore, int finalScore, float delay)
    {
        // Rodar números
        DOVirtual.Int(startScore, finalScore, 2.0f, (x) =>
        {
            scoreText.text = x.ToString();
        })
        .SetDelay(delay)
        .SetEase(Ease.OutExpo)
        .OnComplete(() =>
        {
            if (addedPointsText.gameObject.activeSelf)
            {
                addedPointsText.transform.DOLocalMoveY(20f, 0.5f).SetRelative(true);
                addedPointsText.GetComponent<CanvasGroup>()?.DOFade(0f, 0.5f);
            }
            scoreText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
        });
    }
}