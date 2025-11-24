using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla la visualización del menú de opciones y el acceso al perfil.
/// Este script debe asignarse a un objeto en la escena con referencias a los
/// paneles de opciones y perfil. Al pulsar Escape se abrirá/cerrará el panel.
/// El botón de perfil solo se habilitará si el jugador no es anónimo.
/// </summary>
public class OptionsMenuManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Button profileButton;
    [SerializeField] private GameObject profilePanel;

    private bool isOptionsOpen = false;

    private void Start()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        if (profilePanel != null)
            profilePanel.SetActive(false);

        // Asegurar que el botón de perfil verifica el estado inicial
        UpdateProfileButtonState();

        //// Suscribimos el botón a la apertura del panel de perfil
        //if (profileButton != null)
        //    profileButton.onClick.AddListener(OnProfileButtonClicked);
    }

    private void Update()
    {
        // Detecta la tecla Escape para alternar el menú de opciones
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleOptionsMenu();
        }
    }

    private void ToggleOptionsMenu()
    {
        if (optionsPanel == null) return;

        isOptionsOpen = !isOptionsOpen;
        optionsPanel.SetActive(isOptionsOpen);
        UpdateProfileButtonState();
    }

    /// <summary>
    /// Habilita o deshabilita el botón de perfil en función de si el jugador es anónimo.
    /// </summary>
    private void UpdateProfileButtonState()
    {
        if (profileButton == null) return;
        bool canOpen = true;
        if (CloudAuthManager.Instance != null)
        {
            canOpen = !CloudAuthManager.Instance.IsAnonymousUser;
        }
        profileButton.interactable = canOpen;
    }

    //private void OnProfileButtonClicked()
    //{
    //    // Abrir panel de perfil solo si está disponible
    //    if (profilePanel != null && profileButton != null && profileButton.interactable)
    //    {
    //        profilePanel.SetActive(true);
    //        // Opcionalmente cerrar el menú de opciones
    //        if (optionsPanel != null)
    //        {
    //            //optionsPanel.SetActive(false);
    //            //isOptionsOpen = false;
    //        }
    //    }
    //}
}