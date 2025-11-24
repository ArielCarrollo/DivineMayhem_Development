using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel que muestra la información de perfil de un jugador en el lobby.
/// Esta ventana solo permite ver la información, no editarla.
/// Debe tener referencias a los elementos UI para actualizar: imagen de perfil, nombre, descripción,
/// fecha de cumpleaños y estado.
/// </summary>
public class PlayerInfoPopup : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Image profileImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI birthDateText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Imágenes predefinidas")]
    [SerializeField] private Sprite[] availableProfileImages;
    [SerializeField] private Sprite defaultProfileImage;

    /// <summary>
    /// Muestra el panel con los datos del jugador recibidos.
    /// </summary>
    public void Show(PlayerData data)
    {
        if (panel != null)
            panel.SetActive(true);

        // Asignar textos
        if (nameText != null)
            nameText.text = data.Username.ToString();
        if (descriptionText != null)
        {
            string desc = data.Description.ToString();
            descriptionText.text = string.IsNullOrWhiteSpace(desc) ? "Sin descripción" : desc;
        }
        if (birthDateText != null)
        {
            string bd = data.BirthDate.ToString();
            birthDateText.text = string.IsNullOrWhiteSpace(bd) ? "Sin fecha" : bd;
        }
        if (statusText != null)
        {
            string st = data.Status.ToString();
            statusText.text = string.IsNullOrWhiteSpace(st) ? "Sin estado" : st;
        }

        // Elegir la imagen a mostrar
        Sprite chosen = defaultProfileImage;
        string key = data.ProfileImageKey.ToString();
        if (!string.IsNullOrEmpty(key))
        {
            // si es un índice numérico, lo usamos para la lista predefinida
            if (int.TryParse(key, out int idx))
            {
                if (availableProfileImages != null && idx >= 0 && idx < availableProfileImages.Length)
                {
                    chosen = availableProfileImages[idx];
                }
            }
        }
        if (profileImage != null && chosen != null)
            profileImage.sprite = chosen;
    }

    /// <summary>
    /// Oculta el panel.
    /// </summary>
    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}