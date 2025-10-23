using Unity.Cinemachine;
using UnityEngine;

public class CameraChanger : MonoBehaviour
{
    [Tooltip("La cámara virtual que se activa al entrar en esta zona")]
    public CinemachineCamera zoneCamera;
    public CinemachineCamera previousCamera;

    private int activePriority = 10;
    private int inactivePriority = 0;
    private Collider2D currentPlayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (zoneCamera.Priority == activePriority)
            return;

        // Activar esta cámara subiendo su prioridad
        zoneCamera.Priority = activePriority;
        previousCamera.Priority = inactivePriority;

        currentPlayer = other;
        freezePlayer(other);

        Invoke(nameof(unFreezePlayer), 1.0f);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            //zoneCamera.Priority = inactivePriority;
        }
    }

    void freezePlayer(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Rigidbody2D player = other.GetComponent<Rigidbody2D>();
            if (player != null) {
                player.linearVelocity = Vector2.zero;
                player.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;
            }
        }
    }

    void unFreezePlayer()
    {
        if (currentPlayer == null) return;

        Rigidbody2D player = currentPlayer.GetComponent<Rigidbody2D>();
        if (player != null)
        {
            player.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        currentPlayer = null;
    }
}