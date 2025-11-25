using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Panel de UI para que un jugador local indique si está listo.
/// Este script debería estar en el prefab utilizado para representar a un jugador
/// en el lobby local. Contiene un botón para alternar el estado de listo y
/// muestra el nombre del jugador. Al pulsar el botón se notifica al
/// LocalLobbyManager correspondiente para actualizar el estado.
/// </summary>
public class LocalReadyPanel : MonoBehaviour
{
    [Tooltip("Texto que muestra el nombre del jugador")] public TextMeshProUGUI playerNameText;
    [Tooltip("Botón que alterna el estado listo/no listo")] public Button readyButton;
    [Tooltip("Texto interno del botón de ready")] public TextMeshProUGUI readyButtonText;
    [Tooltip("Imagen para colorear el botón según el estado")] public Image readyButtonImage;

    private PlayerInput playerInput;
    private LocalLobbyManager lobbyManager;
    private bool isReady;

    /// <summary>
    /// Configura el panel con la información del jugador y el lobby asociado.
    /// </summary>
    /// <param name="pi">PlayerInput del jugador</param>
    /// <param name="manager">Referente al LocalLobbyManager</param>
    /// <param name="playerName">Nombre que se mostrará</param>
    public void Setup(PlayerInput pi, LocalLobbyManager manager, string playerName)
    {
        playerInput = pi;
        lobbyManager = manager;
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }
        isReady = false;
        UpdateUI();
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(ToggleReady);
        }
    }

    private void ToggleReady()
    {
        isReady = !isReady;
        UpdateUI();
        if (lobbyManager != null && playerInput != null)
        {
            lobbyManager.SetReadyState(playerInput, isReady);
        }
    }

    private void UpdateUI()
    {
        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "Listo" : "No listo";
        }
        if (readyButtonImage != null)
        {
            // Pon verde si está listo, rojo si no lo está
            readyButtonImage.color = isReady ? Color.green : Color.red;
        }
    }
}