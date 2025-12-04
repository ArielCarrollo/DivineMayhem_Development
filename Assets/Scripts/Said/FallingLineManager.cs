using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Necesario para barajar

public class FallingLineManager : MonoBehaviour
{
    [Header("Configuración de Caída")]
    [SerializeField] private float minTiempo = 1.0f; // Más rápido para más tensión
    [SerializeField] private float maxTiempo = 3.0f;
    [Tooltip("Si es true, busca bloques en los hijos automáticamente")]
    [SerializeField] private bool autoDetectBlocks = true;

    // Usamos una lista interna para gestionar
    private List<FallingBlock> allBlocks = new List<FallingBlock>();
    private bool isGameActive = true;

    private void Awake()
    {
        if (autoDetectBlocks)
        {
            // Busca bloques en los hijos
            allBlocks = GetComponentsInChildren<FallingBlock>(true).ToList();
        }
    }

    private void Start()
    {
        // Mezclamos la lista UNA vez al inicio para que el orden sea aleatorio
        ShuffleBlocks();
        StartCoroutine(RutinaCaida());
    }

    private void ShuffleBlocks()
    {
        // Algoritmo Fisher-Yates para barajar la lista eficientemente
        for (int i = 0; i < allBlocks.Count; i++)
        {
            FallingBlock temp = allBlocks[i];
            int randomIndex = Random.Range(i, allBlocks.Count);
            allBlocks[i] = allBlocks[randomIndex];
            allBlocks[randomIndex] = temp;
        }
    }

    private IEnumerator RutinaCaida()
    {
        // Recorremos la lista barajada
        // Usamos un índice en lugar de remover elementos para evitar errores de memoria
        int currentIndex = 0;

        while (isGameActive && currentIndex < allBlocks.Count)
        {
            // 1. Espera aleatoria
            float espera = Random.Range(minTiempo, maxTiempo);
            yield return new WaitForSeconds(espera);

            // 2. Obtener bloque actual
            FallingBlock bloqueActual = allBlocks[currentIndex];

            // 3. PROTECCIÓN CONTRA NULOS (Vital)
            // Si el bloque fue destruido externamente o desactivado, saltamos al siguiente
            if (bloqueActual == null || !bloqueActual.gameObject.activeInHierarchy)
            {
                currentIndex++;
                continue;
            }

            // 4. Tirar el bloque
            if (!bloqueActual.YaCayo)
            {
                // Advertencia visual opcional (temblor) antes de caer
                yield return StartCoroutine(AdvertenciaCaida(bloqueActual));

                if (bloqueActual != null) // Chequeo doble por si se destruyó durante la advertencia
                    bloqueActual.HacerCaer();
            }

            // Pasamos al siguiente bloque de la lista
            currentIndex++;
        }
    }

    // Opcional: Pequeña advertencia antes de caer (mejora jugabilidad)
    private IEnumerator AdvertenciaCaida(FallingBlock bloque)
    {
        float duracion = 1.0f; // Tiempo que tiembla
        float timer = 0;
        Vector3 posOriginal = bloque.transform.position;
        Renderer rend = bloque.GetComponent<Renderer>();
        Color colorOriginal = rend ? rend.material.color : Color.white;

        while (timer < duracion)
        {
            if (bloque == null) yield break;

            // Temblor
            bloque.transform.position = posOriginal + Random.insideUnitSphere * 0.1f;

            // Parpadeo rojo
            if (rend) rend.material.color = Color.Lerp(colorOriginal, Color.red, timer / duracion);

            timer += Time.deltaTime;
            yield return null;
        }

        // Restaurar antes de soltar (importante para física)
        if (bloque != null) bloque.transform.position = posOriginal;
    }
}