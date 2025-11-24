using System;
using Unity.Collections;

/// <summary>
/// Clase serializable que contiene sólo tipos nativos para almacenar el progreso del jugador en Cloud Save.
/// Se utiliza para convertir desde y hacia <see cref="PlayerData"/> evitando problemas con FixedString.
/// Ahora incluye el campo de puntos acumulados.
/// </summary>
[Serializable]
public class SerializablePlayerData
{
    public ulong ClientId;
    public string Username;
    public bool IsReady;
    // Para mantener compatibilidad con versiones anteriores se conservan los campos Level y CurrentXP,
    // aunque ya no se utilicen en el nuevo sistema de puntos.
    public int Level;
    public int CurrentXP;
    public int Points;
    public int BodyIndex;
    public int EyesIndex;
    public int GlovesIndex;
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
        // Los campos de nivel y experiencia se establecen a cero por compatibilidad.
        Level = 0;
        CurrentXP = 0;
        Points = data.Points;
        BodyIndex = data.BodyIndex;
        EyesIndex = data.EyesIndex;
        GlovesIndex = data.GlovesIndex;
        Description = data.Description.ToString();
        BirthDate = data.BirthDate.ToString();
        Status = data.Status.ToString();
        ProfileImageKey = data.ProfileImageKey.ToString();
        ProfileImageBase64 = data.ProfileImageBase64.ToString();
        IsAnonymous = data.IsAnonymous;
    }

    /// <summary>
    /// Convierte esta instancia serializable en un <see cref="PlayerData"/>. Se puede especificar un ClientId diferente.
    /// </summary>
    public PlayerData ToPlayerData(ulong clientId)
    {
        PlayerData pd = new PlayerData(clientId, Username ?? string.Empty);
        pd.IsReady = IsReady;
        pd.Points = Points;
        pd.BodyIndex = BodyIndex;
        pd.EyesIndex = EyesIndex;
        pd.GlovesIndex = GlovesIndex;
        pd.Description = new FixedString512Bytes(Description ?? string.Empty);
        pd.BirthDate = new FixedString32Bytes(BirthDate ?? string.Empty);
        pd.Status = new FixedString128Bytes(Status ?? string.Empty);
        pd.ProfileImageKey = new FixedString64Bytes(ProfileImageKey ?? string.Empty);
        pd.ProfileImageBase64 = new FixedString4096Bytes(ProfileImageBase64 ?? string.Empty);
        pd.IsAnonymous = IsAnonymous;
        return pd;
    }
}