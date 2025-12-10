using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq; // Necesario para filtrar listas

public class FallingMapController : NetworkBehaviour
{
    [SerializeField] private List<FallingBlock> allBlocks = new List<FallingBlock>();

    private void Awake()
    {
        // Auto-llenado si la lista está vacía
        if (allBlocks.Count == 0)
        {
            allBlocks = FindObjectsOfType<FallingBlock>().ToList();
        }
    }

    /// <summary>
    /// Selecciona una cantidad de bloques aleatorios y los hace caer.
    /// </summary>
    public void DropRandomBlocks(int amount)
    {
        if (!IsServer) return;

        // 1. Limpiar la lista de referencias nulas o bloques que ya cayeron
        // (Esto soluciona el error que tenías antes)
        allBlocks.RemoveAll(b => b == null || b.IsFallingOrDestroyed);

        if (allBlocks.Count == 0) return;

        // 2. Elegir bloques al azar
        for (int i = 0; i < amount; i++)
        {
            if (allBlocks.Count == 0) break;

            int randomIndex = Random.Range(0, allBlocks.Count);
            FallingBlock block = allBlocks[randomIndex];

            if (block != null)
            {
                block.TriggerFall();
                allBlocks.RemoveAt(randomIndex); // Lo sacamos para no elegirlo de nuevo
            }
        }
    }
}