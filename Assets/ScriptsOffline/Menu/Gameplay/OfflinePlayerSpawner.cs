using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class OfflinePlayerSpawner : MonoBehaviour
{
    [Header("Configuración de Clases")]
    [Tooltip("Orden: 0=Inka, 1=Griego, 2=Sintoísta, 3=Nórdico (Debe coincidir con GameManager)")]
    [SerializeField] private GameObject[] pantheonPrefabs;

    [Header("Puntos de Aparición")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        if (GameManager.Instance == null) return;

        List<LocalPlayerData> playersToSpawn = GameManager.Instance.LocalPlayers;

        for (int i = 0; i < playersToSpawn.Count; i++)
        {
            LocalPlayerData data = playersToSpawn[i];

            // Posición
            Vector3 targetPos = (spawnPoints != null && i < spawnPoints.Length) ? spawnPoints[i].position : Vector3.zero;
            Quaternion targetRot = (spawnPoints != null && i < spawnPoints.Length) ? spawnPoints[i].rotation : Quaternion.identity;

            SpawnPlayer(data, targetPos, targetRot);
        }
    }

    private void SpawnPlayer(LocalPlayerData data, Vector3 position, Quaternion rotation)
    {
        InputDevice device = GetDeviceForPlayer(data.PlayerIndex);
        if (Mathf.Abs(rotation.eulerAngles.y) < 1f) rotation = Quaternion.Euler(0, -90, 0);

        // --- SELECCIÓN DE PREFAB ---
        GameObject prefabToUse = null;
        if (pantheonPrefabs != null && data.PantheonIndex < pantheonPrefabs.Length)
        {
            prefabToUse = pantheonPrefabs[data.PantheonIndex];
        }

        if (prefabToUse == null)
        {
            Debug.LogError($"[Spawner] No hay prefab para el Panteón {data.PantheonIndex}! Usando default.");
            if (pantheonPrefabs.Length > 0) prefabToUse = pantheonPrefabs[0];
            else return;
        }
        // ---------------------------

        var pInput = PlayerInput.Instantiate(
            prefabToUse,
            controlScheme: null,
            pairWithDevice: device
        );

        pInput.transform.position = position;
        pInput.transform.rotation = rotation;
        pInput.name = $"Player_{data.PlayerIndex + 1}_{data.Username}";

        var charBase = pInput.GetComponent<CharacterBase>();
        if (charBase != null)
        {
            charBase.SetPlayerInfo(data.PlayerIndex, data.Username);
        }
    }

    private InputDevice GetDeviceForPlayer(int playerIndex)
    {
        var gamepads = Gamepad.all;
        if (playerIndex < gamepads.Count) return gamepads[playerIndex];
        return Keyboard.current;
    }
}