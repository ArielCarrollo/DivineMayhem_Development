using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CrownGameManagerNetcode : NetworkBehaviour
{
    public static CrownGameManagerNetcode Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private float secondsToWin = 30f;
    [SerializeField] private int pointsForWinner = 10;
    [SerializeField] private int pointsForLoser = 5;

    // YA NO NECESITAMOS ESTO:
    // [SerializeField] private IntermissionUI intermissionPanel; 

    // Estructura auxiliar local
    public struct CrownScoreData : INetworkSerializable, System.IEquatable<CrownScoreData>
    {
        public ulong PlayerId;
        public float TimeHeld;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerId);
            serializer.SerializeValue(ref TimeHeld);
        }
        public bool Equals(CrownScoreData other) => PlayerId == other.PlayerId;
    }

    public NetworkList<CrownScoreData> CrownScores = new NetworkList<CrownScoreData>();
    public NetworkVariable<ulong> CurrentKingId = new NetworkVariable<ulong>(9999);

    private List<CharacterBase> alivePlayers = new List<CharacterBase>();
    private bool gameEnded = false;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) CrownScores.Clear();
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (!IsServer) return;
        if (!alivePlayers.Contains(player))
        {
            alivePlayers.Add(player);

            bool found = false;
            foreach (var s in CrownScores) if (s.PlayerId == player.OwnerClientId) found = true;
            if (!found) CrownScores.Add(new CrownScoreData { PlayerId = player.OwnerClientId, TimeHeld = 0 });

            if (CurrentKingId.Value == 9999) TransferCrown(player);
        }
    }

    public void TransferCrown(CharacterBase newKing)
    {
        if (!IsServer || gameEnded) return;

        // Quitar visuales al anterior
        if (CurrentKingId.Value != 9999)
        {
            foreach (var p in alivePlayers)
                if (p.OwnerClientId == CurrentKingId.Value) p.IsKing.Value = false;
        }

        ulong newId = (newKing != null) ? newKing.OwnerClientId : 9999;
        CurrentKingId.Value = newId;

        if (newKing != null) newKing.IsKing.Value = true;

        // Forzar actualización inmediata de la lista para que el HUD sepa que cambió el rey rápido
        ForceSyncTime();
    }
    private void ForceSyncTime()
    {
        // Método auxiliar por si necesitas forzar lógica extra
    }
    private void Update()
    {
        if (!IsServer || gameEnded) return;

        // Sumar tiempo al rey actual
        if (CurrentKingId.Value != 9999)
        {
            // 1. Acumular tiempo en memoria (sin tocar la red aún)
            for (int i = 0; i < CrownScores.Count; i++)
            {
                if (CrownScores[i].PlayerId == CurrentKingId.Value)
                {
                    var data = CrownScores[i];
                    data.TimeHeld += Time.deltaTime;
                    CrownScores[i] = data; // Guardamos en la lista localmente

                    if (data.TimeHeld >= secondsToWin) EndGame(CurrentKingId.Value);
                    break;
                }
            }
        }

        // 2. Condición de victoria por eliminación
        if (alivePlayers.Count <= 1 && CrownScores.Count > 1)
        {
            if (alivePlayers.Count == 1) EndGame(alivePlayers[0].OwnerClientId);
            else EndGameWithTimeCheck();
        }

        // 3. Sincronización periódica (NetworkList dirty)
        // La asignación CrownScores[i] = data marca "dirty" automáticamente, 
        // pero Unity lo enviará al final del frame. Hacerlo cada frame es mucho.
        // *Nota*: En Netcode actual, asignar a la lista ya dispara el evento.
        // Si tienes problemas de rendimiento, podrías usar un diccionario local y pasarlo a la lista cada X tiempo.
        // Pero con 4 jugadores, este código está bien. Si sigue en 0, es que CurrentKingId es 9999.
    }
    public void OnPlayerDied(CharacterBase player)
    {
        if (!IsServer) return;
        if (alivePlayers.Contains(player))
        {
            alivePlayers.Remove(player);
            if (player.IsKing.Value)
            {
                CharacterBase next = (alivePlayers.Count > 0) ? alivePlayers[Random.Range(0, alivePlayers.Count)] : null;
                TransferCrown(next);
            }
        }
    }

    private void EndGameWithTimeCheck()
    {
        ulong bestId = 9999;
        float bestTime = -1f;
        foreach (var s in CrownScores)
        {
            if (s.TimeHeld > bestTime)
            {
                bestTime = s.TimeHeld;
                bestId = s.PlayerId;
            }
        }
        EndGame(bestId);
    }

    private void EndGame(ulong winnerId)
    {
        if (gameEnded) return;
        gameEnded = true;

        // --- REPARTIR PUNTOS ---
        if (GameManager.Instance != null)
        {
            foreach (var scoreData in CrownScores)
            {
                ulong pid = scoreData.PlayerId;
                int points = (pid == winnerId) ? pointsForWinner : pointsForLoser;
                GameManager.Instance.AddPointsToPlayerById(pid, points);
            }

            Debug.Log("Juego terminado. Cargando Intermission...");
            // Llamamos a la transición en GameManager
            GameManager.Instance.LoadIntermission();
        }
    }
}