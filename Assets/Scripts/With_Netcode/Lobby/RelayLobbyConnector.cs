using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Versión simplificada para MODO LOCAL OFFLINE.
/// No usa Relay ni Lobbies, sólo sirve como puente entre UI y escenas.
/// </summary>
public class RelayLobbyConnector : MonoBehaviour
{
    public static RelayLobbyConnector Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Sólo existe para que el código viejo compile. En modo local siempre es null.
    public object CurrentLobby => null;

    /// <summary>
    /// Llamado desde el botón "Crear sala de espera (local)" o desde Bootstrap.
    /// Activa el modo offline y carga la escena de lobby local.
    /// </summary>
    public void StartLocalOfflineLobby()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetOfflineMode(true);
        }

        // Cambia "Lobby_Local" por el nombre REAL de tu escena de lobby local
        SceneManager.LoadScene("Lobby_Local");
    }

    /// <summary>
    /// Stub para el flujo ONLINE antiguo. En modo offline no hace nada,
    /// pero se mantiene para que GameManager.CloseLobbyOnClientRpc pueda llamarlo.
    /// </summary>
    public void ClearCurrentLobby()
    {
        // Antes limpiaba el Lobby de UGS. Ahora no tenemos lobby online,
        // así que no es necesario hacer nada aquí.
        // Si quisieras, podrías resetear algún estado local.
        Debug.Log("[RelayLobbyConnector] ClearCurrentLobby() llamado en modo offline (no hace nada).");
    }

    /// <summary>
    /// Stub para el flujo ONLINE antiguo. En modo offline no muestra ningún panel
    /// de unión online; dejamos este método vacío para que compile.
    /// </summary>
    public void ShowJoiningPanel()
    {
        // Antes abría el panel de "Join by Code" o similar.
        // En tu build local no quieres nada de eso, así que lo dejamos vacío.
        Debug.Log("[RelayLobbyConnector] ShowJoiningPanel() llamado en modo offline (no hace nada).");
        // Si quisieras reutilizar algo, podrías hacer:
        // if (UiGameManager.Instance != null) UiGameManager.Instance.GoToLobbySelection();
    }
}
