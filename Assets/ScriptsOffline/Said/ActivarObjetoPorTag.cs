using UnityEngine;

public class ActivarObjetoPorTag : MonoBehaviour
{
    [Header("Objeto que se activará")]
    [SerializeField] private GameObject objetoAActivar;

    [Header("Tag que puede activar el botón")]
    [SerializeField] private string tagPermitido = "Player";

    [Header("Opcional")]
    [SerializeField] private bool soloUnaVez = true;

    private bool yaActivado = false;

    private void Start()
    {
        // Al inicio el objeto está oculto
        if (objetoAActivar != null)
        {
            objetoAActivar.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Si ya se activó y solo debe funcionar una vez, salimos
        if (soloUnaVez && yaActivado)
            return;

        // Solo reacciona a objetos con la tag correcta
        if (!other.CompareTag(tagPermitido))
            return;

        if (objetoAActivar != null)
        {
            objetoAActivar.SetActive(true);
            yaActivado = true;
        }
    }
}
