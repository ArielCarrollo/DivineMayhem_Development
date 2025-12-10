using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class CloudPlatformRespawn : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float tiempoSoporte = 3.0f; // Tiempo antes de caer
    [SerializeField] private float tiempoTemblor = 1.0f; // Cuánto tiempo tiembla antes de desaparecer (parte de los 3s)
    [SerializeField] private float tiempoRespawn = 5.0f; // Tiempo para volver a aparecer
    [SerializeField] private float intensidadTemblor = 0.1f;

    [Header("Referencias Visuales")]
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private Collider platformCollider;

    private Vector3 posInicial;
    private bool isOccupied = false;

    private void Awake()
    {
        posInicial = transform.position;
        if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        if (platformCollider == null) platformCollider = GetComponent<Collider>();
    }

    private void OnCollisionEnter(Collision other)
    {
        // Solo el servidor decide cuándo se activa la plataforma
        if (!IsServer) return;

        if (isOccupied) return;

        // Verificar si es un jugador
        if (other.gameObject.CompareTag("Player"))
        {
            StartCoroutine(PlatformSequence());
        }
    }

    private IEnumerator PlatformSequence()
    {
        isOccupied = true;

        // 1. Tiempo de soporte estable
        float tiempoEstable = tiempoSoporte - tiempoTemblor;
        if (tiempoEstable > 0)
            yield return new WaitForSeconds(tiempoEstable);

        // 2. Avisar a todos los clientes que tiemblen (Visual)
        TriggerShakeClientRpc();

        // Esperar el tiempo de temblor
        yield return new WaitForSeconds(tiempoTemblor);

        // 3. Desaparecer (Lógica y Visual)
        TogglePlatform(false);
        TogglePlatformClientRpc(false);

        // 4. Esperar Respawn
        yield return new WaitForSeconds(tiempoRespawn);

        // 5. Reaparecer
        TogglePlatform(true);
        TogglePlatformClientRpc(true);

        isOccupied = false;
    }

    // Sincroniza la activación/desactivación visual y física
    [ClientRpc]
    private void TogglePlatformClientRpc(bool active)
    {
        if (IsServer) return; // El servidor ya lo hizo localmente en TogglePlatform
        TogglePlatform(active);
    }

    private void TogglePlatform(bool active)
    {
        if (meshRenderer != null) meshRenderer.enabled = active;
        if (platformCollider != null) platformCollider.enabled = active;

        // Resetear posición al reaparecer para quitar el offset del temblor
        if (active) transform.position = posInicial;
    }

    // Efecto visual de temblor solo en clientes
    [ClientRpc]
    private void TriggerShakeClientRpc()
    {
        StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float timer = 0;
        while (timer < tiempoTemblor)
        {
            transform.position = posInicial + Random.insideUnitSphere * intensidadTemblor;
            timer += Time.deltaTime;
            yield return null;
        }
        transform.position = posInicial;
    }
}