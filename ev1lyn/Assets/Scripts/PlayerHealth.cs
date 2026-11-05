using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lets the player get hit.
/// If the hit lands during dash i-frames, it's a dodge instead of damage.
/// Press H to refill HP.
/// </summary>
[RequireComponent(typeof(PlayerController2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] float maxHP = 100f;
    [Tooltip("How long you lose control after being hit.")]
    [SerializeField] float hitstunTime = 0.35f;
    [Tooltip("Invulnerable for this long after taking a hit, so overlapping attacks can't chain-hit you.")]
    [SerializeField] float mercyTime = 0.5f;
    [Tooltip("Hitstop when YOU get hit. A bit longer than your own attacks so getting hit reads clearly.")]
    [SerializeField] float hitstopOnHurt = 0.08f;

    [Header("Feedback")]
    [SerializeField] SpriteRenderer sprite;
    [SerializeField] Color hurtColor = new(1f, 0.2f, 0.2f);
    [SerializeField] float flashTime = 0.15f;

    public float HP { get; private set; }

    /// <summary>Fired when damage actually lands. Combat should cancel the current attack.</summary>
    public event Action<HitInfo> Hurt;
    /// <summary>Fired when an attack was avoided with i-frames. Hook dodge rewards here later.</summary>
    public event Action Dodged;

    PlayerController2D controller;
    Color baseColor;
    float mercyEnd = -99f;
    float flashUntil = -99f;
    float messageUntil = -99f;
    string message = "";

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) baseColor = sprite.color;
        HP = maxHP;
    }

    /// <summary>
    /// Called by an attack when it hits the player. If the player is invulnerable, this is a dodge instead of damage.
    /// </summary>
    /// <param name="hit"> Information about the hit.</param>
    public void TakeHit(in HitInfo hit)
    {
        if (controller.IsInvulnerable)
        {
            Show("DODGED!");
            Debug.Log($"[Player] Dodged {hit.attackName} with i-frames");
            Dodged?.Invoke();
            return;
        }

        if (Time.time < mercyEnd) return;

        HP = Mathf.Max(0f, HP - hit.damage);
        mercyEnd = Time.time + mercyTime;
        flashUntil = Time.time + flashTime;

        controller.Stun(hitstunTime, hit.knockback);
        HitStop.Do(hitstopOnHurt);

        Show($"-{hit.damage:0}");
        Debug.Log($"[Player] Hit by {hit.attackName} for {hit.damage:0}  |  HP {HP:0}/{maxHP:0}");
        Hurt?.Invoke(hit);

        if (HP <= 0f) Debug.Log("[Player] KO! (press H to refill)");
    }

    /// <summary>
    /// Show a temporary message above the player, like damage taken or "DODGED!".
    /// </summary>
    /// <param name="text"> The text to show.</param>
    void Show(string text)
    {
        message = text;
        messageUntil = Time.time + 0.6f;
    }

    void Update()
    {
        if (sprite != null) sprite.color = Time.time < flashUntil ? hurtColor : baseColor;

        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            HP = maxHP;
            Debug.Log("[debug] HP refilled");
        }
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        GUI.Label(new Rect(20, 140, 600, 30), $"HP: {HP:0}/{maxHP:0}", style);

        if (Time.time >= messageUntil) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.6f);
        if (p.z < 0) return;

        var pop = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        pop.normal.textColor = message == "DODGED!" ? Color.cyan : Color.red;
        GUI.Label(new Rect(p.x - 100, Screen.height - p.y - 20, 200, 40), message, pop);
    }
}