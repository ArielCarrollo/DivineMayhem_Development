using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<CharacterBase>(out CharacterBase player))
        {
            if (SurvivalGameManager.Instance != null)
            {
                SurvivalGameManager.Instance.OnPlayerDied(player);
            }
        }
    }
}
