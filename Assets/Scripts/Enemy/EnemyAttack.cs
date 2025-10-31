using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Detección del jugador")]
    public Transform player;
    public float detectionRange = 8f;

    [Header("Ataque")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 1.5f;
    public float projectileSpeed = 10f;

    private float nextFireTime = 0f;

    void Update()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // Si el jugador está en rango
        if (distance <= detectionRange)
        {
            // Mirar hacia el jugador
            Vector3 dir = player.position - transform.position;
            if (dir.x > 0)
                transform.localScale = new Vector3(1, 1, 1);
            else
                transform.localScale = new Vector3(-1, 1, 1);

            // Disparar si ya pasó el tiempo
            if (Time.time >= nextFireTime)
            {
                Shoot(dir.normalized);
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    void Shoot(Vector2 direction)
    {
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();

        rb.linearVelocity = direction * projectileSpeed;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
