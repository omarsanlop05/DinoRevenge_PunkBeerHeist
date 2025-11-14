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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Inicializar(float dir, Transform jefe)
    {
        direccion = dir;
        jefePosicion = jefe;
        rb.linearVelocity = new Vector2(velocidad * direccion, 0);

        Destroy(gameObject, tiempoDeVida);
    }

    void Update()
    {
        // Rotar el hacha mientras vuela
        transform.Rotate(0, 0, velocidadRotacion * Time.deltaTime * direccion);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Si golpea al jugador
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null && jefePosicion != null)
            {
                // Pasar el daño y la posición X del jefe
                playerHealth.RecibirDaño(danio, jefePosicion.position.x);
            }

            Destroy(gameObject);
        }

        // Si golpea paredes u obstáculos
        if (collision.CompareTag("Pared") || collision.CompareTag("Suelo"))
        {
            Destroy(gameObject);
        }
    }
}