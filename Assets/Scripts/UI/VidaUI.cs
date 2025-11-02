using UnityEngine;
using UnityEngine.UI;

public class VidaUI : MonoBehaviour
{
    public Image vidaVerde; // Asigna el Image de la barra verde
    private PlayerHealth playerHealth;

    void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    void Update()
    {
        if (playerHealth == null || vidaVerde == null) return;

        float porcentaje = playerHealth.vidaActual / playerHealth.vidaMaxima;
        vidaVerde.fillAmount = Mathf.Clamp01(porcentaje);
    }
}