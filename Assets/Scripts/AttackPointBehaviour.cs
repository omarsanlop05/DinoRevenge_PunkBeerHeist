using UnityEngine;

public class AttackPointBehaviour : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Debug.Log("Golpeado: " + other.name);
            // Aquí puedes aplicar daño, efectos, etc.
        }
    }
}
