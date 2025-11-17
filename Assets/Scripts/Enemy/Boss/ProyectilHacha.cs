using UnityEngine;

public class ProyectilHacha : MonoBehaviour
{
    [Header("Configuración")]
    public float velocidad = 8f;
    public float velocidadRotacion = 360f;
    public float danio = 15f;
    public float tiempoDeVida = 5f;

    private Rigidbody2D rb;
    private float direccion;
    private Transform jefePosicion;
    private bool inicializado = false;

    void Awake()
    {
        Debug.Log($"[HACHA] Awake - GameObject: {gameObject.name}");

        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError("[HACHA] ❌ NO TIENE Rigidbody2D!");
        }
        else
        {
            Debug.Log("[HACHA] ✓ Rigidbody2D encontrado");
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError("[HACHA] ❌ NO TIENE SpriteRenderer!");
        }
        else
        {
            Debug.Log($"[HACHA] ✓ SpriteRenderer OK - Sprite: {(sr.sprite != null ? sr.sprite.name : "NULL")}");
            Debug.Log($"[HACHA]   - Enabled: {sr.enabled}, Color: {sr.color}");
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("[HACHA] ❌ NO TIENE Collider2D!");
        }
        else
        {
            Debug.Log($"[HACHA] ✓ Collider2D OK - IsTrigger: {col.isTrigger}");
        }
    }

    void Start()
    {
        Debug.Log($"[HACHA] Start - Posición: {transform.position}");

        if (!inicializado)
        {
            Debug.LogWarning("[HACHA] ⚠️ Start llamado pero NO se llamó Inicializar()!");
        }
    }

    public void Inicializar(float dir, Transform jefe)
    {
        Debug.Log($"[HACHA] === Inicializar llamado ===");
        Debug.Log($"[HACHA] Dirección: {dir}");
        Debug.Log($"[HACHA] Jefe: {(jefe != null ? jefe.name : "NULL")}");

        direccion = dir;
        jefePosicion = jefe;
        inicializado = true;

        if (rb != null)
        {
            Vector2 vel = new Vector2(velocidad * direccion, 0);
            rb.linearVelocity = vel;
            Debug.Log($"[HACHA] ✓ Velocidad asignada: {vel}");
            Debug.Log($"[HACHA] Rigidbody2D bodyType: {rb.bodyType}");
            Debug.Log($"[HACHA] Rigidbody2D gravityScale: {rb.gravityScale}");
        }
        else
        {
            Debug.LogError("[HACHA] ❌ No se pudo asignar velocidad (rb es NULL)");
        }

        Destroy(gameObject, tiempoDeVida);
        Debug.Log($"[HACHA] Programado para destruirse en {tiempoDeVida} segundos");
    }

    void Update()
    {
        // Rotar el hacha mientras vuela
        float rotacion = velocidadRotacion * Time.deltaTime * direccion;
        transform.Rotate(0, 0, rotacion);
    }

    void FixedUpdate()
    {
        if (rb != null && inicializado)
        {
            // Verificar que la velocidad se mantiene
            if (rb.linearVelocity.magnitude < 0.1f)
            {
                Debug.LogWarning($"[HACHA] ⚠️ Velocidad muy baja: {rb.linearVelocity}");
            }
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"[HACHA] OnTriggerEnter2D - Colisionó con: {collision.gameObject.name} (Tag: {collision.tag})");

        // Si golpea al jugador
        if (collision.CompareTag("Player"))
        {
            Debug.Log("[HACHA] ¡Golpeó al jugador!");

            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null && jefePosicion != null)
            {
                playerHealth.RecibirDaño(danio, jefePosicion.position.x);
                Debug.Log($"[HACHA] Daño aplicado: {danio}");
            }
            else
            {
                if (playerHealth == null)
                    Debug.LogWarning("[HACHA] El jugador no tiene componente PlayerHealth");
                if (jefePosicion == null)
                    Debug.LogWarning("[HACHA] jefePosicion es NULL");
            }

            Destroy(gameObject);
        }

    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Por si acaso también está usando colisiones normales
        Debug.Log($"[HACHA] OnCollisionEnter2D (colisión normal) con: {collision.gameObject.name}");
    }

    void OnDestroy()
    {
        Debug.Log($"[HACHA] Siendo destruida en posición: {transform.position}");
    }
}