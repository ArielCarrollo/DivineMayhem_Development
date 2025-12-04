using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<CharacterBase>(out CharacterBase player))
        {
            // Reportar al manager activo
            if (SurvivalGameManager.Instance != null)
                SurvivalGameManager.Instance.OnPlayerDied(player);

            else if (FallingGameManager.Instance != null)
                FallingGameManager.Instance.OnPlayerDied(player);

            // Opcional: También matar si cae en el juego de la pelota
            else if (SpikyBallGameManager.Instance != null)
                SpikyBallGameManager.Instance.OnPlayerDied(player);
        }
    }
}