using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float lifetime = 3f;
    public float damage = 10f;
    public GameObject hitEffect;

    void Start()
    {
        //Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Si toca al jugador
        if (collision.CompareTag("Player"))
        {
            // Aquí podrías hacer daño al jugador
            Debug.Log("Golpeó al jugador");
            PlayerHealth health = collision.GetComponent<PlayerHealth>();
            health.RecibirDaño(damage, transform.position.x);
        }

        // Efecto opcional al impactar
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, transform.position, Quaternion.identity);
            Destroy(effect, 1f);
        }

        Destroy(gameObject);
    }
}
