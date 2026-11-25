using UnityEngine;

// "proyectil" enemy with gear variations, attack half. Adapted from EnemyAttack.cs:
// when the player is in range and visible, plays "atacar" and throws the projectile of this character
// (martini, molotov, cohete...) that FabricaEnemigos gave us through ProceduralEnemyInfo.
// Works together with VariantEnemyPatrol, which stops walking while PlayerInRange is true.
//   Shoes (speed) -> fire rate and projectile speed     Aura (power) -> projectile damage; aura 3 fires a burst of 2
public class VariantEnemyAttack : EnemyVariantBase
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;

    [Header("Attack")]
    [Tooltip("Optional. Used when ProceduralEnemyInfo has no projectile")]
    [SerializeField] private GameObject fallbackProjectile;
    [SerializeField] private float fireRate = 1.8f;
    [SerializeField] private float projectileSpeed = 7f;
    [SerializeField] private float projectileDamage = 10f;
    [Tooltip("Time from the start of the 'atacar' animation to the moment the projectile leaves the hand")]
    [SerializeField] private float releaseDelay = 0.25f;
    [Tooltip("Where the projectile spawns, relative to the body (x is mirrored with the facing)")]
    [SerializeField] private Vector2 firePointOffset = new Vector2(0.25f, 0.1f);

    [Header("Aura 3 burst")]
    [SerializeField] private int burstShotsAtMaxAura = 2;
    [SerializeField] private float burstGap = 0.25f;

    public bool PlayerInRange { get; private set; }

    private float nextFireTime;
    private float releaseTimer = -1f;
    private int shotsLeft;

    protected override void OnActivated()
    {
        nextFireTime = Time.time + 0.75f;   // grace period when the player walks in
    }

    private void Update()
    {
        if (!IsAwake) { PlayerInRange = false; return; }

        // Only shoots at a player standing in its own room (not through the door into the next one)
        PlayerInRange = PlayerInRoom && ToPlayer.magnitude <= detectionRange && HasLineOfSight();

        if (PlayerInRange)
        {
            Face(ToPlayer.x);
            if (Time.time >= nextFireTime && releaseTimer < 0f)
            {
                TriggerAttack();
                shotsLeft = Aura >= CatalogoEnemigos.Niveles ? Mathf.Max(1, burstShotsAtMaxAura) : 1;
                releaseTimer = releaseDelay / SpeedMult;
                nextFireTime = Time.time + fireRate / SpeedMult;
            }
        }

        if (releaseTimer >= 0f)
        {
            releaseTimer -= Time.deltaTime;
            if (releaseTimer < 0f)
            {
                Shoot();
                shotsLeft--;
                if (shotsLeft > 0) releaseTimer = burstGap;
            }
        }
    }

    private void Shoot()
    {
        GameObject prefab = info != null && info.projectilePrefab != null ? info.projectilePrefab : fallbackProjectile;
        if (prefab == null)
        {
            Debug.LogWarning($"[VariantEnemyAttack] {name} has no projectile to throw.");
            return;
        }

        Bounds b = BodyBounds;
        Vector2 origin = (Vector2)b.center + new Vector2(FacingSign * (b.extents.x + firePointOffset.x), firePointOffset.y);
        Vector2 dir = player != null ? ((Vector2)player.position - origin).normalized : new Vector2(FacingSign, 0f);

        GameObject p = Instantiate(prefab, origin, Quaternion.identity);
        var proj = p.GetComponent<VariantProjectile>();
        if (proj == null) proj = p.AddComponent<VariantProjectile>();   // prefab not configured yet
        var demo = p.GetComponent<ProyectilDemo>();
        if (demo != null) Destroy(demo);                                 // test script from the factory
        proj.Launch(dir, projectileSpeed * SpeedMult, projectileDamage * PowerMult, gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
