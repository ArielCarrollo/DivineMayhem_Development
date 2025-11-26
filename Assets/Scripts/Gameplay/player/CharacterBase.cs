using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum PlayerState
{
    Normal,
    Knockback,
    Charging
}

[RequireComponent(typeof(Rigidbody))]
public abstract class CharacterBase : MonoBehaviour
{
    [Header("Stats Base del Personaje")]
    [SerializeField] protected float velocidad = 5f;
    [SerializeField] protected int vidaMaxima = 100;
    [SerializeField] protected int fuerzaBase = 10;
    [SerializeField] protected int nivelBase = 1;
    [SerializeField] protected float estaminaMaxima = 100f;

    [Header("Lógica de Estamina")]
    [SerializeField, Tooltip("Cuánta estamina se recarga por segundo")]
    private float staminaChargeRate = 20f;
    private Coroutine chargingCoroutine;

    [Header("Lógica de Movimiento")]
    [SerializeField, Tooltip("Qué tan rápido gira el personaje (más alto es más rápido)")]
    private float rotationSpeed = 15f;

    [Header("Lógica de Salto")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundDistance = 0.3f;

    [Header("Configuración de Minijuego")]
    [SerializeField, Tooltip("El GameObject de la corona (hijo de este prefab)")]
    private GameObject crownVisual;

    [Header("Componentes")]
    protected Rigidbody rb;
    protected Animator animator;

    [Header("Lógica de Ataque Básico")]
    [SerializeField, Tooltip("El punto en la mano que detecta el golpe")]
    private Transform hitPoint;

    [SerializeField, Tooltip("El radio del golpe (qué tan grande es el 'puño')")]
    private float hitRadius = 0.5f;

    [SerializeField, Tooltip("La fuerza con la que el puñete lanza objetos")]
    private float punchForce = 15f;

    [SerializeField, Tooltip("Qué capas (Layers) pueden ser golpeadas por el puñete")]
    private LayerMask hitableLayers;

    [SerializeField, Tooltip("Segundos desde que se presiona el botón hasta que se registra el golpe")]
    private float attackDelay = 0.3f;

    [SerializeField, Tooltip("Tiempo total entre un ataque y el siguiente (cooldown)")]
    private float attackCooldown = 0.8f;
    private float nextAttackTime = 0f;

    [Header("Lógica de Knockback")]
    [SerializeField, Tooltip("Segundos que el jugador queda en estado 'Knockback'")]
    private float knockbackDuration = 0.5f;
    [SerializeField, Tooltip("La fuerza vertical (hacia arriba) fija del golpe")]
    private float verticalKnockup = 7f;

    // --------- ESTADO LOCAL (ya no NetworkVariables) ---------
    public bool IsKing;
    public PlayerState CurrentState = PlayerState.Normal;
    public int Vida;
    public int Fuerza;
    public int Nivel;
    public float Estamina;

    public float EstaminaMaxima => estaminaMaxima;

    private bool controlsEnabled = true;
    private float moveInput;
    private bool isGrounded;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Inicializar stats
        Vida = vidaMaxima;
        Fuerza = fuerzaBase;
        Nivel = nivelBase;
        Estamina = estaminaMaxima;
    }

    protected virtual void OnEnable()
    {
        OnKingStatusChanged(false, IsKing);
    }

    protected virtual void OnDisable()
    {
        // Aquí podrías desregistrarte de UIManager/MinigameManager si hace falta
    }

    // ---------------- TELEPORT / MUERTE (LOCAL) ----------------

    public void TeleportPlayer(Vector3 newPosition)
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = newPosition;
        transform.rotation = Quaternion.Euler(0, 90, 0);

        if (rb != null)
            rb.isKinematic = false;

        SetInputActive(true);

