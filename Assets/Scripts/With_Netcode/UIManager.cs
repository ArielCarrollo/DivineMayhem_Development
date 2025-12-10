using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Netcode;

//public class UIManager : MonoBehaviour
//{
//    public static UIManager Instance { get; private set; }

//    [Header("UI Prefabs")]
//    [SerializeField] private GameObject playerUIPortraitPrefab;

//    [Header("Contenedores de Layout")]
//    [SerializeField] private Transform ffaLayoutContainer; 
 
//    private Dictionary<ulong, PlayerUIPortrait> playerPortraits = new Dictionary<ulong, PlayerUIPortrait>();

//    private void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//        }
//        else
//        {
//            Instance = this;
//        }
//    }

//    public void RegisterPlayer(CharacterBase player)
//    {
//        ulong playerID = player.OwnerClientId;

//        if (playerPortraits.ContainsKey(playerID)) return;

//        Transform container = ffaLayoutContainer;

//        GameObject portraitGO = Instantiate(playerUIPortraitPrefab, container);

//        PlayerUIPortrait portraitScript = portraitGO.GetComponent<PlayerUIPortrait>();
//        portraitScript.Initialize(player);

//        playerPortraits.Add(playerID, portraitScript);
//    }

    
//    public void UnregisterPlayer(CharacterBase player)
//    {
//        ulong playerID = player.OwnerClientId;

//        if (playerPortraits.TryGetValue(playerID, out PlayerUIPortrait portraitScript))
//        {
//            Destroy(portraitScript.gameObject);

//            playerPortraits.Remove(playerID);
//        }
//    }
//    public void SetGameHUDActive(bool isActive)
//    {
//        if (ffaLayoutContainer != null)
//        {
//            ffaLayoutContainer.gameObject.SetActive(isActive);
//        }
//    }
//}