using UnityEngine;

public class Collectibles : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();

            if (health != null)
            {
                if (health.cervezas < health.maxCervezas)
                {
                    health.cervezas++;
                    Debug.Log("🍺 Cerveza recogida. Cervezas en inventario: " + health.cervezas);
                    Destroy(gameObject);
                }
                else
                {
                    Debug.Log("🚫 Inventario de cervezas lleno. No se recogió.");
                }
            }
        }
    }
}