using UnityEngine;
using UnityEngine.InputSystem;

public class Ninja : CharacterBase
{
    [Header("Stats Específicos de Ninja")]
    [SerializeField] private int shurikenCount = 10;
    [SerializeField] private float velocidadNinja = 8f;

    protected override void Awake()
    {
        base.Awake();
        velocidad = velocidadNinja;
    }

    public void OnSpecialAttack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!enabled) return;

        SpecialAttack();
    }

    private void SpecialAttack()
    {
        if (shurikenCount <= 0)
        {
            Debug.Log("Ninja: No quedan shurikens.");
            return;
        }

        shurikenCount--;
        Debug.Log("Ninja lanza Shuriken! Quedan: " + shurikenCount);

        // Aquí va tu lógica de instanciar el shuriken localmente
        // GameObject shuriken = Instantiate(shurikenPrefab, ...);
        // shuriken.GetComponent<Rigidbody>().AddForce(...);
    }

    protected override void UltimateAttack()
    {
        Debug.Log("ULTI DE NINJA: 'Kage Bunshin no Jutsu' (local)");
        // Aquí iría la lógica de la ulti: clones, invisibilidad, etc.
    }
}
