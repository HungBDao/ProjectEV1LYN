using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ComboLogger : MonoBehaviour
{
    enum Request
    {
        None,
        Basic,
        Hold,
        EX
    }

    enum Action
    {
        None,
        BA1,
        BA2,
        BA3,
        EX,
        JumpAttack,
        Hold
    }

    public enum HoldMode
    {
        DecideOnRelease, // tap = basic on release, hold = HOLD. Clean, but taps fire slightly later.
        FireOnPress // basic fires instantly; keep holding to follow up with HOLD (BA1 -> HOLD).
    }

    [Header("Refs")]
    [SerializeField] PlayerController2D player;

    [Header("Hold attack")]
    [SerializeField] HoldMode holdMode = HoldMode.DecideOnRelease;
    [SerializeField] float holdThreshold = 0.3f;

    [Header("Action durations (s) - stand-ins for animation length")]
    [SerializeField] float[] basicDurations = { 0.25f, 0.3f, 0.45f };
    [SerializeField] float exDuration = 0.6f;
    [SerializeField] float jumpAttackDuration = 0.35f;
    [SerializeField] float holdDuration = 0.6f;

    [Header("Combo feel (s)")]
    [Tooltip("After an action ends, how long the chain stays alive before resetting to BA1.")]
    [SerializeField] float comboWindow = 0.4f;
    [Tooltip("How long an early press (during an action) is remembered.")]
    [SerializeField] float bufferWindow = 0.2f;
    [SerializeField] bool exCancelsActions = true;

    [Header("Energy")]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float exCost = 50f;
    [SerializeField] float energy = 100f;
    [Tooltip("Fake gain per basic until real hits exist.")]
    [SerializeField] float debugEnergyPerBasic = 10f;

    // runtime state
    Action current = Action.None;
    float actionEndTime;
    float chainExpireTime;
    int nextBasic; // 0 = BA1, 1 = BA2, 2 = BA3
    bool airAttackUsed;

    Request buffered = Request.None;
    float bufferedAt;

    float pressStart;
    bool pressHandled; // basic already resolved for this press
    bool holdConsumed; // hold already fired (or not allowed) for this press

    readonly List<string> chain = new();
    string lastMessage = "";

    bool Acting => current != Action.None;
    bool Grounded => player == null || player.IsGrounded;

    // Update is called once per frame
    void Update()
    {
        float now = Time.time;
        if (Grounded) airAttackUsed = false;

        // Finish current action if its duration has passed
        if (Acting && now >= actionEndTime)
        {
            current = Action.None;
            chainExpireTime = now + comboWindow;
        }

        // Run a buffered input if it is still fresh
        if (!Acting && buffered != Request.None)
        {
            var r = buffered;
            buffered = Request.None;
            if (now - bufferedAt <= bufferWindow) Execute(r);
        }

        // Read new input from the mouse and keyboard
        ReadInput(now);

        // Chain timeout : if the player is not acting and the chain has expired, reset to BA1
        if (!Acting && chain.Count > 0 && now > chainExpireTime)
        {
            Debug.Log($"<b>Chain ended:</b> {string.Join(" -> ", chain)}");
            chain.Clear();
            nextBasic = 0;
        }
    }

    /// <summary>
    /// Reads input from the mouse and keyboard, and submits requests to perform actions based on the current state and timing.
    /// </summary>
    /// <param name="now"> The current time </param>
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
            holdConsumed = !Grounded; // can't hold in air

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
    /// Submits a request to perform an action. 
    /// If the player is currently acting, the request may be buffered for later execution.
    /// </summary>
    /// <param name="r"> The requested action </param>
    /// <param name="now"> The current time </param>
    void Submit(Request r, float now)
    {
        if (!Acting) { 
            Execute(r); 
            return; 
        }

        if (r == Request.EX && exCancelsActions && current != Action.EX && energy >= exCost)
        {
            Execute(r); // EX interrupts whatever is playing
            return;
        }

        buffered = r; // latest press wins
        bufferedAt = now;
    }

    /// <summary>
    /// Executes a request immediately, assuming the player is not currently acting. 
    /// Updates the current action and schedules its end.
    /// </summary>
    /// <param name="r"> The requested action </param>
    void Execute(Request r)
    {
        switch(r)
        {
            case Request.EX:
                if (energy < exCost)
                {
                    lastMessage = $"EX failed - energy {energy:0}/{maxEnergy:0}";
                    Debug.Log(lastMessage);
                    return;
                }
                energy -= exCost;
                nextBasic = 0; // EX resets the basic string: BA1 -> EX -> BA1
                Begin(Action.EX, exDuration);
                break;
            
            case Request.Hold:
                nextBasic = 0;
                Begin(Action.Hold, holdDuration);
                break;

            case Request.Basic:
                if (!Grounded)
                {
                    if (airAttackUsed) return; // one jump attack per airtime
                    airAttackUsed = true;
                    Begin(Action.JumpAttack, jumpAttackDuration);
                    break;
                }
                var a = (Action)((int)Action.BA1 + nextBasic);
                Begin(a, basicDurations[nextBasic]);
                nextBasic = (nextBasic + 1) % 3;
                energy = Mathf.Min(maxEnergy, energy + debugEnergyPerBasic);
                break;

        }
    }

    /// <summary>
    /// Begins an action, setting the current state and scheduling its end.
    /// </summary>
    /// <param name="a"> The action </param>
    /// <param name="duration"> The duration of the action </param>
    void Begin(Action a, float duration)
    {
        current = a;
        actionEndTime = Time.time + duration;
        chain.Add(a == Action.JumpAttack ? "JUMP_ATK" : a.ToString().ToUpper());
        lastMessage = string.Join(" -> ", chain);
        Debug.Log(lastMessage);
    }

    /// <summary>
    /// Quick on screen display of the current state for debugging.
    /// </summary>
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        GUI.Label(new Rect(20, 20, 1200, 30), $"Chain: {string.Join(" -> ", chain)}", style);
        GUI.Label(new Rect(20, 50, 1200, 30), $"Doing: {current}   Grounded: {Grounded}", style);
        GUI.Label(new Rect(20, 80, 1200, 30), $"Energy: {energy:0}/{maxEnergy:0}   Hold mode: {holdMode}", style);
        GUI.Label(new Rect(20, 110, 1200, 30), lastMessage, style);
    }
}
