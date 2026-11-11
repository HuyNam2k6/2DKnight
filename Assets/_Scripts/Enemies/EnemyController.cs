using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    [Header("--- Chỉ số cơ bản (Stats) ---")]
    [SerializeField] private int maxHealth = 30;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("--- Tầm phát hiện & Tấn công ---")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float attackRange = 1.2f;

    [Header("--- Tuần tra & Kiểm tra môi trường ---")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float checkDistance = 0.5f;

    // Component References
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private Animator anim;
    private Transform player;

    // Trạng thái AI
    private int currentHealth;
    private bool isFacingRight = true;
    private bool isDead = false;
    private bool isAttacking = false;
    private float lastAttackTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        
        // Tự động tìm Player
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (isDead || isAttacking) return;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        float distanceToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : 999f;

        if (distanceToPlayer <= attackRange)
        {
            // Trong tầm đánh -> Tấn công
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(PerformAttack());
            }
            else
            {
                StopMoving();
            }
        }
        else if (distanceToPlayer <= detectionRange)
        {
            // Trong tầm nhìn -> Đuổi theo Player
            ChasePlayer();
        }
        else
        {
            // Ngoài tầm nhìn -> Tuần tra (Patrol)
            Patrol();
        }

        UpdateAnimation();
    }

    #region AI Behaviors
    private void Patrol()
    {
        // Kiểm tra bờ vực phía trước
        Vector2 checkPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position + new Vector2(isFacingRight ? 0.6f : -0.6f, -0.6f);
        RaycastHit2D groundHit = Physics2D.Raycast(checkPos, Vector2.down, checkDistance, groundLayer);

        // Kiểm tra chướng ngại vật / tường phía trước
        Vector2 wallPos = wallCheck != null ? (Vector2)wallCheck.position : (Vector2)transform.position;
        RaycastHit2D wallHit = Physics2D.Raycast(wallPos, isFacingRight ? Vector2.right : Vector2.left, checkDistance, groundLayer);

        // Nếu gặp vực hoặc đụng tường -> Quay đầu
        if (!groundHit.collider || wallHit.collider)
        {
            Flip();
        }

        float direction = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
    }

    private void ChasePlayer()
    {
        if (player == null) return;

        float directionToPlayer = player.position.x - transform.position.x;

        // Quay mặt về phía Player
        if (directionToPlayer > 0.2f && !isFacingRight) Flip();
        else if (directionToPlayer < -0.2f && isFacingRight) Flip();

        float moveDir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(moveDir * moveSpeed * 1.3f, rb.linearVelocity.y); // Đuổi nhanh hơn tuần tra 30%
    }

    private void StopMoving()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        StopMoving();

        if (anim != null) anim.SetTrigger("Attack");

        // Chờ thời điểm ra đòn (vung kiếm)
        yield return new WaitForSeconds(0.35f);

        // Gây sát thương nếu Player vẫn còn trong tầm đánh
        if (player != null && Vector2.Distance(transform.position, player.position) <= attackRange + 0.3f)
        {
            Debug.Log($"{gameObject.name} đánh trúng Player gây {attackDamage} sát thương!");
            // Gọi hàm nhận sát thương của Player (nếu có script PlayerHealth)
            player.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }

        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }
    #endregion

    #region Take Damage & Death
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} trúng đòn! Máu còn: {currentHealth}/{maxHealth}");

        // Hiệu ứng giật lùi (Knockback)
        float knockDir = isFacingRight ? -1f : 1f;
        rb.linearVelocity = new Vector2(knockDir * 4f, 3f);

        StartCoroutine(FlashRed());

        if (anim != null) anim.SetTrigger("Hurt");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashRed()
    {
        if (sr != null)
        {
            sr.color = Color.red;
            yield return new WaitForSeconds(0.12f);
            sr.color = Color.white;
        }
    }

    private void Die()
    {
        isDead = true;
        StopMoving();
        col.enabled = false; // Tắt va chạm để Player đi xuyên qua
        rb.gravityScale = 0f; // Đứng yên khi chết

        if (anim != null) anim.SetTrigger("Die");

        Debug.Log($"{gameObject.name} đã bị tiêu diệt!");
        Destroy(gameObject, 1.5f); // Tự hủy sau khi chạy xong animation chết
    }
    #endregion

    #region Helpers
    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    private void UpdateAnimation()
    {
        if (anim == null) return;
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        anim.SetBool("isMoving", isMoving);
    }

    private void OnDrawGizmosSelected()
    {
        // Tầm phát hiện
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Tầm tấn công
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Raycast kiểm tra vực & tường
        Gizmos.color = Color.cyan;
        Vector2 checkPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position + new Vector2(isFacingRight ? 0.6f : -0.6f, -0.6f);
        Gizmos.DrawLine(checkPos, checkPos + Vector2.down * checkDistance);

        Vector2 wallPos = wallCheck != null ? (Vector2)wallCheck.position : (Vector2)transform.position;
        Gizmos.DrawLine(wallPos, wallPos + (isFacingRight ? Vector2.right : Vector2.left) * checkDistance);
    }
    #endregion
}
