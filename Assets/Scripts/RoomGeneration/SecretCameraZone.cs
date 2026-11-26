using Unity.Cinemachine;
using UnityEngine;

// Camera switch for a secret room. Replaces the two enter-only CameraChangers it used before: their trigger boxes
// overlapped in the shaft, so going up and down again inside the overlap left the wrong camera on.
// This one looks at where the CENTER of the player is every frame:
//   below the main room (inside the secret room or the tunnel part under it) -> secret camera,
//   anywhere else over the secret room's columns                          -> main room camera.
// A small band around the border (hysteresis) stops it from flickering while the player stands on it.
public class SecretCameraZone : MonoBehaviour
{
    private CinemachineCamera secretCam;
    private CinemachineCamera mainCam;
    private float xMin, xMax, yMin, borderY;
    private const float Hysteresis = 0.5f;
    private const int ActivePriority = 10, InactivePriority = 0;

    private Transform player;
    private Collider2D playerCollider;
    private bool inside;

    // xMin..xMax: secret room columns (world). yMin: bottom of the secret room. borderY: bottom edge of the main room.
    public void Init(CinemachineCamera secretCamera, CinemachineCamera mainCamera, float xMin, float xMax, float yMin, float borderY)
    {
        secretCam = secretCamera;
        mainCam = mainCamera;
        this.xMin = xMin;
        this.xMax = xMax;
        this.yMin = yMin;
        this.borderY = borderY;
    }

    private void Update()
    {
        if (secretCam == null || mainCam == null) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
            playerCollider = p.GetComponent<Collider2D>();
        }

        Vector2 c = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;
        bool overColumns = c.x >= xMin && c.x <= xMax && c.y >= yMin;
        bool nowInside = inside
            ? overColumns && c.y < borderY + Hysteresis      // stays in until clearly above the border
            : overColumns && c.y < borderY - Hysteresis;     // gets in once clearly below it

        if (nowInside == inside) return;
        inside = nowInside;

        if (inside)
        {
            secretCam.Priority = ActivePriority;
            mainCam.Priority = InactivePriority;
        }
        else if (secretCam.Priority == ActivePriority)
        {
            // Only hand the camera back if this zone still owns it (another room's changer may have taken it)
            mainCam.Priority = ActivePriority;
            secretCam.Priority = InactivePriority;
        }
    }
}
