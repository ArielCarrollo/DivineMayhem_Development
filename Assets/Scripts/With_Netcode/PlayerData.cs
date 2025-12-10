using Unity.Collections;
using Unity.Netcode;
using System;

[System.Serializable]
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes Username;
    public bool IsReady;

    public int Points;
    public int LastAddedPoints; // <--- NUEVO CAMPO

    // 0: Inka, 1: Sintoísta, 2: Griego, 3: Nórdico
    public int PantheonIndex;

    public FixedString512Bytes Description;
    public FixedString32Bytes BirthDate;
    public FixedString128Bytes Status;
    public FixedString64Bytes ProfileImageKey;
    public FixedString4096Bytes ProfileImageBase64;
    public bool IsAnonymous;

    public PlayerData(ulong clientId, string username, bool isReady = false)
    {
        ClientId = clientId;
        Username = new FixedString64Bytes(username);
        IsReady = isReady;
        Points = 0;
        LastAddedPoints = 0; // Inicializar

        PantheonIndex = 0;

        Description = new FixedString512Bytes("");
        BirthDate = new FixedString32Bytes("");
        Status = new FixedString128Bytes("");
        ProfileImageKey = new FixedString64Bytes("0");
        ProfileImageBase64 = new FixedString4096Bytes("");
        IsAnonymous = false;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Username);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref Points);
        serializer.SerializeValue(ref LastAddedPoints); // Serializar

        serializer.SerializeValue(ref PantheonIndex);

        serializer.SerializeValue(ref Description);
        serializer.SerializeValue(ref BirthDate);
        serializer.SerializeValue(ref Status);
        serializer.SerializeValue(ref ProfileImageKey);
        serializer.SerializeValue(ref IsAnonymous);
        serializer.SerializeValue(ref ProfileImageBase64);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               Username == other.Username &&
               IsReady == other.IsReady &&
               Points == other.Points &&
               LastAddedPoints == other.LastAddedPoints && // Comparar
               PantheonIndex == other.PantheonIndex &&
               Description == other.Description &&
               BirthDate == other.BirthDate &&
               Status == other.Status &&
               ProfileImageKey == other.ProfileImageKey &&
               IsAnonymous == other.IsAnonymous &&
               ProfileImageBase64 == other.ProfileImageBase64;
    }
}