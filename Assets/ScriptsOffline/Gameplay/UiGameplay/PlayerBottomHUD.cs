using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerBottomHUD : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Slider inactivitySlider; // Barra de "Vida por aburrimiento"
    [SerializeField] private Slider cooldownSlider;   // Barra de "Empuje"
    [SerializeField] private TextMeshProUGUI nameText;
    [Header("Estado")]
    [SerializeField]
    private GameObject eliminatedOverlay;

    private CharacterBase target;

    public void Initialize(CharacterBase player)
    {
        target = player;
        if (nameText)
        {
            nameText.text = $"P{player.PlayerIndex + 1}";
            nameText.color = GetColor(player.PlayerIndex);
        }

        // Configurar máximos
        if (inactivitySlider)
        {
            inactivitySlider.maxValue = player.MaxInactivityTime;
            inactivitySlider.value = player.MaxInactivityTime;
        }

        if (cooldownSlider)
        {
            cooldownSlider.maxValue = player.PushCooldown;
            cooldownSlider.value = 0; // Empieza listo para usar (0 espera)
        }
    }

    private void Update()
    {
        if (target == null) return;
        // Si el target desapareció o se desactivó
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (eliminatedOverlay != null && !eliminatedOverlay.activeSelf)
            {
                eliminatedOverlay.SetActive(true); // Mostrar "Descansando"
            }
            return;
        }

        // Si revive (por si acaso), ocultamos el texto
        if (eliminatedOverlay != null && eliminatedOverlay.activeSelf)
        {
            eliminatedOverlay.SetActive(false);
        }
        // 1. Barra de Inactividad (Funciona como Vida: se vacía si te quedas quieto)
        if (inactivitySlider)
        {
            // NO HACER ESTO EN UPDATE: inactivitySlider.DOValue(...)
            // DOTween en Update crea millones de tweens.
            // Mejor usar Lerp manual o un tween guardado.

            // Opción Lerp para suavidad simple:
            inactivitySlider.value = Mathf.Lerp(inactivitySlider.value, target.InactivityTimer, Time.deltaTime * 5f);

            // Color Dinámico: Verde -> Amarillo -> Rojo parpadeante
            if (inactivitySlider.fillRect != null) // La imagen de relleno
            {
                Image fill = inactivitySlider.fillRect.GetComponent<Image>();
                float pct = target.InactivityTimer / target.MaxInactivityTime;

                if (pct > 0.5f) fill.color = Color.green;
                else if (pct > 0.2f) fill.color = Color.yellow;
                else
                {
                    // Rojo parpadeante
                    float blink = Mathf.PingPong(Time.time * 10f, 1f);
                    fill.color = Color.Lerp(Color.red, Color.black, blink);
                }
            }
        }

        // 2. Barra de Cooldown (Funciona como Tiempo de Espera)
        if (cooldownSlider)
        {
            // El script CharacterBase reduce el PushTimer hasta 0.
            // Si PushTimer es 3, el slider está lleno (no puedes usarlo).
            // Si PushTimer es 0, el slider está vacío (listo).
            cooldownSlider.value = target.PushTimer;
        }
    }

    private Color GetColor(int index)
    {
        switch (index)
        {
            case 0: return Color.blue;
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }
}