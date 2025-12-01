using UnityEngine;
using System.Collections.Generic;

public class MapSettings : MonoBehaviour
{
    [Header("Puntos de Aparición (En Orden)")]
    // Arrastra aquí los GameObjects vacíos (transforms) de tu escena
    public List<Transform> spawnPoints;

    private void OnDrawGizmos()
    {
        // Esto es solo visual: Dibuja esferas azules en el editor para ver dónde están
        Gizmos.color = Color.blue;
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.5f);
        }
    }

    /// <summary>
    /// Devuelve el Transform de spawn para un índice de jugador específico.
    /// Si hay más jugadores que puntos, da la vuelta (usa módulo).
    /// </summary>
    public Transform GetSpawnPoint(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return transform; // Si no hay puntos, devuelve la posición de este objeto

        // El operador % (módulo) asegura que si tienes 5 puntos y entra el jugador 6,
        // este vaya al punto 0 de nuevo.
        return spawnPoints[playerIndex % spawnPoints.Count];
    }
}