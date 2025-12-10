using UnityEngine;
using UnityEngine.UI; // ¡Importante para el Slider!
using Unity.Netcode;

public struct PlayerScore : INetworkSerializable, System.IEquatable<PlayerScore>
{
    public ulong PlayerId;
    public int Score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref Score);
    }

    public bool Equals(PlayerScore other)
    {
        return PlayerId == other.PlayerId && Score == other.Score;
    }
}