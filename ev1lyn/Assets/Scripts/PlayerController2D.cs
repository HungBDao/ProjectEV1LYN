using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

/// <summary>
/// 2D platformer movement controller. Handles running, jumping, double jumps, dashing, and coyote time.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Run")]
    [SerializeField] float runSpeed = 8f;
    [Tooltip("Units/s² toward run speed. Very high = near-instant start")]
    [SerializeField] float groundAccel = 150f;
    [SerializeField] float groundDecel = 200f;
    [SerializeField] float airAccel = 90f;
    [SerializeField] float airDecel = 60f;

    [Header("Jump (designed by height & time)")]
    [Tooltip("Apex height in world units. The character is 2 units tall.")]
    [SerializeField] float jumpHeight = 3f;
    [Tooltip("Seconds to reach the apex. Lower = snappier.")]
    [SerializeField] float timeToApex = 0.3f;
    [Tooltip("Gravity multiplier while falling. >1 = rise floaty-ish, fall fast (feels heavy, not floaty).")]
    [SerializeField] float fallGravityMultiplier = 1.7f;
    [Tooltip("Upward velocity is multiplied by this if the player release jump early. 0.5 = short hop.")]
    [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.5f;
    [SerializeField] float maxFallSpeed = 24f;
    [SerializeField] int airJumps = 1;
    [Tooltip("Grace period to still jump after walking off a ledge.")]
    [SerializeField] float coyoteTime = 0.08f;
    [Tooltip("Pressing jump slightly before landing still counts.")]
    [SerializeField] float jumpBufferTime = 0.1f;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 22f;
    [SerializeField] float dashDuration = 0.15f;
    [SerializeField] float dashCooldown = 0.3f;
    [SerializeField] int airDashes = 1;
    [SerializeField] float iFrameDuration = 0.15f;

    [Header("Ground check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.1f;
    [SerializeField] LayerMask groundMask;

    [Header("Visual (optional)")]
    [Tooltip("Child holding the sprite. Gets flipped to face left/right.")]
    [SerializeField] Transform visual;

    public bool IsGrounded { get; private set; }
    public int Facing { get; private set; } = 1; // 1 = right, -1 = left
    public bool Locked { get; set; } // set by combat
    public bool IsDashing => Time.time < dashEndTime;
    public bool IsInvulnerable => Time.time < iFrameEnd; // enemies will check this later

    /// <summary>Fired the moment a dash starts. Combat listens to cancel the current attack.</summary>
    public event Action Dashed;

    // The two formulas this whole script is built on (from h = v*t - g*t²/2 at the apex):
    float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
    float JumpVelocity => 2f * jumpHeight / timeToApex;

    Rigidbody2D rb;
    float moveInput;
    bool jumpHeld;

    float lastGroundedTime = -99f;
    float lastJumpPressedTime = -99f;
    bool jumpCutAvailable;
    int airJumpsLeft;
    int airDashesLeft;

    float dashEndTime = -99f;
    float nextDashTime;
    float iFrameEnd = -99f;
    int dashDir = 1;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        moveInput = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        jumpHeld = kb.spaceKey.isPressed;

        if (kb.spaceKey.wasPressedThisFrame) lastJumpPressedTime = Time.time;
        if (kb.leftShiftKey.wasPressedThisFrame) TryDash();

        if (!Locked && !IsDashing && moveInput != 0f) Facing = moveInput > 0f ? 1 : -1;

        if (visual != null)
        {
            var s = visual.localScale;
            visual.localScale = new Vector3(Mathf.Abs(s.x) * Facing, s.y, s.z);
        }
    }

    /// <summary>
    /// Attempts to dash in the current direction.
    /// If grounded, can dash once. If airborne, can dash a limited number of times.
    /// </summary>
    void TryDash()
    {
        if (Time.time < nextDashTime) return;
        if (!IsGrounded)
        {
            if (airDashesLeft <= 0) return;
            airDashesLeft--;
        }
        dashDir = moveInput != 0f ? (moveInput > 0f ? 1 : -1) : Facing;
        Facing = dashDir;
        dashEndTime = Time.time + dashDuration;
        nextDashTime = dashEndTime + dashCooldown;
        iFrameEnd = Time.time + iFrameDuration;
        Dashed?.Invoke();
    }

    void FixedUpdate()
    {
        float now = Time.time;
        float dt = Time.fixedDeltaTime;

        // Only count as grounded when not moving up, otherwise the first frames of a jump
        // still touch the ground and would refund double jump.
        bool touching = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
        IsGrounded = touching && rb.linearVelocity.y <= 0.01f;
        if (IsGrounded)
        {
            lastGroundedTime = now;
            airJumpsLeft = airJumps;
            airDashesLeft = airDashes;
        }

        // Dashing overrides all other movement. It is not affected by gravity or friction.
        if (IsDashing)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            jumpCutAvailable = false;
            return;
        }

        // Horizontal: accelerate toward a target speed instead of setting it instantly
        float vx = rb.linearVelocity.x;
        if (Locked)
        {
            if (IsGrounded) vx = Mathf.MoveTowards(vx, 0f, groundDecel * dt);
        }
        else
        {
            float target = moveInput * runSpeed;
            bool pushing = Mathf.Abs(target) > 0.01f;
            float rate = IsGrounded ? (pushing ? groundAccel : groundDecel)
                                    : (pushing ? airAccel : airDecel);
            vx = Mathf.MoveTowards(vx, target, rate * dt);
        }

        // Jumping: buffer + coyote time + air jumps
        float vy = rb.linearVelocity.y;
        bool jumpBuffered = now - lastJumpPressedTime <= jumpBufferTime;
        bool canCoyote = now - lastGroundedTime <= coyoteTime;

        if (jumpBuffered && !Locked)
        {
            if (canCoyote)
            {
                vy = JumpVelocity;
                lastGroundedTime = -99f; // spend the coyote window
                lastJumpPressedTime = -99f; // spend the buffered press
                jumpCutAvailable = true;
            }
            else if (airJumpsLeft > 0)
            {
                airJumpsLeft--;
                vy = JumpVelocity;
                lastJumpPressedTime = -99f;
                jumpCutAvailable = true;
            }
        }

        // Variable jump height: let go early = short hop
        if (jumpCutAvailable && !jumpHeld && vy > 0f)
        {
            vy *= jumpCutMultiplier;
            jumpCutAvailable = false;
        }
        if (vy <= 0f) jumpCutAvailable = false;

        // Gravity: fall faster than rise
        float g = Gravity * (vy < 0f ? fallGravityMultiplier : 1f);
        rb.gravityScale = g / Mathf.Abs(Physics2D.gravity.y);

        vy = Mathf.Max(vy, -maxFallSpeed);
        rb.linearVelocity = new Vector2(vx, vy);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}