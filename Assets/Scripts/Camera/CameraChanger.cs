using Unity.Cinemachine;
using UnityEngine;

public class CameraChanger : MonoBehaviour
{
    [Tooltip("La cámara virtual que se activa al entrar en esta zona")]
    public CinemachineCamera zoneCamera;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Activar esta cámara subiendo su prioridad
            zoneCamera.Priority = 10;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Bajar prioridad para que otra cámara tome control
            zoneCamera.Priority = 0;
        }
    }
}