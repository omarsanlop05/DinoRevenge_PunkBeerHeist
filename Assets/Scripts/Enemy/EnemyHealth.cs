using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public float vida = 100f;

    // Fired once when the enemy dies (RoomCombatTracker uses it to know when a room is cleared)
    public event System.Action<EnemyHealth> Died;

    private bool muerto;

    public void RecibirDano(float dano)
    {
        if (muerto) return;   // several hits in the same frame must not count the kill twice

        vida -= dano;
        Debug.Log(name + " recibio " + dano + " de dano. Vida restante: " + vida);
        if (vida <= 0)
        {
            muerto = true;
            bool isBoss = gameObject.CompareTag("Boss");
            var variant = GetComponent<EnemyVariantBase>();
            bool fast = !isBoss && variant != null && variant.KilledFast;
            PlayerScore.Instance?.RegisterEnemyDefeated(isBoss, fast);
            Died?.Invoke(this);
            Morir();
        }
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
                rb.bodyType = RigidbodyType2D.Static; // Hacer que sea estntico
            }

            // Reproducir animacinn de muerte
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

        if (GameManagerProcedural.instance != null)
        {
            GameManagerProcedural.instance.ShowVictoryScreen();
        }
        else if(GameManager.instance != null){
            GameManager.instance.ShowVictoryScreen();
        } 
        else
        {
            Debug.LogWarning("⚠️ No se encontró el GameManager.");
        }

        Destroy(gameObject);
    }
}
