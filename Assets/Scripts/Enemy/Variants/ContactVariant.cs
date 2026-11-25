using UnityEngine;

// "contacto" enemy with gear variations. Based on RollingSkull (walks and hurts on touch), plus a real attack:
//   Patrol  -> walks its platform, turns at walls and ledges (never falls off)
//   Windup  -> sees the player at its height: stops and plays "atacar" (telegraph)
//   Charge  -> dashes toward the player until a wall, a ledge or chargeDuration
//   Recover -> short pause, then back to patrol (attack on cooldown)
// Shoes (speed) scale patrol / charge speed and cooldowns; aura (power) scales damage.
public class ContactVariant : EnemyVariantBase
{
    [Header("Patrol")]
    [SerializeField] private float patrolSpeed = 1.5f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 6f;
    [Tooltip("Max height difference to consider the player 'on the same floor'")]
    [SerializeField] private float verticalTolerance = 1.5f;

    [Header("Charge attack")]
    [SerializeField] private float windupTime = 0.45f;
    [SerializeField] private float chargeSpeed = 6f;
    [SerializeField] private float chargeDuration = 0.8f;
    [SerializeField] private float recoverTime = 0.5f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Damage (multiplied by the aura)")]
    [SerializeField] private float chargeDamage = 15f;
    [SerializeField] private float touchDamage = 5f;

    private enum State { Patrol, Windup, Charge, Recover }
    private State state = State.Patrol;
    private float stateTimer;
    private float nextAttackTime;
    private float chargeDir;

    protected override void OnActivated()
    {
        state = State.Patrol;
        nextAttackTime = Time.time + 0.5f;   // small grace period when the player walks in
    }

    private void FixedUpdate()
    {
        if (!IsAwake) return;

        switch (state)
        {
            case State.Patrol: Patrol(); break;
            case State.Windup: Windup(); break;
            case State.Charge: Charge(); break;
            case State.Recover: Recover(); break;
        }
    }

    private void Patrol()
    {
        if (CanStartAttack())
        {
            Face(ToPlayer.x);
            Enter(State.Windup, windupTime / SpeedMult);
            TriggerAttack();
            return;
        }

        float dir = FacingSign;
        if (IsGrounded() && (WallAhead(dir) || !GroundAhead(dir)))
        {
            Face(-dir);
            dir = -dir;
        }

        rb.linearVelocity = new Vector2(dir * patrolSpeed * SpeedMult, rb.linearVelocity.y);
        SetRunning(true);
    }

    private void Windup()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        SetRunning(false);
        if (player != null) Face(ToPlayer.x);

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer > 0f) return;

        chargeDir = FacingSign;
        Enter(State.Charge, chargeDuration);
    }

    private void Charge()
    {
        SetRunning(true);
        stateTimer -= Time.fixedDeltaTime;

        bool blocked = WallAhead(chargeDir, 0.1f) || (IsGrounded() && !GroundAhead(chargeDir, 0.05f));
        if (stateTimer <= 0f || blocked)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            nextAttackTime = Time.time + attackCooldown / SpeedMult;
            Enter(State.Recover, recoverTime);
            return;
        }

        rb.linearVelocity = new Vector2(chargeDir * chargeSpeed * SpeedMult, rb.linearVelocity.y);
    }

    private void Recover()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        SetRunning(false);
        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f) state = State.Patrol;
    }

    private void Enter(State s, float time)
    {
        state = s;
        stateTimer = time;
    }

    private bool CanStartAttack()
    {
        if (Time.time < nextAttackTime || player == null || !IsGrounded()) return false;

        Vector2 d = ToPlayer;
        if (Mathf.Abs(d.x) > detectionRange || Mathf.Abs(d.y) > verticalTolerance) return false;
        return HasLineOfSight();
    }

    // ---- Damage on touch: strong while charging, weak otherwise ----

    private void OnCollisionEnter2D(Collision2D collision) => HandleTouch(collision.gameObject);
    private void OnCollisionStay2D(Collision2D collision) => HandleTouch(collision.gameObject);

    private void HandleTouch(GameObject other)
    {
        float damage = (state == State.Charge ? chargeDamage : touchDamage) * PowerMult;
        if (TryHitPlayer(other, damage) && state == State.Charge)
        {
            // Hit lands: stop the charge so it does not push the player across the room
            nextAttackTime = Time.time + attackCooldown / SpeedMult;
            Enter(State.Recover, recoverTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRange * 2f, verticalTolerance * 2f, 0f));
    }
}
