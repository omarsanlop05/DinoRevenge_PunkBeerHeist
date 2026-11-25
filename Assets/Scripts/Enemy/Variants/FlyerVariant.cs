using UnityEngine;

// "volador" enemy with gear variations. Based on BatEnemy (FlyingDude.cs), plus a real attack that reads as
// anticipation -> action -> impact (the factory controller has: idle, correr (loop) and ataque (0.45 s, Any State)):
//   Patrol -> flies along the waypoints EnemyPlacer built for it          anim: correr moving / idle waiting
//   Hover  -> player detected in its room: moves above and beside them    anim: correr
//   Windup -> stops and pulls back / up a little, eyes on the player      anim: idle   (the telegraph)
//   Dive   -> swoops to where the player was, tilted toward the dive      anim: correr, and "atacar" right before
//             arriving, so the hit frames land on the contact               arriving (or on contact)
//   Return -> climbs back up and straightens, then hovers again           anim: correr
// It never leaves its room (ProceduralEnemyInfo.roomBounds) and ignores the player outside it.
// Shoes (speed) scale speeds and cooldowns; aura (power) scales damage.
public class FlyerVariant : EnemyVariantBase
{
    [Header("Patrol")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waypointReachDistance = 0.3f;
    [SerializeField] private float waitAtPoint = 0.6f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 7f;

    [Header("Hover")]
    [SerializeField] private Vector2 hoverOffset = new Vector2(2.5f, 3f);
    [SerializeField] private float hoverSpeed = 3.5f;
    [SerializeField] private float hoverTimeBeforeDive = 0.6f;

    [Header("Windup (telegraph)")]
    [SerializeField] private float windupTime = 0.5f;
    [Tooltip("How far it pulls back and up before diving")]
    [SerializeField] private float windupPullBack = 0.8f;

    [Header("Dive")]
    [SerializeField] private float diveSpeed = 8f;
    [SerializeField] private float maxDiveTime = 1f;
    [Tooltip("Seconds before reaching the target when the 'atacar' animation starts (the clip lasts 0.45 s)")]
    [SerializeField] private float attackAnimLead = 0.2f;
    [Tooltip("Max tilt of the body toward the dive, in degrees (0 = no tilt)")]
    [SerializeField] private float diveTilt = 25f;
    [SerializeField] private float tiltSpeed = 360f;

    [Header("Return")]
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
    private Vector2 windupDir;
    private bool attackAnimPlayed;
    private Vector2 home;
    private float targetTilt;
    private float hoverSide;   // -1 = left of the player, 1 = right, 0 = not chosen yet

    private Transform[] route;
    private int routeIndex;
    private float waitTimer;

    protected override void Awake()
    {
        base.Awake();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;   // physics never spins it; the tilt is set by code
    }

    protected override void OnActivated()
    {
        home = ClampToRoom(transform.position);
        route = info != null ? info.waypoints : null;
        state = State.Patrol;
        nextAttackTime = Time.time + 0.5f;
    }

    private void FixedUpdate()
    {
        if (!IsAwake) return;

        targetTilt = 0f;
        switch (state)
        {
            case State.Patrol: Patrol(); break;
            case State.Hover: Hover(); break;
            case State.Windup: Windup(); break;
            case State.Dive: Dive(); break;
            case State.Return: Return(); break;
        }

        rb.rotation = Mathf.MoveTowardsAngle(rb.rotation, targetTilt, tiltSpeed * Time.fixedDeltaTime);
        ConfineToRoom();
    }

    // ---- States ----

    private void Patrol()
    {
        if (PlayerDetected()) { Enter(State.Hover, hoverTimeBeforeDive); return; }

        if (route == null || route.Length == 0)
        {
            SetRunning(MoveTowards(home, patrolSpeed) > waypointReachDistance);
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            SetRunning(false);
            return;
        }

        Transform target = route[routeIndex];
        if (target == null) { routeIndex = (routeIndex + 1) % route.Length; return; }

        SetRunning(true);
        float left = MoveTowards(target.position, patrolSpeed);
        // Reached it, or a platform is in the way: go for the next one
        if (left <= waypointReachDistance || CheckStuck(true))
        {
            routeIndex = (routeIndex + 1) % route.Length;
            waitTimer = waitAtPoint;
            ResetStuck();
        }
    }

    private void Hover()
    {
        if (!PlayerInRoom) { state = State.Patrol; hoverSide = 0f; return; }
        SetRunning(true);

        // Stay above the player, on the side we are already on
        if (hoverSide == 0f) hoverSide = transform.position.x >= player.position.x ? 1f : -1f;
        Vector2 spot = (Vector2)player.position + new Vector2(hoverSide * hoverOffset.x, hoverOffset.y);
        float left = MoveTowards(spot, hoverSpeed);
        Face(ToPlayer.x);

        // Something blocks the way to that spot: try the other side of the player
        if (left > waypointReachDistance && CheckStuck(true))
        {
            hoverSide = -hoverSide;
            ResetStuck();
        }

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f && Time.time >= nextAttackTime && HasLineOfSight())
        {
            // Anticipation: idle pose while it pulls back, away from the player and a bit up
            Vector2 away = -ToPlayer.normalized;
            windupDir = (away + Vector2.up * 0.6f).normalized;
            if (animator != null) animator.ResetTrigger(AnimAttack);
            Enter(State.Windup, windupTime / SpeedMult);
        }
    }

