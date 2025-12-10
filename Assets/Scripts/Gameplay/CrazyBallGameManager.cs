using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CrazyBallGameManager : NetworkBehaviour
{
    public static CrazyBallGameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private SpikyBallHazard ball;

    [Header("Configuración")]
    [SerializeField] private float startDelay = 3.0f;
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
        RegisterAllPlayers();
        yield return new WaitForSeconds(startDelay);
        if (ball != null) ball.LaunchBall();
    }

    private void RegisterAllPlayers()
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

        Debug.Log($"[CrazyBall] Jugadores vivos: {alivePlayers.Count}");
    }

    public void OnPlayerEliminated(CharacterBase victim)
    {
        if (!IsServer || gameEnded) return;

        if (alivePlayers.Contains(victim))
        {
            alivePlayers.Remove(victim);
            AlivePlayersCount.Value = alivePlayers.Count;
            victim.KillPlayerClientRpc();

            int pointsIndex = Mathf.Clamp(deadCount, 0, pointsByRank.Length - 2);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPointsToPlayerById(victim.OwnerClientId, pointsByRank[pointsIndex]);

            deadCount++;

            if (ball != null) ball.ResetSpeed();

            // ¡AQUÍ ESTÁ LA MAGIA!
            // Ahora que CharacterBase llama a esto al morir por inactividad,
            // esta condición se cumplirá si solo queda 1.
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

        if (ball != null) ball.StopBall();

        if (alivePlayers.Count > 0)
        {
            CharacterBase winner = alivePlayers.First();
            int winPoints = pointsByRank[pointsByRank.Length - 1];

            if (GameManager.Instance != null)
                GameManager.Instance.AddPointsToPlayerById(winner.OwnerClientId, winPoints);

            Debug.Log($"[CrazyBall] Ganador: {winner.OwnerClientId}");
        }

        StartCoroutine(FinishRoutine());
    }

    private IEnumerator FinishRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        if (GameManager.Instance != null) GameManager.Instance.LoadIntermission();
    }
}