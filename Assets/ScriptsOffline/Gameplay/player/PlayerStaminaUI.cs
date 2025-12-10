using UnityEngine;
using UnityEngine.UI; // Slider

public class PlayerStaminaUI : MonoBehaviour
{
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private CharacterBase characterBase;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;

        if (characterBase == null)
        {
            Debug.LogError("PlayerStaminaUI: no hay CharacterBase asignado.");
            enabled = false;
            return;
        }

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = characterBase.EstaminaMaxima;
            staminaSlider.value = characterBase.Estamina;
        }
    }

    void Update()
    {
        if (characterBase != null && staminaSlider != null)
        {
            staminaSlider.value = characterBase.Estamina;
        }
    }

    // Billboard para mirar a la cámara
    void LateUpdate()
    {
        if (mainCamera == null) return;

        transform.LookAt(
            transform.position + mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up
        );
    }
}
