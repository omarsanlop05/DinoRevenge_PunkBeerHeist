using UnityEngine;

public class BossHitboxZone : MonoBehaviour
{
    private BossAttackHitbox parentHitbox;

    void Start()
    {
        // Obtener referencia al script padre
        parentHitbox = GetComponentInParent<BossAttackHitbox>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (parentHitbox != null)
        {
            parentHitbox.OnZonaTriggerEnter(other);
        }
    }
}