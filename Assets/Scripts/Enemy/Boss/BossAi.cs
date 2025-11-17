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

    [Header("Debug")]
    public bool mostrarDebugLogs = true;
    public bool mostrarEstadoConstante = true;
    private float tiempoUltimoLogEstado = 0f;

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
        DebugLog("=== BOSS INICIADO ===");

        rb = GetComponent<Rigidbody2D>();

        if (jugador == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                jugador = playerObj.transform;
                DebugLog("Jugador encontrado automáticamente");
            }
            else
            {
                Debug.LogError("NO SE ENCONTRÓ EL JUGADOR!");
            }
        }

        if (attackHitbox == null)
        {
            attackHitbox = GetComponentInChildren<BossAttackHitbox>();
        }

        // VERIFICACIONES CRÍTICAS
        if (hachaPrefab == null)
        {
            Debug.LogError("¡HACHA PREFAB NO ASIGNADO EN EL INSPECTOR!");
        }
        else
        {
            DebugLog($"Hacha Prefab asignado: {hachaPrefab.name}");
        }

        if (puntoDisparo == null)
        {
            Debug.LogError("¡PUNTO DE DISPARO NO ASIGNADO EN EL INSPECTOR!");
        }
        else
        {
            DebugLog($"Punto de disparo asignado: {puntoDisparo.name} en posición {puntoDisparo.position}");
        }

        if (animator == null)
        {
            Debug.LogError("¡ANIMATOR NO ASIGNADO!");
        }

        estadoActual = EstadoJefe.Persiguiendo;
        tiempoUltimoAtaqueMelee = -cooldownAtaqueMelee;
        tiempoUltimoAtaqueRango = -cooldownAtaqueRango;
        estaAtacando = false;
    }

    void Update()
    {
        if (jugador == null) return;

        float distanciaAlJugador = Vector2.Distance(transform.position, jugador.position);

        // Log de estado cada 0.5 segundos
        if (mostrarEstadoConstante && Time.time - tiempoUltimoLogEstado > 0.5f)
        {
            DebugLog($"📊 ESTADO: {estadoActual} | Atacando: {estaAtacando} | Distancia: {distanciaAlJugador:F2}");
            tiempoUltimoLogEstado = Time.time;
        }


        // Voltear sprite según dirección del jugador (solo si no está atacando)
        if (!estaAtacando)
        {
            if (jugador.position.x > transform.position.x && !mirandoDerecha)
            {
                Voltear();
            }
            else if (jugador.position.x < transform.position.x && mirandoDerecha)
            {
                Voltear();
            }
        }

        // Máquina de estados
        switch (estadoActual)
        {
            case EstadoJefe.Persiguiendo:
                ActualizarPersecucion(distanciaAlJugador);
                break;

            case EstadoJefe.AtacandoMelee:
                // Mantener velocidad en 0 durante el ataque
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;

            case EstadoJefe.AtacandoRango:
                // Mantener velocidad en 0 durante el ataque
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
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
            DebugLog($"Distancia: {distancia:F2} - Iniciando ataque MELEE");
            IniciarAtaqueMelee();
            return;
        }

        // Si está a distancia media-larga, considerar ataque a distancia
        if (distancia > distanciaAtaqueMelee && distancia < distanciaMaxima && PuedeAtacarRango())
        {
            float randomValue = Random.value;
            if (randomValue < probabilidadAtaqueRango)
            {
                DebugLog($"Distancia: {distancia:F2}, Random: {randomValue:F2} - Iniciando ataque RANGO");
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
        DebugLog(">>> IniciarAtaqueMelee <<<");
        estaAtacando = true;
        estadoActual = EstadoJefe.AtacandoMelee;
        rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetBool("Caminando", false);
            animator.SetTrigger("AtaqueMelee");
            DebugLog("Trigger 'AtaqueMelee' activado");
        }

        tiempoUltimoAtaqueMelee = Time.time;

        // PROTECCIÓN: Si la animación no llama a FinalizarAtaque, lo forzamos después de 2 segundos
        StartCoroutine(ProteccionAtaqueMelee());
    }

    private IEnumerator ProteccionAtaqueMelee()
    {
        yield return new WaitForSeconds(2f);

        if (estaAtacando && estadoActual == EstadoJefe.AtacandoMelee)
        {
            Debug.LogWarning("[BOSS] ⚠️ PROTECCIÓN ACTIVADA: FinalizarAtaque no fue llamado por la animación!");
            FinalizarAtaque();
        }
    }

    void IniciarAtaqueRango()
    {
        DebugLog(">>> IniciarAtaqueRango <<<");
        estaAtacando = true;
        estadoActual = EstadoJefe.AtacandoRango;
        rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetBool("Caminando", false);
            animator.SetTrigger("AtaqueRango");
            DebugLog("Trigger 'AtaqueRango' activado");
        }

        tiempoUltimoAtaqueRango = Time.time;

        // PROTECCIÓN: Si la animación no llama a FinalizarAtaque, lo forzamos después de 2 segundos
        StartCoroutine(ProteccionAtaqueRango());
    }

    private IEnumerator ProteccionAtaqueRango()
    {
        yield return new WaitForSeconds(2f);

        if (estaAtacando && estadoActual == EstadoJefe.AtacandoRango)
        {
            Debug.LogWarning("[BOSS] ⚠️ PROTECCIÓN ACTIVADA: FinalizarAtaque no fue llamado por la animación!");
            FinalizarAtaque();
        }
    }

    // ===== LLAMADOS DESDE ANIMATION EVENTS =====

    public void StartMeleeAttack()
    {
        DebugLog("*** StartMeleeAttack llamado desde animación ***");
        if (attackHitbox != null)
        {
            attackHitbox.StartAttack();
        }
    }

    public void ActivarZona1()
    {
        DebugLog("Zona 1 activada");
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(0);
        }
    }

    public void ActivarZona2()
    {
        DebugLog("Zona 2 activada");
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(1);
        }
    }

    public void ActivarZona3()
    {
        DebugLog("Zona 3 activada");
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(2);
        }
    }

    public void ActivarZona4()
    {
        DebugLog("Zona 4 activada");
        if (attackHitbox != null)
        {
            attackHitbox.ActivarZona(3);
        }
    }

    public void EndMeleeAttack()
    {
        DebugLog("*** EndMeleeAttack llamado desde animación ***");
        if (attackHitbox != null)
        {
            attackHitbox.EndAttack();
        }
    }

    public void LanzarHacha()
    {
        DebugLog("========================================");
        DebugLog("*** LanzarHacha llamado desde animación ***");
        DebugLog("========================================");

        // VERIFICACIÓN EXHAUSTIVA
        if (hachaPrefab == null)
        {
            Debug.LogError("❌ hachaPrefab es NULL! Asigna el prefab en el Inspector.");
            return;
        }
        else
        {
            DebugLog($"✓ hachaPrefab OK: {hachaPrefab.name}");
        }

        if (puntoDisparo == null)
        {
            Debug.LogError("❌ puntoDisparo es NULL! Asigna un Transform en el Inspector.");
            return;
        }
        else
        {
            DebugLog($"✓ puntoDisparo OK: {puntoDisparo.name} en {puntoDisparo.position}");
        }

        // Determinar altura aleatoria del proyectil
        int tipoLanzamiento = Random.Range(0, 3);
        Vector3 posicionLanzamiento = puntoDisparo.position;

        string tipoTexto = "";
        switch (tipoLanzamiento)
        {
            case 0:
                posicionLanzamiento.y += offsetArriba;
                tipoTexto = "ARRIBA";
                break;
            case 1:
                tipoTexto = "CENTRO";
                break;
            case 2:
                posicionLanzamiento.y += offsetAbajo;
                tipoTexto = "ABAJO";
                break;
        }

        DebugLog($"Tipo de lanzamiento: {tipoTexto}");
        DebugLog($"Posición de lanzamiento: {posicionLanzamiento}");
        DebugLog($"Mirando derecha: {mirandoDerecha}");

        // INSTANCIAR HACHA
        GameObject hacha = Instantiate(hachaPrefab, posicionLanzamiento, Quaternion.identity);

        if (hacha == null)
        {
            Debug.LogError("❌ ERROR CRÍTICO: Instantiate devolvió NULL!");
            return;
        }
        else
        {
            DebugLog($"✓ Hacha instanciada exitosamente: {hacha.name}");
            DebugLog($"  - Posición: {hacha.transform.position}");
            DebugLog($"  - Activa: {hacha.activeInHierarchy}");

            // Verificar componentes del hacha
            SpriteRenderer sr = hacha.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError("❌ El prefab del hacha NO TIENE SpriteRenderer!");
            }
            else
            {
                DebugLog($"✓ SpriteRenderer encontrado, sprite: {(sr.sprite != null ? sr.sprite.name : "NULL")}");
                DebugLog($"  - Enabled: {sr.enabled}");
                DebugLog($"  - Color: {sr.color}");
            }

            Rigidbody2D rbHacha = hacha.GetComponent<Rigidbody2D>();
            if (rbHacha == null)
            {
                Debug.LogError("❌ El prefab del hacha NO TIENE Rigidbody2D!");
            }
            else
            {
                DebugLog($"✓ Rigidbody2D encontrado");
            }
        }

        ProyectilHacha proyectil = hacha.GetComponent<ProyectilHacha>();

        if (proyectil == null)
        {
            Debug.LogError("❌ El prefab NO TIENE el script ProyectilHacha!");
        }
        else
        {
            DebugLog("✓ Script ProyectilHacha encontrado");
            float direccion = mirandoDerecha ? 1f : -1f;
            proyectil.Inicializar(direccion, transform);
            DebugLog($"✓ Proyectil inicializado con dirección: {direccion}");
        }

        DebugLog("========================================");
    }

    public void FinalizarAtaque()
    {
        DebugLog("*** FinalizarAtaque llamado desde animación ***");
        DebugLog($"Estado antes: {estadoActual}, Atacando: {estaAtacando}");

        estaAtacando = false;
        estadoActual = EstadoJefe.Persiguiendo;

        DebugLog($"Estado después: {estadoActual}, Atacando: {estaAtacando}");
    }

    void Voltear()
    {
        mirandoDerecha = !mirandoDerecha;
        Vector3 escala = transform.localScale;
        escala.x *= -1;
        transform.localScale = escala;
        DebugLog($"Jefe volteado. Mirando derecha: {mirandoDerecha}");
    }

    void DebugLog(string mensaje)
    {
        if (mostrarDebugLogs)
        {
            Debug.Log($"[BOSS {Time.time:F2}s] {mensaje}");
        }
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

        // Visualizar punto de disparo
        if (puntoDisparo != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(puntoDisparo.position, 0.3f);

            // Líneas para los offsets
            Gizmos.color = Color.magenta;
            Vector3 posArriba = puntoDisparo.position;
            posArriba.y += offsetArriba;
            Gizmos.DrawWireSphere(posArriba, 0.2f);

            Vector3 posAbajo = puntoDisparo.position;
            posAbajo.y += offsetAbajo;
            Gizmos.DrawWireSphere(posAbajo, 0.2f);
        }
    }
}