using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;

public class LocalLobbyManager : MonoBehaviour
{
    [Header("UI Referencias")]
    [SerializeField] private RectTransform readyPanelParent;
    [SerializeField] private GameObject readyPanelPrefab;
    [SerializeField] private TextMeshProUGUI statusText;

    private Dictionary<LocalPlayerContext, LocalReadyPanel> activePanels = new Dictionary<LocalPlayerContext, LocalReadyPanel>();
    private List<LocalPlayerData> finalPlayersList = new List<LocalPlayerData>();

    private void Awake()
    {
        if (activePanels == null) activePanels = new Dictionary<LocalPlayerContext, LocalReadyPanel>();
    }

    private void OnEnable()
    {
        // 1. LIMPIEZA DE FANTASMAS: Borrar cualquier cosa que haya en la UI antes de empezar
        CleanUpUI();

        LocalPlayerContext.OnPlayerJoinedContext += HandlePlayerContextJoined;
        LocalPlayerContext.OnPlayerLeftContext += HandlePlayerContextLeft;

        RefreshExistingPlayers();
    }

    private void OnDisable()
    {
        LocalPlayerContext.OnPlayerJoinedContext -= HandlePlayerContextJoined;
        LocalPlayerContext.OnPlayerLeftContext -= HandlePlayerContextLeft;
    }

    private void CleanUpUI()
    {
        // Esto elimina esos objetos "falsos" que veías al inicio
        // (por ejemplo, placeholders que dejaste en el editor)
        if (readyPanelParent != null)
        {
            foreach (Transform child in readyPanelParent)
            {
                Destroy(child.gameObject);
            }
        }
        activePanels.Clear();
    }

    private void RefreshExistingPlayers()
    {
        var existingPlayers = FindObjectsByType<LocalPlayerContext>(FindObjectsSortMode.None);
        foreach (var p in existingPlayers)
        {
            HandlePlayerContextJoined(p);
        }
    }

    private void HandlePlayerContextJoined(LocalPlayerContext context)
    {
        // --- NUEVA PROTECCIÓN ---
        if (context == null) return;

        // Si el contexto no tiene datos (es un fantasma o no se ha inicializado), lo ignoramos
        if (context.Data == null || string.IsNullOrEmpty(context.Data.Username))
        {
            // Opcional: Intentar forzar inicialización si es necesario, 
            // pero mejor ignorarlo si es un objeto basura.
            return;
        }
        if (activePanels.ContainsKey(context)) return; // Evitar duplicados reales

        // Crear Panel
        GameObject panelObj = Instantiate(readyPanelPrefab, readyPanelParent);
        LocalReadyPanel panelScript = panelObj.GetComponent<LocalReadyPanel>();

        if (panelScript != null)
        {
            // Determinar color para coherencia (El mismo que el cursor)
            Color pColor = GetColorForIndex(context.Input.playerIndex);

            // Inicializar pasando el color
            panelScript.Initialize(this, context.Data, context.Input, pColor);

            activePanels.Add(context, panelScript);
            UpdateLobbyStatus();
            var pEventSystem = context.GetComponent<UnityEngine.EventSystems.EventSystem>();
            if (pEventSystem != null && panelScript.ReadyButton != null)
            {
                pEventSystem.SetSelectedGameObject(panelScript.ReadyButton.gameObject);
            }
        }
    }

    private void HandlePlayerContextLeft(LocalPlayerContext context)
    {
        if (activePanels.TryGetValue(context, out LocalReadyPanel panel))
        {
            if (panel != null) Destroy(panel.gameObject);
            activePanels.Remove(context);
        }
        UpdateLobbyStatus();
    }

    // Helper de colores (Mismo orden que en LocalPlayerContext para que coincida con el cursor)
    private Color GetColorForIndex(int index)
    {
        switch (index)
        {
            case 0: return Color.blue;       // P1
            case 1: return Color.red;        // P2
            case 2: return Color.green;      // P3
            case 3: return Color.yellow;     // P4
            default: return Color.white;
        }
    }

    public void OnPlayerReadyChange()
    {
        UpdateLobbyStatus();
        CheckIfAllReady();
    }

    private void UpdateLobbyStatus()
    {
        if (statusText == null) return;
        int readyCount = 0;
        foreach (var kvp in activePanels)
        {
            if (kvp.Key.Data.IsReady) readyCount++;
        }
        statusText.text = $"Jugadores: {activePanels.Count} | Listos: {readyCount}";
    }

    private void CheckIfAllReady()
    {
        if (activePanels.Count > 0)
        {
            finalPlayersList.Clear();
            bool allReady = true;

            foreach (var kvp in activePanels)
            {
                finalPlayersList.Add(kvp.Key.Data);
                if (!kvp.Key.Data.IsReady) allReady = false;
            }

            if (allReady)
            {
                StartCoroutine(StartGameRoutine());
            }
            else
            {
                StopAllCoroutines();
                statusText.text += " (Esperando...)";
            }
        }
    }

    private IEnumerator StartGameRoutine()
    {
        statusText.text = "¡Iniciando partida en 3...";
        yield return new WaitForSeconds(1f);
        statusText.text = "¡Iniciando partida en 2...";
        yield return new WaitForSeconds(1f);
        statusText.text = "¡Iniciando partida en 1...";
        yield return new WaitForSeconds(1f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayers(finalPlayersList);
            GameManager.Instance.StartLocalGame();
        }
    }
}