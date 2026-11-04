using Unity.Cinemachine;
using UnityEngine;

public class CameraChanger : MonoBehaviour
{
    [Tooltip("La cámara virtual que se activa al entrar en esta zona")]
    public CinemachineCamera zoneCamera;
    public CinemachineCamera previousCamera;

    public bool freezePlayerOnSwitch = true;

    public bool awardsRoomPoints = true;
    private bool pointsAwarded;
    private int activePriority = 10;
    private int inactivePriority = 0;

    private GameObject currentPlayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (zoneCamera.Priority == activePriority)
            return;

        // Award room points only once per changer (and never for the "go back" changer)
        if (awardsRoomPoints && !pointsAwarded)
        {
            pointsAwarded = true;
            PlayerScore.Instance?.RegisterRoomChange();
        }

        // Activate this camera by raising its priority
        zoneCamera.Priority = activePriority;
        previousCamera.Priority = inactivePriority;

        if (freezePlayerOnSwitch)
        {
            freezePlayer(other);
            Invoke(nameof(unFreezePlayer), 1.0f);
        }

        // Resume after 1 second
        Invoke(nameof(unFreezePlayer), 1.0f);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // zoneCamera.Priority = inactivePriority;
        }
    }

    void freezePlayer(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        currentPlayer = other.gameObject;

        Rigidbody2D rb = currentPlayer.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;
        }

        Animator animator = currentPlayer.GetComponent<Animator>();
        if (animator != null)
        {
            animator.speed = 0f; // Pausa en el frame actual
        }
    }

    void unFreezePlayer()
    {
        if (currentPlayer == null) return;


        Rigidbody2D rb = currentPlayer.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        
        Animator animator = currentPlayer.GetComponent<Animator>();
        if (animator != null)
        {
            animator.speed = 1f; // Reanuda animación
        }

        currentPlayer = null;
    }
}