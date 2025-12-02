using UnityEngine;

public class ShowImageOnZone : MonoBehaviour
{
    public string controlName; // Nombre del control que esta zona debe mostrar (ej: "ASD", "Space", "E")
    public GameObject player;  // Referencia al jugador

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Entró al trigger con: " + other.name);

        if (other.gameObject == player)
        {
            Transform controlPoint = player.transform.Find(controlName);
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
        if (other.gameObject == player)
        {
            // Al salir de la zona, desactivar el hijo correspondiente
            Transform controlPoint = player.transform.Find(controlName);
            if (controlPoint != null)
            {
                controlPoint.gameObject.SetActive(false);
            }
        }
    }

}