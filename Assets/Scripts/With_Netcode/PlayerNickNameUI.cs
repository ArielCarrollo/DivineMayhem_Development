using UnityEngine;
using Unity.Netcode;
using TMPro;
using Unity.Collections;

public class PlayerNicknameUI : NetworkBehaviour
{
    [SerializeField]
    private TextMeshProUGUI nicknameText;

    // Aunque ya no mostremos los puntos aquí, necesitamos las variables
    // para que la sincronización interna funcione si otros scripts la usan.
    public NetworkVariable<FixedString64Bytes> Nickname = new NetworkVariable<FixedString64Bytes>();
    public NetworkVariable<int> Points = new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        // Suscribirse a cambios
        Nickname.OnValueChanged += HandleDisplayTextChanged;

        // Configurar color y texto inicial
        UpdateDisplayText();
        UpdateColor();
    }

    private void HandleDisplayTextChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        if (nicknameText != null)
        {
            // Solo mostramos el nombre, limpio.
            nicknameText.text = Nickname.Value.ToString();
        }
    }

    private void UpdateColor()
    {
        if (nicknameText != null)
        {
            // El color depende del ClientId (OwnerClientId)
            nicknameText.color = GetColor((int)OwnerClientId);
        }
    }

    private Color GetColor(int index)
    {
        switch (index % 4)
        {
            case 0: return Color.blue;
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }

    public override void OnNetworkDespawn()
    {
        Nickname.OnValueChanged -= HandleDisplayTextChanged;
    }
}