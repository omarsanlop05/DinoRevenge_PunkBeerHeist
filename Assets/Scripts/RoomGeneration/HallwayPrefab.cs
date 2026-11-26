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
    [Tooltip("Changer at the hallway's entrance that gives the camera back to the last room when the player walks back. " +
             "If empty, the child named \"CameraChangerAnterior\" is used")]
    public CameraChanger backChanger;

    // Scene references can't be saved inside a prefab, so the manager sets them at runtime
    public void Initialize(CinemachineCamera previousCamera)
    {
        entryChanger.zoneCamera = hallwayCamera;
        entryChanger.previousCamera = previousCamera;
        hallwayCamera.Priority = 0;

        // Going back to the last room: that room's camera turns on and the hallway's turns off
        if (backChanger == null)
        {
            Transform t = transform.Find("CameraChangerAnterior");
            if (t == null)
                foreach (CameraChanger ch in GetComponentsInChildren<CameraChanger>(true))
                    if (ch.name == "CameraChangerAnterior") { t = ch.transform; break; }
            if (t != null) backChanger = t.GetComponent<CameraChanger>();
        }
        if (backChanger != null && backChanger != entryChanger)
        {
            backChanger.zoneCamera = previousCamera;   // the camera we go TO
            backChanger.previousCamera = hallwayCamera; // the camera we leave
            backChanger.awardsRoomPoints = false;       // going back must not give room points
        }
        else
        {
            Debug.LogWarning("[HallwayPrefab] No back CameraChanger found; the player can't get the last room's camera back.");
        }

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
