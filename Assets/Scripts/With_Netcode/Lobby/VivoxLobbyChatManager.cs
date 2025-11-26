using System;
using UnityEngine;

/// <summary>
/// Stub de VivoxLobbyChatManager para compilaciones offline.
/// Este componente sustituye al gestor de chat y voz en línea y no realiza
/// ninguna acción de red. Se proporciona para evitar errores de
/// compilación en scripts que referencian a VivoxLobbyChatManager.
/// </summary>
public class VivoxLobbyChatManager : MonoBehaviour
{
    public static VivoxLobbyChatManager Instance { get; private set; }

    // Eventos de texto (no se invocan en modo offline)
    public event Action<string, string, bool> OnTextMessage;
    public event Action<string, string, string> OnDirectMessage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // Propiedades de estado (siempre falsos en offline)
    public bool IsMicMuted => false;
    public bool IsDeafened => false;
    public bool IsLoggedIn => false;

    // Métodos de utilidad que no realizan ninguna operación
    public void ToggleMicMute() { }
    public void ToggleDeafen() { }

    public System.Threading.Tasks.Task LoginIfNeeded(string displayName, bool isHostFlag)
    {
        // Devuelve una tarea completada inmediatamente
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task JoinLobbyChannel(string lobbyCodeOrId)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task LeaveCurrentChannel()
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SendTextMessage(string message)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SendDirectMessage(string targetPlayerId, string message)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}