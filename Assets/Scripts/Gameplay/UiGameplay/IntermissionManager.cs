using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class IntermissionUI : NetworkBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI roundTitleText;
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private GameObject rowPrefab;

    [Header("Pantalla Ganador")]
    [SerializeField] private GameObject winnerPanel;
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private Image winnerIcon; // <--- NUEVO: Icono del ganador

    [Header("Configuración")]
    [SerializeField] private float timeBeforeNextRound = 5f;

    [Header("Info Rondas")]
    [SerializeField] private TextMeshProUGUI roundInfoText;

    [Header("Sprites de Panteones")]
    [Tooltip("Orden: 0:Inca, 1:Shinto, 2:Greek, 3:Norse. Debe coincidir con el Lobby.")]
    [SerializeField] private Sprite[] pantheonSprites; // <--- NUEVO: Sprites para mostrar

    private readonly string[] pantheonNames = { "Inca", "Shinto", "Greek", "Norse" };

    public override void OnNetworkSpawn()
    {
        StartCoroutine(DisplayResultsRoutine());
    }

    private IEnumerator DisplayResultsRoutine()
    {
        yield return null;

        if (GameManager.Instance == null) yield break;

        if (roundInfoText != null)
        {
            int cur = GameManager.Instance.CurrentRound.Value;
            int tot = GameManager.Instance.TotalRounds.Value;
            roundInfoText.text = $"ROUND {cur} / {tot}";
        }

        if (rowsContainer != null) foreach (Transform child in rowsContainer) Destroy(child.gameObject);
        if (winnerPanel != null) winnerPanel.SetActive(false);
        if (roundTitleText != null) roundTitleText.text = "RESULTS";

        var sortedPlayers = new List<PlayerData>();
        foreach (var p in GameManager.Instance.PlayersInLobby) sortedPlayers.Add(p);
        sortedPlayers.Sort((a, b) => b.Points.CompareTo(a.Points));

        float delay = 0.2f;
        foreach (var p in sortedPlayers)
        {
            if (rowPrefab != null && rowsContainer != null)
            {
                GameObject go = Instantiate(rowPrefab, rowsContainer);
                var rowScript = go.GetComponent<IntermissionPlayerRow>();

                if (rowScript != null)
                {
                    string pName = (p.PantheonIndex >= 0 && p.PantheonIndex < pantheonNames.Length)
                        ? pantheonNames[p.PantheonIndex] : "Unknown";
                    Color col = GetColor((int)p.ClientId);

                    rowScript.Setup(p.Username.ToString(), p.Points, p.LastAddedPoints, col, pName);

                    rowScript.AnimateEntrance(delay);
                    rowScript.AnimateScore(p.Points - p.LastAddedPoints, p.Points, delay + 0.5f);
                }
            }
            delay += 0.2f;
        }

        yield return new WaitForSeconds(timeBeforeNextRound);

        if (IsServer)
        {
            int curRound = GameManager.Instance.CurrentRound.Value;
            int totalRounds = GameManager.Instance.TotalRounds.Value;

            if (curRound >= totalRounds)
            {
                ulong winnerId = sortedPlayers.Count > 0 ? sortedPlayers[0].ClientId : 999;
                ShowFinalWinnerClientRpc(winnerId);
                yield return new WaitForSeconds(5f);
            }

            GameManager.Instance.AdvanceRoundOrFinish();
        }
    }

    [ClientRpc]
    private void ShowFinalWinnerClientRpc(ulong winnerId)
    {
        string winnerName = "Unknown";
        Color col = Color.white;
        string pName = "";
        int pIndex = -1;

        if (GameManager.Instance != null)
        {
            foreach (var p in GameManager.Instance.PlayersInLobby)
            {
                if (p.ClientId == winnerId)
                {
                    winnerName = p.Username.ToString();
                    col = GetColor((int)winnerId);
                    pIndex = p.PantheonIndex;
                    if (p.PantheonIndex >= 0 && p.PantheonIndex < pantheonNames.Length)
                        pName = pantheonNames[p.PantheonIndex];
                    break;
                }
            }
        }

        if (winnerPanel != null)
        {
            winnerPanel.SetActive(true);
            if (winnerNameText != null)
            {
                winnerNameText.text = $"¡VICTORY OF PANTHEON {pName.ToUpper()}!\n{winnerName}";
                winnerNameText.color = col;
            }

            // --- MOSTRAR ICONO ---
            if (winnerIcon != null)
            {
                if (pantheonSprites != null && pIndex >= 0 && pIndex < pantheonSprites.Length)
                {
                    winnerIcon.sprite = pantheonSprites[pIndex];
                    winnerIcon.gameObject.SetActive(true);
                }
                else
                {
                    winnerIcon.gameObject.SetActive(false);
                }
            }
        }
    }

    private Color GetColor(int index)
    {
        switch (index % 4)
        {
            case 0: return Color.blue;
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }
}