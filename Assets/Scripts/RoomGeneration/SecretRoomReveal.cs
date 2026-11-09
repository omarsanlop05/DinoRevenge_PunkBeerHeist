using System;
using UnityEngine;

// Waits until the secret door is broken (destroyed or disabled), then runs the reveal once.
public class SecretRoomReveal : MonoBehaviour
{
    private GameObject door;
    private Action onBroken;

    public void Init(GameObject doorObject, Action revealAction)
    {
        door = doorObject;
        onBroken = revealAction;
    }

    private void Update()
    {
        if (door != null && door.GetComponentInChildren<Collider2D>() != null) return;

        onBroken?.Invoke();
        Destroy(this);
    }
}