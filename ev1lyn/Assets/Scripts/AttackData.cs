using UnityEngine;

/// <summary>
/// One attack's numbers.
/// </summary>
[CreateAssetMenu(menuName = "EV1LYN/Combat/Attack Data", fileName = "Atk_New")]
public class AttackData : ScriptableObject
{
    public string displayName = "BA1";

    [Header("Timing (seconds)")]
    [Tooltip("Wind-up before the hitbox appears.")]
    public float startup = 0.06f;
    [Tooltip("How long the hitbox is live.")]
    public float active = 0.08f;
    [Tooltip("Cool-down after the hitbox disappears.")]
    public float recovery = 0.15f;

    [Header("Hit")]
    public float damage = 10f;
    [Tooltip("Energy gained per target hit (ZZZ-style: earn EX by landing hits).")]
    public float energyGain = 4f;
    [Tooltip("Hit stop (freeze frames) on contact. 0.03-0.12 is the useful range.")]
    public float hitstop = 0.05f;
    [Tooltip("X is multiplied by facing direction automatically.")]
    public Vector2 knockback = new(2f, 0f);

    [Header("Hitbox (relative to player, facing right)")]
    public Vector2 hitboxOffset = new(0.8f, 0f);
    public Vector2 hitboxSize = new(1.2f, 1f);

    public float Duration => startup + active + recovery;
}