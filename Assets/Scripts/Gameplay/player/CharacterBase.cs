using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using static Sirenix.OdinInspector.Editor.Internal.FastDeepCopier;
public enum PlayerState
{
    Normal,
    Knockback,
    Charging

}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkAnimator))]
public abstract class CharacterBase : NetworkBehaviour
{
    [Header("Stats Base del Personaje")]
    [SerializeField] protected float velocidad = 8f; // Aumentado ligeramente para compensar la aceleración
    [SerializeField] protected int vidaMaxima = 100;
    [SerializeField] protected int fuerzaBase = 10;
    [SerializeField] protected int nivelBase = 1;
    [SerializeField] protected float estaminaMaxima = 100f;

    // --- NUEVO: FÍSICAS DE MOVIMIENTO ---
    [Header("Fluidez de Movimiento (Physics)")]
    [SerializeField, Tooltip("Qué tan rápido alcanza la velocidad máxima")]
    private float acceleration = 60f;
    [SerializeField, Tooltip("Qué tan rápido frena al soltar el input")]
    private float deceleration = 40f;
    [SerializeField, Tooltip("Control en el aire (0 a 1). 1 es igual que en suelo.")]
    private float airControlMultiplier = 0.5f;
    [SerializeField, Tooltip("Multiplicador de gravedad para caer más rápido (sensación de peso)")]
    private float fallMultiplier = 2.5f;
    // ------------------------------------

    [Header("Lógica de Estamina")]
    [SerializeField] private float staminaChargeRate = 20f;
    private Coroutine chargingCoroutine;

    [Header("Lógica de Movimiento")]
    [SerializeField] private float rotationSpeed = 20f; // Más rápido para respuesta instantánea visual

    [Header("Lógica de Salto")]
    [SerializeField] private float jumpForce = 15f; // Ajustado para trabajar con la nueva gravedad
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundDistance = 0.3f;

    [Header("Configuración de Minijuego")]
    [SerializeField] private GameObject crownVisual;

    [Header("Componentes")]
    protected Rigidbody rb;
    protected Animator animator;

    [Header("Lógica de Ataque Básico")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private float hitRadius = 0.5f;
    [SerializeField] private float punchForce = 15f;
    [SerializeField] private LayerMask hitableLayers;
    [SerializeField] private float attackDelay = 0.1f; // Más responsivo
    [SerializeField] private float attackCooldown = 0.5f;
    private float nextAttackTime = 0f;

    [Header("Lógica de Knockback")]
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private float verticalKnockup = 7f;

    // Network Variables
    public NetworkVariable<bool> IsKing = new NetworkVariable<bool>(false);
    public NetworkVariable<PlayerState> CurrentState = new NetworkVariable<PlayerState>(PlayerState.Normal);
    public NetworkVariable<int> Vida = new NetworkVariable<int>();
    public NetworkVariable<int> Fuerza = new NetworkVariable<int>();
    public NetworkVariable<int> Nivel = new NetworkVariable<int>();
    public NetworkVariable<float> Estamina = new NetworkVariable<float>();

    private bool controlsEnabled = true;
    public float EstaminaMaxima { get { return estaminaMaxima; } }

    private float serverMoveInput;
    private bool serverIsGrounded;
    private float clientMoveInput;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // --- NUEVO: Configuración de Rigidbody para evitar deslizamientos raros ---
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Suaviza visualmente
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); // ¡Siempre primero!
        SetInputActive(true);
        transform.rotation = Quaternion.Euler(0, 90, 0);

        if (IsServer)
        {
            Vida.Value = vidaMaxima;
            Fuerza.Value = fuerzaBase;
            Nivel.Value = nivelBase;
            Estamina.Value = estaminaMaxima;
        }

        // --- CAMBIO AQUÍ: INICIAMOS EL REGISTRO SEGURO ---
        StartCoroutine(WaitForManagersAndRegister());

