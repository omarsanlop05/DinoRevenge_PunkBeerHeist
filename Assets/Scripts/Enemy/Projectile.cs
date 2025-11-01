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
            // Aqu� podr�as hacer da�o al jugador
            Debug.Log("Golpe� al jugador");
            PlayerHealth health = collision.GetComponent<PlayerHealth>();
            health.RecibirDa�o(damage);
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
