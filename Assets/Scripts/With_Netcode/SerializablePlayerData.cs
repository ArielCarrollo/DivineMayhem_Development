using System;
using Unity.Collections;

[Serializable]
public class SerializablePlayerData
{
    public ulong ClientId;
    public string Username;
    public bool IsReady;
    public int Points;

    // Nuevo campo
    public int PantheonIndex;

    public string Description;
    public string BirthDate;
    public string Status;
    public string ProfileImageKey;
    public string ProfileImageBase64;
    public bool IsAnonymous;

    public SerializablePlayerData() { }

    public SerializablePlayerData(PlayerData data)
    {
        ClientId = data.ClientId;
        Username = data.Username.ToString();
        IsReady = data.IsReady;
        Points = data.Points;

        PantheonIndex = data.PantheonIndex;

        Description = data.Description.ToString();
        BirthDate = data.BirthDate.ToString();
        Status = data.Status.ToString();
        ProfileImageKey = data.ProfileImageKey.ToString();
        ProfileImageBase64 = data.ProfileImageBase64.ToString();
        IsAnonymous = data.IsAnonymous;
    }

    public PlayerData ToPlayerData(ulong clientId)
    {
        PlayerData pd = new PlayerData(clientId, Username ?? string.Empty);
        pd.IsReady = IsReady;
        pd.Points = Points;

        pd.PantheonIndex = PantheonIndex;

        pd.Description = new FixedString512Bytes(Description ?? string.Empty);
        pd.BirthDate = new FixedString32Bytes(BirthDate ?? string.Empty);
        pd.Status = new FixedString128Bytes(Status ?? string.Empty);
        pd.ProfileImageKey = new FixedString64Bytes(ProfileImageKey ?? string.Empty);
        pd.ProfileImageBase64 = new FixedString4096Bytes(ProfileImageBase64 ?? string.Empty);
        pd.IsAnonymous = IsAnonymous;
        return pd;
    }
}