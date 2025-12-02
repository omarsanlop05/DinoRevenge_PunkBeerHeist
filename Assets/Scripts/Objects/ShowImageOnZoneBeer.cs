using UnityEngine;

public class ShowImageOnZoneBeer : MonoBehaviour
{
    public string controlName; // Nombre del control que esta zona debe mostrar (ej: "ASD", "Space", "E")
    public GameObject player;  // Referencia al jugador

    private Transform controlPoint;
    private bool playerInside = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == player)
        {
            playerInside = true;
            controlPoint = player.transform.Find(controlName);

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null && health.cervezas > 0 && controlPoint != null)
            {
                controlPoint.gameObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == player)
        {
            playerInside = false;
            if (controlPoint != null)
            {
                controlPoint.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (playerInside && controlPoint != null)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                if (health.cervezas > 0)
                {
                    // Mantener activo mientras tenga cervezas
                    controlPoint.gameObject.SetActive(true);
                }
                else
                {
                    // Apagar inmediatamente si ya no tiene cervezas
                    controlPoint.gameObject.SetActive(false);
                }
            }
        }
    }
}