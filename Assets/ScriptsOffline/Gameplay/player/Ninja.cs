using UnityEngine;
// No necesitamos "using UnityEngine.InputSystem;" aquí porque CharacterBase ya maneja el input.

public class Ninja : CharacterBase
{
    [Header("Stats Específicos de Ninja")]
    [SerializeField] private int shurikenCount = 10;
    [SerializeField] private float velocidadNinja = 9f; // Un poco más rápido

    protected override void Awake()
    {
        base.Awake();
        // Sobrescribimos la velocidad base
        velocidad = velocidadNinja;
    }

    // Ya no usamos "OnSpecialAttack(InputAction...)", usamos el override de CharacterBase
    protected override void UltimateAttack()
    {
        // Esta función se llama sola cuando aprietas el botón "Special" (tu antigua Ulti)
        // gracias al script CharacterBase.

        if (shurikenCount <= 0)
        {
            Debug.Log("Ninja: No quedan shurikens.");
            return;
        }

        shurikenCount--;
        Debug.Log($"Ninja lanza Shuriken! Quedan: {shurikenCount} (Ejecutando lógica local)");

        // Lógica de instanciar Shuriken
        // GameObject shuriken = Instantiate(shurikenPrefab, hitPoint.position, transform.rotation);
        // ...
    }
}