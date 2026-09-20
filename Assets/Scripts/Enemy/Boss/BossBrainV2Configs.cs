using UnityEngine;

// ============================================================
// STATE-SPECIFIC CONFIGS (Data Classes)
// ============================================================

[System.Serializable]
public class IdleConfig
{
    [Header("Global Idle Defaults")]
    public float defaultIdleMin = 1.0f;
    public float defaultIdleMax = 2.5f;

    [Header("Initial Startup")]
    public float initialIdleDuration = 10.0f;
}

[System.Serializable]
public class WingAttackConfig
{
    [Header("Timing")]
    public float windupTime = 1.5f;
    public float fastWindupTime = 0.5f;
    public float activeTime = 0.5f;
    public float recoveryIdleTime = 1.5f;
    [Tooltip("Speed multiplier for windup animation. 1 = normal, 2 = double speed.")]
    public float windupAnimSpeed = 1.0f;

    [Header("Cooldown")]
    public float cooldownDuration = 3.0f;
}

[System.Serializable]
public class DashAttackConfig
{
    [Header("Timing")]
    public float windupTime = 1.0f;
    [Tooltip("How long the dash lasts. Ignored if Dash Distance > 0.")]
    public float dashDuration = 0.6f;
    public float recoveryIdleTime = 1.5f;
    [Tooltip("Speed multiplier for windup animation. 1 = normal, 2 = double speed, etc.")]
    public float windupAnimSpeed = 1.0f;

    [Header("Movement")]
    public float dashSpeed = 25f;
    [Tooltip("If > 0, boss dashes exactly this far (overrides dashDuration). Duration = distance / speed.")]
    public float dashDistance = 0f;

    [Header("Combat")]
    public float damage = 20f;
    public float knockbackForce = 80f;
    public float knockbackStunDuration = 1.0f;

    [Header("Charge VFX")]
    public GameObject chargeVFXPrefab;
    public Vector3 chargeVFXOffset = Vector3.zero;
    public Vector3 chargeVFXScale = Vector3.one;

    [Header("Dash VFX")]
    public GameObject dashVFXPrefab;
    public Vector3 dashVFXOffset = Vector3.zero;
    public Vector3 dashVFXScale = Vector3.one;

    [Header("Cooldown")]
    public float cooldownDuration = 5.0f;
}

[System.Serializable]
public class WavePushBackConfig
{
    [Header("Timing")]
    public float windupTime = 1.5f;
    public float flapInterval = 0.5f;
    public float recoveryIdleTime = 1.5f;
    [Tooltip("Speed multiplier for windup animation. 1 = normal, 2 = double speed.")]
    public float windupAnimSpeed = 1.0f;

    [Header("Flap Count")]
    public int minFlaps = 3;
    public int maxFlaps = 6;

    [Header("Combat — Per Flap")]
    [Tooltip("Force for the first flap")]
    public float firstFlapForce = 42f;
    public float firstFlapStun = 0.5f;
    [Tooltip("Force for the second flap")]
    public float secondFlapForce = 70f;
    public float secondFlapStun = 1.2f;
    [Tooltip("Force for subsequent flaps")]
    public float followUpFlapForce = 32f;
    public float followUpFlapStun = 0.4f;

    [Header("Cooldown")]
    public float cooldownDuration = 8.0f;
}

[System.Serializable]
public class HeadSmashConfig
{
    [Header("Timing")]
    public float windupTime = 2.0f;
    public float actionTime = 0.4f;
    public float stuckTime = 1.5f;
    public float recoveryIdleTime = 1.5f;
    [Tooltip("Speed multiplier for windup animation. 1 = normal, 2 = double speed.")]
    public float windupAnimSpeed = 1.0f;

    [Header("Close Smash")]
    public float smashDamage = 40f;
    public float smashRadius = 5.5f;
    public float smashKnockbackForce = 95f;
    public float smashKnockbackStun = 1.0f;
    public float throwKnockbackForce = 140f;
    public float throwKnockbackStun = 1.5f;
    public int throwAfterUsageCount = 3;

    [Header("Shockwave Mode")]
    public float closeRangeThreshold = 10f;
    public float shockwaveExitTime = 2.5f;
    public float shockwaveSpeed = 22f;
    public float shockwaveTravelDistance = 32f;
    public float shockwaveDamage = 20f;

