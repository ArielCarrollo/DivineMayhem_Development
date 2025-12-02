using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CrownMinigameHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Prefab con un simple TextMeshProUGUI")]
    [SerializeField] private GameObject scoreEntryPrefab;
    [Tooltip("El contenedor (Horizontal Layout Group) donde van los textos")]
    [SerializeField] private Transform container;

    private Dictionary<int, TextMeshProUGUI> scoreTexts = new Dictionary<int, TextMeshProUGUI>();

    public void RegisterPlayer(int playerIndex)
    {
        if (!scoreTexts.ContainsKey(playerIndex))
        {
            if (scoreEntryPrefab == null || container == null)
            {
                Debug.LogError("CrownMinigameHUD: Faltan referencias en el Inspector.");
                return;
            }

            GameObject go = Instantiate(scoreEntryPrefab, container);
            TextMeshProUGUI txt = go.GetComponent<TextMeshProUGUI>();

            // Color inicial
            txt.color = GetColor(playerIndex);
            txt.text = $"P{playerIndex + 1}: 0s";

            scoreTexts.Add(playerIndex, txt);
        }
    }

    public void UpdateScore(int playerIndex, float currentSeconds, float maxSeconds)
    {
        if (scoreTexts.TryGetValue(playerIndex, out TextMeshProUGUI txt))
        {
            // Formato: P1: 12.5s
            txt.text = $"P{playerIndex + 1}: {currentSeconds:F1}s / {maxSeconds:F0}s";

            // Efecto visual si tiene puntos
            txt.fontStyle = (currentSeconds > 0) ? FontStyles.Bold : FontStyles.Normal;
            txt.fontSize = (currentSeconds > 0) ? 36 : 30; // Ejemplo de tamaño
        }
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