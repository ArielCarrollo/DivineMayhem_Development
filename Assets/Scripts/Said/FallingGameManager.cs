using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class FallingGameManager : NetworkBehaviour
{
    public static FallingGameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private FallingMapController mapController;

    [Header("Dificultad")]
    [SerializeField] private float initialSpawnRate = 2.0f;
    [SerializeField] private float minSpawnRate = 0.5f;
    [SerializeField] private float difficultyRampUp = 0.1f;
    [SerializeField] private int blocksPerCycle = 1;

    [Header("Puntos")]
    [SerializeField] private int[] pointsByRank = new int[] { 5, 10, 15, 30 };

    public NetworkVariable<int> AlivePlayersCount = new NetworkVariable<int>(0);

    private HashSet<CharacterBase> alivePlayers = new HashSet<CharacterBase>();
    private int deadCount = 0;
    private bool gameEnded = false;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) StartCoroutine(GameLoopRoutine());
    }

    private IEnumerator GameLoopRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        RegisterPlayers();
        yield return new WaitForSeconds(2.0f);

        float currentRate = initialSpawnRate;

        while (!gameEnded)
        {
            if (mapController != null) mapController.DropRandomBlocks(blocksPerCycle);

            yield return new WaitForSeconds(currentRate);
            currentRate = Mathf.Max(minSpawnRate, currentRate - difficultyRampUp);
        }
    }

    private void RegisterPlayers()
    {
        alivePlayers.Clear();
        var players = FindObjectsOfType<CharacterBase>();
        foreach (var p in players)
        {
            if (p.gameObject.activeInHierarchy) alivePlayers.Add(p);
        }
        AlivePlayersCount.Value = alivePlayers.Count;
        deadCount = 0;
        gameEnded = false;

        Debug.Log($"[FallingFloor] Iniciado con {alivePlayers.Count} jugadores.");
    }

    public void OnPlayerFell(CharacterBase victim)
    {
        if (!IsServer || gameEnded) return;

        if (alivePlayers.Contains(victim))
        {
            // 1. Eliminar
            alivePlayers.Remove(victim);
            AlivePlayersCount.Value = alivePlayers.Count;
            victim.KillPlayerClientRpc();

            // 2. Puntos Derrota
            int pointsIndex = Mathf.Clamp(deadCount, 0, pointsByRank.Length - 2);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPointsToPlayerById(victim.OwnerClientId, pointsByRank[pointsIndex]);

            deadCount++;

            // 3. VICTORIA AUTOMÁTICA
            // Si queda 1, se acaba inmediatamente. No esperamos a que caiga.
            if (alivePlayers.Count <= 1)
            {
                EndGame();
            }
        }
    }

    private void EndGame()
    {
        if (gameEnded) return;
        gameEnded = true;

        if (alivePlayers.Count > 0)
        {
            CharacterBase winner = alivePlayers.First();
            int winPoints = pointsByRank[pointsByRank.Length - 1];

            if (GameManager.Instance != null)
                GameManager.Instance.AddPointsToPlayerById(winner.OwnerClientId, winPoints);

            Debug.Log($"[FallingFloor] Ganador: Player {winner.OwnerClientId}");
        }

        StartCoroutine(FinishRoutine());
    }

    private IEnumerator FinishRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadIntermission();
        }
    }
}