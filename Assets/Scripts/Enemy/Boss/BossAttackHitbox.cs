using UnityEngine;
using System.Collections.Generic;

public class BossAttackHitbox : MonoBehaviour
{
    [Header("Configuración")]
    public float dañoGolpe = 20f;

    [Header("Zonas de Hitbox (de arriba a abajo)")]
    public GameObject[] zonasHitbox; // 4 zonas para el arco del hacha

    // Sistema igual al del jugador
    private HashSet<Collider2D> objetivosGolpeados = new HashSet<Collider2D>();
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
        objetivosGolpeados.Clear();
        zonaActualActiva = -1;
    }

    public void EndAttack()
    {
        attackActive = false;
        objetivosGolpeados.Clear();

        // Desactivar todas las zonas
        foreach (GameObject zona in zonasHitbox)
        {
            if (zona != null)
            {
                zona.SetActive(false);
            }
        }

        zonaActualActiva = -1;
    }

    public void ActivarZona(int indiceZona)
    {
        if (!attackActive) return;
        if (indiceZona < 0 || indiceZona >= zonasHitbox.Length) return;

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
        }
    }

    // Este método es llamado por los triggers de cada zona
    public void OnZonaTriggerEnter(Collider2D other)
    {
        if (!attackActive) return;

        if (other.CompareTag("Player") && objetivosGolpeados.Add(other))
        {
            AplicarDaño(other);
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
            Debug.Log("Jefe golpeó al jugador con el hacha!");
        }
    }
}
