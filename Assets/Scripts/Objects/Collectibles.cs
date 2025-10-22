using UnityEngine;

public class Collectibles : MonoBehaviour
{
    // Este m�todo se llama autom�ticamente cuando otro collider entra en contacto
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Verifica si el objeto que colision� tiene el tag "Player"
        if (other.CompareTag("Player"))
        {
            // Destruye este objeto (el collectible)
            Destroy(gameObject);
        }
    }
}