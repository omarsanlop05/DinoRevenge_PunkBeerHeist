using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Gravedad personalizada")]
    public float fallMultiplier = 2.5f;

    [Header("Coyote Time")]
    public float coyoteTime = 0.2f;
    private float coyoteTimeCounter = 0f;

    [Header("Hitbox de ataque")]
    public CircleCollider2D attackPoint;

    [Header("Ataque")]
    public float attackForce = 10f;
    public float attackCooldown = 0.8f;
    private bool isAttacking = false;
    private float nextAttackTime = 0f;

    [Header("Ataque dinámico")]
    public float attackDuration = 0.2f;
    public AnimationCurve attackCurve;
    private float attackTimer = 0f;
    private Vector2 attackDirection;

    private float moveInput;
    private bool jumpQueued = false;
    private bool attackQueued = false;

    public LayerMask groundLayer;
    public float groundCheckDistance = 0.2f;

    private Rigidbody2D rb;
    private int facingDirection = 1;

    private BoxCollider2D playerCollider;

    public bool IsFacingRight => facingDirection == 1;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = rb.GetComponent<BoxCollider2D>();
        attackPoint.enabled = false;
    }

    void Update()
    {
        moveInput = isAttacking ? 0 : Input.GetAxisRaw("Horizontal");

        if (moveInput != 0)
        {
            facingDirection = (int)Mathf.Sign(moveInput);
            Vector3 rotation = transform.eulerAngles;
            rotation.y = (facingDirection == 1) ? 0f : 180f;
            transform.eulerAngles = rotation;

            animator.SetBool("isRunning", true);
        }
        else
        {
            animator.SetBool("isRunning", false);
        }

        if (Input.GetKeyDown(KeyCode.Space))
            jumpQueued = true;

        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
            attackQueued = true;
    }

    void FixedUpdate()
    {
        if (!isAttacking)
            Movimiento();
        if (IsGrounded())
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter = Mathf.Max(coyoteTimeCounter - Time.fixedDeltaTime, 0f);
        Saltar();
        AplicarGravedad();
        Atacar();

        if (isAttacking)
            MovimientoDeAtaque();
    }

    void Movimiento()
    {
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    void Saltar()
    {
        if (jumpQueued && coyoteTimeCounter > 0f)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            coyoteTimeCounter = 0f; 
        }

        jumpQueued = false;
    }

    void AplicarGravedad()
    {
        rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
    }

    void Atacar()
    {
        if (attackQueued)
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;

            attackTimer = 0f;
            attackDirection = new Vector2(facingDirection, 0).normalized;

            attackPoint.enabled = true;
            animator.SetTrigger("Attack");

            attackQueued = false;
        }
    }

    void MovimientoDeAtaque()
    {
        attackTimer += Time.fixedDeltaTime;

        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;

        float t = attackTimer / attackDuration;
        float force = attackCurve.Evaluate(t) * attackForce;

        rb.linearVelocity = new Vector2(attackDirection.x * force, rb.linearVelocity.y);

        if (attackTimer >= attackDuration)
            FinAtaque();
    }

    void FinAtaque()
    {
        isAttacking = false;
        attackPoint.enabled = false;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    /*
     bool IsGrounded() { 
        if (playerCollider == null) return false; 
        Vector2 rayStart = (Vector2)transform.position + Vector2.down * (playerCollider.bounds.extents.y + 0.01f); 
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 0.1f); 
        return hit.collider != null; 
     }
     */

    bool IsGrounded()
    {
        if (playerCollider == null) return false;

        float halfWidth = playerCollider.bounds.extents.x;
        Vector2 basePos = (Vector2)playerCollider.bounds.center + Vector2.down * (playerCollider.bounds.extents.y + 0.01f);

        Vector2 leftRay = basePos + Vector2.left * (halfWidth);
        Vector2 centerRay = basePos;
        Vector2 rightRay = basePos + Vector2.right * (halfWidth);

        RaycastHit2D leftHit = Physics2D.Raycast(leftRay, Vector2.down, groundCheckDistance);
        RaycastHit2D centerHit = Physics2D.Raycast(centerRay, Vector2.down, groundCheckDistance);
        RaycastHit2D rightHit = Physics2D.Raycast(rightRay, Vector2.down, groundCheckDistance);
        return leftHit.collider != null || centerHit.collider != null || rightHit.collider != null;
    }

    void OnDrawGizmos()
    {
        if (playerCollider == null) return;

        float halfWidth = playerCollider.bounds.extents.x;
        Vector2 basePos = playerCollider.bounds.center;

        // Ajustar la posición base (punto inferior del collider)
        Vector2 bottomCenter = basePos + Vector2.down;

        Vector2 leftRay = bottomCenter + Vector2.left * (halfWidth - 0.05f);
        Vector2 centerRay = bottomCenter;
        Vector2 rightRay = bottomCenter + Vector2.right * (halfWidth - 0.05f);

        Gizmos.color = Color.green;

        // Dibuja líneas de rayos
        Gizmos.DrawLine(leftRay, leftRay + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(centerRay, centerRay + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(rightRay, rightRay + Vector2.down * groundCheckDistance);

        // Dibuja pequeñas esferas en los extremos
        Gizmos.DrawSphere(leftRay + Vector2.down * groundCheckDistance, 0.02f);
        Gizmos.DrawSphere(centerRay + Vector2.down * groundCheckDistance, 0.02f);
        Gizmos.DrawSphere(rightRay + Vector2.down * groundCheckDistance, 0.02f);
    }

}