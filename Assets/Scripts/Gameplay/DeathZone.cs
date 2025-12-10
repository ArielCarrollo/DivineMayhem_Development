using UnityEngine;
using Unity.Netcode;

public class DeathZone : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Solo el servidor procesa la muerte
        if (!IsServer) return;

        if (other.TryGetComponent<CharacterBase>(out CharacterBase player))
        {
            // Verificar cuál manager está activo
            if (FallingGameManager.Instance != null)
            {
                FallingGameManager.Instance.OnPlayerFell(player);
            }
            // (Aquí podrías añadir 'else if' para otros minijuegos si usas la misma DeathZone)
        }
        // Destruir bloques que caigan para limpiar la escena
        else if (other.GetComponent<FallingBlock>() != null)
        {
            // Si el bloque tiene NetworkObject, despawnearlo
            var netObj = other.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Despawn();
            else Destroy(other.gameObject);
        }
    }
}