    [Header("Visuals")]
    public GameObject groundSmashVFXPrefab;
    public float groundSmashHeightOffset = 0.5f;
    public float groundSmashForwardOffset = 2.5f;
    public Vector3 groundSmashRotationOffset = Vector3.zero;

    [Header("Cooldown")]
    public float cooldownDuration = 7.0f;
}

[System.Serializable]
public class RangedAttackConfig
{
    [Header("Timing")]
    public float windupTime = 1.0f;
    public float recoveryIdleTime = 1.5f;
    [Tooltip("Speed multiplier for windup animation. 1 = normal, 2 = double speed.")]
    public float windupAnimSpeed = 1.0f;

    [Header("Visuals")]
    public GameObject fireVFXPrefab;
    public float vfxYOffset = 0.5f;
    public Vector3 vfxScale = new Vector3(2.5f, 2.5f, 2.5f);
    public Vector3 vfxRotationOffset = Vector3.zero;
    public Vector3 projectileBaseScale = new Vector3(0.8f, 1.3f, 0.8f);
    public GameObject projectilePrefab;

    [Header("Straight Shots")]
    public int minShots = 3;
    public int maxShots = 6;
    public float shotIntervalMin = 0.35f;
    public float shotIntervalMax = 0.6f;
    public float projectileSpeed = 35f;
    public float projectileDamage = 20f;

    [Header("Ring Drop (Barrage)")]
    public float ringDropDelay = 1.2f;
    public float ringDropWaitAfterFire = 2.5f;
    public int ringCount = 12;
    public float ringRadius = 8f;
    public float ringDamage = 25f;
    public int centerShots = 3;

    [Header("Cooldown")]
    public float cooldownDuration = 8.0f;
}

[System.Serializable]
public class PreciseProjectileConfig
{
    [Header("Timing")]
    public float windupTime = 3.0f;
    public float actionTime = 0.5f;
    public float recoveryIdleTime = 2.0f;
    [Tooltip("Speed multiplier for windup animation. 1 = normal, 2 = double speed.")]
    public float windupAnimSpeed = 1.0f;

    [Header("Combat")]
    public float projectileSpeed = 65f;
    public float projectileDamage = 45f;
    public float punishRadius = 4.0f;
    public float punishDamage = 10f;
    public float punishKnockbackForce = 80f;
    public float punishKnockbackStun = 0.6f;

    [Header("Cooldown")]
    public float cooldownDuration = 10.0f;
    
    [Header("Projectile")]
    public GameObject projectilePrefab;
}

[System.Serializable]
public class UpDownAttackConfig
{
    [Header("Timing")]
    public float windupTime = 2.0f;
    public float actionTime = 3.0f;
    public float recoveryIdleTime = 2.0f;
    [Tooltip("Speed multiplier for fly-up animation. 1 = normal, 2 = double speed.")]
    public float windupAnimSpeed = 1.0f;

    [Header("Jump")]
    public float jumpHeight = 15f;
    public float dropTime = 0.2f;
    [Tooltip("If true, boss rotates to face the player before jumping. If false, boss keeps current facing.")]
    public bool facePlayerOnJump = false;

    [Header("Landing Impact")]
    public float landDamage = 40f;
    public float landRadius = 6f;
    public float landKnockbackForce = 80f;
    public float landKnockbackStun = 1.0f;

    [Header("Rocks")]
    public int minRocks = 5;
    public int maxRocks = 8;
    public float rockForce = 2000f;
    public float rockSpawnRadius = 3.0f;
    public float rockScale = 2.5f;
    public GameObject rockPrefab;

    [Header("Rock Visuals")]
    public GameObject fireVFXPrefab;
    public float vfxYOffset = 0.5f;
    public Vector3 vfxScale = new Vector3(3.0f, 3.0f, 3.0f);
    public Vector3 vfxRotationOffset = Vector3.zero;

    [Header("Cooldown")]
    public float cooldownDuration = 10.0f;
}

[System.Serializable]
public class ChaseConfig
{
    [Header("Timing")]
    public float maxChaseTime = 12.0f;
    public float pushPauseDuration = 0.8f;
    public float recoveryIdleTime = 1.0f;

    [Header("Movement")]
    public float startSpeed = 6.0f;
    public float maxSpeed = 12.0f;
    public float acceleration = 1.5f;
    public float runAnimThreshold = 9.0f;

    [Header("Combat")]
    public float attackRange = 5.0f;
    public float pushbackDamage = 15f;
    public float pushbackForce = 100f;
    public float pushbackStun = 1.2f;
    public int maxPushbacks = 3;

