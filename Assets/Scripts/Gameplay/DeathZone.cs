using UnityEngine;
using Unity.Netcode;

public class DeathZone : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Solo el servidor decide quién muere
        if (!IsServer) return;

        // ¿Cayó un jugador?
        if (other.TryGetComponent<CharacterBase>(out CharacterBase player))
        {
            // Avisamos al Manager de este nivel que alguien murió
            if (SurvivalGameManager.Instance != null)
            {
                SurvivalGameManager.Instance.OnPlayerDied(player);
            }
        }
    }
}