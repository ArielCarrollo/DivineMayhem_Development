using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Este componente debe colocarse en la escena de juego para posiciones iniciales
/// de los jugadores en modo offline. Cuando el modo offline está activado en
/// GameManager, buscará todos los PlayerInput persistentes (creados en el
/// lobby) y los posicionará en los puntos de aparición definidos.
/// También puedes asignar aquí scripts de movimiento o cámara personalizados
/// que se necesiten sólo en modo local.
/// </summary>
public class OfflinePlayerSpawner : MonoBehaviour
{
    [Tooltip("Puntos de aparición para los jugadores locales. La longitud de este array debe ser al menos igual al número máximo de jugadores.")]
    public Transform[] spawnPoints;

    private void Start()
    {
        // Sólo necesitamos spawnear en modo offline
        if (GameManager.Instance == null || !GameManager.Instance.IsOfflineMode)
            return;

        // Encontrar todos los PlayerInput persistentes
        var playerInputs = FindObjectsOfType<PlayerInput>();
        // Ordenar por su index de unión (opcional) para asignar el mismo orden que en el lobby
        // Aquí asumimos que el orden en la lista es el mismo que el de joinedPlayers

        for (int i = 0; i < playerInputs.Length; i++)
        {
            var pi = playerInputs[i];
            // Asegurarse de que hay suficientes spawn points
            if (spawnPoints != null && i < spawnPoints.Length)
            {
                pi.transform.position = spawnPoints[i].position;
            }
            else
            {
                // Si no hay suficientes, los dejamos en el origen
                Debug.LogWarning("OfflinePlayerSpawner: no hay suficientes puntos de spawn asignados. Usa el origen para los sobrantes.");
                pi.transform.position = Vector3.zero;
            }
            // Aquí podrías habilitar un script de movimiento para cada jugador si aún no lo tiene
            // o configurar su cámara personalizada.
        }
    }
}