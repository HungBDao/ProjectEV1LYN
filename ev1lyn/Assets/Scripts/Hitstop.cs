using UnityEngine;

/// <summary>
/// Global freeze-frame.
/// </summary>
public class HitStop : MonoBehaviour
{
    static HitStop instance;
    float until;

    /// <summary>
    /// Freezes the game for a given duration in seconds.
    /// </summary>
    /// <param name="duration"> Duration in seconds. Must be greater than 0.</param>
    public static void Do(float duration)
    {
        if (duration <= 0f) return;
        if (instance == null)
            instance = new GameObject("[HitStop]").AddComponent<HitStop>();
        instance.until = Mathf.Max(instance.until, Time.unscaledTime + duration);
        Time.timeScale = 0f;
    }

    void Update() // Update still runs at timeScale 0
    {
        if (Time.timeScale == 0f && Time.unscaledTime >= until)
            Time.timeScale = 1f;
    }

    void OnDestroy() => Time.timeScale = 1f;
}