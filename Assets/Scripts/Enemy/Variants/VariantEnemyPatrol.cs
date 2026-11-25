using UnityEngine;

// "proyectil" enemy with gear variations, movement half. Adapted from EnemyPatrol.cs:
// walks along its platform and turns at walls / ledges. The ground and wall checks are computed from the
// collider, so the prefab does not need groundCheck / wallCheck Transforms.
// When VariantEnemyAttack sees the player it stops, faces the player and lets the attack script shoot.
//   Shoes (speed) -> patrol speed
[RequireComponent(typeof(VariantEnemyAttack))]
public class VariantEnemyPatrol : EnemyVariantBase
{
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [Tooltip("Pause after turning around, so it does not jitter at the edges")]
    [SerializeField] private float turnPause = 0.3f;

    private VariantEnemyAttack attack;
    private float pauseTimer;

    protected override void Awake()
    {
        base.Awake();
        attack = GetComponent<VariantEnemyAttack>();
    }

    private void FixedUpdate()
    {
        if (!IsAwake) return;
        ConfineToRoom();

        // Shooting: stand still (VariantEnemyAttack already faces the player)
        if (attack != null && attack.PlayerInRange)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetRunning(false);
            return;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetRunning(false);
            return;
        }

        float dir = FacingSign;
        bool stuck = CheckStuck(true);   // wedged on a step or a corner the rays did not catch
        if (stuck || AtRoomEdge(dir) || (IsGrounded() && (WallAhead(dir) || !GroundAhead(dir))))
        {
            Face(-dir);
            pauseTimer = turnPause;
            ResetStuck();
            return;
        }

        rb.linearVelocity = new Vector2(dir * patrolSpeed * SpeedMult, rb.linearVelocity.y);
        SetRunning(true);
    }

    // Like EnemyPatrol: bumping into another enemy in front makes it turn around
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (attack != null && attack.PlayerInRange) return;   // standing and shooting: does not walk anywhere
        if (!BumpedEnemyAhead(collision, new Vector2(FacingSign, 0f), out _)) return;

        Face(-FacingSign);
        pauseTimer = turnPause;
        ResetStuck();
    }
}
