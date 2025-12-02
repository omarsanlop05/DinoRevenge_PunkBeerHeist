using UnityEngine;

public class ShowImageOnZoneDestroy : MonoBehaviour
{
    public string controlName; // Nombre del control que esta zona debe mostrar (ej: "ASD", "Space", "E")
    public GameObject player;  // Referencia al jugador
    public GameObject vinculo; // Objeto que debe existir para mantener la zona activa

    private Transform controlPoint;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Entró al trigger con: " + other.name);

        if (other.gameObject == player)
        {
            controlPoint = player.transform.Find(controlName);
            if (controlPoint != null)
            {
                Debug.Log("Activando: " + controlName);
                controlPoint.gameObject.SetActive(true);
            }
            else
            {
                Debug.Log("No se encontró el hijo: " + controlName);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == player && controlPoint != null)
        {
            controlPoint.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Si el vinculo deja de existir, destruir este objeto (la zona con el código)
        if (vinculo == null)
        {
            controlPoint.gameObject.SetActive(false);
            Debug.Log("El vínculo murió, destruyendo la zona: " + gameObject.name);
            Destroy(gameObject);
        }
    }
}