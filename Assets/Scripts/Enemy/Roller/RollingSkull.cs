using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RollingSkull_Raycast : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    public Vector2 moveDirection = Vector2.left;

    [Header("Rotation")]
    public float rotationSpeed = 360f;
    public Transform spriteTransform;

    [Header("Sensors")]
    public float sensorDistance = 0.25f;          // distancia del raycast
    public float sensorHeightOffset = 0.1f;       // sube el origen del raycast para evitar suelo
    public LayerMask obstacleMask;                // asigna el layer del tilemap/walls

    [Header("Damage")]
    public int damage = 15;

    private Rigidbody2D rb;
    private int rotationDirection = 1;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 1.5f;

        if (spriteTransform == null) spriteTransform = transform;
    }

    void FixedUpdate()
    {
        // mover
        rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        spriteTransform.Rotate(Vector3.forward * rotationDirection * rotationSpeed * Time.fixedDeltaTime);

        // sensor: raycast desde un poco arriba del centro
        Vector2 origin = (Vector2)transform.position + Vector2.up * sensorHeightOffset;
        Vector2 dir = Vector2.right * Mathf.Sign(moveDirection.x);

        RaycastHit2D hit = Physics2D.Raycast(origin, dir, sensorDistance, obstacleMask);
        Debug.DrawRay(origin, dir * sensorDistance, Color.red); // útil en escena

        if (hit.collider != null)
        {
            // chequeo extra de normal para evitar falsos positivos
            if (Mathf.Abs(hit.normal.x) > 0.2f)
            {
                InvertDirection();
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth health = collision.gameObject.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.RecibirDaño(damage, transform.position.x);
                Debug.Log("RollingSkull: daño aplicado al jugador.");
            }
        }
    }

    void InvertDirection()
    {
        moveDirection.x *= -1;
        rotationDirection *= -1;
        rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        
    }
}
