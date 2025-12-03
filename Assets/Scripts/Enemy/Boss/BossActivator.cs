using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BossActivator : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("El GameObject del jefe que se activará")]
    public GameObject jefe;

    [Tooltip("Tiempo antes de destruir el trigger después de activar")]
    public float tiempoAntesDeDestruir = 0.5f;

    [Header("Música del Boss")]
    [Tooltip("Clip de audio para la música del boss")]
    public AudioClip musicaBoss;

    [Tooltip("¿Reproducir música en loop?")]
    private bool musicaEnLoop = true;

    [Header("Objeto Adicional")]
    [Tooltip("Objeto adicional que se activará con el boss (opcional)")]
    public GameObject objetoAdicional;

    [Header("Efectos Opcionales")]
    [Tooltip("Sonido al activar (opcional)")]
    public AudioClip sonidoActivacion;

    [Tooltip("Partículas al activar (opcional)")]
    public GameObject particulasActivacion;

    [Tooltip("Duración del destello (segundos)")]
    public float duracionDestello = 0.5f;

    private bool yaActivado = false;

    void Start()
    {
        // Verificar que el jefe esté asignado
        if (jefe == null)
        {
            Debug.LogError("¡No se asignó el jefe en el BossActivator!");
            return;
        }

        // Verificar que este objeto tenga un Collider2D con IsTrigger
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("¡BossActivator necesita un Collider2D!");
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

        // Activar el jefe
        if (jefe != null)
        {
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

        // Activar objeto adicional
        if (objetoAdicional != null)
        {
            objetoAdicional.SetActive(true);
            Debug.Log($"[TRIGGER] ✓ Objeto adicional '{objetoAdicional.name}' activado!");
        }

        // Reproducir música del boss con SoundManager
        if (musicaBoss != null && SoundManager.instance != null)
        {
            if (musicaEnLoop)
            {
                // Usar loopSource para música continua
                SoundManager.instance.gameMusicSource.loop = true;
                SoundManager.instance.playMusic(musicaBoss);
                Debug.Log($"[TRIGGER] ♪ Música del boss reproducida en loop");
            }
            else
            {
                // Usar sfxSource para un solo disparo
                SoundManager.instance.playOnce(musicaBoss);
                Debug.Log($"[TRIGGER] ♪ Música del boss reproducida");
            }
        }
        else if (musicaBoss == null)
        {
            Debug.LogWarning("[TRIGGER] No se asignó música del boss");
        }
        else if (SoundManager.instance == null)
        {
            Debug.LogWarning("[TRIGGER] No se encontró SoundManager en la escena");
        }

        // Reproducir sonido de activación si está asignado
        if (sonidoActivacion != null)
        {
            if (SoundManager.instance != null)
            {
                SoundManager.instance.playOnce(sonidoActivacion);
            }
            else
            {
                AudioSource.PlayClipAtPoint(sonidoActivacion, transform.position);
            }
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
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
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

        // Dibujar línea hacia el objeto adicional
        if (objetoAdicional != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, objetoAdicional.transform.position);
            Gizmos.DrawWireCube(objetoAdicional.transform.position, Vector3.one * 0.5f);
        }

        
    }
}