using UnityEngine;

public class KillOnFall : MonoBehaviour
{

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth health = collision.gameObject.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.MorirInst();   
            }
            else
            {
                Debug.LogWarning("KillOnFall: El jugador no tiene componente PlayerHealth.");
            }
        }
        else
        {
            Destroy(collision.gameObject);
        }
    }
}