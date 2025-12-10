using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;

public class CrownMinigameHUD : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject scoreEntryPrefab;
    [SerializeField] private Transform container;
    [SerializeField] private float maxSeconds = 30f;

    // Diccionario para acceso rápido a los textos
    private Dictionary<ulong, TextMeshProUGUI> scoreTexts = new Dictionary<ulong, TextMeshProUGUI>();

    // Diccionario para llevar la cuenta visual localmente
    private Dictionary<ulong, float> localVisualTimes = new Dictionary<ulong, float>();

    private void Start()
    {
        if (CrownGameManagerNetcode.Instance != null)
        {
            CrownGameManagerNetcode.Instance.CrownScores.OnListChanged += OnScoresChanged;
            RefreshFullListFromNetwork(); // Carga inicial
        }
    }

    private void OnDestroy()
    {
        if (CrownGameManagerNetcode.Instance != null)
            CrownGameManagerNetcode.Instance.CrownScores.OnListChanged -= OnScoresChanged;
    }

    private void OnScoresChanged(NetworkListEvent<CrownGameManagerNetcode.CrownScoreData> changeEvent)
    {
        // Cuando llega un dato del servidor, "corregimos" nuestro tiempo visual
        RefreshFullListFromNetwork();
    }

    private void Update()
    {
        if (CrownGameManagerNetcode.Instance == null) return;

        // PREDICCIÓN VISUAL:
        // Si hay un rey activo, sumamos tiempo localmente en cada frame
        // para que la barra/texto se mueva suavemente.
        ulong currentKingId = CrownGameManagerNetcode.Instance.CurrentKingId.Value;

        if (currentKingId != 9999)
        {
            if (localVisualTimes.ContainsKey(currentKingId))
            {
                localVisualTimes[currentKingId] += Time.deltaTime;
                UpdateTextUI(currentKingId, localVisualTimes[currentKingId]);
            }
        }
    }

    private void RefreshFullListFromNetwork()
    {
        if (CrownGameManagerNetcode.Instance == null) return;

        foreach (var entry in CrownGameManagerNetcode.Instance.CrownScores)
        {
            ulong id = entry.PlayerId;
            float networkTime = entry.TimeHeld;

            // Sincronizar tiempo visual con el del servidor
            // (Si la diferencia es pequeña, podríamos interpolar, pero asignar directo está bien)
            if (!localVisualTimes.ContainsKey(id)) localVisualTimes.Add(id, networkTime);
            else localVisualTimes[id] = networkTime;

            // Crear UI si no existe
            if (!scoreTexts.ContainsKey(id))
            {
                if (scoreEntryPrefab != null && container != null)
                {
                    GameObject go = Instantiate(scoreEntryPrefab, container);
                    scoreTexts[id] = go.GetComponent<TextMeshProUGUI>();
                }
            }

            UpdateTextUI(id, networkTime);
        }
    }

    private void UpdateTextUI(ulong id, float time)
    {
        if (scoreTexts.TryGetValue(id, out TextMeshProUGUI txt))
        {
            // Clamp para no mostrar 30.1s
            float displayTime = Mathf.Min(time, maxSeconds);
            txt.text = $"P{id + 1}: {displayTime:F1}s / {maxSeconds:F0}s";

            // Resaltar al rey actual
            bool isKing = (CrownGameManagerNetcode.Instance.CurrentKingId.Value == id);

            txt.color = GetColor((int)id);
            txt.fontStyle = isKing ? FontStyles.Bold : FontStyles.Normal;
            // Opcional: Hacer más grande el texto del rey
            txt.fontSize = isKing ? 30 : 24;
        }
    }

    private Color GetColor(int index)
    {
        switch (index % 4)
        {
            case 0: return Color.blue;
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }
}