        IsKing.OnValueChanged += OnKingStatusChanged;
        OnKingStatusChanged(false, IsKing.Value);
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        StartCoroutine(RegisterWithDelay());
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UnregisterPlayer(this);
        }
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.UnregisterPlayer(this);
        }
        IsKing.OnValueChanged -= OnKingStatusChanged;
        OnKingStatusChanged(false, IsKing.Value);

    }
    private IEnumerator WaitForManagersAndRegister()
    {
        while (UIManager.Instance == null)
        {
            yield return null;
        }
        UIManager.Instance.RegisterPlayer(this);

        while (MinigameManager.Instance == null)
        {
            yield return null;
        }
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterPlayer(this);
        }
        // 2. ¿O es el juego de Supervivencia?
        else if (SurvivalGameManager.Instance != null)
        {
            SurvivalGameManager.Instance.RegisterPlayer(this);
        }
    }
    private IEnumerator RegisterWithDelay()
    {
        yield return null;

        if (IsServer) 
        {
            if (MinigameManager.Instance != null)
                MinigameManager.Instance.RegisterPlayer(this);
            else
                Debug.LogError($"[Player {OwnerClientId}] ¡MinigameManager NULL al intentar registrarse!");
        }

        // Todos necesitan UI
        if (UIManager.Instance != null)
            UIManager.Instance.RegisterPlayer(this);
        else
            Debug.LogError($"[Player {OwnerClientId}] ¡UIManager NULL al intentar registrarse!");
    }
    [ClientRpc]
    public void TeleportPlayerClientRpc(Vector3 newPosition)
    {
        // 1. Desactivar física momentáneamente para evitar conflictos
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 2. Mover el objeto (Transform)
        transform.position = newPosition;
        // Opcional: Rotarlo
        transform.rotation = Quaternion.Euler(0, 90, 0);

        // 3. Reactivar física
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // 4. Reactivar controles y resetear estado
        SetInputActive(true); // ¡Esto te devuelve el movimiento!

        // (Solo visual) Asegurar que la animación esté en Idle
        if (animator) animator.Play("Idle"); // O el nombre de tu estado base
    }

    // Función auxiliar para el Servidor
    public void ServerTeleport(Vector3 newPosition)
    {
        // Reseteamos estado lógico en el servidor
        CurrentState.Value = PlayerState.Normal;
        IsKing.Value = false;

        // Ordenamos a TODOS los clientes (incluido el host) que muevan visualmente al jugador
        TeleportPlayerClientRpc(newPosition);
    }
    [ClientRpc]
    public void KillPlayerClientRpc()
    {
       
        SetInputActive(false);

        GetComponent<Collider>().enabled = false;
        rb.isKinematic = true; // Que no caiga al infinito

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
            serverMoveInput = 0;
            if (animator) animator.SetFloat("Speed", 0);
        }
    }
    // --- MANEJO DE INPUT
    public virtual void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value != PlayerState.Normal || !controlsEnabled)
        {
            clientMoveInput = 0; 
            return;
        }
        clientMoveInput = context.ReadValue<float>();
    }
    public virtual void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value != PlayerState.Normal || !controlsEnabled) return;

        if (context.performed)
        {
            JumpServerRpc();
        }
    }
    public virtual void OnNormalAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value != PlayerState.Normal || !controlsEnabled) return;
        
        if (context.performed)
        {
            NormalAttackServerRpc();
        }
    }
    public virtual void OnUltimateAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value != PlayerState.Normal || !controlsEnabled) return;
        
        if (context.performed)
        {
            UltimateAttackServerRpc();
        }
    }
    public virtual void OnCharge(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value == PlayerState.Knockback || !controlsEnabled) return;

        // Si presionó el botón
        if (context.performed)
        {
            ChargeStaminaServerRpc(true);
        }
        // Si soltó el botón
        else if (context.canceled)
        {
            ChargeStaminaServerRpc(false);
        }
    }

    [Rpc(SendTo.Server)]
    protected virtual void UpdateServerMovementRpc(float moveInput)
    {
        this.serverMoveInput = moveInput;
    }

    [Rpc(SendTo.Server)]
    protected virtual void JumpServerRpc()
    {
        if (serverIsGrounded)
        {
            // Resetear velocidad Y para que el salto sea consistente incluso si bajabas una pendiente
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, 0);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetTrigger("Jump");
        }
    }

    [Rpc(SendTo.Server)]
    protected virtual void NormalAttackServerRpc()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }
        nextAttackTime = Time.time + attackCooldown;
        Debug.Log("SERVIDOR: ¡Iniciando PUÑETE BASE!");
        animator.SetTrigger("NormalAttack");
        StartCoroutine(HitCheckDelay());
    }

    [Rpc(SendTo.Server)]
    protected virtual void UltimateAttackServerRpc()
    {
        Debug.Log("SERVIDOR: Ulti base (no hace nada)");
    }
    [Rpc(SendTo.Server)]
    protected virtual void ChargeStaminaServerRpc(bool startCharging)
    {
        if (startCharging && CurrentState.Value == PlayerState.Normal)
        {
            CurrentState.Value = PlayerState.Charging;

            chargingCoroutine = StartCoroutine(ChargeStaminaCoroutine());
        }
        else if (!startCharging || CurrentState.Value != PlayerState.Charging)
        {
            CurrentState.Value = PlayerState.Normal;

            if (chargingCoroutine != null)
            {
                StopCoroutine(chargingCoroutine);
                chargingCoroutine = null;
            }
        }
    }

    private IEnumerator ChargeStaminaCoroutine()
    {
        Debug.Log("Servidor: Empezando a cargar Estamina...");
        while (Estamina.Value < estaminaMaxima)
        {
            Estamina.Value += staminaChargeRate * Time.deltaTime;

            Estamina.Value = Mathf.Clamp(Estamina.Value, 0, estaminaMaxima);

            yield return null;
        }

        Debug.Log("Servidor: Estamina llena.");
        CurrentState.Value = PlayerState.Normal;
        chargingCoroutine = null;
    }

    protected virtual void Update()
    {
        if (!IsOwner) return;
        // Optimización: Solo enviar RPC si hay un cambio significativo o cada X frames podría ser mejor, 
        // pero por ahora lo dejamos en Update para responsividad.
        UpdateServerMovementRpc(clientMoveInput);
    }

    protected virtual void FixedUpdate()
    {
        if (!IsServer) return;

        serverIsGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (CurrentState.Value == PlayerState.Normal)
        {
            HandleMovementAndRotation();
            ApplyBetterGravity(); // --- NUEVO ---
        }
        else if (CurrentState.Value == PlayerState.Knockback)
        {
            ApplyBetterGravity(); // Aplicar gravedad también en knockback
        }

        // Animaciones
        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsGrounded", serverIsGrounded);
    }

    // --- AQUÍ ESTÁ LA MAGIA DE LA FLUIDEZ ---
    private void HandleMovementAndRotation()
    {
        // 1. Calcular velocidad objetivo
        float targetSpeed = serverMoveInput * velocidad;

        // 2. Definir tasa de cambio (Aceleración vs Deceleración)
        // Si el jugador quiere moverse (input != 0) usamos aceleración.
        // Si el jugador suelta (input == 0) o cambia de dirección, usamos deceleración (fricción).
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;

        // Si estamos en el aire, aplicamos el multiplicador de control aéreo
        if (!serverIsGrounded)
        {
            accelRate *= airControlMultiplier;
        }

        // 3. Aplicar movimiento suavizado (Mathf.MoveTowards)
        // Esto evita el "snapping" instantáneo
        float newSpeedX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newSpeedX, rb.linearVelocity.y, 0f);

        // 4. Rotación (Visual)
        // Solo rotamos si hay input significativo, no basado en velocidad residual (para evitar giros raros al frenar)
        if (Mathf.Abs(serverMoveInput) > 0.1f)
        {
            Quaternion targetRotation = (serverMoveInput > 0)
                                        ? Quaternion.Euler(0, 90, 0)
                                        : Quaternion.Euler(0, -90, 0);

            // Usamos RotateTowards para una rotación más lineal y controlada que Slerp
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime * 10f);
        }
    }

    // --- NUEVO: Hace que la caída se sienta pesada y rápida (estilo Mario/Celeste/Hollow Knight) ---
    private void ApplyBetterGravity()
    {
        // Si estamos cayendo (velocidad Y negativa), aplicamos gravedad extra
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        // Salto pequeño: Si el jugador suelta el botón de salto antes de llegar al pico, 
        // incrementamos gravedad (lógica del lado del servidor es difícil de predecir sin input de "soltar", 
        // así que por ahora solo usamos caída rápida).
    }
    public void HitCheck()
    {
        if (!IsServer) return;

        Collider[] hits = Physics.OverlapSphere(hitPoint.position, hitRadius, hitableLayers);

        foreach (Collider hit in hits)
        {
            if (hit.transform == this.transform) continue;

            // --- ¡AQUÍ ESTÁ LA CORRECCIÓN! ---

            // Opción 1: ¿Es un jugador?
            if (hit.TryGetComponent<CharacterBase>(out CharacterBase victimPlayer))
            {
                // 1. Calcular la dirección SÓLO HORIZONTAL
                Vector3 horizontalDir = (victimPlayer.transform.position - transform.position);
                horizontalDir.y = 0; // Ignorar diferencia de altura
                horizontalDir.z = 0; // Asegurar que es 2D
                horizontalDir.Normalize(); // Dirección pura (izquierda o derecha)

                ulong attackerId = this.OwnerClientId;
                ulong victimId = victimPlayer.OwnerClientId;

                bool victimIsKing = (MinigameManager.Instance.CurrentKingId.Value == victimId);

                if (victimIsKing && attackerId != victimId)
                {
                    MinigameManager.Instance.TransferCrown(this);
                }

                // 2. Pasar SÓLO el vector horizontal al knockback
                victimPlayer.ApplyKnockback(horizontalDir, punchForce);
            }
            // Opción 2: ¿Es un objeto (barril, etc.)?
            else if (hit.TryGetComponent<Rigidbody>(out Rigidbody objectRb))
            {
                // A los objetos sí les damos la dirección original (con el 'up')
                Vector3 objectDirection = (hit.transform.position - transform.position).normalized + (Vector3.up * 0.3f);
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
        if (!IsServer) return;

        if (CurrentState.Value != PlayerState.Normal) return;

        CurrentState.Value = PlayerState.Knockback;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        rb.AddForce(horizontalDirection * horizontalForce, ForceMode.Impulse); // Fuerza Horizontal (costado)
        rb.AddForce(Vector3.up * verticalKnockup, ForceMode.Impulse);          // Fuerza Vertical (arriba)

        StartCoroutine(KnockbackCooldown());
    }

    private IEnumerator KnockbackCooldown()
    {
        yield return new WaitForSeconds(knockbackDuration);

        CurrentState.Value = PlayerState.Normal;
    }
}