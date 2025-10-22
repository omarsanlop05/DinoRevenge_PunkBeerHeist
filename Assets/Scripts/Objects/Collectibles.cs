using UnityEngine;

public class Collectibles : MonoBehaviour
{
    // Este método se llama automáticamente cuando otro collider entra en contacto
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Verifica si el objeto que colisionó tiene el tag "Player"
        if (other.CompareTag("Player"))
        {
            // Destruye este objeto (el collectible)
            Destroy(gameObject);
        }
    }
}