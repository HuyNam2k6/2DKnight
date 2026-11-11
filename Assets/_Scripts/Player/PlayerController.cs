using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("--- Di chuyển (Movement) ---")]
    [SerializeField] private float moveSpeed = 9f;
    [SerializeField] private float acceleration = 65f;
    [SerializeField] private float deceleration = 50f;

    [Header("--- Nhảy (Jumping) ---")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.5f; // Nhả nút Space sớm thì rơi nhanh hơn (Variable Jump)
    [SerializeField] private float fallGravityMultiplier = 2.5f; // Trọng lực khi rơi để cảm giác đầm tay
    [SerializeField] private int maxExtraJumps = 1; // Số lần nhảy thêm trên không (1 = Double Jump)
    [SerializeField] private float coyoteTime = 0.15f; // Thời gian cho phép nhảy sau khi vừa bước hụt mép vực
    [SerializeField] private float jumpBufferTime = 0.15f; // Nhận lệnh nhảy trước khi chạm đất

    [Header("--- Lướt (Dash) ---")]
    [SerializeField] private bool enableDash = true;
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.8f;

    [Header("--- Bám tường & Bật tường (Wall Slide & Jump) ---")]
    [SerializeField] private bool enableWallSlide = true;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(10f, 13f);
    [SerializeField] private float wallJumpDuration = 0.2f;

    [Header("--- Kiểm tra va chạm (Collision Checks) ---")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.1f, 0.8f);
    [SerializeField] private LayerMask wallLayer;

    // Component references
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;

    // Trạng thái (States)
    private float horizontalInput;
    private bool isFacingRight = true;
    private bool isGrounded;
    private bool isTouchingWall;
    private bool isWallSliding;
    private bool isWallJumping;
    private bool isDashing;
    private bool canDash = true;

    // Bộ đếm thời gian (Timers)
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private int remainingExtraJumps;
    private float originalGravity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        originalGravity = rb.gravityScale;
    }

    private void Update()
    {
        GatherInput();

        if (isDashing) return;

        CheckCollisions();
        HandleJumpBufferAndCoyote();
        HandleWallSlide();
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        if (!isWallJumping)
        {
            ApplyMovement();
        }

        ApplyBetterJumpPhysics();
    }

    #region Input Gathering
    private void GatherInput()
    {
        horizontalInput = 0f;

        // Hỗ trợ trực tiếp New Input System (Bàn phím + Gamepad)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontalInput -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontalInput += 1f;

            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                jumpBufferCounter = jumpBufferTime;
            }

            if (Keyboard.current.spaceKey.wasReleasedThisFrame || Keyboard.current.wKey.wasReleasedThisFrame || Keyboard.current.upArrowKey.wasReleasedThisFrame)
            {
                OnJumpReleased();
            }

            if (enableDash && (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.kKey.wasPressedThisFrame))
            {
                if (canDash && !isDashing)
                {
                    StartCoroutine(PerformDash());
                }
            }
        }

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.15f) horizontalInput = Mathf.Sign(stickX);

            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                jumpBufferCounter = jumpBufferTime;
            }

            if (Gamepad.current.buttonSouth.wasReleasedThisFrame)
            {
                OnJumpReleased();
            }

            if (enableDash && (Gamepad.current.rightTrigger.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame))
            {
                if (canDash && !isDashing)
                {
                    StartCoroutine(PerformDash());
                }
            }
        }

        // Xoay hướng mặt nhân vật (Flip Sprite)
        if (horizontalInput > 0.1f && !isFacingRight)
        {
            Flip();
        }
        else if (horizontalInput < -0.1f && isFacingRight)
        {
            Flip();
        }
    }
    #endregion

    #region Collision Checks
    private void CheckCollisions()
    {
        Vector3 groundPos = groundCheck != null ? groundCheck.position : transform.position - new Vector3(0, col.bounds.extents.y, 0);
        isGrounded = Physics2D.OverlapBox(groundPos, groundCheckSize, 0f, groundLayer);

        Vector3 wallPos = wallCheck != null ? wallCheck.position : transform.position + new Vector3((isFacingRight ? 1 : -1) * col.bounds.extents.x, 0, 0);
        isTouchingWall = Physics2D.OverlapBox(wallPos, wallCheckSize, 0f, wallLayer);

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            remainingExtraJumps = maxExtraJumps;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }
    #endregion

    #region Movement & Physics
    private void ApplyMovement()
    {
        float targetSpeed = horizontalInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        float movement = speedDiff * accelRate * Time.fixedDeltaTime;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
    }

    private void HandleJumpBufferAndCoyote()
    {
        jumpBufferCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0f)
        {
            // Nhảy khi đang ở trên mặt đất (hoặc trong khoảng coyote time)
            if (coyoteTimeCounter > 0f)
            {
                ExecuteJump();
                jumpBufferCounter = 0f;
            }
            // Bật nhảy từ tường (Wall Jump)
            else if (isWallSliding)
            {
                ExecuteWallJump();
                jumpBufferCounter = 0f;
            }
            // Nhảy đúp trên không (Double Jump)
            else if (remainingExtraJumps > 0)
            {
                ExecuteJump();
                remainingExtraJumps--;
                jumpBufferCounter = 0f;
            }
        }
    }

    private void ExecuteJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        coyoteTimeCounter = 0f;
    }

    private void OnJumpReleased()
    {
        // Khi nhả nút nhảy lúc đang bay lên, giảm vận tốc Y để rơi sớm (Variable Jump Height)
        if (rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }
    }

    private void ApplyBetterJumpPhysics()
    {
        if (isWallSliding) return;

        // Trọng lực rơi mạnh hơn khi rơi xuống
        if (rb.linearVelocity.y < 0f)
        {
            rb.gravityScale = originalGravity * fallGravityMultiplier;
        }
        else
        {
            rb.gravityScale = originalGravity;
        }
    }

    private void HandleWallSlide()
    {
        if (!enableWallSlide) return;

        if (isTouchingWall && !isGrounded && horizontalInput != 0f)
        {
            isWallSliding = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -wallSlideSpeed, float.MaxValue));
        }
        else
        {
            isWallSliding = false;
        }
    }

    private void ExecuteWallJump()
    {
        isWallJumping = true;
        float jumpDirection = isFacingRight ? -1f : 1f;

        rb.linearVelocity = new Vector2(wallJumpForce.x * jumpDirection, wallJumpForce.y);
        Flip();

        StartCoroutine(ResetWallJump());
    }

    private IEnumerator ResetWallJump()
    {
        yield return new WaitForSeconds(wallJumpDuration);
        isWallJumping = false;
    }

    private IEnumerator PerformDash()
    {
        canDash = false;
        isDashing = true;

        float originalGravityScale = rb.gravityScale;
        rb.gravityScale = 0f;

        float dashDirection = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravityScale;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;
    }
    #endregion

    #region Editor Gizmos
    private void OnDrawGizmosSelected()
    {
        // Vẽ ô kiểm tra mặt đất (Ground Check)
        Gizmos.color = Color.green;
        Vector3 groundPos = groundCheck != null ? groundCheck.position : transform.position - new Vector3(0, 0.5f, 0);
        Gizmos.DrawWireCube(groundPos, groundCheckSize);

        // Vẽ ô kiểm tra tường (Wall Check)
        Gizmos.color = Color.blue;
        Vector3 wallPos = wallCheck != null ? wallCheck.position : transform.position + new Vector3(0.5f, 0, 0);
        Gizmos.DrawWireCube(wallPos, wallCheckSize);
    }
    #endregion
}
