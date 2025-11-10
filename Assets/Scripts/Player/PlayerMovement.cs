using System;
using System.Collections;
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("Movimiento")]
    public float moveSpeed = 8f;
    private float moveInput;
    private bool isWalking = false;

    [Header("Salto")]
    public bool isJumping = false;
    public float jumpForce = 12f;
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.3f;
    public bool jumpQueued = false;
    private float jumpInputBuffer = 0.15f; // Buffer para capturar input temprano
    private float jumpInputTimer = 0f;

    [Header("Control de saltos")]
    public int maxJumpCount = 1;
    private int jumpCount = 0;
    private bool wasGrounded = false;

    [Header("Gravedad personalizada")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Coyote Time")]
    public float coyoteTime = 0.15f;
    private float coyoteTimeCounter = 0f;

    [Header("Hitbox de ataque")]
    public CircleCollider2D attackPoint;
    public AttackPointBehaviour attackBehaviour;

    [Header("Ataque")]
    public float attackForce = 5f;
    public float attackCooldown = 0.8f;
    public bool isAttacking = false;
    private float nextAttackTime = 0f;
    private bool attackQueued = false;

    [Header("Ataque dinámico")]
    public float attackDuration = 0.75f;
    public AnimationCurve attackCurve;
    private float attackTimer = 0f;
    private Vector2 attackDirection;

    [Header("Cerveza")]
    public bool isDrinking = false;

    [Header("Invulnerabilidad")]
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
        coyoteTimeCounter = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearVelocity = Vector2.zero;
        animator.Rebind();
        animator.Update(0f);
    }

    void Update()
    {
        if (isDrinking || isHurt || isDead)
            return;

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
        {
            jumpQueued = true;
            jumpInputTimer = jumpInputBuffer; // Iniciar timer del buffer
        }

        // Decrementar timer del buffer
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

        // Resetear contador de saltos cuando toca el suelo
        if (isGrounded)
        {
            if (!wasGrounded)
            {
                jumpCount = 0;
                isJumping = false; // IMPORTANTE: Resetear flag de salto
            }
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter = Mathf.Max(coyoteTimeCounter - Time.fixedDeltaTime, 0f);
        }

        wasGrounded = isGrounded;

        if (!isAttacking)
            Movimiento();

        Saltar();
        AplicarGravedad();
        Atacar();

        if (isAttacking)
            MovimientoDeAtaque();
    }


    void Movimiento()
    {
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        bool currentlyWalking = Mathf.Abs(rb.linearVelocity.x) > 0.1f;

        if (currentlyWalking && !isWalking && IsGrounded())
        {
            // Empezó a caminar
            SoundManager.instance.loopSource.loop = true;
            SoundManager.instance.playSound(walkSFX);
            isWalking = true;
        }
        else if ((!currentlyWalking && isWalking) || !IsGrounded())
        {
            // Dejó de caminar
            SoundManager.instance.stopSound(walkSFX);
            SoundManager.instance.loopSource.loop = false;
            isWalking = false;
        }
    }

    void Saltar()
    {
        // Usar el timer en lugar de solo el flag
        if (jumpInputTimer <= 0f)
            return;

        // Verificar si puede saltar
        bool canJumpFromGround = coyoteTimeCounter > 0f && jumpCount == 0;
        bool canDoubleJump = jumpCount > 0 && jumpCount < maxJumpCount;

        if (canJumpFromGround || canDoubleJump)
        {
            // Cancelar velocidad vertical anterior para salto consistente
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

            // Aplicar fuerza de salto
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            jumpCount++;
            isJumping = true;
            coyoteTimeCounter = 0f;

            animator.SetTrigger("Jump");
            SoundManager.instance.playOnce(jumpSFX);

            // Consumir el input inmediatamente
            jumpInputTimer = 0f;
            jumpQueued = false;
        }
    }

    void AplicarGravedad()
    {
        if (isHurt || isAttacking)
            return;

        // No aplicar gravedad extra si está en el suelo
        if (IsGrounded() && rb.linearVelocity.y <= 0)
            return;

        if (rb.linearVelocity.y < 0)
        {
            // Cayendo - sensación pesada
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0)
        {
            // Saltando - gravedad más suave para arco de salto
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void Atacar()
    {
        if (attackQueued)
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;

            attackTimer = 0f;
            attackDirection = new Vector2(facingDirection, 0).normalized;

            animator.SetTrigger("Attack");
            SoundManager.instance.playOnce(attackSFX);

            Invoke(nameof(ActivarHitbox), 0.35f);

            attackQueued = false;
        }
    }

    void ActivarHitbox()
    {
        if (!isAttacking) return;
        attackPoint.enabled = true;
        attackBehaviour.StartAttack();
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
        attackBehaviour.EndAttack();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public void SetHurtState(float duration, float hitSource)
    {
        isHurt = true;
        StartInvulnerability();
        Knockback(hitSource);

        // Detener movimiento
        Invoke(nameof(FreezeAfterKnockback), 0.05f);

        // Cancelar saltos pendientes
        jumpQueued = false;
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

        // Restaurar opacidad al final
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
        // Cancelar movimiento actual para evitar bugs con salto
        rb.linearVelocity = Vector2.zero;

        // Dirección de empuje según lado del impacto
        int direction = (transform.position.x < hitSourceX) ? -1 : 1;

        // Aplicar knockback
        rb.AddForce(new Vector2(direction * knockbackForceX, knockbackForceY), ForceMode2D.Impulse);

        // Evitar que ataque mientras es empujado
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
        jumpQueued = false;
        isJumping = false;
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

        Invoke(nameof(FinDeTomarCerveza), drinkTime);
    }

    void FinDeTomarCerveza()
    {
        isDrinking = false;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public bool IsGrounded()
    {
        if (playerCollider == null) return false;

        float halfWidth = playerCollider.bounds.extents.x;
        Vector2 basePos = (Vector2)playerCollider.bounds.center + Vector2.down * (playerCollider.bounds.extents.y + 0.01f);

        Vector2 leftRay = basePos + Vector2.left * (halfWidth - 0.05f);
        Vector2 centerRay = basePos;
        Vector2 rightRay = basePos + Vector2.right * (halfWidth - 0.05f);

        RaycastHit2D leftHit = Physics2D.Raycast(leftRay, Vector2.down, groundCheckDistance, groundLayer);
        RaycastHit2D centerHit = Physics2D.Raycast(centerRay, Vector2.down, groundCheckDistance, groundLayer);
        RaycastHit2D rightHit = Physics2D.Raycast(rightRay, Vector2.down, groundCheckDistance, groundLayer);

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