using Unity.Cinemachine;
using UnityEngine;

public class CameraChanger : MonoBehaviour
{
    [Tooltip("La c�mara virtual que se activa al entrar en esta zona")]
    public CinemachineCamera zoneCamera;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Activar esta c�mara subiendo su prioridad
            zoneCamera.Priority = 10;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Bajar prioridad para que otra c�mara tome control
            zoneCamera.Priority = 0;
        }
    }
}