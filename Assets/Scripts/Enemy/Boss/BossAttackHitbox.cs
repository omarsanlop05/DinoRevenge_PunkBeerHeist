using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossAttackHitbox : MonoBehaviour
{
    [Header("Configuración")]
    public float dañoGolpe = 20f;

    [Header("Zonas de Hitbox (de arriba a abajo)")]
    public GameObject[] zonasHitbox; // 4 zonas para el arco del hacha

    // Sistema mejorado: rastrea golpes POR ZONA
    private HashSet<Collider2D> objetivosGolpeadosEnZonaActual = new HashSet<Collider2D>();
    private bool attackActive = false;
    private int zonaActualActiva = -1;

    void Start()
    {
        // Desactivar todas las zonas al inicio
        foreach (GameObject zona in zonasHitbox)
        {
            if (zona != null)
            {
                zona.SetActive(false);
            }
        }
    }

    public void StartAttack()
    {
        attackActive = true;
        objetivosGolpeadosEnZonaActual.Clear();
        zonaActualActiva = -1;
        Debug.Log("[HITBOX] Nuevo ataque iniciado - HashSet limpiado");
    }

    public void EndAttack()
    {
        attackActive = false;
        objetivosGolpeadosEnZonaActual.Clear();

        // Desactivar todas las zonas
        foreach (GameObject zona in zonasHitbox)
        {
            if (zona != null)
            {
                zona.SetActive(false);
            }
        }

        zonaActualActiva = -1;
        Debug.Log("[HITBOX] Ataque finalizado - HashSet limpiado");
    }

    public void ActivarZona(int indiceZona)
    {
        if (!attackActive) return;
        if (indiceZona < 0 || indiceZona >= zonasHitbox.Length) return;

        // CLAVE: Limpiar el HashSet cuando cambiamos de zona
        objetivosGolpeadosEnZonaActual.Clear();
        Debug.Log($"[HITBOX] Zona {indiceZona} activada - HashSet limpiado para nueva zona");

        // Desactivar zona anterior
        if (zonaActualActiva >= 0 && zonaActualActiva < zonasHitbox.Length)
        {
            if (zonasHitbox[zonaActualActiva] != null)
            {
                zonasHitbox[zonaActualActiva].SetActive(false);
            }
        }

        // Activar nueva zona
        if (zonasHitbox[indiceZona] != null)
        {
            zonasHitbox[indiceZona].SetActive(true);
            zonaActualActiva = indiceZona;

            // SOLUCIÓN: Forzar detección inmediata después de activar la zona
            StartCoroutine(DetectarColisionesInmediatas(zonasHitbox[indiceZona]));
        }
    }

    // Verificar inmediatamente si hay colliders dentro de la zona recién activada
    private IEnumerator DetectarColisionesInmediatas(GameObject zona)
    {
        // Esperar 1 frame de física para que Unity actualice los triggers
        yield return new WaitForFixedUpdate();

        // Obtener todos los Collider2D en la zona
        Collider2D zonaCollider = zona.GetComponent<Collider2D>();
        if (zonaCollider != null)
        {
            // Crear un overlap para detectar qué está dentro
            ContactFilter2D filtro = new ContactFilter2D();
            filtro.useTriggers = true;
            filtro.SetLayerMask(Physics2D.AllLayers);

            List<Collider2D> resultados = new List<Collider2D>();
            int cantidad = zonaCollider.Overlap(filtro, resultados);

            Debug.Log($"[HITBOX] Detección inmediata: {cantidad} colliders encontrados en zona {zonaActualActiva}");

            // Verificar cada collider detectado
            foreach (Collider2D col in resultados)
            {
                if (col.CompareTag("Player"))
                {
                    Debug.Log($"[HITBOX] ¡Jugador detectado inmediatamente en zona {zonaActualActiva}!");
                    OnZonaTriggerStay(col);
                }
            }
        }
    }

    // Detecta colliders que YA ESTÁN DENTRO del trigger
    public void OnZonaTriggerStay(Collider2D other)
    {
        if (!attackActive)
        {
            Debug.Log("[HITBOX] OnZonaTriggerStay llamado pero ataque NO activo");
            return;
        }

        if (other.CompareTag("Player"))
        {
            Debug.Log($"[HITBOX] OnZonaTriggerStay - Jugador detectado. Zona: {zonaActualActiva}");

            // Add() devuelve true si el elemento fue agregado (no existía antes)
            if (objetivosGolpeadosEnZonaActual.Add(other))
            {
                Debug.Log($"[HITBOX] ¡Golpe conectado (Stay)! Zona {zonaActualActiva}");
                AplicarDaño(other);
            }
            else
            {
                Debug.Log($"[HITBOX] Jugador ya fue golpeado en esta zona (Stay ignorado)");
            }
        }
    }

    // Para cuando el jugador entra mientras la zona está activa
    public void OnZonaTriggerEnter(Collider2D other)
    {
        if (!attackActive)
        {
            Debug.Log("[HITBOX] OnZonaTriggerEnter llamado pero ataque NO activo");
            return;
        }

        if (other.CompareTag("Player"))
        {
            Debug.Log($"[HITBOX] OnZonaTriggerEnter - Jugador entró. Zona: {zonaActualActiva}");

            if (objetivosGolpeadosEnZonaActual.Add(other))
            {
                Debug.Log($"[HITBOX] ¡Golpe conectado (Enter)! Zona {zonaActualActiva}");
                AplicarDaño(other);
            }
            else
            {
                Debug.Log($"[HITBOX] Jugador ya fue golpeado en esta zona (Enter ignorado)");
            }
        }
    }

    void AplicarDaño(Collider2D player)
    {
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            // Obtener posición X del jefe (padre de este objeto)
            float bossPosX = transform.parent.position.x;
            playerHealth.RecibirDaño(dañoGolpe, bossPosX);
            Debug.Log($"[HITBOX] ✅ Daño aplicado: {dañoGolpe} desde posición X: {bossPosX}");
        }
        else
        {
            Debug.LogError("[HITBOX] ❌ PlayerHealth no encontrado!");
        }
    }
}