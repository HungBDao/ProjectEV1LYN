using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bare-minimum movement for the combat playground: A/D to move, Space to jump.
/// Combat sets Locked while attacking, which roots you on the ground and freezes facing.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float jumpVelocity = 12f;

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

    Rigidbody2D rb;
    float moveInput;
    bool jumpQueued;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        moveInput = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);

        if (!Locked && moveInput != 0f) Facing = moveInput > 0f ? 1 : -1;
        if (!Locked && kb.spaceKey.wasPressedThisFrame && IsGrounded) jumpQueued = true;

        if (visual != null)
        {
            var s = visual.localScale;
            visual.localScale = new Vector3(Mathf.Abs(s.x) * Facing, s.y, s.z);
        }
    }

    void FixedUpdate()
    {
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        float x = Locked
            ? (IsGrounded ? 0f : rb.linearVelocity.x) // rooted on ground, keep momentum in air
            : moveInput * moveSpeed;
        rb.linearVelocity = new Vector2(x, rb.linearVelocity.y);

        if (jumpQueued)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
            jumpQueued = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}