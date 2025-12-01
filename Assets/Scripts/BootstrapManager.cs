using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapManager : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("El nombre de la escena del Menú Principal o Lobby Local.")]
    [SerializeField] private string sceneToLoad = "MainMenu";

    private void Start()
    {
        // En modo Local Offline, no necesitamos inicializar Unity Services,
        // Relay, ni Vivox. Simplemente cargamos la siguiente escena.
        Debug.Log("Bootstrap: Modo Offline detectado. Cargando menú...");

        LoadNextScene();
    }

    private void LoadNextScene()
    {
        // Si tienes un gestor de transiciones, úsalo. Si no, carga directa.
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadSceneWithFade(sceneToLoad);
        }
        else
        {
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
        }
    }
}