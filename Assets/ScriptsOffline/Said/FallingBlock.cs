using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class FallingBlock : MonoBehaviour, IDestructible
{
    [Header("Configuración")]
    [SerializeField] private float tiempoVida = 5f; // Tiempo antes de desaparecer tras caer
    [SerializeField] private float gravedadExtra = 2f; // Empujón inicial hacia abajo

    private Rigidbody rb;
    private Collider col;
    private bool yaCayo = false;
    public bool YaCayo => yaCayo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<BoxCollider>();

        // 1. Configuración Física "Hielo"
        PhysicsMaterial slipperyMat = new PhysicsMaterial("BloqueResbaladizo");
        slipperyMat.dynamicFriction = 0f;
        slipperyMat.staticFriction = 0f;
        slipperyMat.frictionCombine = PhysicsMaterialCombine.Minimum;

        col.material = slipperyMat;

        // 2. Setup Rigidbody
        rb.useGravity = false;
        rb.isKinematic = true;
        // Interpolación para que se vea suave al caer
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Masa estándar
        rb.mass = 10f;
    }

    public void HacerCaer()
    {
        if (yaCayo) return;
        yaCayo = true;

        // Activar físicas
        rb.isKinematic = false;
        rb.useGravity = true;

        // TRUCO: Despertar el RB y empujar abajo para romper la fricción estática
        rb.WakeUp();
        rb.AddForce(Vector3.down * gravedadExtra, ForceMode.VelocityChange);

        // Iniciar cuenta atrás para desactivar (limpieza)
        StartCoroutine(DesactivarRutina());
    }

    private IEnumerator DesactivarRutina()
    {
        // Esperamos un tiempo prudente para que caiga al vacío
        yield return new WaitForSeconds(tiempoVida);

        // Lo desactivamos suavemente
        gameObject.SetActive(false);
    }

    // Interfaz IDestructible (por si la bola o algo lo golpea)
    public void TriggerDestruction() => HacerCaer();
    public void TriggerDestruction(Vector3 origin) => HacerCaer();
}
