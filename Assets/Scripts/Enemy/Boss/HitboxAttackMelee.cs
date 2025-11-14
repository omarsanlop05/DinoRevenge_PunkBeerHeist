using UnityEngine;

public class HitboxAttackMelee : MonoBehaviour
{
    [Header("Configuración")]
    public float danio = 20f;
    public LayerMask capaJugador;

    private bool yaGolpeo;
    private Transform jefePosicion;

    void Start()
    {
        // Obtener referencia al jefe (padre)
        jefePosicion = transform.parent;
    }

    void OnEnable()
    {
        // Resetear al activarse (útil si reutilizas la hitbox)
        yaGolpeo = false;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (yaGolpeo) return;

        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null && jefePosicion != null)
            {
                // Pasar el daño y la posición X del jefe
                playerHealth.RecibirDaño(danio, jefePosicion.position.x);
                yaGolpeo = true;
            }
        }
    }
}

