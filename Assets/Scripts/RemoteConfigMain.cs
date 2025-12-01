using UnityEngine;
using Unity.Services.RemoteConfig;
using System;
using System.Threading.Tasks; // Necesario para 'Task'

public class RemoteConfigMain : MonoBehaviour
{
    public float PlayerMovementSpeed;

    // Structs definidos (con los nombres tal cual aparecen en tu código)
    struct UserAtrributes { }
    struct AppAtrributes { }

    private void Awake()
    {
        // Suscripción al evento de completado
        RemoteConfigService.Instance.FetchCompleted += OnRemoteConfigFetched;
    }

    void Start()
    {
        // Puedes llamar a StartFetching() aquí si quieres que arranque automático
    }

    // --- CÓDIGO AÑADIDO DE LA PRIMERA IMAGEN (Async/Task) ---

    // Nota: El atributo [Button] requiere un plugin (como NaughtyAttributes o Odin) 
    // o un script de editor personalizado. Si te da error, coméntalo.
    // [Button] 
    public async void StartFetching()
    {
        await FetchConfig();
    }

    public async Task FetchConfig()
    {
        Debug.Log("Trying to call data");

        try
        {
            // Se pasan las instancias de los structs definidos arriba
            await RemoteConfigService.Instance.FetchConfigsAsync(new UserAtrributes(), new AppAtrributes());
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    // --- CÓDIGO AÑADIDO DE LA SEGUNDA IMAGEN (Switch y Asignación) ---

    private void OnRemoteConfigFetched(ConfigResponse response)
    {
        switch (response.requestOrigin)
        {
            case ConfigOrigin.Default:
                Debug.Log("Valores por defecto");
                break;
            case ConfigOrigin.Cached:
                Debug.Log("Usando valores en la cache");
                break;
            case ConfigOrigin.Remote:
                Debug.Log("Usando valores desde la nube");
                break;
        }

        // Asignación del valor de la nube a tu variable local
        // Si no encuentra "MovementSpeed", mantendrá el valor actual de PlayerMovementSpeed
        PlayerMovementSpeed = RemoteConfigService.Instance.appConfig.GetFloat("MovementSpeed", PlayerMovementSpeed);
    }
}