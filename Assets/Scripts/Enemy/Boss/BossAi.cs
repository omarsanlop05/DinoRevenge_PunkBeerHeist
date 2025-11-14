using UnityEngine;
using System.Collections;

public class BossIA : MonoBehaviour
{
    [Header("Referencias")]
    public Transform jugador;
    public Transform puntoDisparo;
    public GameObject hachaPrefab;
    public Animator animator;
    public BossAttackHitbox attackHitbox;

    [Header("Configuración de Movimiento")]
    public float velocidadMovimiento = 3f;
    public float distanciaMinima = 2f;
    public float distanciaMaxima = 8f;
    public float distanciaAtaqueMelee = 2.5f;

    [Header("Configuración de Ataques")]
    public float cooldownAtaqueMelee = 2f;
    public float cooldownAtaqueRango = 3f;
    public float probabilidadAtaqueRango = 0.6f;

    [Header("Offset de Proyectiles")]
    public float offsetArriba = 1.5f;
    public float offsetAbajo = -1.5f;

    [Header("Estado")]
    public bool mirandoDerecha = true;

    // Variables privadas
    private Rigidbody2D rb;
    private float tiempoUltimoAtaqueMelee;
    private float tiempoUltimoAtaqueRango;
    private bool estaAtacando;
    private EstadoJefe estadoActual;

    private enum EstadoJefe
    {
        Persiguiendo,
        AtacandoMelee,
        AtacandoRango,
        Esperando
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (jugador == null)
        {
            jugador = GameObject.FindGameObjectWithTag("Player").transform;
        }

        if (attackHitbox == null)
        {
            attackHitbox = GetComponentInChildren<BossAttackHitbox>();
        }

        estadoActual = EstadoJefe.Persiguiendo;
        tiempoUltimoAtaqueMelee = -cooldownAtaqueMelee;
        tiempoUltimoAtaqueRango = -cooldownAtaqueRango;
    }

    void Update()
    {
        if (jugador == null) return;

        float distanciaAlJugador = Vector2.Distance(transform.position, jugador.position);

        // Voltear sprite según dirección del jugador
        if (jugador.position.x > transform.position.x && !mirandoDerecha)
        {
            Voltear();
        }
        else if (jugador.position.x < transform.position.x && mirandoDerecha)
        {
            Voltear();
        }

        // Máquina de estados
        switch (estadoActual)
        {
            case EstadoJefe.Persiguiendo:
                ActualizarPersecucion(distanciaAlJugador);
                break;

            case EstadoJefe.AtacandoMelee:
                // El estado cambia desde la animación
                break;

            case EstadoJefe.AtacandoRango:
                // El estado cambia desde la animación
                break;

            case EstadoJefe.Esperando:
                ActualizarEspera();
                break;
        }
    }

    void ActualizarPersecucion(float distancia)
    {
        if (estaAtacando) return;

        // Si está muy cerca, ataque melee
        if (distancia <= distanciaAtaqueMelee && PuedeAtacarMelee())
        {
            IniciarAtaqueMelee();
            return;
        }

        // Si está a distancia media-larga, considerar ataque a distancia
        if (distancia > distanciaAtaqueMelee && distancia < distanciaMaxima && PuedeAtacarRango())
        {
            if (Random.value < probabilidadAtaqueRango)
            {
                IniciarAtaqueRango();
                return;
            }
        }

        // Moverse hacia el jugador
        if (distancia > distanciaMinima)
        {
            Vector2 direccion = (jugador.position - transform.position).normalized;
            rb.linearVelocity = new Vector2(direccion.x * velocidadMovimiento, rb.linearVelocity.y);

            if (animator != null)
            {
                animator.SetBool("Caminando", true);
            }
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            if (animator != null)
            {
                animator.SetBool("Caminando", false);
            }
        }
    }

    void ActualizarEspera()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

        if (!estaAtacando)
        {
            estadoActual = EstadoJefe.Persiguiendo;
        }
    }

    bool PuedeAtacarMelee()
    {
        return Time.time >= tiempoUltimoAtaqueMelee + cooldownAtaqueMelee;
    }

    bool PuedeAtacarRango()
    {
        return Time.time >= tiempoUltimoAtaqueRango + cooldownAtaqueRango;
    }

    void IniciarAtaqueMelee()
    {
        estaAtacando = true;
        estadoActual = EstadoJefe.AtacandoMelee;
        rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetTrigger("AtaqueMelee");
        }

        tiempoUltimoAtaqueMelee = Time.time;
    }

    void IniciarAtaqueRango()
    {
        estaAtacando = true;
        estadoActual = EstadoJefe.AtacandoRango;
        rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetTrigger("AtaqueRango");
        }

        tiempoUltimoAtaqueRango = Time.time;
    }

    // ===== LLAMADOS DESDE ANIMATION EVENTS =====

    // Para el ataque melee - activa el sistema de hitbox
    public void StartMeleeAttack()
    {
        if (attackHitbox != null)
        {
            attackHitbox.StartAttack();
        }
    }

    // Activar cada zona de hitbox secuencialmente
    public void ActivarZona1() // Inicio del golpe (arriba)
    {
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(0);
        }
    }

    public void ActivarZona2() // Medio del golpe
    {
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(1);
        }
    }

    public void ActivarZona3() // Medio-bajo
    {
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(2);
        }
    }

    public void ActivarZona4() // Final del golpe (abajo)
    {
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(3);
        }
    }

    public void EndMeleeAttack()
    {
        if (attackHitbox != null)
        {
            attackHitbox.EndAttack();
        }
    }

    // Para el ataque de rango
    public void LanzarHacha()
    {
        if (hachaPrefab == null || puntoDisparo == null) return;

        // Determinar altura aleatoria del proyectil
        int tipoLanzamiento = Random.Range(0, 3);
        Vector3 posicionLanzamiento = puntoDisparo.position;

        switch (tipoLanzamiento)
        {
            case 0: // Arriba
                posicionLanzamiento.y += offsetArriba;
                break;
            case 1: // Centro (sin offset)
                break;
            case 2: // Abajo
                posicionLanzamiento.y += offsetAbajo;
                break;
        }

        GameObject hacha = Instantiate(hachaPrefab, posicionLanzamiento, Quaternion.identity);
        ProyectilHacha proyectil = hacha.GetComponent<ProyectilHacha>();

        if (proyectil != null)
        {
            float direccion = mirandoDerecha ? 1f : -1f;
            proyectil.Inicializar(direccion, transform);
        }
    }

    // Al finalizar cualquier ataque
    public void FinalizarAtaque()
    {
        estaAtacando = false;
        estadoActual = EstadoJefe.Persiguiendo;
    }

    void Voltear()
    {
        mirandoDerecha = !mirandoDerecha;
        Vector3 escala = transform.localScale;
        escala.x *= -1;
        transform.localScale = escala;
    }

    void OnDrawGizmosSelected()
    {
        // Visualizar distancias en el editor
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaAtaqueMelee);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaMinima);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, distanciaMaxima);
    }
}
