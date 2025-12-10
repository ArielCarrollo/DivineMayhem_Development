using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // Necesario para Button

public class LocalLobbyManager : MonoBehaviour
{
    [Header("UI Referencias Principales")]
    [SerializeField] private RectTransform readyPanelParent;
    [SerializeField] private GameObject readyPanelPrefab;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Configuración de Partida (Solo P1)")]
    [SerializeField] private TextMeshProUGUI roundsText;
    [SerializeField] private TextMeshProUGUI gameModeText;
    [SerializeField] private Button btnRoundUp;
    [SerializeField] private Button btnRoundDown;
    [SerializeField] private Button btnGameNext;
    [SerializeField] private Button btnGamePrev;

    // Variables de configuración interna
    private int selectedRounds = 5;
    private int selectedGameIndex = 0; // 0 = Aleatorio, 1...N = Juegos específicos
    private List<string> availableGames;

    // Diccionario para acceder rápidamente a los datos
    private Dictionary<LocalPlayerContext, LocalReadyPanel> activePanels = new Dictionary<LocalPlayerContext, LocalReadyPanel>();
    private List<LocalPlayerData> finalPlayersList = new List<LocalPlayerData>();

    // Cantidad de panteones (definido en GameManager)
    private int totalPantheons = 4;

    private void Awake() { if (activePanels == null) activePanels = new Dictionary<LocalPlayerContext, LocalReadyPanel>(); }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            availableGames = GameManager.Instance.MinigameScenes;
            totalPantheons = GameManager.Instance.PantheonNames.Length;
        }
        else availableGames = new List<string>();

        UpdateConfigUI();
        btnRoundUp.onClick.AddListener(() => ChangeRounds(1));
        btnRoundDown.onClick.AddListener(() => ChangeRounds(-1));
        btnGameNext.onClick.AddListener(() => ChangeGameMode(1));
        btnGamePrev.onClick.AddListener(() => ChangeGameMode(-1));
    }
    private void ChangeRounds(int delta)
    {
        selectedRounds = Mathf.Clamp(selectedRounds + delta, 1, 20); // Mínimo 1, Máximo 20 rondas
        UpdateConfigUI();
    }

    private void ChangeGameMode(int delta)
    {
        // Rango: 0 (Aleatorio) hasta N (Cantidad de juegos)
        int maxIndex = availableGames.Count;
        selectedGameIndex = (selectedGameIndex + delta);

        // Loop ciclico
        if (selectedGameIndex < 0) selectedGameIndex = maxIndex;
        else if (selectedGameIndex > maxIndex) selectedGameIndex = 0;

        UpdateConfigUI();
    }

    private void UpdateConfigUI()
    {
        roundsText.text = $"{selectedRounds}";

        if (selectedGameIndex == 0)
        {
            gameModeText.text = "ALEATORIO";
        }
        else
        {
            // Restamos 1 porque el 0 es "Aleatorio"
            // Mostramos el nombre de la escena limpio
            string sceneName = availableGames[selectedGameIndex - 1];
            gameModeText.text = sceneName;
        }
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
        if (context == null || context.Data == null) return;
        if (activePanels.ContainsKey(context)) return;

        // 1. Asignar Clase Inicial (Sin repetir si es posible)
        // Intentamos darle la clase que corresponde a su índice (P1->0, P2->1)
        int initialClass = context.Input.playerIndex % totalPantheons;

        // Verificar si ya está cogida, si sí, buscar la siguiente libre
        while (IsClassTaken(initialClass, context))
        {
            initialClass = (initialClass + 1) % totalPantheons;
        }
        context.Data.PantheonIndex = initialClass;

        // 2. UI
        GameObject panelObj = Instantiate(readyPanelPrefab, readyPanelParent);
        LocalReadyPanel panelScript = panelObj.GetComponent<LocalReadyPanel>();

        if (panelScript != null)
        {
            Color pColor = GetColorForIndex(context.Input.playerIndex);
            panelScript.Initialize(this, context.Data, context.Input, pColor);

            activePanels.Add(context, panelScript);
            UpdateLobbyStatus();

            // Configurar Navegación
            StartCoroutine(SetupNavigationRoutine(context, panelScript));
        }
    }

    private IEnumerator SetupNavigationRoutine(LocalPlayerContext context, LocalReadyPanel panel)
    {
        yield return null;

        // Forzar selección en el botón de "Siguiente Clase" o "Ready"
        var es = context.GetComponent<EventSystem>();
        if (es != null && panel.ReadyButton != null)
        {
            es.SetSelectedGameObject(panel.ReadyButton.gameObject);
        }

        if (context.Input.playerIndex == 0) SetupP1Navigation(panel.ReadyButton, panel.BtnNextClass, panel.BtnPrevClass);
    }

    public void ChangePlayerClass(LocalPlayerData data, int direction)
    {
        int originalIndex = data.PantheonIndex;
        int newIndex = originalIndex;

        // Buscar el siguiente índice libre
        for (int i = 0; i < totalPantheons; i++)
        {
            newIndex = (newIndex + direction);
            if (newIndex < 0) newIndex = totalPantheons - 1;
            else if (newIndex >= totalPantheons) newIndex = 0;

            // Si nadie más tiene esta clase, nos la quedamos
            if (!IsClassTaken(newIndex, null)) // null porque ya tenemos la ref en 'data' pero la función necesita contexto para ignorarse a sí mismo (simplificado aquí)
            {
                // Pequeña corrección: IsClassTaken necesita saber quién pregunta para no contar al propio jugador si ya la tiene (aunque aquí cambiamos)
                // Revisamos la lista global
                bool taken = false;
                foreach (var kvp in activePanels)
                {
                    // Si otro jugador (no yo) tiene esa clase
                    if (kvp.Key.Data != data && kvp.Key.Data.PantheonIndex == newIndex)
                    {
                        taken = true;
                        break;
                    }
                }

                if (!taken)
                {
                    data.PantheonIndex = newIndex;
                    // Actualizar UI del panel correspondiente
                    foreach (var kvp in activePanels)
                    {
                        if (kvp.Key.Data == data)
                        {
                            kvp.Value.UpdateClassUI();
                            break;
                        }
                    }
                    return; // Éxito
                }
            }
        }
    }

    private bool IsClassTaken(int index, LocalPlayerContext ignoreMe)
    {
        foreach (var kvp in activePanels)
        {
            if (kvp.Key != ignoreMe && kvp.Key.Data.PantheonIndex == index) return true;
        }
        return false;
    }

    private void SetupP1Navigation(Button p1Ready, Button p1Next, Button p1Prev)
    {
        // 1. Conectar fila superior entre sí (Configuración Global)
        void LinkH(Button l, Button r)
        {
            Navigation ln = l.navigation; ln.mode = Navigation.Mode.Explicit; ln.selectOnRight = r; l.navigation = ln;
            Navigation rn = r.navigation; rn.mode = Navigation.Mode.Explicit; rn.selectOnLeft = l; r.navigation = rn;
        }
        LinkH(btnRoundDown, btnRoundUp);
        LinkH(btnRoundUp, btnGamePrev);
        LinkH(btnGamePrev, btnGameNext);

        // Si tenemos los botones de clase (Prev/Next), hacemos la conexión en "H"
        if (p1Next != null && p1Prev != null)
        {
            // --- BAJADA (Desde Config a Clases) ---
            // Lado Izquierdo (Rondas) baja al botón < (Prev)
            void LinkDownTo(Button origin, Button target)
            { Navigation n = origin.navigation; n.selectOnDown = target; origin.navigation = n; }

            LinkDownTo(btnRoundDown, p1Prev);
            LinkDownTo(btnRoundUp, p1Prev);

            // Lado Derecho (Juegos) baja al botón > (Next)
            LinkDownTo(btnGamePrev, p1Next);
            LinkDownTo(btnGameNext, p1Next);

            // --- SUBIDA (Desde Clases a Config) ---
            // Botón < (Prev) sube a Rondas
            Navigation prevNav = p1Prev.navigation;
            prevNav.mode = Navigation.Mode.Explicit;
            prevNav.selectOnUp = btnRoundDown;
            prevNav.selectOnRight = p1Next; // Ir a la derecha cruza al Next
            prevNav.selectOnDown = p1Ready; // Bajar va al Ready
            p1Prev.navigation = prevNav;

            // Botón > (Next) sube a Juegos
            Navigation nextNav = p1Next.navigation;
            nextNav.mode = Navigation.Mode.Explicit;
            nextNav.selectOnUp = btnGameNext;
            nextNav.selectOnLeft = p1Prev;  // Ir a la izquierda cruza al Prev
            nextNav.selectOnDown = p1Ready; // Bajar va al Ready
            p1Next.navigation = nextNav;

            // --- READY (El ancla final) ---
            Navigation rNav = p1Ready.navigation;
            rNav.mode = Navigation.Mode.Explicit;
            rNav.selectOnUp = p1Next; // Subir va por defecto a la derecha (o Prev si prefieres)
            // Truco: Hacemos que Left/Right en el botón Ready vayan a Prev/Next también
            rNav.selectOnLeft = p1Prev;
            rNav.selectOnRight = p1Next;
            p1Ready.navigation = rNav;
        }
        else
        {
            // Si no hay botones de clase (fallback), todo baja al Ready
            void LinkToReady(Button t) { Navigation n = t.navigation; n.selectOnDown = p1Ready; t.navigation = n; }
            LinkToReady(btnRoundDown); LinkToReady(btnRoundUp);
            LinkToReady(btnGamePrev); LinkToReady(btnGameNext);

            Navigation rNav = p1Ready.navigation;
            rNav.mode = Navigation.Mode.Explicit;
            rNav.selectOnUp = btnRoundDown;
            p1Ready.navigation = rNav;
        }
    }
    private IEnumerator SelectButtonNextFrame(EventSystem es, GameObject btn)
    {
        yield return null; // Esperar un frame
        es.SetSelectedGameObject(null); // Limpiar
        es.SetSelectedGameObject(btn);  // Seleccionar
    }

    // --- NUEVO MÉTODO PARA COSER LOS BOTONES ---
    private void SetupP1Navigation(Button p1ReadyBtn)
    {
        // 1. Conectar los botones de configuración entre ellos (Horizontalmente)
        // Asumimos orden visual: [Round-] [Round+] [Game-] [Game+]

        // Helper para conectar A <-> B
        void LinkHorizontal(Button left, Button right)
        {
            // Configurar Izquierda
            Navigation l = left.navigation;
            l.mode = Navigation.Mode.Explicit;
            l.selectOnRight = right; // Derecha va al otro

            // Si quieres que sea cíclico o volver al P1 al bajar, mantenemos el selectOnDown previo
            // PERO CUIDADO: Al leer .navigation, leemos lo que ya tenía. 
            // Si Unity lo resetea, hay que reasignar todo.
            // Para asegurar, reescribimos todo aquí.
            left.navigation = l;

            // Configurar Derecha
            Navigation r = right.navigation;
            r.mode = Navigation.Mode.Explicit;
            r.selectOnLeft = left;
            right.navigation = r;
        }

        // Cadena Horizontal:
        LinkHorizontal(btnRoundDown, btnRoundUp);
        LinkHorizontal(btnRoundUp, btnGamePrev);
        LinkHorizontal(btnGamePrev, btnGameNext);

        // 2. Conectar TODOS los botones de arriba hacia ABAJO (al P1 Ready)
        void LinkDownToPlayer(Button target)
        {
            Navigation n = target.navigation;
            n.selectOnDown = p1ReadyBtn; // Abajo -> P1
            target.navigation = n;
        }

        LinkDownToPlayer(btnRoundDown);
        LinkDownToPlayer(btnRoundUp);
        LinkDownToPlayer(btnGamePrev);
        LinkDownToPlayer(btnGameNext);

        // 3. Conectar el botón del P1 hacia ARRIBA (a los de configuración)
        Navigation p1Nav = p1ReadyBtn.navigation;
        p1Nav.mode = Navigation.Mode.Explicit;

        // Subir lleva al primero (Round Down)
        p1Nav.selectOnUp = btnRoundDown;
        // Opcional: Si quieres que también vaya con la derecha al RoundDown si está muy a la derecha
        // p1Nav.selectOnRight = btnRoundDown; 

        p1ReadyBtn.navigation = p1Nav;
    }

    private void LinkConfigButtonsBackToP1(Button p1ReadyBtn)
    {
        // Configuramos la navegación de los botones de config para que al bajar vuelvan al P1
        void SetNavDown(Button target, Button destination)
        {
            Navigation n = target.navigation;
            n.mode = Navigation.Mode.Explicit;
            n.selectOnDown = destination;
            // Configura Left/Right entre ellos si quieres
            target.navigation = n;
        }

        SetNavDown(btnRoundUp, p1ReadyBtn);
        SetNavDown(btnRoundDown, p1ReadyBtn);
        SetNavDown(btnGameNext, p1ReadyBtn);
        SetNavDown(btnGamePrev, p1ReadyBtn);
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
        Debug.Log("[Lobby] Iniciando rutina de comienzo de partida...");

        // 1. Validaciones
        if (GameManager.Instance == null)
        {
            Debug.LogError("[Lobby] ERROR: GameManager.Instance es NULL. No se puede iniciar.");
            statusText.text = "Error: Falta GameManager";
            yield break;
        }

        if (finalPlayersList.Count == 0)
        {
            Debug.LogWarning("[Lobby] Advertencia: La lista final de jugadores está vacía.");
        }

        statusText.text = "Configurando partida...";
        Debug.Log("[Lobby] Enviando configuración al GameManager...");

        // 2. Enviar datos
        GameManager.Instance.SetPlayers(finalPlayersList);

        // --- Try-Catch para evitar que el lobby se congele si la configuración falla ---
        bool configSuccess = true;
        try
        {
            GameManager.Instance.ConfigureMatch(selectedRounds, selectedGameIndex);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Lobby] Excepción al configurar partida: {e.Message}\n{e.StackTrace}");
            statusText.text = "Error en Configuración";
            configSuccess = false;
        }

        if (!configSuccess) yield break;

        yield return new WaitForSeconds(1f);

        statusText.text = "¡Iniciando!";
        yield return new WaitForSeconds(0.5f);

        // --- LIMPIEZA: Destruir los objetos del Lobby para que no viajen ---
        var lobbyPlayers = FindObjectsByType<LocalPlayerContext>(FindObjectsSortMode.None);
        foreach (var p in lobbyPlayers)
        {
            Destroy(p.gameObject);
        }

        if (GameManager.Instance != null) GameManager.Instance.StartGameSequence();
    }
}