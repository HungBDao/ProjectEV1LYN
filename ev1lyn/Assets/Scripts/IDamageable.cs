using UnityEngine;

/// <summary>
/// Information about a hit, including damage, knockback, and source.
/// </summary>
public struct HitInfo
{
    public float damage;
    public Vector2 knockback;
    public string attackName;
    public GameObject source;
}

/// <summary>
/// Interface for objects that can take damage and knockback from attacks.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Applies damage and knockback to this object. Called by the attack system when a hit is detected.
    /// </summary>
    /// <param name="hitInfo"> Information about the hit, including damage, knockback, and source.</param>
    void TakeHit(in HitInfo hitInfo);
}
