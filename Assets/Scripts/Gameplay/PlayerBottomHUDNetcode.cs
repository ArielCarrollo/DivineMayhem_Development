using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Necesario para el ordenamiento (OrderBy)
using Unity.Netcode;

public class PlayerBottomHUDNetcode : MonoBehaviour
{
    [Header("Referencias Prefab")]
    [Tooltip("Arrastra aquí el prefab que contiene el script PlayerHUDItem")]
    [SerializeField] private GameObject hudPrefab;

    [Tooltip("El contenedor con Horizontal Layout Group donde se instanciarán los HUDs")]
    [SerializeField] private Transform container;

    // Diccionario para rastrear qué HUD pertenece a qué ClientId
    private Dictionary<ulong, PlayerHUDItem> hudItems = new Dictionary<ulong, PlayerHUDItem>();

    private void Start()
    {
        // Limpiamos hijos previos por si acaso
        foreach (Transform child in container) Destroy(child.gameObject);

        StartCoroutine(FindPlayersRoutine());
    }

    private IEnumerator FindPlayersRoutine()
    {
        // Ejecutamos esto indefinidamente (cada 0.5s)
        while (true)
        {
            // 1. Buscar todos los personajes en la escena
            var players = FindObjectsOfType<CharacterBase>();
            bool listChanged = false;

            // 2. Añadir nuevos
            foreach (var p in players)
            {
                if (!hudItems.ContainsKey(p.OwnerClientId))
                {
                    CreateHUD(p);
                    listChanged = true;
                }
            }

            // 3. Limpiar desconectados (Si el objeto CharacterBase fue destruido)
            // Creamos una lista temporal para no modificar el diccionario mientras lo iteramos
            List<ulong> idsToRemove = new List<ulong>();

            foreach (var kvp in hudItems)
            {
                // Si el script PlayerHUDItem es nulo o su objetivo (CharacterBase) es nulo
                if (kvp.Value == null || kvp.Value.TargetCharacter == null)
                {
                    idsToRemove.Add(kvp.Key);
                }
            }

            foreach (var id in idsToRemove)
            {
                // Destruir el objeto de UI si aún existe
                if (hudItems[id] != null) Destroy(hudItems[id].gameObject);
                hudItems.Remove(id);
                listChanged = true;
            }

            // 4. Si hubo cambios, ORDENAR visualmente (P1, P2, P3...)
            if (listChanged)
            {
                SortHUDs();
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private void CreateHUD(CharacterBase player)
    {
        GameObject go = Instantiate(hudPrefab, container);
        PlayerHUDItem item = go.GetComponent<PlayerHUDItem>();

        if (item != null)
        {
            item.Initialize(player);
        }
        else
        {
            Debug.LogError("El prefab asignado en PlayerBottomHUDNetcode no tiene el componente PlayerHUDItem.");
        }

        hudItems.Add(player.OwnerClientId, item);
    }

    private void SortHUDs()
    {
        // Ordenamos las entradas del diccionario por la Key (ClientId: 0, 1, 2...)
        var sortedEntries = hudItems.OrderBy(x => x.Key).ToList();

        // Reordenamos sus transform en la jerarquía
        for (int i = 0; i < sortedEntries.Count; i++)
        {
            if (sortedEntries[i].Value != null)
            {
                sortedEntries[i].Value.transform.SetSiblingIndex(i);
            }
        }
    }
}