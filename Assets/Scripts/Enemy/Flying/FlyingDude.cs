using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class BatEnemy : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Animator animator;

    [Header("Patrulla Aérea")]
    [Tooltip("Puntos de patrulla. El murciélago volará entre estos puntos")]
    public Transform[] patrolPoints;
    [Tooltip("Velocidad durante la patrulla")]
    public float patrolSpeed = 2f;
    [Tooltip("Distancia mínima para considerar que llegó a un punto")]
    public float waypointReachDistance = 0.3f;
    [Tooltip("Tiempo de espera al llegar a un punto")]
    public float waitTimeAtPoint = 1f;

    [Header("Detección y Persecución")]
    [Tooltip("Distancia para detectar al jugador")]
    public float detectionRange = 8f;
    [Tooltip("Distancia máxima de persecución (si se aleja más, vuelve a patrullar)")]
    public float maxChaseDistance = 12f;
    [Tooltip("Velocidad durante la persecución")]
    public float chaseSpeed = 4f;
    [Tooltip("Distancia a la que se mantiene del jugador al perseguir")]
    public float keepDistanceFromPlayer = 2f;

    [Header("Ataque de Contacto")]
    [Tooltip("Daño que hace al tocar al jugador")]
    public int contactDamage = 10;
    [Tooltip("Cooldown entre ataques de contacto")]
    public float attackCooldown = 1.5f;

    [Header("Configuración de Volteo")]
    [Tooltip("Umbral mínimo de movimiento en X para voltear (evita flipping infinito)")]
    public float flipThreshold = 0.1f;

    [Header("Debug")]
    public bool showDebugLogs = true;
    public bool showDebugGizmos = true;

    // Variables privadas
    private Rigidbody2D rb;
    private int currentPatrolIndex = 0;
    private bool isFacingRight = true;
    private float lastAttackTime = 0f;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    private enum BatState
    {
        Patrolling,
        Chasing,
        Returning
    }

    private BatState currentState = BatState.Patrolling;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Configurar Rigidbody2D para vuelo
        rb.gravityScale = 0f; // Sin gravedad para volar
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Buscar jugador si no está asignado
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                DebugLog("Jugador encontrado automáticamente");
            }
            else
            {
                Debug.LogWarning("[BAT] No se encontró el jugador!");
            }
        }

        // Validar puntos de patrulla
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogError("[BAT] ¡No hay puntos de patrulla asignados!");
        }

        DebugLog("Murciélago inicializado en modo patrulla");
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Máquina de estados
        switch (currentState)
        {
            case BatState.Patrolling:
                UpdatePatrolling(distanceToPlayer);
                break;

            case BatState.Chasing:
                UpdateChasing(distanceToPlayer);
                break;

            case BatState.Returning:
                UpdateReturning(distanceToPlayer);
                break;
        }

        // Actualizar animaciones si hay animator
        if (animator != null)
        {
            // Verificar si los parámetros existen antes de establecerlos
            if (HasParameter(animator, "IsChasing"))
                animator.SetBool("IsChasing", currentState == BatState.Chasing);

            if (HasParameter(animator, "Speed"))
                animator.SetFloat("Speed", rb.linearVelocity.magnitude);
        }
    }

    void UpdatePatrolling(float distanceToPlayer)
    {
        // Si detecta al jugador, cambiar a persecución
        if (distanceToPlayer <= detectionRange)
        {
            DebugLog($"¡Jugador detectado a {distanceToPlayer:F2}m! Iniciando persecución");
            currentState = BatState.Chasing;
            isWaiting = false;
            return;
        }

        // Si no hay puntos de patrulla, quedarse quieto
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Si está esperando en un punto
        if (isWaiting)
        {
            rb.linearVelocity = Vector2.zero;
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                // Avanzar al siguiente punto
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                DebugLog($"Continuando hacia punto {currentPatrolIndex}");
            }
            return;
        }

        // Moverse hacia el punto de patrulla actual
        Transform targetPoint = patrolPoints[currentPatrolIndex];

        if (targetPoint == null)
        {
            Debug.LogWarning($"[BAT] Punto de patrulla {currentPatrolIndex} es null!");
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            return;
        }

        Vector2 directionToPoint = (targetPoint.position - transform.position);
        float distanceToPoint = directionToPoint.magnitude;
        directionToPoint.Normalize();

        // SOLUCIÓN: Solo voltear si hay movimiento significativo en X
        if (Mathf.Abs(directionToPoint.x) > flipThreshold)
        {
            FlipTowardsDirection(directionToPoint.x);
        }

        // Moverse hacia el punto
        rb.linearVelocity = directionToPoint * patrolSpeed;

        // Si llegó al punto
        if (distanceToPoint <= waypointReachDistance)
        {
            DebugLog($"Llegó al punto {currentPatrolIndex}, esperando...");
            isWaiting = true;
            waitTimer = waitTimeAtPoint;
            rb.linearVelocity = Vector2.zero;
        }
    }

    void UpdateChasing(float distanceToPlayer)
    {
        // Si el jugador se alejó mucho, volver a patrullar
        if (distanceToPlayer > maxChaseDistance)
        {
            DebugLog($"Jugador muy lejos ({distanceToPlayer:F2}m), volviendo a patrulla");
            currentState = BatState.Returning;
            return;
        }

        // Calcular dirección hacia el jugador
        Vector2 directionToPlayer = (player.position - transform.position);
        directionToPlayer.Normalize();

        // SOLUCIÓN: Solo voltear si hay movimiento significativo en X
        if (Mathf.Abs(directionToPlayer.x) > flipThreshold)
        {
            FlipTowardsDirection(directionToPlayer.x);
        }

        // Mantener cierta distancia del jugador
        if (distanceToPlayer > keepDistanceFromPlayer)
        {
            // Acercarse al jugador
            rb.linearVelocity = directionToPlayer * chaseSpeed;
        }
        else
        {
            // Mantener distancia (moverse más lento o detenerse)
            rb.linearVelocity = directionToPlayer * (chaseSpeed * 0.3f);
        }

        DebugLog($"Persiguiendo jugador - Distancia: {distanceToPlayer:F2}m");
    }

    void UpdateReturning(float distanceToPlayer)
    {
        // Si detecta al jugador de nuevo, volver a perseguir
        if (distanceToPlayer <= detectionRange)
        {
            DebugLog("¡Jugador detectado nuevamente! Volviendo a persecución");
            currentState = BatState.Chasing;
            return;
        }

        // Si no hay puntos de patrulla, quedarse quieto
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            rb.linearVelocity = Vector2.zero;
            currentState = BatState.Patrolling;
            return;
        }

        // Volver al punto de patrulla más cercano
        Transform closestPoint = GetClosestPatrolPoint();

        if (closestPoint == null)
        {
            currentState = BatState.Patrolling;
            return;
        }

        Vector2 directionToPoint = (closestPoint.position - transform.position);
        float distanceToPoint = directionToPoint.magnitude;
        directionToPoint.Normalize();

        // SOLUCIÓN: Solo voltear si hay movimiento significativo en X
        if (Mathf.Abs(directionToPoint.x) > flipThreshold)
        {
            FlipTowardsDirection(directionToPoint.x);
        }

        // Moverse hacia el punto
        rb.linearVelocity = directionToPoint * patrolSpeed;

        // Si llegó al punto, volver a patrullar
        if (distanceToPoint <= waypointReachDistance)
        {
            DebugLog("Regresó al punto de patrulla, reanudando patrulla");
            currentState = BatState.Patrolling;
            isWaiting = false;
            rb.linearVelocity = Vector2.zero;
        }
    }

    Transform GetClosestPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return null;

        Transform closest = patrolPoints[0];
        float minDistance = Vector2.Distance(transform.position, patrolPoints[0].position);

        for (int i = 1; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;

            float distance = Vector2.Distance(transform.position, patrolPoints[i].position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = patrolPoints[i];
                currentPatrolIndex = i;
            }
        }

        return closest;
    }

    void FlipTowardsDirection(float directionX)
    {
        // Solo voltear si la dirección es significativamente diferente a la actual
        bool shouldFaceRight = directionX > 0;

        if (shouldFaceRight != isFacingRight)
        {
            Flip();
        }
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;

        DebugLog($"Volteado - Mirando derecha: {isFacingRight}");
    }

    // Daño por contacto
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Verificar cooldown de ataque
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.RecibirDaño(contactDamage, transform.position.x);
                    lastAttackTime = Time.time;
                    DebugLog($"¡Golpeó al jugador! Daño: {contactDamage}");
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Alternativa si usas triggers en lugar de colliders
        if (collision.CompareTag("Player"))
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.RecibirDaño(contactDamage, transform.position.x);
                    lastAttackTime = Time.time;
                    DebugLog($"¡Golpeó al jugador! Daño: {contactDamage}");
                }
            }
        }
    }

    void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[BAT {Time.time:F2}s] {message}");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Rango de detección (amarillo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango máximo de persecución (rojo)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxChaseDistance);

        // Distancia de mantenimiento (verde)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, keepDistanceFromPlayer);

        // Líneas entre puntos de patrulla
        if (patrolPoints != null && patrolPoints.Length > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;

                // Dibujar esfera en cada punto
                Gizmos.DrawWireSphere(patrolPoints[i].position, 0.3f);

                // Dibujar línea al siguiente punto
                int nextIndex = (i + 1) % patrolPoints.Length;
                if (patrolPoints[nextIndex] != null)
                {
                    Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[nextIndex].position);
                }
            }

            // Marcar punto actual con color diferente
            if (currentPatrolIndex < patrolPoints.Length && patrolPoints[currentPatrolIndex] != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(patrolPoints[currentPatrolIndex].position, 0.5f);
            }
        }

        // Línea hacia el objetivo actual
        if (Application.isPlaying)
        {
            if (currentState == BatState.Chasing && player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, player.position);
            }
            else if (currentState == BatState.Patrolling && patrolPoints != null && patrolPoints.Length > 0)
            {
                if (patrolPoints[currentPatrolIndex] != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, patrolPoints[currentPatrolIndex].position);
                }
            }
        }
    }

    // Método auxiliar para verificar si un parámetro existe
    bool HasParameter(Animator animator, string paramName)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
}