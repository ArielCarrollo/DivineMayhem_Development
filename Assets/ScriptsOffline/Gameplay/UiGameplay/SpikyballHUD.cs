using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class SpikyBallHUD : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject playerEntryPrefab; // Texto "P1" o Icono
    [SerializeField] private Transform container; // Horizontal Layout Group

    private Dictionary<int, GameObject> entries = new Dictionary<int, GameObject>();

    public void RegisterPlayer(int index, string name)
    {
        if (entries.ContainsKey(index)) return;

        GameObject go = Instantiate(playerEntryPrefab, container);
        go.transform.localScale = Vector3.one;

        TextMeshProUGUI txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = $"P{index + 1}";
            txt.color = GetColor(index);
        }

        // Animación Entrada
        go.transform.localScale = Vector3.zero;
        go.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);

        entries.Add(index, go);
    }

    public void OnPlayerEliminated(int index)
    {
        if (entries.TryGetValue(index, out GameObject go))
        {
            // Efecto de eliminación
            TextMeshProUGUI txt = go.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                // Cambiar a gris/rojo y tachar
                txt.DOColor(Color.gray, 0.3f);
                txt.fontStyle = FontStyles.Strikethrough;
            }

            // Agitar el elemento
            go.transform.DOShakePosition(0.5f, 10f);
            go.transform.DOScale(0.8f, 0.3f);
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