using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpikyBallHazard : MonoBehaviour
{
    [Header("Movimiento (Modo DVD)")]
    [SerializeField] private float initialSpeed = 8f;
    [SerializeField] private float maxSpeed = 30f; // Un poco más rápido tope
    [SerializeField] private float speedIncreaseRate = 0.5f;

    [Tooltip("Velocidad mínima en un eje para evitar rebotes planos o atascos.")]
    [SerializeField] private float minAxisSpeed = 2f;

    [SerializeField] private Vector2 initialDirection = new Vector2(1, 1);

    [Header("Daño")]
    [SerializeField] private int damageToBlocksPerHit = 1;

    private Rigidbody rb;
    private float currentSpeed;
    private float fixedZPosition = 0f;
    private bool isActive = false; // Para saber si el juego empezó

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotation;

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        fixedZPosition = transform.position.z;
    }

    // Ya no lanzamos en OnEnable, esperamos la orden del Manager
    private void OnEnable()
    {
        // Asegurar posición y parada inicial
        StopBall();
    }

    public void ActivateBall()
    {
        isActive = true;
        currentSpeed = initialSpeed;

        // Reset posición Z
        Vector3 startPos = transform.position;
        startPos.z = fixedZPosition;
        transform.position = startPos;

        LaunchBall();
    }

    public void StopBall()
    {
        isActive = false;
        rb.linearVelocity = Vector3.zero;
    }
    public void ResetSpeed()
    {
        currentSpeed = initialSpeed;
        // Opcional: Si quisieras que cambie de color al resetearse para dar feedback visual
        // GetComponent<Renderer>().material.color = Color.white; 
    }
    private void LaunchBall()
    {
        Vector3 dir = new Vector3(initialDirection.x, initialDirection.y, 0).normalized;
        // Evitar ceros perfectos
        if (Mathf.Abs(dir.x) < 0.1f) dir.x += 0.5f;
        if (Mathf.Abs(dir.y) < 0.1f) dir.y += 0.5f;
        dir.Normalize();

        rb.linearVelocity = dir * currentSpeed;
    }

    private void Update()
    {
        if (!isActive) return;

        // Aumentar velocidad progresivamente
        if (currentSpeed < maxSpeed)
        {
            currentSpeed += speedIncreaseRate * Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        if (!isActive) return;

        // 1. MANTENER VELOCIDAD CONSTANTE
        if (rb.linearVelocity.magnitude > 0.01f)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * currentSpeed;
        }

        // 2. ANTI-ATASCO (CRUCIAL)
        // Si la velocidad en X o Y es muy baja, significa que está rebotando muy plano
        // o se quedó pillada en una esquina. Forzamos una diagonal mínima.
        Vector3 vel = rb.linearVelocity;
        bool needsCorrection = false;

        if (Mathf.Abs(vel.x) < minAxisSpeed)
        {
            // Le damos velocidad en X conservando el signo (o positivo si es 0)
            float sign = (vel.x >= 0) ? 1 : -1;
            vel.x = sign * minAxisSpeed;
            needsCorrection = true;
        }

        if (Mathf.Abs(vel.y) < minAxisSpeed)
        {
            float sign = (vel.y >= 0) ? 1 : -1;
            vel.y = sign * minAxisSpeed;
            needsCorrection = true;
        }

        if (needsCorrection)
        {
            rb.linearVelocity = vel.normalized * currentSpeed;
        }

        // 3. Corregir Z
        if (Mathf.Abs(transform.position.z - fixedZPosition) > 0.01f)
        {
            Vector3 pos = transform.position;
            pos.z = fixedZPosition;
            transform.position = pos;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;

        ContactPoint cp = collision.contacts[0];
        Vector3 reflectDir = Vector3.Reflect(rb.linearVelocity.normalized, cp.normal);

        reflectDir.z = 0;
        reflectDir.Normalize();

        rb.linearVelocity = reflectDir * currentSpeed;

        if (collision.collider.TryGetComponent<CharacterBase>(out CharacterBase player))
        {
            if (SpikyBallGameManager.Instance != null)
                SpikyBallGameManager.Instance.OnPlayerHitByBall(player);
        }

        var health = collision.collider.GetComponentInParent<DestructibleHealth>();
        if (health != null)
        {
            health.TakeHit(damageToBlocksPerHit, transform.position);
        }
    }
}