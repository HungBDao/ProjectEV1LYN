using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Punching bag: flashes, gets knocked back, and tracks hits/damage per combo.
/// Press T to teleport it back to its start position.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class TrainingDummy : MonoBehaviour, IDamageable
{
    [SerializeField] SpriteRenderer sprite;
    [SerializeField] Color flashColor = new(1f, 0.35f, 0.35f);
    [SerializeField] float flashTime = 0.1f;
    [Tooltip("No hits for this long = combo counter resets.")]
    [SerializeField] float comboResetTime = 1.2f;

    [Header("Weight")]
    [Tooltip("Normal gravity scale. 1 = real-world gravity, which reads as floaty at game scale.")]
    [SerializeField] float gravityScale = 5f;
    [SerializeField] float maxFallSpeed = 25f;
    [Tooltip("Units/s² - how fast horizontal knockback bleeds off.")]
    [SerializeField] float knockbackDecel = 30f;

    [Header("Juggle")]
    [Tooltip("After each hit, gravity is reduced for this long so the next hit can connect.")]
    [SerializeField] float hangTime = 0.12f;
    [SerializeField] float hangGravityScale = 0.5f;

    Rigidbody2D rb;
    Color baseColor;
    Vector3 home;

    int comboHits;
    float comboDamage;
    float lastHitTime = -999f;
    float flashUntil;
    float hangUntil;
    string lastHit = "-";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        baseColor = sprite.color;
        home = transform.position;
        rb.gravityScale = gravityScale;
    }

    /// <summary>
    /// Called by the attack system when this dummy is hit. Updates combo counters, knockback, and flash/hang timers.
    /// </summary>
    /// <param name="hit"> Information about the hit, including damage, knockback, and attack name.</param>
    public void TakeHit(in HitInfo hit)
    {
        if (Time.time - lastHitTime > comboResetTime)
        {
            comboHits = 0;
            comboDamage = 0f;
        }

        comboHits++;
        comboDamage += hit.damage;
        lastHitTime = Time.time;
        lastHit = hit.attackName;

        rb.linearVelocity = hit.knockback;
        flashUntil = Time.time + flashTime; // Time.time is frozen during hitstop, so the flash holds through it
        hangUntil = Time.time + hangTime;

        Debug.Log($"[Dummy] {hit.attackName} for {hit.damage:0}  |  combo: {comboHits} hits, {comboDamage:0} dmg");
    }

    private void FixedUpdate()
    {
        rb.gravityScale = Time.time < hangUntil ? hangGravityScale : gravityScale;

        var v = rb.linearVelocity;
        v.x = Mathf.MoveTowards(v.x, 0f, knockbackDecel * Time.fixedDeltaTime);
        v.y = Mathf.Max(v.y, -maxFallSpeed);
        rb.linearVelocity = v;
    }

    void Update()
    {
        sprite.color = Time.time < flashUntil ? flashColor : baseColor;

        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = home;
        }
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
        if (p.z < 0) return;

        bool live = Time.time - lastHitTime <= comboResetTime;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = live ? Color.yellow : Color.white;
        GUI.Label(new Rect(p.x - 150, Screen.height - p.y - 40, 300, 50),
            $"{comboHits} hits / {comboDamage:0} dmg\nlast: {lastHit}", style);
    }
}