using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
// Eliminados los using de Unity Services para modo offline.
// En modo local no utilizamos servicios de autenticación ni guardado en la nube.
using UnityEngine;

public class CloudAuthManager : MonoBehaviour
{
    public static CloudAuthManager Instance { get; private set; }
    // Para una build local offline, siempre devolvemos true. Esto evita
    // cualquier intento de conexión a servicios remotos y fuerza el uso de
    // PlayerPrefs para cargar/guardar datos del jugador. Antes se basaba en
    // UNITY_WSA_10_0 (Xbox), pero la build de testing es totalmente offline.
    private bool IsXboxOffline => true;
    public event Action OnSignInSuccess;
    public event Action<string> OnSignInFailed;
    public event Action<string> OnPlayerNameUpdated;

    public PlayerData LocalPlayerData { get; private set; }
    private const string PLAYER_PROGRESS_KEY = "PLAYER_PROGRESS_DATA";
    private string playerId;
    private string playerName;

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
        // En modo offline no inicializamos servicios Unity. Retornamos
        // inmediatamente para evitar llamadas a UnityServices.
        await Task.CompletedTask;
    }

    public async Task SignUpWithUsernamePassword(string username, string password)
    {
        // En modo offline simplemente simulamos un login local. No usamos
        // servicios de autenticación ni contraseña. El nombre de usuario se
        // almacena en PlayerPrefs. Si el nombre está vacío, asignamos uno
        // por defecto.
        if (string.IsNullOrWhiteSpace(username))
        {
            username = "LocalPlayer";
        }
        MockLoginInfo(username);
        await SavePlayerProgress();
        OnSignInSuccess?.Invoke();
        await Task.CompletedTask;
    }
    public async Task UpdatePlayerNameAsync(string newName)
    {
        // Actualiza el nombre localmente en modo offline. No llama a Unity services.
        if (string.IsNullOrWhiteSpace(newName))
        {
            Debug.LogError("El nombre no puede estar vacío.");
            return;
        }
        playerName = newName;
        if (LocalPlayerData.Username.Length == 0)
        {
            var tmp = LocalPlayerData;
            tmp.Username = new FixedString64Bytes(newName);
            UpdateLocalData(tmp);
        }
        OnPlayerNameUpdated?.Invoke(newName);
        await Task.CompletedTask;
    }
    public async Task SignInWithUsernamePassword(string username, string password)
    {
        // En modo offline, ignoramos la contraseña y simulamos un login local.
        if (string.IsNullOrWhiteSpace(username))
        {
            username = "LocalPlayer";
        }
        MockLoginInfo(username);
        await LoadPlayerProgress();
        OnSignInSuccess?.Invoke();
        await Task.CompletedTask;
    }


    //private string ConvertExceptionToMessage(AuthenticationException ex)
    //{
    //    // https://docs.unity.com/authentication/manual/exception-codes
    //    switch (ex.ErrorCode)
    //    {
    //        case 10200: // USERNAME_EXISTS
    //            return "Este nombre de usuario ya está en uso.";
    //        case 10202: // INVALID_PASSWORD
    //            return "La contraseña no es válida. Debe tener al menos 8 caracteres.";
    //        case 10203: // INVALID_USERNAME
    //            return "El nombre de usuario no es válido.";
    //        default:
    //            return "Error desconocido en el registro.";
    //    }
    //}
    public async Task LoadPlayerProgress()
    {
        // Siempre carga datos locales en modo offline. Si no existen datos en
        // PlayerPrefs, crea unos por defecto usando el nombre del jugador
        // actual. No se conectan servicios de nube.
        Debug.Log("[Offline] Cargando datos locales...");
        string json = PlayerPrefs.GetString(PLAYER_PROGRESS_KEY, "");

        if (!string.IsNullOrEmpty(json))
        {
            LocalPlayerData = JsonConvert.DeserializeObject<PlayerData>(json);
        }
        else
        {
            string defaultName = !string.IsNullOrWhiteSpace(playerName) ? playerName : "Player";
            LocalPlayerData = new PlayerData(0, defaultName);
        }
        await Task.CompletedTask;
    }


    public async Task SavePlayerProgress()
    {
        // Siempre guarda datos localmente en PlayerPrefs. No conecta a servicios
        // remotos en modo offline. Serializa PlayerData y lo escribe.
        string json = JsonConvert.SerializeObject(LocalPlayerData);
        PlayerPrefs.SetString(PLAYER_PROGRESS_KEY, json);
        PlayerPrefs.Save();
        Debug.Log("[Offline] Progreso guardado en PlayerPrefs.");
        await Task.CompletedTask;
    }
    public void SignOutIfSignedIn()
    {
        // En modo offline no usamos AuthenticationService. Simplemente
        // limpiamos los datos locales.
        playerId = null;
        playerName = null;
        Debug.Log("[Offline] Datos de sesión limpiaos.");
    }
    private void MockLoginInfo(string name)
    {
        playerId = "LocalXboxID_" + Guid.NewGuid().ToString().Substring(0, 5);
        playerName = name;
        LocalPlayerData = new PlayerData(0, name);
        Debug.Log($"[Xbox Offline] Login simulado para: {playerName}");
    }

    public void UpdateLocalData(PlayerData data)
    {
        LocalPlayerData = data;
    }
    public string GetPlayerName() => playerName;
    public string GetPlayerId() => playerId;
}