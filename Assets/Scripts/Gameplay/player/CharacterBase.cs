using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Normal,
    Knockback,
    Charging
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkTransform))]
public abstract class CharacterBase : NetworkBehaviour
{
    [Header("Stats Base")]
    [SerializeField] protected float velocidad = 8f;
    [SerializeField] protected float estaminaMaxima = 100f;

    [Header("Fluidez de Movimiento")]
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 40f;
    [SerializeField] private float airControlMultiplier = 0.5f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Mecánicas Globales (Inactividad/Empuje)")]
    [SerializeField] private float maxInactivityTime = 5f;
    [SerializeField] private float pushCooldown = 3f;

    // Sincronizamos estos valores para que el HUD los vea
    public NetworkVariable<float> InactivityTimer = new NetworkVariable<float>();
    public NetworkVariable<float> PushTimer = new NetworkVariable<float>();

    public float MaxInactivityTime => maxInactivityTime;
    public float PushCooldown => pushCooldown;

    [Header("Visuales")]
    [SerializeField] private GameObject crownVisual;

    [Header("Combate")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private float hitRadius = 0.5f;
    [SerializeField] private float punchForce = 15f;
    [SerializeField] private LayerMask hitableLayers;
    [SerializeField] private float attackDelay = 0.1f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private float verticalKnockup = 7f;

    // --- Network Variables ---
    public NetworkVariable<bool> IsKing = new NetworkVariable<bool>(false);
    public NetworkVariable<PlayerState> CurrentState = new NetworkVariable<PlayerState>(PlayerState.Normal);
    public NetworkVariable<float> Estamina = new NetworkVariable<float>();

    // Identificador para el HUD (usamos OwnerClientId)
    public int PlayerIndex => (int)OwnerClientId;

    protected Rigidbody rb;
    protected Animator animator;
    private PlayerInput playerInput; // Si usas Input System

    // Inputs locales
    private float clientMoveInput;
    private bool controlsEnabled = true;
    private float nextAttackTime = 0f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;
    private bool isGrounded;
    private float serverMoveInput;

    // Hashes animador
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimJump = Animator.StringToHash("Jump");
    private static readonly int AnimAttack = Animator.StringToHash("NormalAttack");
    private static readonly int AnimGrounded = Animator.StringToHash("IsGrounded");

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();

        PhysicsMaterial slipperyMat = new PhysicsMaterial("PersonajeResbaladizo");
        slipperyMat.dynamicFriction = 0f;
        slipperyMat.staticFriction = 0f;
        slipperyMat.frictionCombine = PhysicsMaterialCombine.Minimum;
        slipperyMat.bounceCombine = PhysicsMaterialCombine.Minimum;
        if (TryGetComponent<Collider>(out Collider col)) col.material = slipperyMat;

        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck_Auto");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, 0.05f, 0);
            groundCheck = gc.transform;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Estamina.Value = estaminaMaxima;
            InactivityTimer.Value = maxInactivityTime;
            PushTimer.Value = 0f;
        }

        IsKing.OnValueChanged += OnKingStatusChanged;
        OnKingStatusChanged(false, IsKing.Value);