    private void Windup()
    {
        SetRunning(false);
        Face(ToPlayer.x);

        // Pull back fast at the start and slow down (ease out), like gathering strength
        float total = windupTime / SpeedMult;
        float t = 1f - Mathf.Clamp01(stateTimer / total);
        float speed = windupPullBack / total * 2f * (1f - t);
        rb.linearVelocity = windupDir * speed;

        // Keep aiming during the windup; the target is locked when the dive starts
        if (player != null) diveTarget = ClampToRoom(player.position);

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer > 0f) return;

        attackAnimPlayed = false;
        Enter(State.Dive, maxDiveTime);
    }

    private void Dive()
    {
        SetRunning(true);
        stateTimer -= Time.fixedDeltaTime;

        Vector2 d = diveTarget - (Vector2)transform.position;
        float dist = MoveTowards(diveTarget, diveSpeed);
        targetTilt = TiltToward(d);

        // Start the strike animation just before arriving, so the hit lands on the contact
        float timeLeft = dist / Mathf.Max(0.01f, diveSpeed * SpeedMult);
        if (!attackAnimPlayed && timeLeft <= attackAnimLead) PlayStrike();

        if (dist < 0.2f || stateTimer <= 0f || CheckStuck(true)) EndDive();   // stuck = hit a platform corner
    }

    private void Return()
    {
        SetRunning(true);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.9f, hoverSpeed * SpeedMult);   // climb

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer > 0f) return;

        if (PlayerInRoom) Enter(State.Hover, hoverTimeBeforeDive);
        else { state = State.Patrol; hoverSide = 0f; }
    }

    private void PlayStrike()
    {
        attackAnimPlayed = true;
        TriggerAttack();
    }

    private void EndDive()
    {
        if (!attackAnimPlayed) PlayStrike();   // reached the spot or crashed: still show the strike
        nextAttackTime = Time.time + attackCooldown / SpeedMult;
        Enter(State.Return, returnTime);
    }

    private void Enter(State s, float time)
    {
        state = s;
        stateTimer = time;
    }

    // ---- Helpers ----

    // Returns the distance left to the target (clamped inside the room)
    private float MoveTowards(Vector2 target, float speed)
    {
        target = ClampToRoom(target);
        Vector2 d = target - (Vector2)transform.position;
        float dist = d.magnitude;
        rb.linearVelocity = dist > 0.01f ? d / dist * speed * SpeedMult : Vector2.zero;
        if (Mathf.Abs(d.x) > 0.1f) Face(d.x);
        return dist;
    }

    // Nose down toward the dive: clockwise when going right, counterclockwise when going left
    private float TiltToward(Vector2 dir)
    {
        if (diveTilt <= 0f || dir.sqrMagnitude < 0.0001f) return 0f;
        float angle = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;   // negative when going down
        angle = Mathf.Clamp(angle, -diveTilt, diveTilt);
        return dir.x >= 0f ? angle : -angle;
    }

    private bool PlayerDetected()
    {
        if (!PlayerInRoom || Time.time < nextAttackTime) return false;
        return ToPlayer.magnitude <= detectionRange && HasLineOfSight();
    }

    // ---- Damage on touch ----

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTouch(collision.gameObject);
        HandleEnemyBump(collision);
    }

    // Bumped into another enemy: change where it is going
    private void HandleEnemyBump(Collision2D collision)
    {
        if (!BumpedEnemyAhead(collision, rb.linearVelocity, out _)) return;

        switch (state)
        {
            case State.Patrol:
                if (route != null && route.Length > 0)
                {
                    routeIndex = (routeIndex + 1) % route.Length;   // head to the next waypoint instead
                    waitTimer = 0f;
                }
                break;
            case State.Hover:
                hoverSide = -hoverSide;   // go around to the other side of the player
                break;
            case State.Dive:
                EndDive();                // abort the swoop and climb back
                break;
        }
        ResetStuck();
    }
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
        if (HasRoom)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Room.center, Room.size);
        }
    }
}
