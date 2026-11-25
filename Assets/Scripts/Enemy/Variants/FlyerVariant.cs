using UnityEngine;

// "volador" enemy with gear variations. Based on BatEnemy (FlyingDude.cs), plus a real attack:
//   Patrol -> flies along the waypoints EnemyPlacer built for it (no gravity)
//   Hover  -> player detected: moves to a spot above and to the side of the player
//   Windup -> stops and plays "atacar" (telegraph), remembers where the player is
//   Dive   -> swoops to that spot fast
//   Return -> climbs back up, then hovers again (or goes home if the player left)
// It never chases further than leashDistance from where it spawned, so it stays in its room.
// Shoes (speed) scale speeds and cooldowns; aura (power) scales damage.
public class FlyerVariant : EnemyVariantBase
{
    [Header("Patrol")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waypointReachDistance = 0.3f;
    [SerializeField] private float waitAtPoint = 0.6f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 7f;
    [Tooltip("Max distance from its spawn point while chasing")]
    [SerializeField] private float leashDistance = 10f;

    [Header("Dive attack")]
    [SerializeField] private Vector2 hoverOffset = new Vector2(2.5f, 3f);
    [SerializeField] private float hoverSpeed = 3.5f;
    [SerializeField] private float hoverTimeBeforeDive = 0.6f;
    [SerializeField] private float windupTime = 0.5f;
    [SerializeField] private float diveSpeed = 8f;
    [SerializeField] private float maxDiveTime = 1f;
    [SerializeField] private float returnTime = 0.8f;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Damage (multiplied by the aura)")]
    [SerializeField] private float diveDamage = 15f;
    [SerializeField] private float touchDamage = 5f;

    private enum State { Patrol, Hover, Windup, Dive, Return }
    private State state = State.Patrol;
    private float stateTimer;
    private float nextAttackTime;
    private Vector2 diveTarget;
    private Vector2 home;

    private Transform[] route;
    private int routeIndex;
    private float waitTimer;

    protected override void Awake()
    {
        base.Awake();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    protected override void OnActivated()
    {
        home = transform.position;
        route = info != null ? info.waypoints : null;
        state = State.Patrol;
        nextAttackTime = Time.time + 0.5f;
    }

    private void FixedUpdate()
    {
        if (!IsAwake) return;

        switch (state)
        {
            case State.Patrol: Patrol(); break;
            case State.Hover: Hover(); break;
            case State.Windup: Windup(); break;
            case State.Dive: Dive(); break;
            case State.Return: Return(); break;
        }
    }

    // ---- States ----

    private void Patrol()
    {
        SetRunning(false);
        if (PlayerDetected()) { Enter(State.Hover, hoverTimeBeforeDive); return; }

        if (route == null || route.Length == 0)
        {
            MoveTowards(home, patrolSpeed);
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Transform target = route[routeIndex];
        if (target == null) { routeIndex = (routeIndex + 1) % route.Length; return; }

        if (MoveTowards(target.position, patrolSpeed) <= waypointReachDistance)
        {
            routeIndex = (routeIndex + 1) % route.Length;
            waitTimer = waitAtPoint;
        }
    }

    private void Hover()
    {
        SetRunning(true);
        if (LostPlayer()) { state = State.Patrol; return; }

        // Stay above the player, on the side we are already on
        float side = Mathf.Sign(transform.position.x - player.position.x);
        if (side == 0f) side = 1f;
        Vector2 spot = (Vector2)player.position + new Vector2(side * hoverOffset.x, hoverOffset.y);
        MoveTowards(spot, hoverSpeed);
        Face(ToPlayer.x);

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f && Time.time >= nextAttackTime && HasLineOfSight())
        {
            Enter(State.Windup, windupTime / SpeedMult);
            TriggerAttack();
        }
    }

    private void Windup()
    {
        rb.linearVelocity = Vector2.zero;
        Face(ToPlayer.x);
        diveTarget = player != null ? (Vector2)player.position : (Vector2)transform.position;

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f) Enter(State.Dive, maxDiveTime);
    }

    private void Dive()
    {
        SetRunning(true);
        stateTimer -= Time.fixedDeltaTime;

        float dist = MoveTowards(diveTarget, diveSpeed);
        if (dist < 0.2f || stateTimer <= 0f) EndDive();
    }

    private void Return()
    {
        SetRunning(true);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.9f, hoverSpeed * SpeedMult);   // climb

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer > 0f) return;

        if (LostPlayer()) state = State.Patrol;
        else Enter(State.Hover, hoverTimeBeforeDive);
    }

    private void EndDive()
    {
        nextAttackTime = Time.time + attackCooldown / SpeedMult;
        Enter(State.Return, returnTime);
    }

    private void Enter(State s, float time)
    {
        state = s;
        stateTimer = time;
    }

    // ---- Helpers ----

    // Returns the distance left to the target
    private float MoveTowards(Vector2 target, float speed)
    {
        Vector2 d = target - (Vector2)transform.position;
        float dist = d.magnitude;
        rb.linearVelocity = dist > 0.01f ? d / dist * speed * SpeedMult : Vector2.zero;
        if (Mathf.Abs(d.x) > 0.1f) Face(d.x);
        return dist;
    }

    private bool PlayerDetected()
    {
        if (player == null || Time.time < nextAttackTime) return false;
        if (Vector2.Distance(player.position, home) > leashDistance) return false;
        return ToPlayer.magnitude <= detectionRange && HasLineOfSight();
    }

    private bool LostPlayer() =>
        player == null || Vector2.Distance(player.position, home) > leashDistance ||
        Vector2.Distance(transform.position, home) > leashDistance;

    // ---- Damage on touch ----

    private void OnCollisionEnter2D(Collision2D collision) => HandleTouch(collision.gameObject);
    private void OnCollisionStay2D(Collision2D collision) => HandleTouch(collision.gameObject);

    private void HandleTouch(GameObject other)
    {
        bool diving = state == State.Dive;
        if (TryHitPlayer(other, (diving ? diveDamage : touchDamage) * PowerMult) && diving)
            EndDive();
        else if (diving && !other.CompareTag("Player") && ((1 << other.layer) & groundMask.value) != 0)
            EndDive();   // crashed into a wall or the floor
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)home : transform.position, leashDistance);
    }
}
