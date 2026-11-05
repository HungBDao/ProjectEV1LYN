using UnityEngine;

public class HitStop : MonoBehaviour
{
    static HitStop instance;

    float freezeUntil; // unscaled time
    float slowUntil; // unscaled time
    float slowScale = 1f;
    float baseFixedDelta;

    static HitStop Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("[HitStop]").AddComponent<HitStop>();
            return instance;
        }
    }

    /// <summary>True while slow-mo is running (ignoring any hitstop on top of it).</summary>
    public static bool IsSlowMo => instance != null && Time.unscaledTime < instance.slowUntil;

    void Awake() => baseFixedDelta = Time.fixedDeltaTime;

    /// <summary>
    /// Freezes the game for `duration` real seconds. This is a "hitstop" effect, often used to emphasize hits or impacts.
    /// </summary>
    /// <param name="duration"> Duration in real seconds to freeze the game. Must be greater than 0.</param>
    public static void Do(float duration)
    {
        if (duration <= 0f) return;
        var i = Instance;
        i.freezeUntil = Mathf.Max(i.freezeUntil, Time.unscaledTime + duration);
        i.Apply();
    }

    /// <summary>
    /// Slows the game to `scale` for `duration` real seconds. 
    /// This is a "slow-motion" effect, often used to emphasize dodges or special moves.
    /// </summary>
    /// <param name="scale"> The time scale to slow the game to. Must be between 0.05 and 1.0.</param>
    /// <param name="duration"> Duration in real seconds to keep the game slowed. Must be greater than 0.</param>
    public static void SlowMo(float scale, float duration)
    {
        if (duration <= 0f) return;
        var i = Instance;
        i.slowScale = Mathf.Clamp(scale, 0.05f, 1f);
        i.slowUntil = Mathf.Max(i.slowUntil, Time.unscaledTime + duration);
        i.Apply();
    }

    void Update() => Apply(); // Update still runs at timeScale 0

    /// <summary>
    /// Applies the current hitstop or slow-mo effect by adjusting Time.timeScale and Time.fixedDeltaTime.
    /// </summary>
    void Apply()
    {
        float now = Time.unscaledTime;
        float scale = now < freezeUntil ? 0f
                    : now < slowUntil ? slowScale
                    : 1f;

        Time.timeScale = scale;

        // Physics steps every fixedDeltaTime of GAME time. In slow-mo that means fewer steps per
        // real second -> choppy motion. Shrinking the step with the scale keeps it smooth.
        Time.fixedDeltaTime = baseFixedDelta * (scale > 0f ? scale : 1f);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
    }
}