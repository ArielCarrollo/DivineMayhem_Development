using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Prefabs")]
    [SerializeField] private GameObject playerUIPortraitPrefab;

    [Header("Contenedores de Layout")]
    [SerializeField] private Transform ffaLayoutContainer;

    // Antes: Dictionary<ulong, PlayerUIPortrait>
    // Ahora: usamos como clave directamente el CharacterBase del jugador.
    private Dictionary<CharacterBase, PlayerUIPortrait> playerPortraits =
        new Dictionary<CharacterBase, PlayerUIPortrait>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // Si quieres que persista entre escenas:
            // DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// Registra un jugador en la UI, creando su retrato (stamina, vida, puntaje, etc.).
    /// Llamar cuando un CharacterBase entra al minijuego.
    /// </summary>
    public void RegisterPlayer(CharacterBase player)
    {
        if (player == null) return;

        if (playerPortraits.ContainsKey(player))
            return;

        Transform container = ffaLayoutContainer;
        if (container == null)
        {
            Debug.LogWarning("UIManager: ffaLayoutContainer no asignado.");
            return;
        }

        GameObject portraitGO = Instantiate(playerUIPortraitPrefab, container);

        PlayerUIPortrait portraitScript = portraitGO.GetComponent<PlayerUIPortrait>();
        if (portraitScript != null)
        {
            portraitScript.Initialize(player);
            playerPortraits.Add(player, portraitScript);
        }
        else
        {
            Debug.LogError("UIManager: El prefab no tiene PlayerUIPortrait.");
            Destroy(portraitGO);
        }
    }

    /// <summary>
    /// Quita al jugador de la UI cuando abandona el minijuego.
    /// </summary>
    public void UnregisterPlayer(CharacterBase player)
    {
        if (player == null) return;

        if (playerPortraits.TryGetValue(player, out PlayerUIPortrait portraitScript))
        {
            if (portraitScript != null)
            {
                Destroy(portraitScript.gameObject);
            }
            playerPortraits.Remove(player);
        }
    }

    /// <summary>
    /// Activa o desactiva el HUD de partida (los retratos).
    /// Lo usan MinigameManager / SurvivalGameManager.
    /// </summary>
    public void SetGameHUDActive(bool isActive)
    {
        if (ffaLayoutContainer != null)
        {
            ffaLayoutContainer.gameObject.SetActive(isActive);
        }
    }
}
