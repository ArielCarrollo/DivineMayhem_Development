using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class OfflinePlayerSpawner : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("El prefab del personaje JUGABLE (debe tener PlayerInput, CharacterBase y PlayerAppearance).")]
    [SerializeField] private GameObject playerGamePrefab;

    [Header("Puntos de Aparición (Ordenados)")]
    [Tooltip("Arrastra aquí los Transforms vacíos. El Elemento 0 es para el P1, el 1 para el P2, etc.")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        // 1. Validar GameManager
        if (GameManager.Instance == null)
        {
            Debug.LogError("[Spawner] No hay GameManager. No puedo saber quiénes juegan.");
            return;
        }

        List<LocalPlayerData> playersToSpawn = GameManager.Instance.LocalPlayers;

        if (playersToSpawn == null || playersToSpawn.Count == 0)
        {
            Debug.LogWarning("[Spawner] La lista de jugadores está vacía.");
            return;
        }

        // 2. Recorrer la lista de jugadores registrados y spawnearlos en orden
        for (int i = 0; i < playersToSpawn.Count; i++)
        {
            LocalPlayerData data = playersToSpawn[i];

            // Validar que exista un punto de spawn para este índice
            if (spawnPoints != null && i < spawnPoints.Length)
            {
                SpawnPlayer(data, spawnPoints[i].position, spawnPoints[i].rotation);
            }
            else
            {
                Debug.LogWarning($"[Spawner] No hay punto de spawn definido para el Jugador {i + 1}. Spawneando en (0,0,0).");
                SpawnPlayer(data, Vector3.zero, Quaternion.identity);
            }
        }
    }

    private void SpawnPlayer(LocalPlayerData data, Vector3 position, Quaternion rotation)
    {
        // A. RECUPERAR EL DISPOSITIVO (Mando/Teclado)
        // Esto responde a tu duda: Aquí buscamos EXACTAMENTE el mando que usó este jugador.
        InputDevice device = GetDeviceForPlayer(data.PlayerIndex);

        // B. INSTANCIAR CON CONTROL ASIGNADO (La clave para que no interfieran)
        // 'pairWithDevice' fuerza a que este prefab SOLO escuche a ese mando.
        var pInput = PlayerInput.Instantiate(
            playerGamePrefab,
            controlScheme: null,
            pairWithDevice: device
        );

        // C. POSICIONAR
        pInput.transform.position = position;
        pInput.transform.rotation = rotation;
        pInput.name = $"Player_{data.PlayerIndex + 1}_{data.Username}";
        // --- CORRECCIÓN DE ÍNDICES ---
        var charBase = pInput.GetComponent<CharacterBase>();
        if (charBase != null)
        {
            // IMPORTANTE: Esta línea arregla el P2/P1
            charBase.SetPlayerInfo(data.PlayerIndex, data.Username);
        }
        // D. APLICAR DATOS VISUALES (Skin)
        // Buscamos el script de apariencia que hicimos antes
        var appearance = pInput.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.ApplyOfflineAppearance(data.BodyIndex, data.EyesIndex, data.GlovesIndex);
        }

        // E. REGISTRARSE AUTOMÁTICAMENTE
        // (Tu CharacterBase ya hace esto en Start, pero es bueno asegurarse o pasarle datos extra si hace falta)
        if (charBase != null)
        {
            // Opcional: Si quieres pasarle el nombre para que lo muestre encima de la cabeza
            // charBase.SetUsername(data.Username); 
        }
    }

    // Lógica para re-conectar el mando correcto
    private InputDevice GetDeviceForPlayer(int playerIndex)
    {
        // NOTA: Esto asume que el orden de los Gamepads no cambió drásticamente.
        // En local suele ser estable: Gamepad[0] es el primero que se conectó.

        var gamepads = Gamepad.all;

        // Estrategia: Si el índice del jugador coincide con un gamepad, se lo damos.
        if (playerIndex < gamepads.Count)
        {
            return gamepads[playerIndex];
        }

        // Si no hay suficientes gamepads, quizás era el teclado.
        // Asignamos el teclado al jugador que no tenga gamepad.
        return Keyboard.current;
    }
}