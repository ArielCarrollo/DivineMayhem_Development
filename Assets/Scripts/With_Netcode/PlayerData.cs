using Unity.Collections;
using Unity.Netcode;
using System;

[System.Serializable]
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes Username;
    public bool IsReady;

    public int Level;
    public int CurrentXP;

    public int BodyIndex;
    public int EyesIndex;
    public int GlovesIndex;

    // --- Campos adicionales para el perfil del jugador ---
    // Descripción personal del jugador (hasta 512 caracteres).
    public FixedString512Bytes Description;
    // Fecha de cumpleaños en formato string (hasta 32 caracteres).
    public FixedString32Bytes BirthDate;
    // Estado personal del jugador (hasta 128 caracteres).
    public FixedString128Bytes Status;
    // Clave de imagen de perfil predefinida. Almacena un índice o identificador de la imagen.
    public FixedString64Bytes ProfileImageKey;
    // Contiene la imagen de perfil personalizada en formato base64 comprimido y reducido.
    // Esta cadena se replica a través de la red para que otros jugadores puedan ver el avatar importado.
    public FixedString4096Bytes ProfileImageBase64;
    // Indica si el jugador inició sesión de forma anónima. Sirve para deshabilitar opciones como el perfil.
    public bool IsAnonymous;

    public PlayerData(ulong clientId, string username, bool isReady = false)
    {
        ClientId = clientId;
        Username = new FixedString64Bytes(username);
        IsReady = isReady;

        Level = 1;
        CurrentXP = 0;

        BodyIndex = 0;
        EyesIndex = 0;
        GlovesIndex = 0;

        // Inicializar campos de perfil con valores por defecto
        Description = new FixedString512Bytes("");
        BirthDate = new FixedString32Bytes("");
        Status = new FixedString128Bytes("");
        ProfileImageKey = new FixedString64Bytes("0");
        IsAnonymous = false;

        // Inicializar la base64 de imagen personalizada vacía
        ProfileImageBase64 = new FixedString4096Bytes("");
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Username);
        serializer.SerializeValue(ref IsReady);

        // --- Serializar nuevos datos ---
        serializer.SerializeValue(ref Level);
        serializer.SerializeValue(ref CurrentXP);

        serializer.SerializeValue(ref BodyIndex);
        serializer.SerializeValue(ref EyesIndex);
        serializer.SerializeValue(ref GlovesIndex);

        // Serializar campos adicionales del perfil
        serializer.SerializeValue(ref Description);
        serializer.SerializeValue(ref BirthDate);
        serializer.SerializeValue(ref Status);
        serializer.SerializeValue(ref ProfileImageKey);
        serializer.SerializeValue(ref IsAnonymous);
        // Serializar la imagen de perfil en base64
        serializer.SerializeValue(ref ProfileImageBase64);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               Username == other.Username &&
               IsReady == other.IsReady &&
               Level == other.Level &&           // --- Añadido a la comparación ---
               CurrentXP == other.CurrentXP &&   // --- Añadido a la comparación ---
               BodyIndex == other.BodyIndex &&
               EyesIndex == other.EyesIndex &&
               GlovesIndex == other.GlovesIndex &&
               // Comparar campos adicionales del perfil
               Description == other.Description &&
               BirthDate == other.BirthDate &&
               Status == other.Status &&
               ProfileImageKey == other.ProfileImageKey &&
               IsAnonymous == other.IsAnonymous &&
               ProfileImageBase64 == other.ProfileImageBase64;
    }
}