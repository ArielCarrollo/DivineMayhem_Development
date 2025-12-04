using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class OfflinePlayerSpawner : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private GameObject playerGamePrefab;
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        // 1. Validar GameManager
        if (GameManager.Instance == null)
        {
            Debug.LogError("[Spawner] ❌ ERROR: No hay GameManager. No puedo leer jugadores.");
            return;
        }

        List<LocalPlayerData> playersToSpawn = GameManager.Instance.LocalPlayers;

        if (playersToSpawn == null || playersToSpawn.Count == 0)
        {
            Debug.LogWarning("[Spawner] ⚠️ La lista de jugadores del GameManager está vacía.");
            return;
        }

        Debug.Log($"[Spawner] Iniciando spawn de {playersToSpawn.Count} jugadores...");

        // 2. Spawnear
        for (int i = 0; i < playersToSpawn.Count; i++)
        {
            LocalPlayerData data = playersToSpawn[i];
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;

            // Validar punto de spawn
            if (spawnPoints != null && i < spawnPoints.Length && spawnPoints[i] != null)
            {
                targetPos = spawnPoints[i].position;
                targetRot = spawnPoints[i].rotation;
                Debug.Log($"[Spawner] Jugador {i + 1} ({data.Username}) -> Asignado SpawnPoint[{i}] en {targetPos}");
            }
            else
            {
                Debug.LogError($"[Spawner] ❌ Jugador {i + 1} NO tiene punto de spawn válido. Usando (0,0,0). Revisa el Inspector.");
                targetPos = Vector3.zero;
            }

            SpawnPlayer(data, targetPos, targetRot);
        }
    }

    private void SpawnPlayer(LocalPlayerData data, Vector3 position, Quaternion rotation)
    {
        InputDevice device = GetDeviceForPlayer(data.PlayerIndex);

        // Instanciar vinculando el mando específico
        var pInput = PlayerInput.Instantiate(
            playerGamePrefab,
            controlScheme: null,
            pairWithDevice: device
        );

        // Mover a la posición
        pInput.transform.position = position;
        pInput.transform.rotation = rotation;

        // Poner nombre para identificarlo en jerarquía
        pInput.name = $"Player_{data.PlayerIndex + 1}_{data.Username}";

        // Configurar CharacterBase
        var charBase = pInput.GetComponent<CharacterBase>();
        if (charBase != null)
        {
            charBase.SetPlayerInfo(data.PlayerIndex, data.Username);
        }

        // Configurar Apariencia
        var appearance = pInput.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.ApplyOfflineAppearance(data.BodyIndex, data.EyesIndex, data.GlovesIndex);
        }
    }

    private InputDevice GetDeviceForPlayer(int playerIndex)
    {
        var gamepads = Gamepad.all;
        if (playerIndex < gamepads.Count) return gamepads[playerIndex];
        return Keyboard.current;
    }
}