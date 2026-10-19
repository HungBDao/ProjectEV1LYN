using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpVelocity = 14f;

    [Header("Ground check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.1f;
    [SerializeField] LayerMask groundMask;

    public bool IsGrounded { get; private set; }

    Rigidbody2D rb;
    float moveInput;
    bool jumpQueued;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        moveInput = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        if (kb.spaceKey.wasPressedThisFrame && IsGrounded) jumpQueued = true;
    }

    void FixedUpdate()
    {
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

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
