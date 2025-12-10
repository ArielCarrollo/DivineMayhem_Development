using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUDItem : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Slider inactivitySlider;
    [SerializeField] private Slider cooldownSlider;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject eliminatedOverlay;

    public CharacterBase TargetCharacter { get; private set; }

    // Variable local para suavizar el movimiento del cooldown
    private float _visualPushTimer = 0f;

    public void Initialize(CharacterBase player)
    {
        TargetCharacter = player;

        if (nameText != null)
        {
            nameText.text = $"P{player.OwnerClientId + 1}";
            nameText.color = GetColor((int)player.OwnerClientId);
        }

        if (inactivitySlider != null) inactivitySlider.maxValue = player.MaxInactivityTime;
        if (cooldownSlider != null) cooldownSlider.maxValue = player.PushCooldown;

        // Inicializar el timer visual
        _visualPushTimer = 0f;
    }

    private void Update()
    {
        if (TargetCharacter == null) return;

        bool isDead = !TargetCharacter.gameObject.activeInHierarchy;
        if (eliminatedOverlay != null) eliminatedOverlay.SetActive(isDead);

        if (!isDead)
        {
            // --- INACTIVIDAD ---
            if (inactivitySlider != null)
            {
                // La inactividad puede ser directa porque es lenta y no crítica visualmente
                inactivitySlider.value = TargetCharacter.InactivityTimer.Value;
                UpdateInactivityColor();
            }

            // --- COOLDOWN (EMPUEJE) SUAVIZADO ---
            if (cooldownSlider != null)
            {
                float networkTimer = TargetCharacter.PushTimer.Value;

                // 1. Detectar uso de habilidad (El timer subió de golpe)
                // Si el valor de red es mucho mayor que el visual, es que se reseteó el cooldown.
                // Actualizamos inmediatamente para que la barra se "vacíe" al instante.
                if (networkTimer > _visualPushTimer + 0.1f)
                {
                    _visualPushTimer = networkTimer;
                }
                else
                {
                    // 2. Simulación local suave (Cuenta regresiva)
                    // Restamos el tiempo localmente para que se vea fluido (60 FPS)
                    if (_visualPushTimer > 0)
                    {
                        _visualPushTimer -= Time.deltaTime;
                    }

                    // 3. Corrección de deriva (Drift correction)
                    // Si nos desincronizamos mucho del servidor (por lag), corregimos suavemente
                    if (Mathf.Abs(_visualPushTimer - networkTimer) > 0.5f)
                    {
                        _visualPushTimer = Mathf.Lerp(_visualPushTimer, networkTimer, Time.deltaTime * 5f);
                    }

                    // Nunca bajar de 0
                    _visualPushTimer = Mathf.Max(0, _visualPushTimer);
                }

                // Aplicar al slider (Invertido: Lleno cuando es 0)
                cooldownSlider.value = cooldownSlider.maxValue - _visualPushTimer;
            }
        }
    }

    private void UpdateInactivityColor()
    {
        if (inactivitySlider.fillRect == null) return;
        Image fill = inactivitySlider.fillRect.GetComponent<Image>();
        if (fill == null) return;

        float pct = inactivitySlider.value / inactivitySlider.maxValue;

        if (pct > 0.5f) fill.color = Color.green;
        else if (pct > 0.2f) fill.color = Color.yellow;
        else
        {
            float blink = Mathf.PingPong(Time.time * 10f, 1f);
            fill.color = Color.Lerp(Color.red, Color.black, blink);
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
}