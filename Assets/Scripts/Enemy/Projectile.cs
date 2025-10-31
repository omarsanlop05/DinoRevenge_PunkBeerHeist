using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float lifetime = 3f;
    public GameObject hitEffect;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Si toca al jugador
        if (collision.collider.CompareTag("Player"))
        {
            // Aquí podrías hacer daño al jugador
            Debug.Log("Golpeó al jugador");
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
