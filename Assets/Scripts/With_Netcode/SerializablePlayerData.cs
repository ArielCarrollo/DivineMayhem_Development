using System;

/// <summary>
/// Clase auxiliar para serializar los datos del jugador a Cloud Save.
/// Almacena campos de PlayerData en tipos que JSON puede manejar fácilmente.
/// </summary>
[Serializable]
public class SerializablePlayerData
{
    public ulong ClientId;
    public string Username;
    public bool IsReady;
    public int Level;
    public int CurrentXP;
    public int BodyIndex;
    public int EyesIndex;
    public int GlovesIndex;
    public string Description;
    public string BirthDate;
    public string Status;
    public string ProfileImageKey;
    public bool IsAnonymous;
    // Nota: podrían añadirse más campos en el futuro según PlayerData

    public SerializablePlayerData() {}

    /// <summary>
    /// Crea un SerializablePlayerData a partir de un PlayerData.
    /// Convierte las estructuras FixedString a strings normales.
    /// </summary>
    public SerializablePlayerData(PlayerData data)
    {
        ClientId = data.ClientId;
        Username = data.Username.ToString();
        IsReady = data.IsReady;
        Level = data.Level;
        CurrentXP = data.CurrentXP;
        BodyIndex = data.BodyIndex;
        EyesIndex = data.EyesIndex;
        GlovesIndex = data.GlovesIndex;
        Description = data.Description.ToString();
        BirthDate = data.BirthDate.ToString();
        Status = data.Status.ToString();
        ProfileImageKey = data.ProfileImageKey.ToString();
        IsAnonymous = data.IsAnonymous;
    }

    /// <summary>
    /// Convierte este objeto serializable en un PlayerData. Se requiere proporcionar un clientId externo
    /// porque el ClientId almacenado en la nube puede no coincidir con el ID asignado por la sesión actual.
    /// </summary>
    public PlayerData ToPlayerData(ulong clientId)
    {
        // Crear nuevo PlayerData usando username para inicializar
        var player = new PlayerData(clientId, string.IsNullOrEmpty(Username) ? "" : Username, IsReady);
        player.Level = Level;
        player.CurrentXP = CurrentXP;
        player.BodyIndex = BodyIndex;
        player.EyesIndex = EyesIndex;
        player.GlovesIndex = GlovesIndex;
        player.Description = new Unity.Collections.FixedString512Bytes(Description ?? string.Empty);
        player.BirthDate = new Unity.Collections.FixedString32Bytes(BirthDate ?? string.Empty);
        player.Status = new Unity.Collections.FixedString128Bytes(Status ?? string.Empty);
        player.ProfileImageKey = new Unity.Collections.FixedString64Bytes(ProfileImageKey ?? string.Empty);
        player.IsAnonymous = IsAnonymous;
        return player;
    }
}