        // Registro en los managers (usando tu lógica existente)
        StartCoroutine(RegisterWithManagers());
    }

    public override void OnNetworkDespawn()
    {
        IsKing.OnValueChanged -= OnKingStatusChanged;
    }

    private IEnumerator RegisterWithManagers()
    {
        yield return new WaitForSeconds(0.5f); // Pequeña espera para asegurar que los Singletons existan
        if (IsServer)
        {
            if (CrownGameManagerNetcode.Instance != null) CrownGameManagerNetcode.Instance.RegisterPlayer(this);
            // Agrega aquí otros managers si es necesario
        }
    }

    private void OnKingStatusChanged(bool prev, bool current)
    {
        if (crownVisual != null) crownVisual.SetActive(current);
    }

    public void SetInputActive(bool active)
    {
        controlsEnabled = active;
        if (!active && rb != null) rb.linearVelocity = Vector3.zero;
    }

    // ---------------- INPUT (CLIENTE) ----------------
    // Asumiendo que usas PlayerInput component y Invoke Unity Events o Send Messages
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner || !controlsEnabled) { clientMoveInput = 0; return; }
        Vector2 input = context.ReadValue<Vector2>();
        clientMoveInput = input.x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner || !controlsEnabled) return;
        if (context.performed) JumpServerRpc();
    }

    public void OnNormalAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner || !controlsEnabled) return;
        if (context.performed) NormalAttackServerRpc();
    }

    // ---------------- LÓGICA SERVIDOR ----------------

    protected virtual void Update()
    {
        if (IsOwner)
        {
            // Enviamos input de movimiento al servidor constantemente
            UpdateMoveInputServerRpc(clientMoveInput);
        }

        if (IsServer)
        {
            HandleTimers();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!IsServer) return;

        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (CurrentState.Value == PlayerState.Normal)
        {
            HandleMovementAndRotation();
            ApplyBetterGravity();
        }
        else if (CurrentState.Value == PlayerState.Knockback)
        {
            ApplyBetterGravity();
        }

        if (animator)
        {
            animator.SetFloat(AnimSpeed, Mathf.Abs(rb.linearVelocity.x));
            animator.SetBool(AnimGrounded, isGrounded);
        }
    }

    private void HandleTimers()
    {
        // Lógica de inactividad
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f || Mathf.Abs(rb.linearVelocity.y) > 0.1f)
        {
            // Recupera vida de inactividad si se mueve
            float newValue = InactivityTimer.Value + (Time.deltaTime * 2f);
            InactivityTimer.Value = Mathf.Clamp(newValue, 0, maxInactivityTime);
        }
        else
        {
            // Pierde vida si está quieto
            InactivityTimer.Value -= Time.deltaTime;
        }

        if (InactivityTimer.Value <= 0)
        {
            DieByInactivity();
        }

        // Cooldown de empuje
        if (PushTimer.Value > 0)
        {
            PushTimer.Value -= Time.deltaTime;
        }
    }

    private void HandleMovementAndRotation()
    {
        // 1. Movimiento
        float targetSpeed = serverMoveInput * velocidad;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        if (!isGrounded) accelRate *= airControlMultiplier;

        float newSpeedX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(newSpeedX, rb.linearVelocity.y, 0f);

        // 2. Rotación
        if (Mathf.Abs(serverMoveInput) > 0.1f)
        {
            float targetAngleY = (serverMoveInput > 0) ? 90f : -90f;
            Quaternion targetRotation = Quaternion.Euler(0, targetAngleY, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed * 0.5f);
        }

        // Corrección de rotación indeseada
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

    // ---------------- RPCs ----------------

    [Rpc(SendTo.Server)]
    private void UpdateMoveInputServerRpc(float input)
    {
        serverMoveInput = input;
    }

    [Rpc(SendTo.Server)]
    private void JumpServerRpc()
    {
        if (isGrounded && CurrentState.Value == PlayerState.Normal)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, 0);
            rb.AddForce(Vector3.up * 8f, ForceMode.Impulse); // Ajusta fuerza de salto aquí
            if (animator) animator.SetTrigger(AnimJump);
        }
    }

    [Rpc(SendTo.Server)]
    private void NormalAttackServerRpc()
    {
        if (PushTimer.Value > 0) return; // Cooldown activo

        // PushTimer.Value = pushCooldown; // Reiniciar cooldown
        // Mejor hacerlo al final del ataque o aquí, depende del gusto.

        if (animator) animator.SetTrigger(AnimAttack);
        StartCoroutine(HitCheckRoutine());

        // Aplicar cooldown
        PushTimer.Value = pushCooldown;
    }

    private IEnumerator HitCheckRoutine()
    {
        yield return new WaitForSeconds(attackDelay);

        Vector3 startPoint = transform.position + Vector3.up * 1.0f;
        Vector3 endPoint = hitPoint != null ? hitPoint.position : transform.position + transform.forward;

        Collider[] hits = Physics.OverlapCapsule(startPoint, endPoint, hitRadius, hitableLayers);

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            if (hit.TryGetComponent<CharacterBase>(out CharacterBase victim))
            {
                // ROBAR CORONA (Integración con CrownGameManagerNetcode)
                if (CrownGameManagerNetcode.Instance != null && victim.IsKing.Value)
                {
                    CrownGameManagerNetcode.Instance.TransferCrown(this);
                }

                // EMPUJE FÍSICO
                Vector3 dir = (victim.transform.position - transform.position).normalized;
                dir.y = 0.2f;
                dir.z = 0;
                dir.Normalize();

                victim.ApplyKnockback(dir, punchForce);
            }
        }
    }

    public void ApplyKnockback(Vector3 dir, float force)
    {
        if (!IsServer) return;
        if (CurrentState.Value != PlayerState.Normal) return;

        CurrentState.Value = PlayerState.Knockback;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(dir * force, ForceMode.Impulse);
        rb.AddForce(Vector3.up * verticalKnockup, ForceMode.Impulse);

        StartCoroutine(RecoverFromKnockback());
    }

    private IEnumerator RecoverFromKnockback()
    {
        yield return new WaitForSeconds(knockbackDuration);
        CurrentState.Value = PlayerState.Normal;
    }

    private void DieByInactivity()
    {
        if (!IsServer) return;
        Debug.Log($"Jugador {OwnerClientId} murió de inactividad.");

        // 1. Minijuego Corona
        if (CrownGameManagerNetcode.Instance != null)
            CrownGameManagerNetcode.Instance.OnPlayerDied(this);

        // 2. Minijuego Bola Loca
        if (CrazyBallGameManager.Instance != null)
            CrazyBallGameManager.Instance.OnPlayerEliminated(this);

        // 3. Minijuego Suelo que cae
        if (FallingGameManager.Instance != null)
            FallingGameManager.Instance.OnPlayerFell(this);

        KillPlayerClientRpc();
    }

    [ClientRpc]
    public void KillPlayerClientRpc()
    {
        gameObject.SetActive(false);
        if (GameManager.Instance != null) GameManager.Instance.TriggerCameraShake();
    }

    [ClientRpc]
    public void TeleportPlayerClientRpc(Vector3 pos)
    {
        transform.position = pos;
        if (rb) rb.linearVelocity = Vector3.zero;
    }
}