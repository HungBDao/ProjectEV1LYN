using UnityEngine;

/// <summary>
/// Pays out when dodge with i-frames: a burst of slow-mo and an energy refund.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class DodgeRewards : MonoBehaviour
{
    [SerializeField] ComboLogger combo;

    [Header("Slow-mo")]
    [SerializeField] bool slowMo = true;
    [SerializeField, Range(0.05f, 1f)] float slowScale = 0.25f;
    [Tooltip("Real-time seconds.")]
    [SerializeField] float slowDuration = 0.5f;

    [Header("Energy")]
    [SerializeField] float energyRefund = 15f;

    [Header("Anti-farming")]
    [Tooltip("Real seconds before rewards can trigger again, so one multi-hit attack can't pay out 5 times.")]
    [SerializeField] float rewardCooldown = 1f;

    PlayerHealth health;
    float nextRewardTime;

    void Awake() => health = GetComponent<PlayerHealth>();
    void OnEnable() => health.Dodged += OnDodged;
    void OnDisable() => health.Dodged -= OnDodged;

    void OnDodged()
    {
        if (Time.unscaledTime < nextRewardTime) return;
        nextRewardTime = Time.unscaledTime + rewardCooldown;

        if (slowMo) HitStop.SlowMo(slowScale, slowDuration);
        if (combo != null) combo.AddEnergy(energyRefund);

        Debug.Log($"[DodgeRewards] Slow-mo + {energyRefund:0} energy");
    }
}