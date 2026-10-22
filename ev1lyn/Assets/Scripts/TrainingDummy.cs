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

    Rigidbody2D rb;
    Color baseColor;
    Vector3 home;

    int comboHits;
    float comboDamage;
    float lastHitTime = -999f;
    float flashUntil;
    string lastHit = "-";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        baseColor = sprite.color;
        home = transform.position;
    }

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

        Debug.Log($"[Dummy] {hit.attackName} for {hit.damage:0}  |  combo: {comboHits} hits, {comboDamage:0} dmg");
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