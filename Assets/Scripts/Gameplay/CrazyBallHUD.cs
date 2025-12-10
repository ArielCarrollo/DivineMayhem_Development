using UnityEngine;
using TMPro;
using Unity.Netcode;

public class CrazyBallHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI aliveCountText;
    [SerializeField] private TextMeshProUGUI alertText; // "¡Jugador Eliminado!"

    private void Start()
    {
        if (alertText) alertText.gameObject.SetActive(false);

        if (CrazyBallGameManager.Instance != null)
        {
            // Suscribirse al cambio de vivos
            CrazyBallGameManager.Instance.AlivePlayersCount.OnValueChanged += OnCountChanged;
            UpdateText(CrazyBallGameManager.Instance.AlivePlayersCount.Value);
        }
    }

    private void OnDestroy()
    {
        if (CrazyBallGameManager.Instance != null)
        {
            CrazyBallGameManager.Instance.AlivePlayersCount.OnValueChanged -= OnCountChanged;
        }
    }

    private void OnCountChanged(int prev, int current)
    {
        UpdateText(current);

        // Si bajó la cantidad, alguien murió -> Mostrar alerta
        if (current < prev)
        {
            ShowAlert("¡JUGADOR ELIMINADO!");
        }
    }

    private void UpdateText(int count)
    {
        if (aliveCountText != null)
            aliveCountText.text = $"VIVOS: {count}";
    }

    private void ShowAlert(string msg)
    {
        if (alertText == null) return;

        alertText.text = msg;
        alertText.gameObject.SetActive(true);

        // Ocultar en 2 segundos
        CancelInvoke(nameof(HideAlert));
        Invoke(nameof(HideAlert), 2f);
    }

    private void HideAlert()
    {
        if (alertText) alertText.gameObject.SetActive(false);
    }
}