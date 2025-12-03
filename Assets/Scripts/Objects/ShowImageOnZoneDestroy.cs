using UnityEngine;

public class ShowImageOnZoneDestroy : MonoBehaviour
{
    public GameObject controlPoint; // Nombre del control que esta zona debe mostrar (ej: "ASD", "Space", "E")
    public GameObject player;  // Referencia al jugador
    public GameObject vinculo; // Objeto que debe existir para mantener la zona activa


    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Entró al trigger con: " + other.name);

        if (other.gameObject == player)
        {

            if (controlPoint != null)
            {
                controlPoint.gameObject.SetActive(true);
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