    [Header("Cooldown")]
    public float cooldownDuration = 1.0f;
}

[System.Serializable]
public class ArenaRepositionConfig
{
    [Header("Timing")]
    public float jumpUpDuration = 1.2f;
    public float fireDelay = 0.6f;
    public float waitAfterFire = 2.8f;
    public float waitAtPeak = 2.0f;
    public float jumpDownDuration = 1.0f;
    public float recoveryIdleTime = 2.0f;

    [Header("Jump")]
    public float jumpUpPeakHeight = 6.0f;
    public float jumpDownPeakHeight = 3.0f;

    [Header("Ring Drop")]
    public int ringCount = 14;
    public float ringRadius = 7f;
    public float ringDamage = 25f;
    public int centerShots = 4;
    public float centerDamage = 30f;

    [Header("Cooldown")]
    public float cooldownDuration = 20.0f;
}

[System.Serializable]
public class EdgeWatchConfig
{
    [Header("Timing")]
    public float stalkDuration = 5.0f;
    public float grappleWindowStart = 1.5f;

    [Header("Grapple")]
    public float grappleRange = 15.0f;
    public float grappleDamage = 20f;
    public float grappleKnockbackForce = 15f;
    public float grappleKnockbackStun = 0.5f;
}

[System.Serializable]
public class DeathConfig
{
    public float timeBeforeDespawn = 4f;
}

// ============================================================
// ATTACK CHAIN SYSTEM
// ============================================================

public enum BossAttackType
{
    WingAttack,
    DashAttack,
    WavePushBack,
    HeadSmash,
    RangedAttack,
    PreciseProjectile,
    UpDownAttack,
    Chase,
    ArenaReposition
}

[System.Serializable]
public class AttackChainEntry
{
    public BossAttackType attackType;
    [HideInInspector] public bool isExpanded = true;
    [HideInInspector] public bool showAdvanced = false;

    // Flow Control (drawn by custom editor)
    public bool useIdleAfter = true;
    public float idleTimeMin = 1.0f;
    public float idleTimeMax = 2.0f;

    // Conditions (drawn by custom editor)
    [Tooltip("Min distance to player for this attack. 0 = any range.")]
    public float minDistance = 0f;
    [Tooltip("Max distance to player. 0 = unlimited.")]
    public float maxDistance = 0f;
    [Range(0, 1)] public float probability = 1.0f;
    [Tooltip("If conditions fail, skip to next entry in chain")]
    public bool skipIfConditionsFail = true;

    // Chase Overrides (drawn by custom editor)
    [Tooltip("Specific override for Chase speed. Set to 0 to use global config.")]
    public float chaseSpeedOverride = 0f;
    [Tooltip("Specific override for Chase duration. Set to 0 to use global config.")]
    public float chaseTimeOverride = 0f;
    [Tooltip("Stop chasing at this distance and advance. Set to 0 to use global config.")]
    public float chaseStopDistanceOverride = 0f;
}

// ============================================================
// INTERRUPT / EXCEPTION CONFIG
// ============================================================

[System.Serializable]
public class HitInterruptConfig
{
    [Header("General")]
    public bool enabled = true;
    public int minHitsBeforeInterrupt = 3;
    public int maxHitsBeforeInterrupt = 6;
    [Tooltip("Minimum seconds between interrupts (prevents spam)")]
    public float interruptCooldown = 3.0f;

    [Header("Reactive Responses")]
    [Tooltip("Which attacks can trigger as interrupts. A random one will be chosen.")]
    public AttackChainEntry[] interruptResponses = new AttackChainEntry[]
    {
        new AttackChainEntry { attackType = BossAttackType.WavePushBack },
        new AttackChainEntry { attackType = BossAttackType.WingAttack }
    };
}

[System.Serializable]
public class PhaseConfig
{
    [Tooltip("Health percentage (0-1) at which this phase activates")]
    [Range(0f, 1f)]
    public float healthThreshold = 0.5f;

    [Tooltip("Override attack chain for this phase (leave empty to keep current chain)")]
    public AttackChainEntry[] phaseAttackChain;

    [Tooltip("One-shot animation to play when entering this phase")]
    public string phaseTransitionAnimation = "";

    [Tooltip("Idle time after phase transition animation")]
    public float phaseTransitionIdleTime = 2.0f;
}
