using System;
using System.Collections;
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("Movement")]
    public float moveSpeed = 8f;
    private float moveInput;
    private bool isWalking = false;

    [Header("Jump")]
    public bool isJumping = false;
    public float jumpForce = 12f;
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.45f;
    public bool jumpQueued = false;
    private float jumpInputBuffer = 0.15f;
    private float jumpInputTimer = 0f;

    [Header("Jump Control")]
    public int maxJumpCount = 1;
    private int jumpCount = 0;
    private bool wasGrounded = false;

    [Header("Custom Gravity")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Coyote Time")]
    public float coyoteTime = 0.1f;
    private float coyoteTimeCounter = 0f;

    [Header("Attack Hitbox")]
    public CircleCollider2D attackPoint;
    public AttackPointBehaviour attackBehaviour;

    [Header("Attack")]
    public float attackForce = 5f;
    public float attackCooldown = 0.8f;
    public bool isAttacking = false;
    private float nextAttackTime = 0f;
    private bool attackQueued = false;

    [Header("Dynamic Attack")]
    public float attackDuration = 0.75f;
    public AnimationCurve attackCurve;
    private float attackTimer = 0f;
    private Vector2 attackDirection;

    [Header("Beer")]
    public bool isDrinking = false;

    [Header("Invulnerability")]
    public bool isInvulnerable = false;
    public float invulnerabilityDuration = 1f;
    public bool isHurt = false;
    public bool isDead = false;

    [Header("Knockback")]
    public float knockbackForceX = 4f;
    public float knockbackForceY = 8f;

    [Header("SFX")]
    public AudioClip jumpSFX;
    public AudioClip walkSFX;
    public AudioClip attackSFX;

    private PlayerHealth playerHealth;
    private SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;
    private BoxCollider2D playerCollider;
    private int facingDirection = 1;
    public bool IsFacingRight => facingDirection == 1;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = rb.GetComponent<BoxCollider2D>();
        attackPoint.enabled = false;

        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.controller = this;
        spriteRenderer = GetComponent<SpriteRenderer>();

        ResetPlayerState();
    }

    public void ResetPlayerState()
    {
        isDead = false;
        isHurt = false;
        isAttacking = false;
        isInvulnerable = false;
        isDrinking = false;
        isJumping = false;
        jumpQueued = false;
        attackQueued = false;
        jumpCount = 0;
        jumpInputTimer = 0f;
        coyoteTimeCounter = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearVelocity = Vector2.zero;
        animator.Rebind();
        animator.Update(0f);
    }

    void Update()
    {
        // Block on special states
        if (isDrinking || isHurt || isDead || isAttacking)
        {
            // Clear other inputs
            jumpQueued = false;
            jumpInputTimer = 0f;
            attackQueued = false;
            return;
        }

        moveInput = Input.GetAxisRaw("Horizontal");

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
        {
            jumpQueued = true;
            jumpInputTimer = jumpInputBuffer;
        }

        if (jumpInputTimer > 0f)
            jumpInputTimer -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
            attackQueued = true;

        if (Input.GetKeyDown(KeyCode.E))
            playerHealth.TomarCerveza();
    }

    void FixedUpdate()
    {
        if (isDrinking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isHurt || isDead)
            return;

        bool isGrounded = IsGrounded();

        // Detect roof colition
        bool hitCeiling = IsHittingCeiling();
        if (hitCeiling && isJumping && rb.linearVelocity.y > 0)
        {
            // Force y velocity to 0 when coliding with roof
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            isJumping = false;
        }

        // Reset jump counter when touching floor
        if (isGrounded)
        {
            //Reset on ground
            if ((jumpCount > 0 || isJumping))
            {
                jumpCount = 0;
                isJumping = false;
                jumpQueued = false;
                jumpInputTimer = 0f;
            }
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter = Mathf.Max(coyoteTimeCounter - Time.fixedDeltaTime, 0f);

            //Falling velocity
            if (isJumping && rb.linearVelocity.y <= 0.1f)
            {
                isJumping = false;
            }
        }

        wasGrounded = isGrounded;

        if (!isAttacking)
            Movement();

        Attack();

        // Jump if is not attacking
        if (!isAttacking)
            Jump();

        ApplyGravity();

        if (isAttacking)
            AttackMovement();
    }

    void Jump()
    {
        // Can´t jump when attacking
        if (isAttacking)
        {
            jumpQueued = false;
            jumpInputTimer = 0f;
            return;
        }

        bool hasJumpInput = jumpInputTimer > 0f || jumpQueued;

        if (!hasJumpInput)
            return;

        bool canJumpFromGround = coyoteTimeCounter > 0f && jumpCount == 0;
        bool canDoubleJump = jumpCount > 0 && jumpCount < maxJumpCount;

        // Temporal debug
        if (hasJumpInput && !canJumpFromGround && !canDoubleJump)
        {
            Debug.Log($"Cant Jump - isGrounded: {IsGrounded()}, jumpCount: {jumpCount}, isJumping: {isJumping}, coyoteTime: {coyoteTimeCounter}, velocityY: {rb.linearVelocity.y}");
            jumpInputTimer = 0f;
            jumpQueued = false;
            return;
        }
        else
        {
            Debug.Log($"Jump Successful- isGrounded: {IsGrounded()}, jumpCount: {jumpCount}, isJumping: {isJumping}, coyoteTime: {coyoteTimeCounter}, velocityY: {rb.linearVelocity.y}");
        }

        if (canJumpFromGround || canDoubleJump)
        {
            //Clear buffer before jump
            jumpCount++;
            jumpInputTimer = 0f;
            jumpQueued = false;

            // Reset speed before jump
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            isJumping = true;
            coyoteTimeCounter = 0f;

            animator.SetTrigger("Jump");
            SoundManager.instance.playOnce(jumpSFX);
        }
    }

    void Movement()
    {
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        bool currentlyWalking = Mathf.Abs(rb.linearVelocity.x) > 0.1f;

        if (currentlyWalking && !isWalking && IsGrounded())
        {
            SoundManager.instance.loopSource.loop = true;
            SoundManager.instance.playSound(walkSFX);
            isWalking = true;
        }
        else if ((!currentlyWalking && isWalking) || !IsGrounded())
        {
            SoundManager.instance.stopSound(walkSFX);
            SoundManager.instance.loopSource.loop = false;
            isWalking = false;
        }
    }

    

    void ApplyGravity()
    {
        if (isHurt || isAttacking)
            return;

        if (IsGrounded() && rb.linearVelocity.y <= 0)
            return;

        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void Attack()
    {
        if (attackQueued)
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;

            attackTimer = 0f;
            attackDirection = new Vector2(facingDirection, 0).normalized;

            // Cancel queued jumps
            jumpQueued = false;
            jumpInputTimer = 0f;

            animator.SetTrigger("Attack");
            StartInvulnerability();
            SoundManager.instance.playOnce(attackSFX);

            Invoke(nameof(Activatehitbox), 0.35f);
        }

        attackQueued = false;
    }

    void Activatehitbox()
    {
        if (!isAttacking) return;
        attackPoint.enabled = true;
        attackBehaviour.StartAttack();
    }

    void AttackMovement()
    {
        attackTimer += Time.fixedDeltaTime;

        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;

        float t = attackTimer / attackDuration;
        float force = attackCurve.Evaluate(t) * attackForce;

        rb.linearVelocity = new Vector2(attackDirection.x * force, rb.linearVelocity.y);

        if (attackTimer >= attackDuration)
            CloseAttack();
    }

    void CloseAttack()
    {
        isAttacking = false;
        attackPoint.enabled = false;
        attackBehaviour.EndAttack();
        EndInvulnerability();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public void SetHurtState(float duration, float hitSource)
    {
        isHurt = true;
        StartInvulnerability();
        Knockback(hitSource);

        Invoke(nameof(FreezeAfterKnockback), 0.05f);

        // Cancel queued imputs
        jumpQueued = false;
        jumpInputTimer = 0f;
        isJumping = false;
        coyoteTimeCounter = 0;

        Invoke(nameof(ExitHurtState), duration);
    }

    void ExitHurtState()
    {
        isHurt = false;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void StartInvulnerability()
    {
        isInvulnerable = true;
        StartCoroutine(BlinkSprite());
        Invoke(nameof(EndInvulnerability), invulnerabilityDuration);
    }

    IEnumerator BlinkSprite()
    {
        while (isInvulnerable)
        {
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = (c.a == 1f) ? 0.5f : 1f;
                spriteRenderer.color = c;
            }

            yield return new WaitForSeconds(0.15f);
        }

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }
    }

    void EndInvulnerability()
    {
        isInvulnerable = false;
    }

    void Knockback(float hitSourceX)
    {
        rb.linearVelocity = Vector2.zero;

        int direction = (transform.position.x < hitSourceX) ? -1 : 1;

        rb.AddForce(new Vector2(direction * knockbackForceX, knockbackForceY), ForceMode2D.Impulse);

        isAttacking = false;
    }

    void FreezeAfterKnockback()
    {
        if (!isHurt) return;
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
    }

    public void drinKingState(float drinkTime)
    {
        // Cancel inputs
        jumpQueued = false;
        jumpInputTimer = 0f;
        isJumping = false;
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

        Invoke(nameof(EndBeerDrinking), drinkTime);
    }

    void EndBeerDrinking()
    {
        isDrinking = false;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public bool IsGrounded()
    {
        if (playerCollider == null) return false;

        float skin = 0.02f;
        float halfWidth = playerCollider.bounds.extents.x - skin;
        Vector2 basePos = (Vector2)playerCollider.bounds.center + Vector2.down * (playerCollider.bounds.extents.y + skin);

        Vector2 leftRay = basePos + Vector2.left * (halfWidth - 0.05f);
        Vector2 centerRay = basePos;
        Vector2 rightRay = basePos + Vector2.right * (halfWidth - 0.05f);

        RaycastHit2D leftHit = Physics2D.Raycast(leftRay, Vector2.down, groundCheckDistance, groundLayer);
        RaycastHit2D centerHit = Physics2D.Raycast(centerRay, Vector2.down, groundCheckDistance, groundLayer);
        RaycastHit2D rightHit = Physics2D.Raycast(rightRay, Vector2.down, groundCheckDistance, groundLayer);

        return leftHit.collider != null || centerHit.collider != null || rightHit.collider != null;
    }

    bool IsHittingCeiling()
    {
        if (playerCollider == null) return false;

        float skin = 0.02f;
        float halfWidth = playerCollider.bounds.extents.x - skin;
        float ceilingCheckDistance = 0.1f;

        Vector2 topPos = (Vector2)playerCollider.bounds.center + Vector2.up * (playerCollider.bounds.extents.y + skin);

        Vector2 leftRay = topPos + Vector2.left * (halfWidth - 0.05f);
        Vector2 centerRay = topPos;
        Vector2 rightRay = topPos + Vector2.right * (halfWidth - 0.05f);

        RaycastHit2D leftHit = Physics2D.Raycast(leftRay, Vector2.up, ceilingCheckDistance, groundLayer);
        RaycastHit2D centerHit = Physics2D.Raycast(centerRay, Vector2.up, ceilingCheckDistance, groundLayer);
        RaycastHit2D rightHit = Physics2D.Raycast(rightRay, Vector2.up, ceilingCheckDistance, groundLayer);

        return leftHit.collider != null || centerHit.collider != null || rightHit.collider != null;
    }
    

    void OnDrawGizmos()
    {
        if (playerCollider == null) return;

        float halfWidth = playerCollider.bounds.extents.x;
        Vector2 basePos = playerCollider.bounds.center;
        Vector2 bottomCenter = basePos + Vector2.down * playerCollider.bounds.extents.y;

        Vector2 leftRay = bottomCenter + Vector2.left * (halfWidth - 0.05f);
        Vector2 centerRay = bottomCenter;
        Vector2 rightRay = bottomCenter + Vector2.right * (halfWidth - 0.05f);

        Gizmos.color = Color.green;

        Gizmos.DrawLine(leftRay, leftRay + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(centerRay, centerRay + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(rightRay, rightRay + Vector2.down * groundCheckDistance);

        Gizmos.DrawSphere(leftRay + Vector2.down * groundCheckDistance, 0.02f);
        Gizmos.DrawSphere(centerRay + Vector2.down * groundCheckDistance, 0.02f);
        Gizmos.DrawSphere(rightRay + Vector2.down * groundCheckDistance, 0.02f);
    }
}