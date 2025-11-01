using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Movimiento")]
    public float patrolSpeed = 2f;
    public LayerMask groundLayer;
    public Transform groundCheck;
    public Transform wallCheck;
    public float checkRadius = 0.2f;

    [Header("Detección del jugador")]
    public Transform player;
    public float detectionRange = 8f;

    [Header("Ataque")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 1.5f;
    public float projectileSpeed = 10f;

    [Header("Animator")]
    public Animator animator;

    private float nextFireTime = 0f;
    private bool isFacingRight = true;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (player == null)
        {
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            // Detener movimiento y atacar
            rb.linearVelocity = Vector2.zero;
            FacePlayer();
            animator.SetBool("InTarget", true);

            if (Time.time >= nextFireTime)
            {
                Vector2 directionToPlayer = (player.position - firePoint.position).normalized;
                Shoot(directionToPlayer);
                nextFireTime = Time.time + fireRate;
            }
        }
        else
        {
            Patrol();
            animator.SetBool("InTarget", false);
        }
    }

    void Patrol()
    {
        bool isGroundAhead = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        bool isWallAhead = Physics2D.OverlapCircle(wallCheck.position, checkRadius, groundLayer);

        if (!isGroundAhead || isWallAhead)
        {
            Flip();
        }

        float direction = 0f;
        if (isFacingRight)
        {
            direction = 1f;
        }
        else
        {
            direction = -1f;
        }

        rb.linearVelocity = new Vector2(direction * patrolSpeed, rb.linearVelocity.y);
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;

        if (isFacingRight)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    void FacePlayer()
    {
        if (player.position.x > transform.position.x)
        {
            if (!isFacingRight)
            {
                Flip();
            }
        }
        else
        {
            if (isFacingRight)
            {
                Flip();
            }
        }
    }

    void Shoot(Vector2 direction)
    {
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D rbProj = projectile.GetComponent<Rigidbody2D>();
        rbProj.linearVelocity = direction * projectileSpeed;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(wallCheck.position, checkRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}