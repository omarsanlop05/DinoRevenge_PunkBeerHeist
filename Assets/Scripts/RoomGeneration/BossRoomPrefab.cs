using Unity.Cinemachine;
using UnityEngine;

public class BossRoomPrefab : MonoBehaviour
{
    [Tooltip("Cell corner where the player enters (standing row, left edge of the room)")]
    public Transform entryAnchor;
    public CinemachineCamera bossCamera;
    [Tooltip("Trigger at the room entrance that switches to the boss camera")]
    public CameraChanger entryChanger;

    public float Difficulty { get; private set; } = 1f;

    // Scene references can't be saved inside a prefab, so the manager sets them at runtime
    public void Initialize(CinemachineCamera previousCamera, float difficulty)
    {
        Difficulty = difficulty;

        entryChanger.zoneCamera = bossCamera;
        entryChanger.previousCamera = previousCamera;
        bossCamera.Priority = 0;

        // TODO: apply 'difficulty' to the boss once we define which parameter it controls
    }
}