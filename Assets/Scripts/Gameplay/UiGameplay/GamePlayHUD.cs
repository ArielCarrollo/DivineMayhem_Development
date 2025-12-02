using UnityEngine;
using System.Collections.Generic;

public class GameplayHUD : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject portraitPrefab; // Tu prefab 'PlayerUIPortrait'
    [SerializeField] private Transform portraitsContainer; // Un Panel Horizontal Layout Group

    private List<PlayerUIPortrait> spawnedPortraits = new List<PlayerUIPortrait>();

    private void Start()
    {
        // Esperamos un frame para asegurar que los CharacterBase se hayan registrado
        StartCoroutine(InitializeHUD());
    }

    private System.Collections.IEnumerator InitializeHUD()
    {
        yield return null;

        // Opción A: Buscar jugadores en la escena
        CharacterBase[] players = FindObjectsByType<CharacterBase>(FindObjectsSortMode.InstanceID);

        // Opción B (Mejor): Usar los datos del GameManager para ordenar por PlayerIndex
        // Pero para UI en tiempo real necesitamos la referencia al CharacterBase vivo.

        // Ordenamos por PlayerIndex para que P1 siempre salga a la izquierda, P2 derecha, etc.
        System.Array.Sort(players, (a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));

        foreach (var p in players)
        {
            CreatePortrait(p);
        }
    }

    private void CreatePortrait(CharacterBase player)
    {
        GameObject go = Instantiate(portraitPrefab, portraitsContainer);
        PlayerUIPortrait portrait = go.GetComponent<PlayerUIPortrait>();

        if (portrait != null)
        {
            portrait.Initialize(player);
            spawnedPortraits.Add(portrait);
        }
    }
}