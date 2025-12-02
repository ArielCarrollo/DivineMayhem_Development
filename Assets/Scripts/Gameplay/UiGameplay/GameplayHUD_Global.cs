using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GameplayHUD_Global : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El prefab que acabas de crear (PlayerStatus_Prefab)")]
    [SerializeField] private GameObject hudPrefab;

    [Tooltip("Un Panel con 'Horizontal Layout Group' en la parte inferior de la pantalla")]
    [SerializeField] private Transform container;

    private void Start()
    {
        // Esperamos un momento para que los jugadores se registren y existan
        StartCoroutine(InitializeHUD());
    }

    private System.Collections.IEnumerator InitializeHUD()
    {
        yield return null; // Esperar 1 frame

        // Buscar todos los personajes
        CharacterBase[] players = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);

        // Ordenarlos por P1, P2... para que salgan en orden en la pantalla
        System.Array.Sort(players, (a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));

        foreach (var p in players)
        {
            CreateHUD(p);
        }
    }

    private void CreateHUD(CharacterBase player)
    {
        if (hudPrefab != null && container != null)
        {
            GameObject go = Instantiate(hudPrefab, container);
            PlayerBottomHUD script = go.GetComponent<PlayerBottomHUD>();

            if (script != null)
            {
                script.Initialize(player);
            }
        }
    }
}