        if (animator) animator.Play("Idle");
    }

    /// <summary>
    /// Versión local del "ServerTeleport": resetea estado y teleporta.
    /// </summary>
    public void ServerTeleport(Vector3 newPosition)
    {
        CurrentState = PlayerState.Normal;
        IsKing = false;
        TeleportPlayer(newPosition);
    }

    public void KillPlayer()
    {
        SetInputActive(false);

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnKingStatusChanged(bool previousValue, bool newValue)
    {
        if (crownVisual != null)
        {
            crownVisual.SetActive(newValue);
        }
    }

    public void SetInputActive(bool isActive)
    {
        controlsEnabled = isActive;

        if (!isActive && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            moveInput = 0;

            if (animator) animator.SetFloat("Speed", 0);
        }
    }

    // ---------------- MANEJO DE INPUT (New Input System) ----------------

    public virtual void OnMove(InputAction.CallbackContext context)
    {
        if (!controlsEnabled || CurrentState != PlayerState.Normal)
        {
            moveInput = 0;
            return;
        }

        moveInput = context.ReadValue<float>();
    }

    public virtual void OnJump(InputAction.CallbackContext context)
    {
        if (!controlsEnabled || CurrentState != PlayerState.Normal) return;

        if (context.performed && isGrounded)
        {
            Jump();
        }
    }

    public virtual void OnNormalAttack(InputAction.CallbackContext context)
    {
        if (!controlsEnabled || CurrentState != PlayerState.Normal) return;

        if (context.performed)
        {
            NormalAttack();
        }
    }

    public virtual void OnUltimateAttack(InputAction.CallbackContext context)
    {
        if (!controlsEnabled || CurrentState != PlayerState.Normal) return;

        if (context.performed)
        {
            UltimateAttack();
        }
    }

    public virtual void OnCharge(InputAction.CallbackContext context)
    {
        if (!controlsEnabled || CurrentState == PlayerState.Knockback) return;

        if (context.performed)
        {
            StartChargingStamina();
        }
        else if (context.canceled)
        {
            StopChargingStamina();
        }
    }

    // ---------------- LÓGICA LOCAL (antes RPCs) ----------------

    protected virtual void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        if (animator) animator.SetTrigger("Jump");
    }

    protected virtual void NormalAttack()
    {
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;
        Debug.Log("NORMAL ATTACK (local)");
        if (animator) animator.SetTrigger("NormalAttack");

        StartCoroutine(HitCheckDelay());
    }

    protected virtual void UltimateAttack()
    {
        Debug.Log("ULTI base (local, vacía)");
        // La subclase (Ninja, etc.) puede overridear esto
    }

    protected virtual void StartChargingStamina()
    {
        if (CurrentState != PlayerState.Normal) return;

        CurrentState = PlayerState.Charging;

        if (chargingCoroutine != null)
            StopCoroutine(chargingCoroutine);

        chargingCoroutine = StartCoroutine(ChargeStaminaCoroutine());
    }

    protected virtual void StopChargingStamina()
    {
        if (CurrentState != PlayerState.Charging) return;

        CurrentState = PlayerState.Normal;

        if (chargingCoroutine != null)
        {
            StopCoroutine(chargingCoroutine);
            chargingCoroutine = null;
        }
    }

    private IEnumerator ChargeStaminaCoroutine()
    {
        Debug.Log("Empezando a cargar Estamina (local)...");
        while (Estamina < estaminaMaxima)
        {
            Estamina += staminaChargeRate * Time.deltaTime;
            Estamina = Mathf.Clamp(Estamina, 0, estaminaMaxima);
            yield return null;
        }

        Debug.Log("Estamina llena.");
        CurrentState = PlayerState.Normal;
        chargingCoroutine = null;
    }

    // ---------------- UPDATE / MOVIMIENTO ----------------

    protected virtual void FixedUpdate()
    {
        if (rb == null) return;

        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (CurrentState == PlayerState.Normal)
        {
            HandleMovementAndRotation();
        }

        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        if (animator)
        {
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("IsGrounded", isGrounded);
        }
    }

    private void HandleMovementAndRotation()
    {
        rb.linearVelocity = new Vector3(moveInput * velocidad, rb.linearVelocity.y, 0f);

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            Quaternion targetRotation = (moveInput > 0)
                ? Quaternion.Euler(0, 90, 0)
                : Quaternion.Euler(0, -90, 0);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.fixedDeltaTime * rotationSpeed
            );
        }
    }

    // ---------------- GOLPES / HITBOX ----------------

    public void HitCheck()
    {
        if (hitPoint == null) return;

        Collider[] hits = Physics.OverlapSphere(hitPoint.position, hitRadius, hitableLayers);

        foreach (Collider hit in hits)
        {
            if (hit.transform == this.transform) continue;

            // ¿Es otro jugador?
            if (hit.TryGetComponent<CharacterBase>(out CharacterBase victimPlayer))
            {
                Vector3 horizontalDir = (victimPlayer.transform.position - transform.position);
                horizontalDir.y = 0;
                horizontalDir.z = 0;
                horizontalDir.Normalize();

                // Lógica de corona local (opcional)
                if (victimPlayer.IsKing && !this.IsKing && MinigameManager.Instance != null)
                {
                    MinigameManager.Instance.TransferCrown(this);
                }

                victimPlayer.ApplyKnockback(horizontalDir, punchForce);
            }
            // ¿Es un objeto rígido?
            else if (hit.TryGetComponent<Rigidbody>(out Rigidbody objectRb))
            {
                Vector3 objectDirection =
                    (hit.transform.position - transform.position).normalized +
                    (Vector3.up * 0.3f);
                objectRb.AddForce(objectDirection * punchForce, ForceMode.Impulse);
            }
        }
    }

    private IEnumerator HitCheckDelay()
    {
        yield return new WaitForSeconds(attackDelay);
        HitCheck();
    }

    public void ApplyKnockback(Vector3 horizontalDirection, float horizontalForce)
    {
        if (CurrentState != PlayerState.Normal) return;

        CurrentState = PlayerState.Knockback;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        rb.AddForce(horizontalDirection * horizontalForce, ForceMode.Impulse);
        rb.AddForce(Vector3.up * verticalKnockup, ForceMode.Impulse);

        StartCoroutine(KnockbackCooldown());
    }

    private IEnumerator KnockbackCooldown()
    {
        yield return new WaitForSeconds(knockbackDuration);
        CurrentState = PlayerState.Normal;
    }

    // Gizmo de hitRadius
    private void OnDrawGizmosSelected()
    {
        if (hitPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
    }
}
