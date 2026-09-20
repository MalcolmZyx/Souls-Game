using UnityEngine;

/// <summary>
/// Handles hit reaction logic for the Bird Boss, including a cooldown-based poise system.
/// This component ensures that flinch animations (on an additive/override layer) do not 
/// fire every single frame when hit by rapid-fire attacks.
/// </summary>
public class BossDamageHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The BossDamageable component to listen to. If null, will try to find on same object.")]
    [SerializeField] private BossDamageable damageable;

    [Tooltip("The Animator component. Ensure the flinch animation is on a separate Layer.")]
    [SerializeField] private Animator animator;

    [Header("Hit Reaction Settings")]
    [Tooltip("The Animator Trigger name used for the hit reaction.")]
    [SerializeField] private string hitTriggerName = "TakeDamage";

    [Tooltip("Cooldown in seconds between flinch animations.")]
    [SerializeField] private float flinchCooldown = 1.5f;

    // Internal state tracking
    private float lastFlinchTime = -Mathf.Infinity;

    private void Awake()
    {
        // Find BossDamageable if not assigned
        if (damageable == null)
            damageable = GetComponent<BossDamageable>();

        // Find Animator if not assigned
        if (animator == null)
            animator = GetComponent<Animator>();
            
        if (animator == null || damageable == null)
        {
            Debug.LogError($"[BossDamageHandler] Missing references on {gameObject.name}. " +
                           $"Animator: {animator}, Damageable: {damageable}");
        }
    }

    private void OnEnable()
    {
        // Subscribe to the damage event from BossDamageable
        if (damageable != null)
        {
            damageable.OnDamageTaken += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe to prevent memory leaks or errors when disabled/destroyed
        if (damageable != null)
        {
            damageable.OnDamageTaken -= HandleDamageTaken;
        }
    }

    /// <summary>
    /// Event handler that bridges the BossDamageable event to our flinch logic.
    /// </summary>
    private void HandleDamageTaken(int damage, float hitStun)
    {
        TakeDamage(damage, hitStun);
    }

    /// <summary>
    /// Processes damage and evaluates if the boss should flinch.
    /// </summary>
    public void TakeDamage(int damageAmount, float hitStunForce)
    {
        if (Time.time >= lastFlinchTime + flinchCooldown)
        {
            ExecuteHitReaction();
        }
    }

    private void ExecuteHitReaction()
    {
        if (animator != null)
        {
            // Calculate a multiplier to offset the global animator.speed.
            // If the global speed is 0.5f, the multiplier will be 2.0f,
            // making the local animation play at 1.0f speed.
            float globalSpeed = animator.speed;
            float staggerMultiplier = 1.0f;

            // Safeguard against divide-by-zero if the animator is paused or set to 0.
            if (globalSpeed > 0.001f)
            {
                staggerMultiplier = 1.0f / globalSpeed;
            }

            // Set the "StaggerSpeed" parameter in the Animator.
            // Note: The Animation State in the Animator Controller must be set to 
            // use this Multiplier parameter.
            animator.SetFloat("StaggerSpeed", staggerMultiplier);

            animator.ResetTrigger(hitTriggerName);
            animator.SetTrigger(hitTriggerName);
            
            lastFlinchTime = Time.time;

            Debug.Log($"[BossDamageHandler] Flinch triggered! Global Speed: {globalSpeed}, " +
                      $"Stagger Multiplier: {staggerMultiplier}");
        }
    }
}
