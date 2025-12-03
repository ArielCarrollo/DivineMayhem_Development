using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum PlayerState { Normal, Knockback, Charging }

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public abstract class CharacterBase : MonoBehaviour
{
    [Header("Stats Base")]
    [SerializeField] protected float velocidad = 8f;
    [SerializeField] protected int vidaMaxima = 100;
    [SerializeField] protected float estaminaMaxima = 100f;

    [Header("Fluidez de Movimiento (Physics)")]
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 40f;
    [SerializeField] private float airControlMultiplier = 0.5f;
    [SerializeField] private float fallMultiplier = 2.5f;

    [Header("Lógica de Movimiento y Giro")]
    [SerializeField, Tooltip("Velocidad de giro visual.")]
    private float rotationSpeed = 20f;

    [Header("Mecánicas Globales")]
    [SerializeField] private float maxInactivityTime = 5f;
    [SerializeField] private float pushCooldown = 3f;
    public float InactivityTimer { get; private set; }
    public float PushTimer { get; private set; }
    public float MaxInactivityTime => maxInactivityTime;
    public float PushCooldown => pushCooldown;

    // --- Referencias Visuales ---
    [SerializeField] private GameObject crownVisual;

    // --- Lógica de Ataque ---
    [SerializeField] private Transform hitPoint;
    [SerializeField] private float hitRadius = 0.5f;
    [SerializeField] private float punchForce = 15f;
    [SerializeField] private LayerMask hitableLayers;
    [SerializeField] private float attackDelay = 0.1f;
    [SerializeField] private float attackCooldown = 0.5f;

    // --- Knockback ---
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private float verticalKnockup = 7f;

    // --- ESTADO LOCAL ---
    public int PlayerIndex { get; private set; } = -1;
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

    [Header("Detección de Suelo")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;

    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimJump = Animator.StringToHash("Jump");
    private static readonly int AnimAttack = Animator.StringToHash("NormalAttack");
    private static readonly int AnimGrounded = Animator.StringToHash("IsGrounded");

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();

        // Material resbaladizo para no pegarse a paredes
        PhysicsMaterial slipperyMat = new PhysicsMaterial("PersonajeResbaladizo");
        slipperyMat.dynamicFriction = 0f;
        slipperyMat.staticFriction = 0f;
        slipperyMat.frictionCombine = PhysicsMaterialCombine.Minimum;
        slipperyMat.bounceCombine = PhysicsMaterialCombine.Minimum;

        if (TryGetComponent<Collider>(out Collider col))
        {
            col.material = slipperyMat;
        }

        // Configuración de Rigidbody
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Vida = vidaMaxima;
        Estamina = estaminaMaxima;

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck_Auto");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, 0.05f, 0);
            groundCheck = gc.transform;
        }
        InactivityTimer = maxInactivityTime;
        PushTimer = 0f;
    }

    protected virtual void Start()
    {
        if (PlayerIndex == -1)
        {
            PlayerIndex = playerInput.playerIndex;
        }
        RegisterSelfInGame();
    }

    public void SetPlayerInfo(int index, string username)
    {
        this.PlayerIndex = index;
        this.name = $"Player_{index + 1}_{username}";

        var nickUI = GetComponentInChildren<PlayerNicknameUI>();
        if (nickUI != null)
        {
            nickUI.SetLocalInfo(index, username);
        }
    }

    private void RegisterSelfInGame()
    {
        if (CrownGameManager.Instance != null) { CrownGameManager.Instance.RegisterPlayer(this); return; }
        if (MinigameManager.Instance != null) { MinigameManager.Instance.RegisterPlayer(this); return; }
        if (SurvivalGameManager.Instance != null) { SurvivalGameManager.Instance.RegisterPlayer(this); return; }
    }

    protected virtual void OnEnable()
    {
        if (playerInput != null)
        {
            playerInput.SwitchCurrentActionMap("Control"); // Asegúrate que en Input Actions se llame 'Player'
            playerInput.onActionTriggered += HandleInput;
        }
        UpdateCrownVisual(IsKing);
    }

    protected virtual void OnDisable()
    {
        if (playerInput != null) playerInput.onActionTriggered -= HandleInput;
    }

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

    protected virtual void Jump()
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, 0);
            rb.AddForce(Vector3.up * 8f, ForceMode.Impulse); // Ajusta la fuerza si es necesario
            if (animator) animator.SetTrigger(AnimJump);
        }
    }

    protected virtual void NormalAttack()
    {
        if (PushTimer > 0) return;
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + attackCooldown;

        if (animator) animator.SetTrigger(AnimAttack);
        StartCoroutine(HitCheckRoutine());
        PushTimer = pushCooldown;
    }

    private void DieByInactivity()
    {
        if (!gameObject.activeSelf) return;

        Debug.Log($"Jugador {PlayerIndex + 1} se durmió.");

        if (CrownGameManager.Instance != null) CrownGameManager.Instance.OnPlayerDied(this);
        else if (SurvivalGameManager.Instance != null) SurvivalGameManager.Instance.OnPlayerDied(this);

        if (GameManager.Instance != null) GameManager.Instance.TriggerCameraShake();

        gameObject.SetActive(false);
    }

    protected virtual void UltimateAttack() { }
    private void StartCharging() { }
    private void StopCharging() { }

    protected virtual void Update()
    {
        if (rb.linearVelocity.magnitude > 0.1f) InactivityTimer += Time.deltaTime * 2f;
        else InactivityTimer -= Time.deltaTime;

        InactivityTimer = Mathf.Clamp(InactivityTimer, 0, maxInactivityTime);

        if (InactivityTimer <= 0) DieByInactivity();
        if (PushTimer > 0) PushTimer -= Time.deltaTime;
    }

    protected virtual void FixedUpdate()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (CurrentState == PlayerState.Normal)
        {
            HandleMovementAndRotation();
            ApplyBetterGravity();
        }
        else if (CurrentState == PlayerState.Knockback)
        {
            ApplyBetterGravity();
        }

        if (animator)
        {
            animator.SetFloat(AnimSpeed, Mathf.Abs(rb.linearVelocity.x));
            animator.SetBool(AnimGrounded, isGrounded);
        }
    }

    private void HandleMovementAndRotation()
    {
        // 1. MOVIMIENTO (Esto estaba bien, lo dejamos igual)
        float targetSpeed = moveInput * velocidad;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        if (!isGrounded) accelRate *= airControlMultiplier;

        float newSpeedX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(newSpeedX, rb.linearVelocity.y, 0f);

        // 2. ROTACIÓN (Versión Corregida y Simplificada)
        // Solo intentamos rotar si hay un input significativo
        if (Mathf.Abs(moveInput) > 0.1f)
        {
            // Determinamos el ángulo objetivo: 90 (Derecha) o -90 (Izquierda)
            float targetAngleY = (moveInput > 0) ? 90f : -90f;
            Quaternion targetRotation = Quaternion.Euler(0, targetAngleY, 0);

            // Usamos Quaternion.Slerp para una rotación suave y natural
            // Time.fixedDeltaTime * rotationSpeed * 10 es un buen factor de velocidad
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed * 0.5f);
        }

        // --- PROTECCIÓN CONTRA DOTWEEN ---
        // A veces DOTween modifica la escala o rotación y deja "basura" en el eje Z/X.
        // Forzamos que la rotación en X y Z sea siempre 0 para mantener al personaje de pie.
        Vector3 currentEuler = transform.rotation.eulerAngles;
        if (Mathf.Abs(currentEuler.x) > 1f || Mathf.Abs(currentEuler.z) > 1f)
        {
            transform.rotation = Quaternion.Euler(0, currentEuler.y, 0);
        }
    }

    private void ApplyBetterGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

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
                dir.z = 0;
                dir.Normalize();

                victim.ApplyKnockback(dir, punchForce);

                if (CrownGameManager.Instance != null && victim.IsKing)
                {
                    CrownGameManager.Instance.TransferCrown(this);
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
        if (CurrentState != PlayerState.Normal) return;

        CurrentState = PlayerState.Knockback;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(dir * force, ForceMode.Impulse);
        rb.AddForce(Vector3.up * verticalKnockup, ForceMode.Impulse);

        StartCoroutine(RecoverFromKnockback());
    }

    private IEnumerator RecoverFromKnockback()
    {
        yield return new WaitForSeconds(knockbackDuration);
        CurrentState = PlayerState.Normal;
    }

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