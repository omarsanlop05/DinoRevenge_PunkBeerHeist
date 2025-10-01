using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Gravedad personalizada")]
    public float fallMultiplier = 2.5f;

    [Header("Ataque")]
    public float attackForce = 10f;
    public float attackCooldown = 0.8f;
    private bool isAttacking = false;
    private float nextAttackTime = 0f;

    [Header("Ataque dinámico")]
    public float attackDuration = 0.2f;
    public AnimationCurve attackCurve; // Puedes definirla en el Inspector
    private float attackTimer = 0f;
    private Vector2 attackDirection;

    private float moveInput;
    private bool jumpQueued = false;
    private bool attackQueued = false;

    private Rigidbody2D rb;
    private int facingDirection = 1; // 1 = derecha, -1 = izquierda

    private CircleCollider2D attackPoint;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        attackPoint = rb.GetComponent<CircleCollider2D>();
        attackPoint.enabled = false;
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        // Actualizar dirección
        if (moveInput != 0)
        {
            facingDirection = (int)Mathf.Sign(moveInput);
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facingDirection;
            transform.localScale = scale;
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
        if (jumpQueued && IsGrounded())
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
        jumpQueued = false;
    }

    void AplicarGravedad()
    {
            // Caída rápida
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
  
    }

    /*void Atacar()
    {
        if (attackQueued)
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;

            Vector2 attackDir = new Vector2(facingDirection, 0);
            rb.AddForce(attackDir * attackForce, ForceMode2D.Impulse);

            Invoke(nameof(FinAtaque), 0.3f);
        }
        attackQueued = false;
    }*/

    void Atacar()
    {
        if (attackQueued)
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;

            attackTimer = 0f;
            attackDirection = new Vector2(facingDirection, 0).normalized;

            attackPoint.enabled = true;

            attackQueued = false;
        }
    }
    void MovimientoDeAtaque()
    {
        attackTimer += Time.fixedDeltaTime;

        float t = attackTimer / attackDuration;
        float force = attackCurve.Evaluate(t) * attackForce;

        rb.linearVelocity = new Vector2(attackDirection.x * force, rb.linearVelocity.y);

        if (attackTimer >= attackDuration)
        {
            FinAtaque();
        }
    }

    void FinAtaque()
    {
        isAttacking = false;
        attackPoint.enabled = false;
    }

    bool IsGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.1f);
        return hit.collider != null;
    }
}