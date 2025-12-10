using UnityEngine;
using TMPro;
using Unity.Netcode;

public class FallingGameHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI aliveText;
    [SerializeField] private TextMeshProUGUI alertText;

    private void Start()
    {
        if (alertText) alertText.gameObject.SetActive(false);

        if (FallingGameManager.Instance != null)
        {
            FallingGameManager.Instance.AlivePlayersCount.OnValueChanged += OnCountChanged;
            UpdateText(FallingGameManager.Instance.AlivePlayersCount.Value);
        }
    }

    private void OnDestroy()
    {
        if (FallingGameManager.Instance != null)
        {
            FallingGameManager.Instance.AlivePlayersCount.OnValueChanged -= OnCountChanged;
        }
    }

    private void OnCountChanged(int prev, int cur)
    {
        UpdateText(cur);
        if (cur < prev) ShowAlert();
    }

    private void UpdateText(int count)
    {
        if (aliveText) aliveText.text = $"VIVOS: {count}";
    }

    private void ShowAlert()
    {
        if (alertText)
        {
            alertText.text = "¡JUGADOR CAÍDO!";
            alertText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideAlert));
            Invoke(nameof(HideAlert), 1.5f);
        }
    }

    private void HideAlert() => alertText.gameObject.SetActive(false);
}