using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gives the TrainingDummy a looping attack 
/// A tiny state machine: Cooldown -> Startup (telegraph) -> Active (hitbox) -> Recovery -> Cooldown ...
/// Press Y to toggle attacking on/off.
/// </summary>
public class DummyAttacker : MonoBehaviour
{
    enum Phase { Cooldown, Startup, Active, Recovery }

    [SerializeField] AttackData attack;
    [Tooltip("The player. Used to face toward them and check range.")]
    [SerializeField] Transform target;
    [Tooltip("Layers this attack can hit - your Player layer.")]
    [SerializeField] LayerMask hitMask;

    [Header("Behaviour")]
    [Tooltip("Seconds of rest between attacks.")]
    [SerializeField] float cooldown = 1.5f;
    [Tooltip("Only start an attack when the player is this close (horizontally). 0 = always attack.")]
    [SerializeField] float attackRange = 4f;
    [SerializeField] bool attacking = true;

    Phase phase = Phase.Cooldown;
    float phaseStart;
    int facing = 1;

    readonly HashSet<IDamageable> hitThisSwing = new();
    readonly List<Collider2D> results = new();
    ContactFilter2D filter;

    void Awake()
    {
        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hitMask);
        phaseStart = Time.time;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
        {
            attacking = !attacking;
            Debug.Log($"[DummyAttacker] attacking = {attacking}");
        }
        if (attack == null) return;

        float t = Time.time - phaseStart;

        switch (phase)
        {
            case Phase.Cooldown:
                if (attacking && t >= cooldown && InRange()) Enter(Phase.Startup);
                break;

            case Phase.Startup:
                if (t >= attack.startup) Enter(Phase.Active);
                break;

            case Phase.Active:
                CheckHit();
                if (t >= attack.active) Enter(Phase.Recovery);
                break;

            case Phase.Recovery:
                if (t >= attack.recovery) Enter(Phase.Cooldown);
                break;
        }
    }

    /// <summary>
    /// Enter a new phase of the attack.
    /// </summary>
    /// <param name="next">The phase to enter.</param>
    void Enter(Phase next)
    {
        phase = next;
        phaseStart = Time.time;

        switch (next)
        {
            case Phase.Startup:
                // Commit to a direction when the wind-up starts. Turning mid-swing would feel unfair.
                if (target != null) facing = target.position.x >= transform.position.x ? 1 : -1;
                hitThisSwing.Clear();
                Debug.Log($"[DummyAttacker] winding up {attack.displayName}...");
                break;

            case Phase.Active:
                CheckHit(); // check on the first active frame too, in case the window is shorter than a frame
                break;
        }
    }

    /// <summary>
    /// Returns true if the target is within attackRange horizontally, or if attackRange is 0 (always attack).
    /// </summary>
    /// <returns> True if the target is in range, false otherwise.</returns>
    bool InRange() =>
        attackRange <= 0f || target == null ||
        Mathf.Abs(target.position.x - transform.position.x) <= attackRange;

    /// <summary>
    /// Returns the world position of the center of the hitbox, based on the dummy's position, facing, and the attack's hitbox offset.
    /// </summary>
    /// <returns> The world position of the center of the hitbox.</returns>
    Vector2 HitboxCenter() =>
        (Vector2)transform.position + new Vector2(attack.hitboxOffset.x * facing, attack.hitboxOffset.y);

    /// <summary>
    /// Checks for collisions with the hitbox and applies damage to any IDamageable objects hit.
    /// </summary>
    void CheckHit()
    {
        results.Clear();
        Physics2D.OverlapBox(HitboxCenter(), attack.hitboxSize, 0f, filter, results);

        foreach (var col in results)
        {
            var victim = col.GetComponentInParent<IDamageable>();
            // Added to the set BEFORE TakeHit: if the player dodges this swing, the swing is "spent"
            // on them and can't catch them a frame later when their i-frames run out. Generous on purpose.
            if (victim == null || !hitThisSwing.Add(victim)) continue;

            victim.TakeHit(new HitInfo
            {
                damage = attack.damage,
                knockback = new Vector2(attack.knockback.x * facing, attack.knockback.y),
                attackName = attack.displayName,
                source = gameObject
            });
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || attack == null || phase == Phase.Cooldown) return;

        Gizmos.color = phase switch
        {
            Phase.Startup => new Color(1f, 0.8f, 0f, 0.35f),
            Phase.Active => new Color(1f, 0f, 0f, 0.6f),
            _ => new Color(0.5f, 0.5f, 0.5f, 0.25f)
        };
        Gizmos.DrawCube(HitboxCenter(), attack.hitboxSize);
    }

    void OnGUI()
    {
        if (phase != Phase.Startup) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 p = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.4f);
        if (p.z < 0) return;

        // Telegraph: "!" that turns from yellow to red as the hit gets closer
        float progress = attack.startup > 0f ? (Time.time - phaseStart) / attack.startup : 1f;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 40,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.Lerp(Color.yellow, Color.red, progress);
        GUI.Label(new Rect(p.x - 50, Screen.height - p.y - 30, 100, 60), "!", style);
    }
}