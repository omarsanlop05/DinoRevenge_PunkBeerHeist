using Unity.Cinemachine;
using UnityEngine;

//NO SIRVE
public class CameraYRecentering : MonoBehaviour
{
    public CinemachineCamera cineCam;
    public Transform player;
    public float reCenterSpeed = 1f;
    public float targetHeight = 0f; // altura base de cámara

    private CinemachinePositionComposer composer;
    private float startY;

    void Start()
    {
        composer = cineCam.GetComponent<CinemachinePositionComposer>();
        startY = composer.TargetOffset.y;
    }

    void Update()
    {
        Vector3 offset = composer.TargetOffset;
        offset.y = Mathf.Lerp(offset.y, startY, Time.deltaTime * reCenterSpeed);
        composer.TargetOffset = offset;
    }
}
