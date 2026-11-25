using System.Collections;
using UnityEngine;

// Projectile of the "proyectil" enemies (martini, molotov, cohete, ...). Adapted from Projectile.cs.
// Lives on proyectilObjetoBase, so the 6 projectile prefabs (variants of it) inherit it.
// VariantEnemyAttack calls Launch() with the speed and damage already scaled by the enemy's shoes and aura.
// On hitting the player or a wall it plays "impactar" and is destroyed when the animation ends.
public class VariantProjectile : MonoBehaviour
{
    private const string AnimImpact = "impactar";

    [SerializeField] private float lifetime = 4f;
    [Tooltip("Used only if nobody calls Launch (e.g. the prefab dropped in a scene)")]
    [SerializeField] private float defaultDamage = 10f;
    [Tooltip("Rotate the sprite to point where it flies (rockets). Off = only mirror left / right")]
    [SerializeField] private bool rotateToDirection = false;
    [SerializeField] private float colliderRadius = 0.05f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private string sortingLayer = "Personaje";

    private Rigidbody2D rb;
    private Animator animator;
    private GameObject owner;
    private float damage;
    private bool exploded;

    private void Awake()
    {
        damage = defaultDamage;
        if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground");

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;   // dynamic so trigger events fire against the static Tilemap
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            var circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = colliderRadius;
            col = circle;
        }
        col.isTrigger = true;

        animator = GetComponent<Animator>();
        EnemyVariantBase.ApplySortingLayer(GetComponent<SpriteRenderer>(), sortingLayer);
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Launch(Vector2 direction, float speed, float hitDamage, GameObject shooter)
    {
        owner = shooter;
        damage = hitDamage;
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        rb.linearVelocity = dir * speed;

        if (rotateToDirection)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
        else if (dir.x < 0f)
        {
            Vector3 s = transform.localScale;
            s.x = -Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (exploded || other.isTrigger) return;   // ignore camera zones, room triggers, attack hitboxes
        if (owner != null && other.transform.IsChildOf(owner.transform)) return;
        if (other.CompareTag("Enemy")) return;      // enemies don't block each other's shots

        if (other.CompareTag("Player"))
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health != null) health.RecibirDaño(damage, transform.position.x);
            Explode();
        }
        else if (((1 << other.gameObject.layer) & groundMask.value) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        exploded = true;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        StartCoroutine(ImpactAndDie());
    }

    private IEnumerator ImpactAndDie()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Destroy(gameObject);
            yield break;
        }

        animator.SetTrigger(AnimImpact);
        yield return null;   // wait for the state change
        yield return null;
        float length = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(Mathf.Clamp(length, 0.05f, 2f));
        Destroy(gameObject);
    }
}
