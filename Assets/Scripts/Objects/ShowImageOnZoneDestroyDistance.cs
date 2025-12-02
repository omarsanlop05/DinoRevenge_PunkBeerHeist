using UnityEngine;

public class ShowImageOnZoneDestroyDistance : MonoBehaviour
{
    public string controlName;   // Nombre del control que esta zona debe mostrar (ej: "ASD", "Space", "E")
    public GameObject player;    // Referencia al jugador
    public GameObject vinculo;   // Objeto que debe existir para mantener la zona activa
    public float distanciaMaxima = 5f; // Distancia máxima para mostrar el control

    private Transform controlPoint;

    private void Start()
    {
        // Buscar el control al inicio
        controlPoint = player.transform.Find(controlName);
        if (controlPoint != null)
        {
            controlPoint.gameObject.SetActive(false); // aseguramos que empiece apagado
        }
    }

    private void Update()
    {
        // Si el vínculo deja de existir, destruir este objeto (la zona con el código)
        if (vinculo == null)
        {
            if (controlPoint != null)
                controlPoint.gameObject.SetActive(false);

            Debug.Log("El vínculo murió, destruyendo la zona: " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        // Revisar distancia entre jugador y vínculo
        float distancia = Vector2.Distance(player.transform.position, vinculo.transform.position);

        if (controlPoint != null)
        {
            if (distancia <= distanciaMaxima)
            {
                // Si están cerca, mostrar el control
                controlPoint.gameObject.SetActive(true);
            }
            else
            {
                // Si están lejos, apagar el control
                controlPoint.gameObject.SetActive(false);
            }
        }
    }
}