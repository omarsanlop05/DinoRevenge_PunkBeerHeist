using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class PlayerController : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator; // Asigna esto en el Inspector

    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Gravedad personalizada")]
    public float fallMultiplier = 2.5f;

    [Header("Hitbox de ataque")]
    public CircleCollider2D attackPoint;

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

    public LayerMask groundLayer;
    public float groundCheckDistance = 0.2f;

    private Rigidbody2D rb;
    private int facingDirection = 1; // 1 = derecha, -1 = izquierda

    private BoxCollider2D playerCollider;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = rb.GetComponent<BoxCollider2D>();
        attackPoint.enabled = false;
    }

    void Update()
    {
        if (!isAttacking)
            moveInput = Input.GetAxisRaw("Horizontal");
        else
            moveInput = 0;

        // Actualizar dirección
        if (moveInput != 0)
        {
            facingDirection = (int)Mathf.Sign(moveInput);
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facingDirection;
            transform.localScale = scale;
            animator.SetBool("isRunning", true);

        }
        else
        {
            animator.SetBool("isRunning", false);
        }

        //Saltar
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpQueued = true;
         }

        //Atacar
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            attackQueued = true;
        }

        
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
        Debug.Log("Rotación Z: " + transform.rotation.eulerAngles.z);


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
        {
            FinAtaque();
        }
    }

    void FinAtaque()
    {
        isAttacking = false;
        attackPoint.enabled = false;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    bool IsGrounded()
    {
        if (playerCollider == null) return false;

        Vector2 rayStart = (Vector2)transform.position + Vector2.down * (playerCollider.bounds.extents.y + 0.01f);

        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 0.1f); 
        Debug.DrawRay(rayStart, Vector2.down * 0.1f, Color.red);

        return hit.collider != null;
    }


    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        if (playerCollider != null)
        {
            Vector2 rayStart = (Vector2)transform.position + Vector2.down * (playerCollider.bounds.extents.y - 0.05f);
            Gizmos.DrawLine(rayStart, rayStart + Vector2.down * groundCheckDistance);
        }
        else
        {
            // Fallback si playerCollider es null en el editor
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Vector2 rayStart = (Vector2)transform.position + Vector2.down * (col.bounds.extents.y - 0.05f);
                Gizmos.DrawLine(rayStart, rayStart + Vector2.down * groundCheckDistance);
            }
        }
    }
}