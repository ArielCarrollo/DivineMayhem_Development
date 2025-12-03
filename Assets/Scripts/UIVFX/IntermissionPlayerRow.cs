using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntermissionPlayerRow : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI addedPointsText; // Texto pequeño "+10"
    [SerializeField] private Image background;
    [SerializeField] private CanvasGroup rowCanvasGroup;
    public void Setup(string name, int currentScore, int addedPoints, Color color)
    {
        nameText.text = name;
        nameText.color = color;

        // Empezamos mostrando el puntaje ANTES de sumar
        int startScore = currentScore - addedPoints;
        scoreText.text = startScore.ToString();

        // Mostramos cuánto ganamos (+10)
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
    }
    public void AnimateEntrance(float delay)
    {
        // Aparecer suavemente en cascada
        if (rowCanvasGroup) rowCanvasGroup.DOFade(1f, 0.4f).SetDelay(delay);
        transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
    }
    public void AnimateScore(int startScore, int finalScore, float delay)
    {
        if (addedPointsText.gameObject.activeSelf)
        {
            // Secuencia: Esperar -> Rodar números -> Pop final
            DOVirtual.Int(startScore, finalScore, 1.5f, (x) =>
            {
                scoreText.text = x.ToString();
            })
            .SetDelay(delay)
            .SetEase(Ease.OutExpo) // Empieza rápido, termina lento
            .OnComplete(() =>
            {
                // Efecto al terminar: "+10" sube y desaparece
                addedPointsText.transform.DOLocalMoveY(20f, 0.5f).SetRelative(true);
                addedPointsText.DOFade(0f, 0.5f);

                // Texto de puntaje hace un pequeño salto
                scoreText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f);
            });
        }
    }

    public IEnumerator AnimateScoreRoutine(int finalScore, float duration)
    {
        if (!addedPointsText.gameObject.activeSelf) yield break;

        int startScore = int.Parse(scoreText.text);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Interpolación de entero
            int currentDisplay = (int)Mathf.Lerp(startScore, finalScore, t);
            scoreText.text = currentDisplay.ToString();

            yield return null;
        }

        // Finalizar
        scoreText.text = finalScore.ToString();

        // Ocultar el "+10" con un efecto o de golpe
        addedPointsText.gameObject.SetActive(false);

        // Efecto visual de "Pop"
        transform.localScale = Vector3.one * 1.1f;
        yield return new WaitForSeconds(0.1f);
        transform.localScale = Vector3.one;
    }
}