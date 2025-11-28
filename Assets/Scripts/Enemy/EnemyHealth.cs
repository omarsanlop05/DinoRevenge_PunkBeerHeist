using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public float vida = 100f;

    public void RecibirDaño(float daño)
    {
        vida -= daño;
        Debug.Log(name + " recibió " + daño + " de daño. Vida restante: " + vida);
        if (vida <= 0)
            Morir();
    }

    void Morir()
    {
        Debug.Log(name + " ha muerto");
        if (gameObject.CompareTag("Boss"))
        {
            // Bloquear todas las acciones del Boss
            BossIA bossIA = GetComponent<BossIA>();
            if (bossIA != null)
            {
                bossIA.enabled = false; // Desactivar el script de IA
            }

            // Detener el movimiento
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Static; // Hacer que sea estático
            }

            // Reproducir animación de muerte
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Die");
            }

            // Desactivar hitboxes
            BossAttackHitbox hitbox = GetComponentInChildren<BossAttackHitbox>();
            if (hitbox != null)
            {
                hitbox.EndAttack();
                hitbox.enabled = false;
            }

            StartCoroutine(MostrarPantallaDeVictoria(3f));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    IEnumerator MostrarPantallaDeVictoria(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (GameManager.instance != null)
        {
            GameManager.instance.ShowVictoryScreen();
        }
        else
        {
            Debug.LogWarning("No se encontro el GameManager.");
        }

        Destroy(gameObject);
    }
}
