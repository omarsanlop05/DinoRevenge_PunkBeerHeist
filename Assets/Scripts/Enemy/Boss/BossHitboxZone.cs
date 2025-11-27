using UnityEngine;

public class BossHitboxZone : MonoBehaviour
{
    private BossAttackHitbox parentHitbox;

    void Start()
    {
        // Obtener referencia al script padre
        parentHitbox = GetComponentInParent<BossAttackHitbox>();

        if (parentHitbox == null)
        {
            Debug.LogError($"[{gameObject.name}] No se encontró BossAttackHitbox en el padre!");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (parentHitbox != null)
        {
            parentHitbox.OnZonaTriggerEnter(other);
        }
    }

    // CLAVE: Agregar OnTriggerStay2D para detectar colliders que ya están dentro
    void OnTriggerStay2D(Collider2D other)
    {
        if (parentHitbox != null)
        {
            parentHitbox.OnZonaTriggerStay(other);
        }
    }
}