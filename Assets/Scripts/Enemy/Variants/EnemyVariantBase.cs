using UnityEngine;

// Shared logic for the procedural enemies made by FabricaEnemigos (helmet / shoes / aura variations).
//  - Reads its multipliers from ProceduralEnemyInfo (speed from the shoes, damage from the aura).
//  - Sleeps (visible but frozen) until RoomCombatTracker wakes it when the player enters the room.
//  - Animator parameters of the factory controllers: "corriendo" (bool) and "atacar" (trigger).
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyVariantBase : MonoBehaviour
{
    protected const string AnimRunning = "corriendo";
    protected const string AnimAttack = "atacar";

    [Header("Common")]
    [Tooltip("Layers that count as floor and walls (the room Tilemap is on 'Ground')")]
    [SerializeField] protected LayerMask groundMask;
    [Tooltip("True if the sprites look to the right when localScale.x is positive. The factory sprites look LEFT, so false")]
    [SerializeField] protected bool spriteFacesRight = false;
    [Tooltip("Minimum time between two contact hits on the player")]
    [SerializeField] protected float contactCooldown = 1f;
    [Tooltip("Sorting layer of the sprite. 'Default' is drawn under the background, so it must be 'Personaje' or higher")]
    [SerializeField] protected string sortingLayer = "Personaje";

    protected Rigidbody2D rb;
    protected Animator animator;
    protected Collider2D body;
    protected Transform player;
    protected ProceduralEnemyInfo info;

    public bool IsAwake { get; private set; }
    public float ActivatedTime { get; private set; } = -1f;

    private float lastContactHit = -100f;

    protected float SpeedMult => info != null ? info.speedMultiplier : 1f;
    protected float PowerMult => info != null ? info.powerMultiplier : 1f;
    protected int Aura => info != null ? info.aura : 1;
    protected bool FacingRight => (transform.localScale.x >= 0f) == spriteFacesRight;

    // True when the enemy dies less than PlayerScore.FastKillSeconds after it woke up (the player saw it)
    public bool KilledFast
    {
        get
        {
            float limit = PlayerScore.Instance != null ? PlayerScore.Instance.FastKillSeconds : 3f;
            return IsAwake && Time.time - ActivatedTime < limit;
        }
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        body = GetComponent<Collider2D>();
        info = GetComponent<ProceduralEnemyInfo>();
        if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground");
        ApplySortingLayer(GetComponent<SpriteRenderer>(), sortingLayer);
    }

    // Puts the sprite on a visible sorting layer (only if that layer exists in the project)
    public static void ApplySortingLayer(SpriteRenderer sr, string layerName)
    {
        if (sr == null || string.IsNullOrEmpty(layerName)) return;
        if (SortingLayer.NameToID(layerName) == 0 && layerName != "Default") return;   // unknown layer
        sr.sortingLayerName = layerName;
    }

    protected virtual void Start()
    {
        if (info == null) info = GetComponent<ProceduralEnemyInfo>();   // EnemyPlacer adds it after Awake
        FindPlayer();
    }

    // ---- Sleep / wake (called by RoomCombatTracker) ----

    public virtual void Sleep()
    {
        IsAwake = false;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        SetRunning(false);
    }

    public virtual void Activate()
    {
        if (IsAwake) return;
        IsAwake = true;
        ActivatedTime = Time.time;
        if (info == null) info = GetComponent<ProceduralEnemyInfo>();
        rb.simulated = true;
        FindPlayer();
        OnActivated();
    }

    protected virtual void OnActivated() { }

    // ---- Helpers for the behaviours ----

    protected bool FindPlayer()
    {
        if (player != null) return true;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        return player != null;
    }

    protected Vector2 ToPlayer => player != null ? (Vector2)(player.position - transform.position) : Vector2.zero;

    protected void Face(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f) return;
        bool wantRight = dirX > 0f;
        if (wantRight == FacingRight) return;

        Vector3 s = transform.localScale;
        s.x = -s.x;
        transform.localScale = s;
    }

    protected float FacingSign => FacingRight ? 1f : -1f;

    protected void SetRunning(bool running)
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetBool(AnimRunning, running);
    }

    protected void TriggerAttack()
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(AnimAttack);
    }

    // Nothing from the ground layer between the enemy and the player
    protected bool HasLineOfSight()
    {
        if (player == null) return false;
        Vector2 from = transform.position;
        Vector2 to = player.position;
        return Physics2D.Linecast(from, to, groundMask).collider == null;
    }

    protected Bounds BodyBounds => body != null ? body.bounds : new Bounds(transform.position, Vector3.one * 0.5f);

    // Wall right in front, at body height
    protected bool WallAhead(float dir, float distance = 0.15f)
    {
        Bounds b = BodyBounds;
        Vector2 origin = new Vector2(b.center.x + dir * b.extents.x, b.center.y);
        return Physics2D.Raycast(origin, Vector2.right * dir, distance, groundMask).collider != null;
    }

    // Floor just ahead of the front foot (false at a ledge)
    protected bool GroundAhead(float dir, float lookAhead = 0.15f)
    {
        Bounds b = BodyBounds;
        Vector2 origin = new Vector2(b.center.x + dir * (b.extents.x + lookAhead), b.min.y + 0.05f);
        return Physics2D.Raycast(origin, Vector2.down, 0.4f, groundMask).collider != null;
    }

    protected bool IsGrounded()
    {
        Bounds b = BodyBounds;
        return Physics2D.BoxCast(b.center, new Vector2(b.size.x * 0.9f, 0.05f), 0f, Vector2.down,
                                 b.extents.y + 0.05f, groundMask).collider != null;
    }

    // Deals damage to the player respecting contactCooldown. Returns true if it hit.
    protected bool TryHitPlayer(GameObject other, float damage)
    {
        if (!IsAwake || !other.CompareTag("Player")) return false;
        if (Time.time < lastContactHit + contactCooldown) return false;

        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health == null) return false;

        health.RecibirDaño(damage, transform.position.x);
        lastContactHit = Time.time;
        return true;
    }
}
