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

    [Header("Configuración de Rangos (3 Zonas)")]
    public float distanciaAtaqueMelee = 2.5f;      // ZONA 1: Rango de ataque melee
    public float distanciaPersecucion = 8f;        // ZONA 2: Rango de persecución (se acerca)
    public float distanciaTiroHacha = 12f;         // ZONA 3: Lanza hacha si está más lejos

    [Header("Configuración de Cooldowns")]
    public float cooldownAtaqueMelee = 2f;
    public float cooldownAtaqueRango = 3f;

    [Header("Estado")]
    public bool mirandoDerecha = true;

    [Header("Activación")]
    [Tooltip("Si es true, el jefe comienza desactivado y espera ser activado por trigger")]
    public bool requiereActivacion = true;
    private bool jefeActivado = false;

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

        // Si requiere activación, iniciar desactivado
        if (requiereActivacion)
        {
            jefeActivado = false;
            DebugLog("⏸️ Jefe en espera. Requiere activación por trigger.");
        }
        else
        {
            jefeActivado = true;
            DebugLog("▶️ Jefe activado automáticamente (no requiere trigger).");
        }
    }

    void Update()
    {
        // Si el jefe no está activado, no hacer nada
        if (!jefeActivado) return;

        if (jugador == null) return;

        float distanciaAlJugador = Vector2.Distance(transform.position, jugador.position);

        // Log de estado cada 0.5 segundos
        if (mostrarEstadoConstante && Time.time - tiempoUltimoLogEstado > 0.5f)
        {
            string zona = GetZonaActual(distanciaAlJugador);
            DebugLog($"📊 ESTADO: {estadoActual} | Atacando: {estaAtacando} | Distancia: {distanciaAlJugador:F2} | Zona: {zona}");
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
        if (!jefeActivado) return; // No perseguir si no está activado

        // ===== ZONA 1: RANGO DE ATAQUE MELEE =====
        // Si está muy cerca, atacar melee
        if (distancia <= distanciaAtaqueMelee && PuedeAtacarMelee())
        {
            DebugLog($"🗡️ ZONA 1 (Melee) - Distancia: {distancia:F2} - ¡ATACANDO MELEE!");
            IniciarAtaqueMelee();
            return;
        }

        // ===== ZONA 2: RANGO DE PERSECUCIÓN =====
        // Si está a distancia media, perseguir para acercarse
        if (distancia > distanciaAtaqueMelee && distancia <= distanciaPersecucion)
        {
            DebugLog($"🏃 ZONA 2 (Persecución) - Distancia: {distancia:F2} - Persiguiendo al jugador");
            Vector2 direccion = (jugador.position - transform.position).normalized;
            rb.linearVelocity = new Vector2(direccion.x * velocidadMovimiento, rb.linearVelocity.y);

            if (animator != null)
            {
                animator.SetBool("Caminando", true);
            }
            return;
        }

        // ===== ZONA 3: RANGO DE TIRO =====
        // Si está muy lejos, lanzar hacha
        if (distancia > distanciaPersecucion && distancia <= distanciaTiroHacha && PuedeAtacarRango())
        {
            DebugLog($"🪓 ZONA 3 (Tiro) - Distancia: {distancia:F2} - ¡LANZANDO HACHA!");
            IniciarAtaqueRango();
            return;
        }

        // ===== ZONA 4: MÁS ALLÁ DEL RANGO DE TIRO =====
        // Si está incluso más lejos que el rango de tiro, perseguir
        if (distancia > distanciaTiroHacha)
        {
            DebugLog($"🚶 ZONA 4 (Muy lejos) - Distancia: {distancia:F2} - Acercándose (fuera de rango de tiro)");
            Vector2 direccion = (jugador.position - transform.position).normalized;
            rb.linearVelocity = new Vector2(direccion.x * velocidadMovimiento, rb.linearVelocity.y);

            if (animator != null)
            {
                animator.SetBool("Caminando", true);
            }
            return;
        }

        // Por defecto, detener movimiento
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (animator != null)
        {
            animator.SetBool("Caminando", false);
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

    string GetZonaActual(float distancia)
    {
        if (distancia <= distanciaAtaqueMelee)
            return "ZONA 1 (Melee)";
        else if (distancia <= distanciaPersecucion)
            return "ZONA 2 (Persecución)";
        else if (distancia <= distanciaTiroHacha)
            return "ZONA 3 (Tiro)";
        else
            return "ZONA 4 (Muy lejos)";
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

        // Lanzar desde el punto de disparo
        Vector3 posicionLanzamiento = puntoDisparo.position;

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

    // ===== MÉTODO PÚBLICO PARA ACTIVAR AL JEFE =====
    public void ActivarJefe()
    {
        if (jefeActivado)
        {
            DebugLog("⚠️ El jefe ya estaba activado");
            return;
        }

        jefeActivado = true;
        DebugLog("🔥 ¡JEFE ACTIVADO! Iniciando combate...");

        // Opcional: Iniciar con una animación especial o rugido
        if (animator != null)
        {
            // Puedes crear un trigger de "Despertar" o "Rugir" si tienes esa animación
            // animator.SetTrigger("Despertar");
        }
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
        // ZONA 1: Rango de ataque melee (ROJO)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaAtaqueMelee);
        Gizmos.DrawWireSphere(transform.position, distanciaAtaqueMelee);

        // ZONA 2: Rango de persecución (AMARILLO)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaPersecucion);

        // ZONA 3: Rango de tiro de hacha (VERDE)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, distanciaTiroHacha);

        // Visualizar punto de disparo
        if (puntoDisparo != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(puntoDisparo.position, 0.3f);
        }
    }
}