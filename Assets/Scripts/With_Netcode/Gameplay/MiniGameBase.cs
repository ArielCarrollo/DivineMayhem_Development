using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// Clase base abstracta para cada minijuego. Proporciona lógica común para verificar
/// condiciones de finalización, otorgar puntos a los jugadores y cargar el siguiente mapa.
/// Las clases derivadas deben implementar <see cref="IsComplete"/> para definir cuándo
/// termina el nivel y pueden configurar <see cref="pointsOnComplete"/> según sus reglas.
/// </summary>
public abstract class MiniGameBase : NetworkBehaviour
{
    [Header("MiniGame Settings")]
    [Tooltip("Puntos otorgados a cada jugador al completar este minijuego.")]
    [SerializeField] protected int pointsOnComplete = 10;

    // Para evitar que el minijuego termine varias veces
    private bool hasEnded = false;

    // Se actualiza en el servidor para comprobar si la condición de fin se cumple.
    protected virtual void Update()
    {
        if (!IsServer || hasEnded) return;

        if (IsComplete())
        {
            hasEnded = true;
            EndMiniGame();
        }
    }

    /// <summary>
    /// Debe ser sobrescrito en cada minijuego para indicar si se han cumplido las condiciones de victoria.
    /// </summary>
    /// <returns>true si el minijuego ha terminado; de lo contrario false.</returns>
    protected abstract bool IsComplete();

    /// <summary>
    /// Se llama cuando el minijuego ha terminado. Otorga puntos a los jugadores y avanza al siguiente mapa.
    /// </summary>
    protected virtual void EndMiniGame()
    {
        // Otorgamos puntos a todos los jugadores presentes en la partida
        if (GameManager.Instance != null)
        {
            foreach (var player in GameManager.Instance.PlayersInLobby)
            {
                // Método normal del GameManager (no RPC) que suma puntos al jugador por clientId
               //GameManager.Instance.AddPointsToPlayerById(player.ClientId, pointsOnComplete);
            }
        }

        // Seleccionamos un mapa aleatorio para la siguiente ronda
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectRandomNextMap();
            string nextScene = GameManager.Instance.AvailableMapNames[GameManager.Instance.CurrentMapIndex];
            NetworkManager.Singleton.SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
        }
    }
}
