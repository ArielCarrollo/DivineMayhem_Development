using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models; // <-- NUEVO using
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

public class CloudAuthManager : MonoBehaviour
{
    public static CloudAuthManager Instance { get; private set; }

    public event Action OnSignInSuccess;
    public event Action<string> OnSignInFailed;
    public event Action<string> OnPlayerNameUpdated;

    public PlayerData LocalPlayerData { get; private set; }
    private const string PLAYER_PROGRESS_KEY = "PLAYER_PROGRESS_DATA";
    // Clave para almacenar la imagen de perfil en formato base64 en Cloud Save
    private const string PLAYER_PROFILE_IMAGE_KEY = "PLAYER_PROFILE_IMAGE";
    private string playerId;
    private string playerName;

    // Base64 de la imagen de perfil cargada desde Cloud Save
    public string LocalProfileImageBase64 { get; private set; } = string.Empty;

    // Indicador de si el jugador actual usó login anónimo
    // Se delega a LocalPlayerData.IsAnonymous, pero se mantiene para compatibilidad
    public bool IsAnonymousUser => LocalPlayerData.IsAnonymous;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async Task InitializeUnityServices()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;

        try
        {
            var options = new InitializationOptions();
            options.SetEnvironmentName("production");
            await UnityServices.InitializeAsync(options);
            Debug.Log("Unity Services Initialized: " + UnityServices.State);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to initialize Unity Services: " + e);
            OnSignInFailed?.Invoke("Error al inicializar servicios.");
        }
    }

    public async Task SignUpWithUsernamePassword(string username, string password)
    {
        SignOutIfSignedIn();
        await InitializeUnityServices();
        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = username;

            Debug.Log($"Sign Up & Sign In Successful. Player ID: {playerId}, Player Name: {playerName}");

            await UpdatePlayerNameAsync(username);
            LocalPlayerData = new PlayerData(0, username);
            // Como es un registro con usuario/contraseña, no es anónimo
            var tmpData = LocalPlayerData;
            tmpData.IsAnonymous = false;
            LocalPlayerData = tmpData;
            await SavePlayerProgress();
            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            string errorMessage = ConvertExceptionToMessage(ex);
            Debug.LogException(ex);
            OnSignInFailed?.Invoke(errorMessage);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión. Inténtalo de nuevo.");
        }
    }
    public async Task SignInWithUnityPlayerAccount(bool isSigningUp = false)
    {
        // Cerramos cualquier sesión anterior (anónima o de usuario/contraseña)
        SignOutIfSignedIn();

        await InitializeUnityServices();

        // Evitar suscribirse dos veces
        PlayerAccountService.Instance.SignedIn -= OnUnityPlayerAccountSignedIn;
        PlayerAccountService.Instance.SignedIn += OnUnityPlayerAccountSignedIn;

        try
        {
            // Esto es lo que abre el navegador y muestra la pantalla de Unity / Google / etc.
            await PlayerAccountService.Instance.StartSignInAsync(isSigningUp);
        }
        catch (PlayerAccountsException ex)
        {
            Debug.LogError("Error al iniciar flujo de Unity Player Accounts: " + ex);
            OnSignInFailed?.Invoke("No se pudo abrir el inicio de sesión de Unity. Revisa tu conexión.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError("Error al iniciar flujo de Unity Player Accounts (RequestFailed): " + ex);
            OnSignInFailed?.Invoke("Error de conexión. Inténtalo de nuevo.");
        }
    }

    public async Task UpdatePlayerNameAsync(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            Debug.LogError("El nombre no puede estar vacío.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(newName);
            this.playerName = newName;
            Debug.Log($"Nombre actualizado exitosamente a: {newName}");

        // Actualizar también LocalPlayerData.Username y guardar en la nube. Esto garantiza
        // que al volver a iniciar sesión se cargue el nombre correcto desde Cloud Save.
        try
        {
            // Si LocalPlayerData está inicializado, actualizamos su nombre y guardamos.
            var tmp = LocalPlayerData;
            tmp.Username = new FixedString64Bytes(newName);
            UpdateLocalData(tmp);
            await SavePlayerProgress();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al guardar el nuevo nombre en PlayerData: {ex}");
        }
            OnPlayerNameUpdated?.Invoke(newName);
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }
    public async Task SignInWithUsernamePassword(string username, string password)
    {
        SignOutIfSignedIn();
        await InitializeUnityServices();
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            Debug.Log($"Sign In Successful. Player ID: {playerId}, Player Name: {playerName}");
            await LoadPlayerProgress();

            // Marcar que no es anónimo
            if (LocalPlayerData.IsAnonymous)
            {
                var tmp = LocalPlayerData;
                tmp.IsAnonymous = false;
                UpdateLocalData(tmp);
                await SavePlayerProgress();
            }

            // ✅ aquí sin "!= null"
            if (LocalPlayerData.Username.Length == 0 && !string.IsNullOrWhiteSpace(playerName))
            {
                var tmp = LocalPlayerData;                                   // copiar struct
                tmp.Username = new Unity.Collections.FixedString64Bytes(playerName);
                UpdateLocalData(tmp);                                        // reasignar a la propiedad
            }

            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException)
        {
            OnSignInFailed?.Invoke("Usuario o contraseña incorrectos.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión. Inténtalo de nuevo.");
        }
    }

    public async Task SignInAnonymouslyIfNeeded()
    {
        // Aseguramos servicios
        await InitializeUnityServices();

        // Si ya hay sesión, solo usamos esa y nos aseguramos de tener datos locales
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("CloudAuthManager: ya había una sesión activa, usando la existente (puede ser anónima).");

            // Si LocalPlayerData está “vacío”, nos aseguramos de rellenarlo
            if (LocalPlayerData.Username.Length == 0)
            {
                string authName = GetPlayerName();
                if (!string.IsNullOrWhiteSpace(authName))
                {
                    var tmp = LocalPlayerData;
                    tmp.Username = new FixedString64Bytes(authName);
                    UpdateLocalData(tmp);
                }
                else
                {
                    // Nombre fallback
                    LocalPlayerData = new PlayerData(0, "Player");
                }
            }

            OnSignInSuccess?.Invoke();
            return;
        }

        // Si no había sesión, creamos una anónima
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            playerId = AuthenticationService.Instance.PlayerId;
            // Puede que GetPlayerNameAsync devuelva vacío para anónimos, pero lo intentamos
            try
            {
                playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
            }
            catch
            {
                playerName = null;
            }

            Debug.Log($"CloudAuthManager: sesión anónima creada. PlayerID: {playerId}, Name: {playerName}");

            // Cargamos progreso desde Cloud Save (si hay)
            await LoadPlayerProgress();

            // Aseguramos que el PlayerData tenga un nombre visible
            if (LocalPlayerData.Username.Length == 0)
            {
                string authName = GetPlayerName();
                string nameToUse = string.IsNullOrWhiteSpace(authName)
                    ? $"Invitado_{playerId.Substring(0, 6)}"
                    : authName;

                var tmp = LocalPlayerData;
                tmp.Username = new FixedString64Bytes(nameToUse);
                UpdateLocalData(tmp);
            }


            // Marcar como anónimo
            if (!LocalPlayerData.IsAnonymous)
            {
                var tmp = LocalPlayerData;
                tmp.IsAnonymous = true;
                UpdateLocalData(tmp);
            }

            // Guardamos por si acaso
            await SavePlayerProgress();

            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("No se pudo iniciar sesión anónima.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión al iniciar sesión anónima.");
        }
    }

    private string ConvertExceptionToMessage(AuthenticationException ex)
    {
        // https://docs.unity.com/authentication/manual/exception-codes
        switch (ex.ErrorCode)
        {
            case 10200: // USERNAME_EXISTS
                return "Este nombre de usuario ya está en uso.";
            case 10202: // INVALID_PASSWORD
                return "La contraseña no es válida. Debe tener al menos 8 caracteres.";
            case 10203: // INVALID_USERNAME
                return "El nombre de usuario no es válido.";
            default:
                return "Error desconocido en el registro.";
        }
    }
    public async Task LoadPlayerProgress()
    {
        // Usaremos una variable fuera del try para tener acceso después
        Dictionary<string, Item> serverData = null; // <-- AQUÍ EL CAMBIO

        try
        {
            // Solicitamos también la clave de imagen de perfil
            serverData = await CloudSaveService.Instance.Data.Player
                .LoadAsync(new HashSet<string> { PLAYER_PROGRESS_KEY, PLAYER_PROFILE_IMAGE_KEY });

            if (serverData.TryGetValue(PLAYER_PROGRESS_KEY, out var data))
            {
                string jsonData = data.Value.GetAs<string>();
                // Deserializamos el JSON a SerializablePlayerData
                SerializablePlayerData loadedSerializable = null;
                try
                {
                    loadedSerializable = JsonConvert.DeserializeObject<SerializablePlayerData>(jsonData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error al deserializar datos de Cloud Save: {ex}");
                }
                if (loadedSerializable != null)
                {
                    Debug.Log("Datos del jugador cargados desde la nube.");
                    // Convertimos a PlayerData usando ClientId 0 (se actualizará al entrar en la lobby)
                    var loadedPlayer = loadedSerializable.ToPlayerData(0);
                    // Si no viene nombre, lo reinyectamos desde el auth
                    string authName = GetPlayerName();
                    if (loadedPlayer.Username.Length == 0 && !string.IsNullOrWhiteSpace(authName))
                    {
                        loadedPlayer.Username = new Unity.Collections.FixedString64Bytes(authName);
                        Debug.Log($"CloudAuthManager: nombre reinyectado desde Auth: {authName}");
                    }
                    LocalPlayerData = loadedPlayer;
                }
                else
                {
                    Debug.LogError("No se pudo deserializar los datos del jugador. Generando datos por defecto.");
                    string authName = GetPlayerName();
                    LocalPlayerData = new PlayerData(0,
                        string.IsNullOrWhiteSpace(authName) ? "Player" : authName);
                }
            }
            else
            {
                Debug.Log("No se encontraron datos en la nube. Creando datos locales por defecto.");
                string authName = GetPlayerName();
                LocalPlayerData = new PlayerData(0,
                    string.IsNullOrWhiteSpace(authName) ? "Player" : authName);
                await SavePlayerProgress();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error al cargar los datos del jugador: " + e);
            string authName = GetPlayerName();
            LocalPlayerData = new PlayerData(0,
                string.IsNullOrWhiteSpace(authName) ? "Player" : authName);
        }

        // Cargar imagen de perfil base64 si existe
        if (serverData != null)
        {
            try
            {
                if (serverData.TryGetValue(PLAYER_PROFILE_IMAGE_KEY, out var imgData))
                {
                    LocalProfileImageBase64 = imgData.Value.GetAs<string>();
                }
                else
                {
                    LocalProfileImageBase64 = string.Empty;
                }
            }
            catch
            {
                LocalProfileImageBase64 = string.Empty;
            }
        }
        else
        {
            // Si no hubo respuesta de la nube, reseteamos la imagen
            LocalProfileImageBase64 = string.Empty;
        }

        // Actualizar también el campo de PlayerData que se replica en red con la imagen base64 personalizada
        try
        {
            var tmpPlayer = LocalPlayerData;
            // Asegurarse de que la cadena no exceda el tamaño máximo del FixedString4096Bytes
            // Convertir a un tamaño reducido para uso en la red. Esto previene excepciones de truncado
            string networkB64 = ConvertBase64ToNetworkBase64(LocalProfileImageBase64);
            // Truncar si excede el límite del FixedString
            if (networkB64 != null && networkB64.Length > 4095)
            {
                networkB64 = networkB64.Substring(0, 4095);
            }
            tmpPlayer.ProfileImageBase64 = new FixedString4096Bytes(networkB64 ?? string.Empty);
            UpdateLocalData(tmpPlayer);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al asignar ProfileImageBase64 al PlayerData: {ex}");
        }
    }
    private async void OnUnityPlayerAccountSignedIn()
    {
        // Importante: este evento se dispara cuando el navegador ya devolvió los tokens de Unity Player Accounts
        try
        {
            // Aquí creas/inicias sesión en Authentication con el token de Unity Player Accounts
            await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            Debug.Log($"Sign In via Unity Player Accounts OK. Player ID: {playerId}, Player Name: {playerName}");

            // Cargamos progreso desde Cloud Save como en el login normal
            await LoadPlayerProgress();

            // Si tu PlayerData aún no tiene nombre, lo rellenamos con el de Unity
            if (LocalPlayerData.Username.Length == 0 && !string.IsNullOrWhiteSpace(playerName))
            {
                var tmp = LocalPlayerData;
                tmp.Username = new FixedString64Bytes(playerName);
                UpdateLocalData(tmp);
            }

            // Al usar cuenta de Unity dejamos de ser anónimos
            if (LocalPlayerData.IsAnonymous)
            {
                var tmp = LocalPlayerData;
                tmp.IsAnonymous = false;
                UpdateLocalData(tmp);
                await SavePlayerProgress();
            }

            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error al iniciar sesión con la cuenta de Unity.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión al validar la cuenta de Unity.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error inesperado en OnUnityPlayerAccountSignedIn: " + ex);
            OnSignInFailed?.Invoke("Error inesperado al iniciar sesión.");
        }
        finally
        {
            // Evitar múltiples suscripciones
            PlayerAccountService.Instance.SignedIn -= OnUnityPlayerAccountSignedIn;
        }
    }


    public async Task SavePlayerProgress()
    {
        try
        {
            // Serializamos LocalPlayerData a SerializablePlayerData para evitar problemas con FixedString
            var serializable = new SerializablePlayerData(LocalPlayerData);
            string jsonData = JsonConvert.SerializeObject(serializable);
            var dataToSave = new Dictionary<string, object>
            {
                { PLAYER_PROGRESS_KEY, jsonData }
            };
            // Siempre guardamos la imagen de perfil, aunque sea una cadena vacía. Esto evita persistir datos antiguos.
            dataToSave[PLAYER_PROFILE_IMAGE_KEY] = LocalProfileImageBase64 ?? string.Empty;

            await CloudSaveService.Instance.Data.Player.SaveAsync(dataToSave);
            Debug.Log("Progreso del jugador y foto de perfil guardados en la nube.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error al guardar el progreso del jugador: " + e);
        }
    }

    /// <summary>
    /// Actualiza la imagen de perfil del jugador en memoria y la persiste en Cloud Save.
    /// </summary>
    /// <param name="base64">Cadena base64 que representa la imagen. Si es nula, se guardará como cadena vacía.</param>
    public async Task UpdatePlayerProfileImage(string base64)
    {
        // Guardar en la propiedad local
        LocalProfileImageBase64 = base64 ?? string.Empty;
        // Actualizar también el campo de PlayerData que se replica en red con la imagen base64
        try
        {
            var tmp = LocalPlayerData;
            // Convertir la imagen completa a una versión reducida para la red
            string networkB64 = ConvertBase64ToNetworkBase64(LocalProfileImageBase64);
            if (!string.IsNullOrEmpty(networkB64) && networkB64.Length > 4095)
            {
                networkB64 = networkB64.Substring(0, 4095);
            }
            tmp.ProfileImageBase64 = new FixedString4096Bytes(networkB64 ?? string.Empty);
            UpdateLocalData(tmp);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al actualizar ProfileImageBase64 en LocalPlayerData: {ex}");
        }
        try
        {
            // Guardar únicamente la imagen de perfil para no sobrescribir otros datos
            var data = new Dictionary<string, object>
            {
                { PLAYER_PROFILE_IMAGE_KEY, LocalProfileImageBase64 }
            };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log("Imagen de perfil actualizada en Cloud Save.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error al guardar la imagen de perfil: " + e);
        }
    }

    /// <summary>
    /// Convierte una textura en una cadena base64 comprimida adecuada para su envío por red.
    /// Intenta varias combinaciones de tamaño y calidad para garantizar que la longitud resultante
    /// no supere el límite del tipo FixedString4096Bytes (4096 caracteres).
    /// Si ninguna combinación cumple, la cadena devuelta se trunca como último recurso.
    /// </summary>
    private string ConvertTextureToNetworkBase64(Texture2D original)
    {
        if (original == null) return string.Empty;

        // Candidatos de dimensiones máximas (lado más grande) y calidades JPEG. Se probarán
        // de mayor a menor resolución y calidad hasta que el base64 quepa en 4095 caracteres.
        int[] dimensionCandidates = new int[] { 64, 48, 32, 16 };
        int[] qualityCandidates = new int[] { 50, 40, 30, 20 };

        // Almacena la última cadena generada para usarla como fallback en caso de que ninguna combinación
        // cumpla el requisito. Inicialmente está vacía.
        string lastBase64 = string.Empty;

        foreach (int maxDim in dimensionCandidates)
        {
            // Calcular escala inicial según el mayor lado de la textura original
            int origWidth = original.width;
            int origHeight = original.height;
            float maxOrigDim = Mathf.Max(origWidth, origHeight);
            float ratio = maxOrigDim > maxDim ? (float)maxDim / maxOrigDim : 1f;
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(origWidth * ratio));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(origHeight * ratio));

            // Crear y poblar la RenderTexture para escalar la textura
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            Graphics.Blit(original, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            resized.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            // Probar distintas calidades de JPEG para este tamaño
            foreach (int quality in qualityCandidates)
            {
                byte[] encodedBytes;
                try
                {
                    encodedBytes = resized.EncodeToJPG(quality);
                }
                catch
                {
                    // Si EncodeToJPG no está disponible en esta plataforma, usamos PNG (más grande)
                    encodedBytes = resized.EncodeToPNG();
                }
                string b64 = Convert.ToBase64String(encodedBytes);
                lastBase64 = b64;
                // Comprobamos si la longitud cabe en el límite (4095 caracteres, un margen de seguridad)
                if (b64.Length <= 4095)
                {
                    return b64;
                }
            }
        }
        // Si ninguna combinación encaja, devolvemos la última cadena (que será la más pequeña generada)
        // y truncamos para evitar la excepción. Este base64 truncado podría no ser decodificable, pero
        // al menos no provocará error de asignación.
        if (lastBase64 != null && lastBase64.Length > 4095)
        {
            return lastBase64.Substring(0, 4095);
        }
        return lastBase64 ?? string.Empty;
    }

    /// <summary>
    /// Dada una cadena base64 que representa una imagen, la decodifica a una textura y genera una cadena base64 de menor tamaño
    /// para la red. Devuelve una cadena vacía si no se puede procesar.
    /// </summary>
    private string ConvertBase64ToNetworkBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return string.Empty;
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                return ConvertTextureToNetworkBase64(tex);
            }
        }
        catch
        {
        }
        return string.Empty;
    }
    public void SignOutIfSignedIn()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
            playerId = null;
            playerName = null;
            Debug.Log("Auth: sesión anterior cerrada.");
        }
    }

    public void UpdateLocalData(PlayerData data)
    {
        LocalPlayerData = data;
    }
    public string GetPlayerName() => playerName;
    public string GetPlayerId() => playerId;

    /// <summary>
    /// Actualiza los campos de perfil del jugador local y guarda los datos en la nube.
    /// Los parámetros nulos indican que no se debe modificar ese campo.
    /// </summary>
    public async Task UpdatePlayerProfile(string description, string birthDate, string status, string profileImageKey)
    {
        var data = LocalPlayerData;
        if (description != null)
            data.Description = new FixedString512Bytes(description);
        if (birthDate != null)
            data.BirthDate = new FixedString32Bytes(birthDate);
        if (status != null)
            data.Status = new FixedString128Bytes(status);
        if (profileImageKey != null)
            data.ProfileImageKey = new FixedString64Bytes(profileImageKey);
        // Al modificar perfil asumimos que ya no es anónimo si había información
        data.IsAnonymous = false;
        UpdateLocalData(data);
        await SavePlayerProgress();
    }
}
