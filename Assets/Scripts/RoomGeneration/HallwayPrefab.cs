using Unity.Cinemachine;
using UnityEngine;

public class HallwayPrefab : MonoBehaviour
{
    [Tooltip("Cell corner where the player enters (standing row, left edge)")]
    public Transform entryAnchor;
    [Tooltip("Cell corner where the player leaves (standing row, right edge). The boss room attaches here")]
    public Transform exitAnchor;
    public CinemachineCamera hallwayCamera;
    public CameraChanger entryChanger;
    public RoomEntryTrigger entryTrigger;

    // Scene references can't be saved inside a prefab, so the manager sets them at runtime
    public void Initialize(CinemachineCamera previousCamera)
    {
        entryChanger.zoneCamera = hallwayCamera;
        entryChanger.previousCamera = previousCamera;
        hallwayCamera.Priority = 0;

        // 1. Asignar el jugador como objetivo de seguimiento
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            hallwayCamera.Target.TrackingTarget = player.transform;

        // 2. Recalcular el confiner tras mover el prefab al final del nivel
        var confiner = hallwayCamera.GetComponent<CinemachineConfiner2D>();
        if (confiner != null)
            confiner.InvalidateBoundingShapeCache();
    }
}