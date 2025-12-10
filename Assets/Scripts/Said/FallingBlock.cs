using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class FallingBlock : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float timeBeforeFall = 1.0f; // Tiempo que tiembla antes de caer
    [SerializeField] private float destroyDelay = 3.0f;   // Tiempo tras caer para desaparecer
    [SerializeField] private Color warningColor = Color.red;

    private Rigidbody rb;
    private Renderer meshRenderer;
    private Color originalColor;
    private bool isFalling = false;

    // Propiedad pública para saber si está disponible
    public bool IsFallingOrDestroyed => isFalling;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        meshRenderer = GetComponent<Renderer>();

        rb.useGravity = false;
        rb.isKinematic = true;

        if (meshRenderer != null) originalColor = meshRenderer.material.color;
    }

    /// <summary>
    /// Llamado por el Manager para iniciar la secuencia de caída.
    /// </summary>
    public void TriggerFall()
    {
        if (isFalling) return;
        isFalling = true;

        // Ejecutar lógica en todos los clientes
        TriggerFallClientRpc();
    }

    [ClientRpc]
    private void TriggerFallClientRpc()
    {
        StartCoroutine(FallSequence());
    }

    private IEnumerator FallSequence()
    {
        // 1. Fase de Advertencia (Temblor / Cambio de color)
        if (meshRenderer != null) meshRenderer.material.color = warningColor;

        float timer = 0f;
        Vector3 startPos = transform.position;

        while (timer < timeBeforeFall)
        {
            // Efecto de temblor
            float x = Random.Range(-0.05f, 0.05f);
            float z = Random.Range(-0.05f, 0.05f);
            transform.position = startPos + new Vector3(x, 0, z);

            timer += Time.deltaTime;
            yield return null;
        }

        // 2. Caída Física
        transform.position = startPos; // Reset posición exacta
        rb.isKinematic = false;
        rb.useGravity = true;

        // 3. Destrucción / Desactivación
        yield return new WaitForSeconds(destroyDelay);

        // Solo el servidor destruye el objeto de red, o lo desactivamos localmente
        if (IsServer)
        {
            // Opción A: Despawn (Destruir)
            GetComponent<NetworkObject>().Despawn();

            // Opción B: Si prefieres desactivar para pooling (más complejo con Netcode), usa Despawn es más seguro aquí.
        }
    }
}