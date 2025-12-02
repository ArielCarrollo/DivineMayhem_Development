using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum PlayerState { Normal, Knockback, Charging }

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public abstract class CharacterBase : MonoBehaviour
{
    [Header("Stats Base")]
    [SerializeField] protected float velocidad = 8f; // Aumentado ligeramente para compensar aceleración
    [SerializeField] protected int vidaMaxima = 100;
    [SerializeField] protected float estaminaMaxima = 100f;

    [Header("Fluidez de Movimiento (Physics)")]
    [SerializeField] private float acceleration = 60f;    // Qué tan rápido alcanza la velocidad máxima
    [SerializeField] private float deceleration = 40f;    // Qué tan rápido frena
    [SerializeField] private float airControlMultiplier = 0.5f; // Control en el aire
    [SerializeField] private float fallMultiplier = 2.5f; // Caída rápida (estilo Mario)

    [Header("Lógica de Movimiento y Giro")]
    [SerializeField, Tooltip("Velocidad de giro visual.")]
    private float rotationSpeed = 20f;

    // --- Referencias Visuales ---
    [SerializeField] private GameObject crownVisual;

    // --- Lógica de Ataque ---
    [SerializeField] private Transform hitPoint;
    [SerializeField] private float hitRadius = 0.5f;
    [SerializeField] private float punchForce = 15f;
    [SerializeField] private LayerMask hitableLayers;
    [SerializeField] private float attackDelay = 0.1f;    // Reducido para mejor feedback
    [SerializeField] private float attackCooldown = 0.5f;

    // --- Knockback ---
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private float verticalKnockup = 7f;

    // --- ESTADO LOCAL ---
    public int PlayerIndex { get; private set; }
    public bool IsKing { get; private set; }
    public PlayerState CurrentState = PlayerState.Normal;
    public float Vida;
    public float Estamina;
    public float EstaminaMaxima => estaminaMaxima;

    protected Rigidbody rb;
    protected Animator animator;
    protected PlayerInput playerInput;

    private float moveInput;
    private bool isGrounded;
    private float nextAttackTime = 0f;

    // Referencias para detección de suelo
    [Header("Detección de Suelo")]
    [SerializeField] private Transform groundCheck; // Asigna un objeto vacío en los pies
    [SerializeField] private float groundDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;

    // Constantes de animación
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimJump = Animator.StringToHash("Jump");
    private static readonly int AnimAttack = Animator.StringToHash("NormalAttack");
    private static readonly int AnimGrounded = Animator.StringToHash("IsGrounded");

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();

        // --- MEJORA FÍSICA: Material Resbaladizo ---
        // Esto evita que el personaje se pegue a las paredes al saltar contra ellas
        PhysicsMaterial slipperyMat = new PhysicsMaterial("PersonajeResbaladizo");
        slipperyMat.dynamicFriction = 0f;
        slipperyMat.staticFriction = 0f;
        slipperyMat.frictionCombine = PhysicsMaterialCombine.Minimum;
        slipperyMat.bounceCombine = PhysicsMaterialCombine.Minimum;

        if (TryGetComponent<Collider>(out Collider col))
        {
            col.material = slipperyMat;
        }

        // Configuración óptima de Rigidbody para plataformas
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Vida = vidaMaxima;
        Estamina = estaminaMaxima;

        // Crear groundCheck si no existe para evitar errores
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck_Auto");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, 0.05f, 0);
            groundCheck = gc.transform;
        }
    }

    protected virtual void Start()
    {
        PlayerIndex = playerInput.playerIndex;
        RegisterSelfInGame();
    }

    private void RegisterSelfInGame()
    {
        if (MinigameManager.Instance != null) MinigameManager.Instance.RegisterPlayer(this);
        if (SurvivalGameManager.Instance != null) SurvivalGameManager.Instance.RegisterPlayer(this);
    }

    protected virtual void OnEnable()
    {
        if (playerInput != null)
        {
            // 1. FORZAR CAMBIO DE MAPA
            // Asegúrate de que en tu InputActions el mapa de mover se llame "Player"
            playerInput.SwitchCurrentActionMap("Control");

            // 2. Suscribirse a eventos
            playerInput.onActionTriggered += HandleInput;
        }
        UpdateCrownVisual(IsKing);
    }

    protected virtual void OnDisable()
    {
        if (playerInput != null) playerInput.onActionTriggered -= HandleInput;
    }

    // --- MANEJO DE INPUT ---
    private void HandleInput(InputAction.CallbackContext ctx)
    {
        if (CurrentState == PlayerState.Knockback) return;

        string actionName = ctx.action.name;

        if (actionName == "Move" || actionName == "Navigate")
        {
            Vector2 input = ctx.ReadValue<Vector2>();
            moveInput = input.x;
        }

        if (ctx.performed)
        {
            if (actionName == "Jump") Jump();
            if (actionName == "Fire" || actionName == "Submit") NormalAttack();
            if (actionName == "Special") UltimateAttack();
            if (actionName == "Charge") StartCharging();
        }

        if (ctx.canceled && actionName == "Charge") StopCharging();
    }

    // --- ACCIONES ---

    protected virtual void Jump()
    {
        // Solo saltar si estamos en el suelo
        if (isGrounded)
        {
            // Resetear velocidad Y para salto consistente
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, 0);
            rb.AddForce(Vector3.up * 8f, ForceMode.Impulse); // Fuerza fija 15f (ajusta si necesitas más)
            if (animator) animator.SetTrigger(AnimJump);
        }
    }

    protected virtual void NormalAttack()
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + attackCooldown;

        if (animator) animator.SetTrigger(AnimAttack);
        StartCoroutine(HitCheckRoutine());
    }

    protected virtual void UltimateAttack() { }

    private void StartCharging() { /* Lógica carga estamina */ }
    private void StopCharging() { /* Lógica fin carga */ }

    // --- FÍSICAS MEJORADAS (FixedUpdate) ---

    protected virtual void FixedUpdate()
    {
        // Chequeo de suelo
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (CurrentState == PlayerState.Normal)
        {
            HandleMovementAndRotation();
            ApplyBetterGravity(); // Caída rápida
        }
        else if (CurrentState == PlayerState.Knockback)
        {
            ApplyBetterGravity();
        }

        // Animaciones
        if (animator)
        {
            animator.SetFloat(AnimSpeed, Mathf.Abs(rb.linearVelocity.x));
            animator.SetBool(AnimGrounded, isGrounded);
        }
    }

    private void HandleMovementAndRotation()
    {
        // 1. MOVIMIENTO CON ACELERACIÓN / DESACELERACIÓN
        float targetSpeed = moveInput * velocidad;

        // Si nos movemos activamente usamos aceleración, si soltamos el stick usamos desaceleración
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;

        // Si estamos en el aire, tenemos menos control (inercia)
        if (!isGrounded) accelRate *= airControlMultiplier;

        // MoveTowards suaviza el cambio de velocidad actual a la deseada
        float newSpeedX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);

        // Aplicamos la velocidad conservando la Y (gravedad/salto)
        rb.linearVelocity = new Vector3(newSpeedX, rb.linearVelocity.y, 0f);

        // 2. ROTACIÓN SUAVIZADA
        if (Mathf.Abs(moveInput) > 0.01f)
        {
            Quaternion targetRotation = (moveInput > 0)
                ? Quaternion.Euler(0, 90, 0)
                : Quaternion.Euler(0, -90, 0);

            // Giramos rápido pero no instantáneo (rotationSpeed * multiplicador)
            float giroReal = rotationSpeed * 45f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, giroReal * Time.fixedDeltaTime);
        }
    }

    private void ApplyBetterGravity()
    {
        // Si estamos cayendo (velocidad Y negativa), aplicamos gravedad extra
        // Esto hace que el salto se sienta "pesado" al caer y no flotante.
        if (rb.linearVelocity.y < 0)
        {
            // Physics.gravity.y suele ser -9.81. Multiplicamos para caer más rápido.
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        // Opcional: Salto corto (si sueltas botón). 
        // Para implementarlo necesitarías saber si el botón de salto sigue presionado.
    }

    // --- INTERACCIÓN Y DAÑO ---

    private IEnumerator HitCheckRoutine()
    {
        yield return new WaitForSeconds(attackDelay);
        if (hitPoint == null) yield break;

        Collider[] hits = Physics.OverlapSphere(hitPoint.position, hitRadius, hitableLayers);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            if (hit.TryGetComponent<CharacterBase>(out CharacterBase victim))
            {
                Vector3 dir = (victim.transform.position - transform.position).normalized;
                dir.y = 0.2f;
                dir.z = 0; // Asegurar 2.5D
                dir.Normalize();

                victim.ApplyKnockback(dir, punchForce);

                if (MinigameManager.Instance != null && victim.IsKing)
                {
                    MinigameManager.Instance.TransferCrown(this);
                }
            }
            else if (hit.TryGetComponent<Rigidbody>(out Rigidbody objRb))
            {
                Vector3 dir = (hit.transform.position - transform.position).normalized + Vector3.up * 0.3f;
                objRb.AddForce(dir * punchForce, ForceMode.Impulse);
            }
        }
    }

    public void ApplyKnockback(Vector3 dir, float force)
    {
        if (CurrentState != PlayerState.Normal) return; // Evitar stunlock infinito

        CurrentState = PlayerState.Knockback;
        rb.linearVelocity = Vector3.zero; // Frenar en seco antes de aplicar fuerza

        rb.AddForce(dir * force, ForceMode.Impulse);
        rb.AddForce(Vector3.up * verticalKnockup, ForceMode.Impulse);

        StartCoroutine(RecoverFromKnockback());
    }

    private IEnumerator RecoverFromKnockback()
    {
        yield return new WaitForSeconds(knockbackDuration);
        CurrentState = PlayerState.Normal;
    }

    // --- ESTADO DE REY (VISUAL) ---
    public void SetKing(bool status)
    {
        IsKing = status;
        UpdateCrownVisual(status);
    }

    private void UpdateCrownVisual(bool show)
    {
        if (crownVisual != null) crownVisual.SetActive(show);
    }

    public void Teleport(Vector3 pos)
    {
        rb.linearVelocity = Vector3.zero;
        transform.position = pos;
    }

    // Gizmos para debug
    private void OnDrawGizmosSelected()
    {
        if (hitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
        }
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}