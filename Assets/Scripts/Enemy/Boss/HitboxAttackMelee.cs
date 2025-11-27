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
        // CLAVE: Resetear al activarse para permitir golpear de nuevo
        yaGolpeo = false;
        Debug.Log("[HITBOX MELEE] Hitbox activada - yaGolpeo reseteado");
    }

    void OnDisable()
    {
        // Resetear también al desactivarse para el próximo ataque
        yaGolpeo = false;
        Debug.Log("[HITBOX MELEE] Hitbox desactivada - yaGolpeo reseteado");
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
                Debug.Log($"[HITBOX MELEE] ¡Golpe conectado! Daño: {danio}");
            }
        }
    }

    // SOLUCIÓN: Agregar OnTriggerStay2D para detectar jugadores que ya están dentro
    void OnTriggerStay2D(Collider2D collision)
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
                Debug.Log($"[HITBOX MELEE] ¡Golpe conectado (Stay)! Daño: {danio}");
            }
        }
    }
}