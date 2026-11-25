using Unity.Cinemachine;
using UnityEngine;

public class CameraChanger : MonoBehaviour
{
    [Tooltip("La cámara virtual que se activa al entrar en esta zona")]
    public CinemachineCamera zoneCamera;
    public CinemachineCamera previousCamera;

    public bool freezePlayerOnSwitch = true;
    [Tooltip("Seconds the player stays frozen while the camera moves to the new zone")]
    public float freezeDuration = 1.0f;

    public bool awardsRoomPoints = true;
    private bool pointsAwarded;
    private int activePriority = 10;
    private int inactivePriority = 0;

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
            FreezePlayer(other);
    }

    // The collider that touched the trigger can be a child of the player, so look up the controller in the parents.
    // PlayerController.Freeze stops input, physics and animation and unfreezes by itself after freezeDuration.
    private void FreezePlayer(Collider2D other)
    {
        PlayerController controller = other.GetComponentInParent<PlayerController>();
        if (controller != null)
        {
            controller.Freeze(freezeDuration);
            return;
        }

        Debug.LogWarning($"[CameraChanger] {name}: no PlayerController found on '{other.name}' or its parents; the player is not frozen.");
    }
}
