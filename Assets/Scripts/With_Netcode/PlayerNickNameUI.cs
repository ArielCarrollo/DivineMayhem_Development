using UnityEngine;
using Unity.Netcode;
using TMPro;
using Unity.Collections;

public class PlayerNicknameUI : NetworkBehaviour
{
    [SerializeField]
    private TextMeshProUGUI nicknameText;

    // --- NUEVAS VARIABLES ---
    public NetworkVariable<FixedString64Bytes> Nickname = new NetworkVariable<FixedString64Bytes>();
    // Variable de red para almacenar los puntos del jugador. Por defecto inicia en 0.
    public NetworkVariable<int> Points = new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        // Nos suscribimos a los cambios de ambas variables
        Nickname.OnValueChanged += HandleDisplayTextChanged;
        Points.OnValueChanged += HandleDisplayTextChanged;

        // Actualizamos el texto con los valores iniciales
        UpdateDisplayText();
    }

    // Un solo método para manejar el cambio de cualquiera de las dos variables
    private void HandleDisplayTextChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        UpdateDisplayText();
    }

    private void HandleDisplayTextChanged(int previousValue, int newValue)
    {
        UpdateDisplayText();
    }

    // El método que construye el texto final
    private void UpdateDisplayText()
    {
        // Mostramos los puntos acumulados en lugar del nivel
        nicknameText.text = $"[Pts {Points.Value}] {Nickname.Value}";
    }
}