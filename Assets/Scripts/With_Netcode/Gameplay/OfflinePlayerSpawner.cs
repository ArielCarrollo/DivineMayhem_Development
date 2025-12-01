using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class OfflinePlayerSpawner : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("El prefab del personaje JUGABLE (debe tener PlayerInput y un script para recibir la skin).")]
    [SerializeField] private GameObject playerGamePrefab;

    [Tooltip("Puntos de aparición. El índice 0 es para el Jugador 1, etc.")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        // 1. Validar que tenemos datos del GameManager
        if (GameManager.Instance == null)
        {
            Debug.LogError("OfflinePlayerSpawner: No se encontró GameManager.");
            return;
        }

        List<LocalPlayerData> playersToSpawn = GameManager.Instance.LocalPlayers;

        if (playersToSpawn == null || playersToSpawn.Count == 0)
        {
            Debug.LogWarning("OfflinePlayerSpawner: No hay jugadores en la lista del GameManager para spawnear.");
            return;
        }

        // 2. Instanciar cada jugador
        foreach (var data in playersToSpawn)
        {
            SpawnPlayerCharacter(data);
        }
    }

    private void SpawnPlayerCharacter(LocalPlayerData data)
    {
        // A. Determinar posición
        int index = data.PlayerIndex;
        Vector3 startPos = (spawnPoints != null && index < spawnPoints.Length)
            ? spawnPoints[index].position
            : Vector3.zero;

        // B. Instanciar el jugador vinculando su dispositivo (Gamepad/Teclado)
        // Usamos PlayerInput.Instantiate para asegurar que el mando que usó en el lobby
        // se asigne a este nuevo personaje.
        var pInput = PlayerInput.Instantiate(
            playerGamePrefab,
            controlScheme: null, // Usa el esquema por defecto
            pairWithDevice: GetDeviceForPlayer(index)
        );

        // C. Posicionar
        pInput.transform.position = startPos;
        pInput.name = $"Player_{index + 1}_{data.Username}";

        // D. Aplicar Personalización (Skin)
        // Asumimos que tu prefab tiene un script 'PlayerAppearance' o similar.
        // Si tu script se llama diferente, cámbialo aquí.
        var appearance = pInput.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            // Enviamos los índices que guardamos en el Lobby
            appearance.ApplyOfflineAppearance(data.BodyIndex, data.EyesIndex, data.GlovesIndex);
        }

        // E. Configurar UI del jugador (Nombre, Nivel) si existe
        var nicknameUI = pInput.GetComponentInChildren<PlayerNicknameUI>();
        if (nicknameUI != null)
        {
            nicknameUI.SetLocalInfo(data.Username, data.Level);
        }
    }

    // Intenta recuperar el dispositivo que usaba el jugador.
    // En local simple, asumimos que el PlayerIndex coincide con el orden de los Gamepads conectados.
    private InputDevice GetDeviceForPlayer(int playerIndex)
    {
        // Si el jugador estaba usando Teclado en el lobby, PlayerInputManager suele asignarlo
        // pero aquí hacemos una asignación directa basada en gamepads disponibles.
        var gamepads = Gamepad.all;
        if (playerIndex < gamepads.Count)
        {
            return gamepads[playerIndex];
        }

        // Fallback: Si no hay suficientes gamepads, devolver el teclado actual o null
        return Keyboard.current;
    }
}