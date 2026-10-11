using System;
using UnityEngine;

// Se agrega al mismo objeto que CameraChanger. Avisa una sola vez cuando el jugador entra al cuarto.
public class RoomEntryTrigger : MonoBehaviour
{
    public int roomIndex;
    public event Action<int> PlayerEntered;

    private bool fired;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fired || !other.CompareTag("Player")) return;
        fired = true;
        PlayerEntered?.Invoke(roomIndex);
    }
}
