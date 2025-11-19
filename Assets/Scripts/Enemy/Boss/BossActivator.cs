using UnityEngine;

public class BossActivator : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("El GameObject del jefe que se activará")]
    public GameObject jefe;

    [Tooltip("Tiempo antes de destruir el trigger después de activar")]
    public float tiempoAntesDeDestruir = 0.5f;

    [Header("Efectos Opcionales")]
    [Tooltip("Sonido al activar (opcional)")]
    public AudioClip sonidoActivacion;

    [Tooltip("Partículas al activar (opcional)")]
    public GameObject particulasActivacion;

    private bool yaActivado = false;

    void Start()
    {
        // Verificar que el jefe esté asignado
        if (jefe == null)
        {
            Debug.LogError("¡No se asignó el jefe en el BossTriggerActivator!");
            return;
        }

        // Verificar que este objeto tenga un Collider2D con IsTrigger
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("¡BossTriggerActivator necesita un Collider2D!");
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("¡El Collider2D debería tener 'Is Trigger' activado!");
        }

        Debug.Log($"[TRIGGER] Trigger de activación listo. Esperando al jugador...");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Solo activar una vez y solo cuando el jugador entra
        if (yaActivado) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log($"[TRIGGER] ¡Jugador detectado! Activando jefe...");
            ActivarJefe();
        }
    }

    void ActivarJefe()
    {
        yaActivado = true;

        if (jefe != null)
        {
            // Activar el jefe
            jefe.SetActive(true);
            Debug.Log($"[TRIGGER] ✓ Jefe '{jefe.name}' activado!");

            // Buscar el script BossIA y activarlo
            BossIA bossScript = jefe.GetComponent<BossIA>();
            if (bossScript != null)
            {
                bossScript.ActivarJefe();
                Debug.Log($"[TRIGGER] ✓ Script del jefe inicializado!");
            }
        }

        // Reproducir sonido si está asignado
        if (sonidoActivacion != null)
        {
            AudioSource.PlayClipAtPoint(sonidoActivacion, transform.position);
            Debug.Log($"[TRIGGER] ♪ Sonido de activación reproducido");
        }

        // Instanciar partículas si están asignadas
        if (particulasActivacion != null)
        {
            Instantiate(particulasActivacion, transform.position, Quaternion.identity);
            Debug.Log($"[TRIGGER] ✨ Partículas de activación creadas");
        }

        // Destruir el trigger después de un tiempo
        Destroy(gameObject, tiempoAntesDeDestruir);
        Debug.Log($"[TRIGGER] Trigger programado para destruirse en {tiempoAntesDeDestruir}s");
    }

    // Visualizar el trigger en el editor
    void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Rojo semitransparente

            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.offset, box.size);
            }
            else if (col is CircleCollider2D circle)
            {
                Gizmos.DrawSphere(transform.position + (Vector3)circle.offset, circle.radius);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Dibujar línea hacia el jefe
        if (jefe != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, jefe.transform.position);
            Gizmos.DrawWireSphere(jefe.transform.position, 1f);
        }
    }
}