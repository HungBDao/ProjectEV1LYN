using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ComboLogger : MonoBehaviour
{
    enum Request { None, Basic, EX, Hold }

    public enum HoldMode
    {
        DecideOnRelease, // tap = basic on release, hold = HOLD. Clean, but taps fire slightly later.
        FireOnPress      // basic fires instantly; keep holding to follow up with HOLD (BA1 -> HOLD).
    }

    [Header("Refs")]
    [SerializeField] PlayerController2D player;
    [Tooltip("Layers your hitboxes can hit (e.g. Enemy). Must NOT include the player's own layer.")]
    [SerializeField] LayerMask hitMask;

    [Header("Attacks")]
    [SerializeField] AttackData[] basics = new AttackData[3];
    [SerializeField] AttackData exAttack;
    [SerializeField] AttackData jumpAttack;
    [SerializeField] AttackData holdAttack;

    [Header("Hold attack")]
    [SerializeField] HoldMode holdMode = HoldMode.DecideOnRelease;
    [SerializeField] float holdThreshold = 0.3f;

    [Header("Combo feel (s)")]
    [Tooltip("After an action ends, how long the chain stays alive before resetting to BA1.")]
    [SerializeField] float comboWindow = 0.4f;
    [Tooltip("How long an early press (during an action) is remembered.")]
    [SerializeField] float bufferWindow = 0.2f;
    [SerializeField] bool exCancelsActions = true;

    [Header("Energy")]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float exCost = 50f;
    [SerializeField] float energy = 50f;

    // current action
    AttackData current;
    float actionStart;
    bool hitWindowChecked;
    readonly HashSet<IDamageable> hitThisAction = new();
    readonly List<Collider2D> overlapResults = new();
    ContactFilter2D filter;

    // chain
    float chainExpireTime;
    int nextBasic;
    bool airAttackUsed;
    readonly List<string> chain = new();
    string lastMessage = "";

    // input
    Request buffered = Request.None;
    float bufferedAt;
    float pressStart;
    bool pressHandled;
    bool holdConsumed;

    bool Acting => current != null;
    bool Grounded => player == null || player.IsGrounded;
    int Facing => player != null ? player.Facing : 1;
    void OnEnable() 
    { 
        if (player != null) player.Dashed += OnDashed; 
    }
    void OnDisable() 
    { 
        if (player != null) player.Dashed -= OnDashed; 
    }

    void Awake()
    {
        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hitMask);
    }

    void Update()
    {
        float now = Time.time;
        if (Grounded) airAttackUsed = false;

        // Finish the current action
        if (Acting && now - actionStart >= current.Duration)
        {
            current = null;
            chainExpireTime = now + comboWindow;
        }

        // Run a buffered input if it's still fresh
        if (!Acting && buffered != Request.None)
        {
            var r = buffered;
            buffered = Request.None;
            if (now - bufferedAt <= bufferWindow) Execute(r);
        }

        // Read new input
        ReadInput(now);

        // Hitbox
        if (Acting) UpdateHitWindow(now);

        if (player != null) player.Locked = Acting;

        // Chain timeout
        if (!Acting && chain.Count > 0 && now > chainExpireTime)
        {
            Debug.Log($"<b>Chain ended:</b> {string.Join(" -> ", chain)}");
            chain.Clear();
            nextBasic = 0;
        }
    }

    //---------------------------------------------
    // Input handling

    /// <summary>
    /// Reads mouse and keyboard input and submits requests to the combo system.
    /// </summary>
    /// <param name="now"> Current time in seconds (Time.time).</param>
    void ReadInput(float now)
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || kb == null) return;

        var attack = mouse.leftButton;

        if (attack.wasPressedThisFrame)
        {
            pressStart = now;
            pressHandled = false;
            holdConsumed = !Grounded; // no hold attack in the air

            if (!Grounded || holdMode == HoldMode.FireOnPress)
            {
                pressHandled = true;
                Submit(Request.Basic, now);
            }
        }

        if (attack.isPressed && !holdConsumed && Grounded && now - pressStart >= holdThreshold)
        {
            holdConsumed = true;
            pressHandled = true;
            Submit(Request.Hold, now);
        }

        if (attack.wasReleasedThisFrame && !pressHandled)
        {
            pressHandled = true;
            Submit(Request.Basic, now);
        }

        if (kb.eKey.wasPressedThisFrame) Submit(Request.EX, now);

        if (kb.rKey.wasPressedThisFrame)
        {
            energy = maxEnergy;
            Debug.Log("[debug] Energy refilled");
        }
    }

    /// <summary>
    /// Submits a request to the combo system. 
    /// If the player is currently acting, the request is buffered for later execution.
    /// </summary>
    /// <param name="r"> The request to submit (Basic, Ex, or Hold).</param>
    /// <param name="now"> Current time in seconds (Time.time).</param>
    void Submit(Request r, float now)
    {
        if (!Acting) { Execute(r); return; }

        if (r == Request.EX && exCancelsActions && current != exAttack && energy >= exCost)
        {
            Execute(r); // EX interrupts whatever is playing
            return;
        }

        buffered = r; // latest press wins
        bufferedAt = now;
    }

    /// <summary>
    /// Executes a request immediately, starting the corresponding attack and updating the combo chain.
    /// </summary>
    /// <param name="r"> The request to execute (Basic, Ex, or Hold).</param>
    void Execute(Request r)
    {
        switch (r)
        {
            case Request.EX:
                if (energy < exCost)
                {
                    lastMessage = $"EX failed - energy {energy:0}/{maxEnergy:0}";
                    Debug.Log(lastMessage);
                    return;
                }
                energy -= exCost;
                nextBasic = 0; // EX resets the string: BA1 -> EX -> BA1
                Begin(exAttack);
                break;

            case Request.Hold:
                nextBasic = 0;
                Begin(holdAttack);
                break;

            case Request.Basic:
                if (!Grounded)
                {
                    if (airAttackUsed) return; // one jump attack per airtime
                    airAttackUsed = true;
                    Begin(jumpAttack);
                    break;
                }
                Begin(basics[nextBasic]);
                nextBasic = (nextBasic + 1) % basics.Length;
                break;
        }
    }

    /// <summary>
    /// Begins an attack, setting the current action, starting the timer, and logging the combo chain.
    /// </summary>
    /// <param name="a"> The AttackData to begin.</param>
    void Begin(AttackData a)
    {
        if (a == null)
        {
            Debug.LogWarning("[ComboLogger] An AttackData slot is empty - assign it in the Inspector.");
            return;
        }
        current = a;
        actionStart = Time.time;
        hitWindowChecked = false;
        hitThisAction.Clear();

        chain.Add(a.displayName);
        lastMessage = string.Join(" -> ", chain);
        Debug.Log(lastMessage);
    }

    /// <summary>
    /// Called when the player dashes. 
    /// Adds "DASH" to the combo chain, cancels any current attack, and resets the buffered input.
    /// </summary>
    void OnDashed()
    {
        chain.Add("DASH");
        lastMessage = string.Join(" -> ", chain);
        Debug.Log(lastMessage);

        current = null; // cancel the attack mid-swing
        buffered = Request.None; // throw away queued presses
        chainExpireTime = Time.time + comboWindow;
    }

    // --------------------------------
    // Hitbox and damage

    /// <summary>
    /// Calculates the center of the hitbox for the given attack, taking into account the player's position and facing direction.
    /// </summary>
    /// <param name="a"> The AttackData for which to calculate the hitbox center.</param>
    /// <returns> The world position of the hitbox center as a Vector2.</returns>
    Vector2 HitboxCenter(AttackData a) =>
        (Vector2)transform.position + new Vector2(a.hitboxOffset.x * Facing, a.hitboxOffset.y);

    /// <summary>
    /// Checks if the current attack's hitbox is active and, if so, detects collisions with damageable targets. 
    /// Applies damage, knockback, and hit stop to any targets hit, and updates the player's energy accordingly.
    /// </summary>
    /// <param name="now"> Current time in seconds (Time.time).</param>
    void UpdateHitWindow(float now)
    {
        float t = now - actionStart;
        if (t < current.startup) return;

        bool inWindow = t < current.startup + current.active;
        // If the active window is shorter than a frame, still check once.
        if (!inWindow && hitWindowChecked) return;
        hitWindowChecked = true;

        overlapResults.Clear();
        Physics2D.OverlapBox(HitboxCenter(current), current.hitboxSize, 0f, filter, overlapResults);

        float stop = 0f;
        foreach (var col in overlapResults)
        {
            var target = col.GetComponentInParent<IDamageable>();
            if (target == null || !hitThisAction.Add(target)) continue; // each target once per attack

            target.TakeHit(new HitInfo
            {
                damage = current.damage,
                knockback = new Vector2(current.knockback.x * Facing, current.knockback.y),
                attackName = current.displayName,
                source = gameObject
            });

            energy = Mathf.Min(maxEnergy, energy + current.energyGain);
            stop = current.hitstop;
        }

        if (stop > 0f) HitStop.Do(stop);
    }

    /// <summary>
    /// Returns a string representing the current phase of the action: "startup", "ACTIVE", or "recovery".
    /// </summary>
    /// <returns> A string indicating the current phase of the action, or "-" if no action is active.</returns>
    string Phase()
    {
        if (!Acting) return "-";
        float t = Time.time - actionStart;
        if (t < current.startup) return "startup";
        if (t < current.startup + current.active) return "ACTIVE";
        return "recovery";
    }

    // --------------------------------
    // Debugging and visualization

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || current == null) return;
        float t = Time.time - actionStart;
        Gizmos.color = t < current.startup ? new Color(1f, 1f, 0f, 0.35f)
                        : t < current.startup + current.active ? new Color(1f, 0f, 0f, 0.6f)
                        : new Color(0.5f, 0.5f, 0.5f, 0.25f);
        Gizmos.DrawCube(HitboxCenter(current), current.hitboxSize);
    }

    void OnDrawGizmosSelected()
    {
        // Preview BA1's hitbox in edit mode so you can size it without pressing Play
        if (current != null || basics == null || basics.Length == 0 || basics[0] == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireCube(HitboxCenter(basics[0]), basics[0].hitboxSize);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        GUI.Label(new Rect(20, 20, 1200, 30), $"Chain: {string.Join(" -> ", chain)}", style);
        GUI.Label(new Rect(20, 50, 1200, 30), $"Doing: {(Acting ? current.displayName : "-")} [{Phase()}]   Grounded: {Grounded}", style);
        GUI.Label(new Rect(20, 80, 1200, 30), $"Energy: {energy:0}/{maxEnergy:0}   Hold mode: {holdMode}", style);
        GUI.Label(new Rect(20, 110, 1200, 30), lastMessage, style);
    }
}