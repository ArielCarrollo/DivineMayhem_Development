using UnityEngine;

public class PlayerAppearance : MonoBehaviour
{
    [Header("Modelos Visuales")]
    [Tooltip("Arrastra aquí los 4 objetos visuales hijos. Orden: 0:Inca, 1:Sinto, 2:Greek, 3:Norse")]
    [SerializeField] private GameObject[] pantheonModels;

    private void Start()
    {
        // Seguridad: al iniciar, mostrar el primero por defecto si no se ha llamado a nada
        // Opcional: UpdateVisuals(0); 
    }

    public void ApplyAppearance(PlayerData data)
    {
        UpdateVisuals(data.PantheonIndex);
    }

    public void UpdateVisuals(int index)
    {
        if (pantheonModels == null || pantheonModels.Length == 0) return;

        // Protección de índice
        if (index < 0) index = 0;
        if (index >= pantheonModels.Length) index = 0;

        for (int i = 0; i < pantheonModels.Length; i++)
        {
            if (pantheonModels[i] != null)
            {
                // Activa solo el que coincide con el índice
                bool isActive = (i == index);
                pantheonModels[i].SetActive(isActive);

                // Reiniciar animación si se activa (opcional, para que empiece el Idle desde el principio)
                /* if (isActive) {
                    var anim = pantheonModels[i].GetComponent<Animator>();
                    if(anim) anim.Play("Idle", 0, 0f);
                }
                */
            }
        }
    }

    public int GetPantheonCount() => pantheonModels != null ? pantheonModels.Length : 0;
}