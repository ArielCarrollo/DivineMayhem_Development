using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gestiona un menú de pausa global en un contexto de multijugador local. Cuando
/// cualquier jugador pulsa el botón de pausa (por defecto el botón Start del
/// gamepad), el juego se detiene y se muestra un menú. Sólo el jugador que
/// inició la pausa puede reanudar. El menú ofrece opciones para reanudar,
/// abrir un submenú de opciones (sin implementar) o abandonar la partida.
/// </summary>
public class GlobalPauseMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Raíz del menú de pausa que se activa/desactiva.")]
    public GameObject pauseMenuRoot;
    [Tooltip("Botón para reanudar la partida.")]
    public Button resumeButton;
    [Tooltip("Botón para abrir el menú de opciones.")]
    public Button optionsButton;
    [Tooltip("Botón para abandonar la partida y volver al menú principal.")]
    public Button quitButton;

    private bool isPaused = false;
    private PlayerInput pausingPlayer;

    private void Awake()
    {
        // Registrar callbacks
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);
        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
        }
    }

    private void Update()
    {
        // Revisar si se presiona pausa en cualquier gamepad asociado a jugadores
        if (!isPaused)
        {
            // Buscar todos los PlayerInput activos en la escena
            var playerInputs = FindObjectsOfType<PlayerInput>();
            foreach (var pi in playerInputs)
            {
                if (pi == null) continue;
                // Buscar un Gamepad en sus dispositivos
                Gamepad gp = null;
                var devices = pi.devices;
                if (devices.Count > 0)
                {
                    foreach (var dev in devices)
                    {
                        if (dev is Gamepad g)
                        {
                            gp = g;
                            break;
                        }
                    }
                }
                if (gp != null && gp.startButton.wasPressedThisFrame)
                {
                    PauseGame(pi);
                    break;
                }
            }
        }
        else
        {
            // Si está pausado, permitir que el mismo jugador reanude con el botón Start
            if (pausingPlayer != null)
            {
                // Obtener su Gamepad
                Gamepad gp = null;
                var devices2 = pausingPlayer.devices;
                if (devices2.Count > 0)
                {
                    foreach (var dev in devices2)
                    {
                        if (dev is Gamepad g)
                        {
                            gp = g;
                            break;
                        }
                    }
                }
                if (gp != null && gp.startButton.wasPressedThisFrame)
                {
                    ResumeGame();
                }
            }
        }
    }

    /// <summary>
    /// Detiene el juego y muestra el menú de pausa. Se almacena el jugador
    /// responsable de la pausa para permitir que sólo él reanude.
    /// </summary>
    /// <param name="pi">Jugador que pulsó pausa</param>
    private void PauseGame(PlayerInput pi)
    {
        isPaused = true;
        pausingPlayer = pi;
        // Pausar el tiempo
        Time.timeScale = 0f;
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(true);
    }

    /// <summary>
    /// Reanuda la partida si el jugador que intenta reanudar es el mismo
    /// que puso la pausa. Este método se llama desde el botón de UI o por
    /// el botón Start durante la pausa.
    /// </summary>
    public void ResumeGame()
    {
        if (!isPaused)
            return;
        isPaused = false;
        pausingPlayer = null;
        Time.timeScale = 1f;
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);
    }

    private void OnResumeClicked()
    {
        // Reanudar desde el botón de UI. No comprobamos quién hizo click,
        // pues asumimos que el cursor controlado por el jugador que pausó
        // será el que llegue al botón.
        ResumeGame();
    }

    private void OnOptionsClicked()
    {
        // Aquí podrías abrir un submenú de opciones. Por simplicidad no se implementa.
        Debug.Log("Opciones aún no implementadas.");
    }

    private void OnQuitClicked()
    {
        // Abandonar la partida. Si estamos en un juego local offline, volvemos al menú principal.
        // Si tienes una escena principal, cámbiala aquí. Por defecto usamos el nombre "MainMenu".
        Time.timeScale = 1f;
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);
        isPaused = false;
        pausingPlayer = null;

        // Volver al menú principal. Ajusta el nombre según tu proyecto.
        try
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }
        catch
        {
            Debug.LogError("GlobalPauseMenu: no se pudo cargar la escena MainMenu. Asegúrate de que existe en la Build Settings.");
        }
    }
}