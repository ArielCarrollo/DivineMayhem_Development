using UnityEngine;
using TMPro;
using DG.Tweening; // Importante

public class PlayerNicknameUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private CanvasGroup canvasGroup; // Añade un CanvasGroup al padre si quieres fade

    public void SetLocalInfo(int playerIndex, string username)
    {
        if (nicknameText != null)
        {
            // Texto: "P1" o "P1: Nombre"
            nicknameText.text = $"P{playerIndex + 1}"; // O username si prefieres nombres largos

            // Color según jugador
            Color pColor = GetColor(playerIndex);
            nicknameText.color = pColor;

            // Efecto de borde o brillo (si usas TMP con Outline)
            nicknameText.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        }

        // Animación de Entrada (Pop-up)
        transform.localScale = Vector3.zero;
        transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
    }

    // Billboard: Mirar siempre a la cámara
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward, Camera.main.transform.rotation * Vector3.up);
        }
    }

    private Color GetColor(int index)
    {
        switch (index)
        {
            case 0: return new Color(0.2f, 0.5f, 1f); // Azul brillante
            case 1: return new Color(1f, 0.2f, 0.2f); // Rojo brillante
            case 2: return new Color(0.2f, 1f, 0.2f); // Verde brillante
            case 3: return new Color(1f, 1f, 0.2f);   // Amarillo brillante
            default: return Color.white;
        }
    }
}