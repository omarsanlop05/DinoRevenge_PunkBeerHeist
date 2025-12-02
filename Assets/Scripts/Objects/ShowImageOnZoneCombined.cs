using UnityEngine;

public class ShowImageOnZoneCombined : MonoBehaviour
{
    public string controlName; // Nombre del control que esta zona debe mostrar (ej: "ASD", "Space", "E")
    public string controlName2; // Nombre del control que esta zona debe mostrar (ej: "ASD", "Space", "E")
    public GameObject player;  // Referencia al jugador

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Entró al trigger con: " + other.name);

        if (other.gameObject == player)
        {
            Transform controlPoint = player.transform.Find(controlName);
            Transform controlPoint2 = player.transform.Find(controlName2);
            if (controlPoint != null)
            {
                Debug.Log("Activando: " + controlName);
                controlPoint.gameObject.SetActive(true);
                controlPoint2.gameObject.SetActive(true);
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
            Transform controlPoint2 = player.transform.Find(controlName2);
            if (controlPoint != null)
            {
                controlPoint.gameObject.SetActive(false);
                controlPoint2.gameObject.SetActive(false);
            }
        }
    }

}