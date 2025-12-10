using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class IntermissionUI : MonoBehaviour
{
    [Header("Referencias Generales")]
    [SerializeField] private TextMeshProUGUI roundTitleText;
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private GameObject rowPrefab;

    [Header("Pantalla Ganador Final")]
    [SerializeField] private GameObject winnerPanel;
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private Image winnerAvatarImage;

    private void Start()
    {
        winnerPanel.SetActive(false);
        StartCoroutine(IntermissionSequence());
    }

    private IEnumerator IntermissionSequence()
    {
        if (GameManager.Instance == null) yield break;

        // 1. Mostrar Ronda
        roundTitleText.text = $"RESULTADOS RONDA {GameManager.Instance.CurrentRound}";

        // 2. Ordenar Jugadores
        List<LocalPlayerData> sortedPlayers = GameManager.Instance.LocalPlayers
            .OrderByDescending(p => p.MatchScore)
            .ToList();

        // Lista para guardar las referencias a los scripts de fila
        List<IntermissionPlayerRow> rowsScripts = new List<IntermissionPlayerRow>();

        // 3. Crear Filas y Preparar Animación
        float delay = 0.2f;

        foreach (var p in sortedPlayers)
        {
            GameObject go = Instantiate(rowPrefab, rowsContainer);
            var script = go.GetComponent<IntermissionPlayerRow>();

            // Calculamos cuánto ganó en esta ronda
            int pointsEarned = p.MatchScore - p.ScoreAtStartOfRound;

            // Calculamos el puntaje inicial para la animación (Total - Ganado)
            int startScore = p.MatchScore - pointsEarned;

            string pName = GameManager.Instance.GetPantheonName(p.PantheonIndex);

            script.Setup(p.Username, p.MatchScore, pointsEarned, GetColor(p.PlayerIndex), pName);

            // --- ANIMACIONES DOTWEEN ---
            // 1. Entrada (Aparecer)
            script.AnimateEntrance(delay);

            // 2. Sumar Puntos (Rodar números)
            // Se ejecuta medio segundo después de aparecer
            script.AnimateScore(startScore, p.MatchScore, delay + 0.5f);

            rowsScripts.Add(script);
            delay += 0.2f; // Efecto cascada para el siguiente
        }

        // 4. Esperar a que terminen las animaciones
        // (Delay total + un extra para ver el resultado final)
        yield return new WaitForSeconds(delay + 2.5f);

        // 5. DECISIÓN: ¿Siguiente Juego o Final?
        if (GameManager.Instance.HasNextMinigame)
        {
            // --- SIGUIENTE RONDA ---
            GameManager.Instance.LoadNextMinigame();
        }
        else
        {
            // --- FINAL DE LA PARTIDA ---
            ShowWinner(sortedPlayers[0]); // El primero de la lista ordenada es el ganador
        }
    }

    private void ShowWinner(LocalPlayerData winner)
    {
        winnerPanel.SetActive(true);
        string pName = GameManager.Instance.GetPantheonName(winner.PantheonIndex);

        // "¡EL PANTEÓN INKA SE LLEVA LA VICTORIA!"
        winnerNameText.text = $"¡EL {pName.ToUpper()} SE LLEVA LA VICTORIA!";
        winnerNameText.color = GetColor(winner.PlayerIndex);

        StartCoroutine(ReturnToMenuRoutine());
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        yield return new WaitForSeconds(5f);

        // Volver al Login
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithFade("Login"); // Asegúrate de que la escena se llame así
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
    }

    private Color GetColor(int index)
    {
        switch (index)
        {
            case 0: return Color.blue;
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }
}