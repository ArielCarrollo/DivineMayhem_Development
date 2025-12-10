using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class SpikyBallHazard : NetworkBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float initialSpeed = 8f;
    [SerializeField] private float accelerationPerSecond = 2.0f;
    [SerializeField] private float maxSpeed = 35f;

    [SerializeField] private float minAxisSpeedRatio = 0.3f;

    private Rigidbody rb;
    private float currentSpeed;
    private bool isLaunched = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // CORRECCIÓN: Congelar Z (profundidad) para que no caiga al vacío en un juego 2D
        // y congelar rotaciones para que no afecten la trayectoria de forma rara.
        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotation;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            rb.isKinematic = true;
            enabled = false;
        }
        else
        {
            currentSpeed = initialSpeed;
        }
    }

    public void LaunchBall()
    {
        if (!IsServer) return;

        isLaunched = true;
        currentSpeed = initialSpeed;

        // Lanzamiento diagonal en X e Y
        float xDir = Random.Range(0, 2) == 0 ? 1 : -1;
        float yDir = Random.Range(0, 2) == 0 ? 1 : -1;
        Vector3 dir = new Vector3(xDir, yDir, 0).normalized;

        rb.linearVelocity = dir * currentSpeed;
    }

    public void StopBall()
    {
        isLaunched = false;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void ResetSpeed()
    {
        if (!IsServer) return;
        currentSpeed = initialSpeed;
        if (rb.linearVelocity != Vector3.zero)
            rb.linearVelocity = rb.linearVelocity.normalized * currentSpeed;
    }

    private void FixedUpdate()
    {
        if (!IsServer || !isLaunched) return;

        // 1. Aceleración
        if (currentSpeed < maxSpeed)
        {
            currentSpeed += accelerationPerSecond * Time.fixedDeltaTime;
        }

        Vector3 velocity = rb.linearVelocity;

        // 2. CORRECCIÓN "DVD" (Versión X/Y)
        // Evitamos que se quede rebotando solo vertical u horizontalmente.
        Vector3 dir = velocity.normalized;
        bool corrected = false;

        // Si X es muy lento (rebote vertical puro)
        if (Mathf.Abs(dir.x) < minAxisSpeedRatio)
        {
            float sign = (dir.x == 0) ? (Random.Range(0, 2) == 0 ? 1 : -1) : Mathf.Sign(dir.x);
            dir.x = sign * minAxisSpeedRatio;
            corrected = true;
        }

        // Si Y es muy lento (rebote horizontal puro)
        if (Mathf.Abs(dir.y) < minAxisSpeedRatio)
        {
            float sign = (dir.y == 0) ? (Random.Range(0, 2) == 0 ? 1 : -1) : Mathf.Sign(dir.y);
            dir.y = sign * minAxisSpeedRatio;
            corrected = true;
        }

        // Forzamos Z a 0 por seguridad
        dir.z = 0;

        if (corrected)
        {
            dir = dir.normalized;
        }

        rb.linearVelocity = dir * currentSpeed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.TryGetComponent<CharacterBase>(out CharacterBase player))
        {
            if (CrazyBallGameManager.Instance != null)
            {
                CrazyBallGameManager.Instance.OnPlayerEliminated(player);
            }
        }

        if (collision.gameObject.TryGetComponent<DestructibleHealth>(out DestructibleHealth block))
        {
            block.TakeHit(10, transform.position);
        }
    }
}