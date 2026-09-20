using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sound categories any state can trigger from code.
/// Mirrors method names on BossAudio so PlaySound() can route by enum.
/// </summary>
public enum BossSoundCue
{
    Step,
    Impact,
    Smash,
    Roar,
    Death,
    Projectile,
    WingFlap,
    DashWindup,
    DashRelease,
    WavePushback,
    ChargeUp,
    Hurt,
    PhaseTransition,
    LeapTakeoff,
    LeapLand,
    LeapAirLoopStart,
    LeapAirLoopStop,
}

/// <summary>
/// BossBrainV2 — Data-driven, Inspector-tunable boss FSM.
/// Drop-in replacement for BossStateBrain. Implements IAIBrain so BossController works unchanged.
/// </summary>
public class BossBrainV2 : MonoBehaviour, IAIBrain
{
    // ================================================================
    // RUNTIME STATE (hidden from Inspector by default)
    // ================================================================
    private BossStateV2 currentState;
    private bool isStateTransitioning = false;

    [HideInInspector] public IPlayerState currentTarget;
    [HideInInspector] public Vector3 desiredMovementDirection = Vector3.zero;
    [HideInInspector] public bool isAttackingFlag = false;
    [HideInInspector] public bool isPlayerInArena = false;
    [HideInInspector] public bool hasPlayerEverEnteredArena = false;
    [HideInInspector] public bool hasStartedCombat = false;
    [HideInInspector] public bool isInArenaSequence = false;
    [HideInInspector] public bool isRetrievingPlayer = false;
    [HideInInspector] public int consecutiveHits = 0;
    [HideInInspector] public int headSmashUsageCount = 0;
    [HideInInspector] public int consecutiveHopCount = 0;

    public Animator animator { get; private set; }
    public BossWeaponHitbox bossWeapon { get; private set; }
    public BossSensors sensors { get; private set; }
    public BossAudio bossAudio { get; private set; }

    // ================================================================
    // INSPECTOR-TUNABLE CONFIGS
    // ================================================================

    [Header("========== GLOBAL SETTINGS ==========")]
    public IdleConfig idleConfig = new IdleConfig();

    [Header("========== ATTACK CONFIGS ==========")]
    public WingAttackConfig wingAttackConfig = new WingAttackConfig();
    public DashAttackConfig dashAttackConfig = new DashAttackConfig();
    public WavePushBackConfig wavePushBackConfig = new WavePushBackConfig();
    public HeadSmashConfig headSmashConfig = new HeadSmashConfig();
    public RangedAttackConfig rangedAttackConfig = new RangedAttackConfig();
    public PreciseProjectileConfig preciseProjectileConfig = new PreciseProjectileConfig();
    public UpDownAttackConfig upDownAttackConfig = new UpDownAttackConfig();
    public ChaseConfig chaseConfig = new ChaseConfig();
    public ArenaRepositionConfig arenaRepositionConfig = new ArenaRepositionConfig();

    [Header("========== ATTACK CHAIN ==========")]
    [Tooltip("The ordered sequence of attacks. The FSM walks through this list: Attack → Idle → Next → Idle → loop.")]
    public AttackChainEntry[] attackChain = new AttackChainEntry[]
    {
        new AttackChainEntry { attackType = BossAttackType.Chase },
        new AttackChainEntry { attackType = BossAttackType.WingAttack },
        new AttackChainEntry { attackType = BossAttackType.WavePushBack },
        new AttackChainEntry { attackType = BossAttackType.HeadSmash },
        new AttackChainEntry { attackType = BossAttackType.RangedAttack },
        new AttackChainEntry { attackType = BossAttackType.DashAttack },
        new AttackChainEntry { attackType = BossAttackType.PreciseProjectile },
        new AttackChainEntry { attackType = BossAttackType.UpDownAttack },
        new AttackChainEntry { attackType = BossAttackType.ArenaReposition },
    };

    [Tooltip("If no chain entry's distance condition is met, use this attack as fallback.")]
    public BossAttackType fallbackAttack = BossAttackType.Chase;

    [Header("========== EXCEPTION STATES ==========")]
    public HitInterruptConfig hitInterruptConfig = new HitInterruptConfig();
    public EdgeWatchConfig edgeWatchConfig = new EdgeWatchConfig();
    public DeathConfig deathConfig = new DeathConfig();

    [Header("========== PHASE SYSTEM ==========")]
    [Tooltip("Optional phases. Leave empty for single-phase boss.")]
    public PhaseConfig[] phases = new PhaseConfig[0];

    [Header("========== TESTING MODE ==========")]
    [Tooltip("Enable to loop through a custom attack chain defined below. Drag attacks in any order.")]
    public bool testingMode = false;
    
    [Tooltip("The attack sequence to loop through when testing mode is ON. Drag attacks here in desired order.")]
    public AttackChainEntry[] testingModeAttackChain = new AttackChainEntry[]
    {
        new AttackChainEntry { attackType = BossAttackType.WingAttack },
        new AttackChainEntry { attackType = BossAttackType.HeadSmash },
        new AttackChainEntry { attackType = BossAttackType.DashAttack },
    };

    [Header("========== CAMERA SHAKE ==========")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float cameraShakeMagnitude = 0.3f;
    [SerializeField] private float cameraShakeDuration = 0.2f;

    // ================================================================
    // ARENA & NAVIGATION
    // ================================================================

    [Header("========== ARENA ==========")]
    public LayerMask groundLayer;
    public Transform waypointSetupRoot;
    public BossArenaWaypoints arenaWaypoints;

    public Collider arenaCollider { get; private set; }
    public Transform headTransform { get; private set; }

    [Header("Hop Tracking")]
    public int maxHopsBeforeGrapple = 5;

    // ================================================================
    // INTERNAL TRACKING
    // ================================================================

    [HideInInspector] public Vector3 originalPosition;
    [HideInInspector] public Quaternion originalRotation;

    private Dictionary<System.Type, float> stateCooldowns = new Dictionary<System.Type, float>();
    private int currentChainIndex = 0;
    private float lastInterruptTime = -999f;
    private int currentHitThreshold;
    private int currentPhaseIndex = -1; // -1 = no phase active
    private AttackChainEntry[] activeChain; // Current working chain (may be swapped by phases)

    // ================================================================
    // LIFECYCLE
    // ================================================================

    void Awake()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        bossWeapon = GetComponentInChildren<BossWeaponHitbox>();
        sensors = gameObject.AddComponent<BossSensors>();

        // Audio component — optional but heavily recommended.
        bossAudio = GetComponent<BossAudio>();
        if (bossAudio == null) bossAudio = GetComponentInChildren<BossAudio>();
        if (bossAudio == null)
            Debug.LogWarning("[BossV2] No BossAudio component found. PlaySound() calls will be silent.");

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator != null)
                Debug.Log($"[BossV2] Found Animator on child: {animator.gameObject.name}");
        }

        if (arenaCollider == null)
        {
            GameObject fightZone = GameObject.Find("Boss_Fight_Zone");
            if (fightZone != null)
            {
                arenaCollider = fightZone.GetComponent<Collider>();
                if (arenaCollider != null)
                    Debug.Log("[BossV2] Dynamically assigned arena collider.");
                else
                    Debug.LogWarning("[BossV2] Boss_Fight_Zone found but no Collider!");
            }
        }

        if (headTransform == null)
        {
            headTransform = BossStateBrain.FindTransformDeep(transform, "C_head");
            if (headTransform != null) Debug.Log("[BossV2] Found C_head via deep search.");
        }

        // Initialize chain
        activeChain = testingMode && testingModeAttackChain != null && testingModeAttackChain.Length > 0 ? testingModeAttackChain : attackChain;
        currentChainIndex = 0;
        currentHitThreshold = Random.Range(
            hitInterruptConfig.minHitsBeforeInterrupt,
            hitInterruptConfig.maxHitsBeforeInterrupt + 1);
        
        // Auto-find camera if not assigned
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) cameraTransform = mainCam.transform;
        }

        // Start in initial idle
        ChangeState(new State_IdleV2(this, idleConfig.initialIdleDuration));

        // Wire up death/damage events
        BossDamageable damageable = GetComponentInChildren<BossDamageable>();
        if (damageable != null)
        {
            damageable.OnBossDeath += () =>
            {
                PlaySound(BossSoundCue.Death);
                ChangeState(new State_DeathV2(this));
            };
            damageable.OnTakeDamage += HandleHit;
        }
    }

    void Update()
    {
        if (currentTarget == null || !currentTarget.IsAlive())
            return;

        sensors.UpdateSensors(currentTarget);

        // --- EXCEPTION: Player left arena ---
        if (hasPlayerEverEnteredArena && !isPlayerInArena
            && !(currentState is State_EdgeWatchV2)
            && !(currentState is State_ReturnToSpawnV2)
            && !(currentState is State_DeathV2))
        {
            Debug.Log("[BossV2] Player left arena → EdgeWatch.");
            isRetrievingPlayer = false;
            ChangeState(new State_EdgeWatchV2(this));
            return;
        }

        // --- EXCEPTION: Phase transition ---
        CheckPhaseTransition();

        // Normal state tick
        currentState?.Tick();
    }

    public void SetupFireVisuals(GameObject obj, GameObject vfxPrefab, float yOffset, float scale, Vector3 rot, Vector3 baseScale)
    {
        obj.transform.localScale = baseScale;
        if (vfxPrefab != null)
        {
            GameObject vfx = Instantiate(vfxPrefab, obj.transform);
            vfx.transform.localPosition = new Vector3(0, yOffset, 0);
            vfx.transform.localScale = Vector3.one * scale;
            vfx.transform.localEulerAngles = rot;
        }
    }

    // ================================================================
    // AUDIO — Code-driven sound cues
    // ================================================================

    /// <summary>
    /// Generic entry point for states to fire a sound. No-op if BossAudio is missing.
    /// Animation-event sounds (footsteps, etc.) bypass this and call BossAudio directly.
    /// </summary>
    public void PlaySound(BossSoundCue cue)
    {
        if (bossAudio == null) return;
        switch (cue)
        {
            case BossSoundCue.Step:              bossAudio.OnStep(); break;
            case BossSoundCue.Impact:            bossAudio.OnImpact(); break;
            case BossSoundCue.Smash:             bossAudio.OnSmash(); break;
            case BossSoundCue.Roar:              bossAudio.OnRoar(); break;
            case BossSoundCue.Death:             bossAudio.OnDeath(); break;
            case BossSoundCue.Projectile:        bossAudio.OnProjectile(); break;
            case BossSoundCue.WingFlap:          bossAudio.OnWingFlap(); break;
            case BossSoundCue.DashWindup:        bossAudio.OnDashWindup(); break;
            case BossSoundCue.DashRelease:       bossAudio.OnDashRelease(); break;
            case BossSoundCue.WavePushback:      bossAudio.OnWavePushback(); break;
            case BossSoundCue.ChargeUp:          bossAudio.OnChargeUp(); break;
            case BossSoundCue.Hurt:              bossAudio.OnHurt(); break;
            case BossSoundCue.PhaseTransition:   bossAudio.OnPhaseTransition(); break;
            case BossSoundCue.LeapTakeoff:       bossAudio.OnLeapTakeoff(); break;
            case BossSoundCue.LeapLand:          bossAudio.OnLeapLand(); break;
            case BossSoundCue.LeapAirLoopStart:  bossAudio.OnLeapAirLoopStart(); break;
            case BossSoundCue.LeapAirLoopStop:   bossAudio.OnLeapAirLoopStop(); break;
        }
    }

    // ================================================================
    // HIT INTERRUPT
    // ================================================================

    private void HandleHit()
    {
        if (currentState is State_DeathV2) return;

        // AUDIO: every successful hit makes a hurt sound, even if it doesn't trigger an interrupt.
        PlaySound(BossSoundCue.Hurt);

        Debug.Log("[BossV2] Hit");
        
        if (!hitInterruptConfig.enabled) return;

        // Cooldown check — don't interrupt too frequently
        if (Time.time - lastInterruptTime < hitInterruptConfig.interruptCooldown) return;

        consecutiveHits++;
        if (consecutiveHits >= currentHitThreshold)
        {
            TriggerHitInterrupt();
        }
    }

    private void TriggerHitInterrupt()
    {
        if (isStateTransitioning) return;

        consecutiveHits = 0;
        currentHitThreshold = Random.Range(
            hitInterruptConfig.minHitsBeforeInterrupt,
            hitInterruptConfig.maxHitsBeforeInterrupt + 1);

        if (hitInterruptConfig.interruptResponses == null || hitInterruptConfig.interruptResponses.Length == 0)
            return;

        // Simple random selection
        int roll = Random.Range(0, hitInterruptConfig.interruptResponses.Length);
        AttackChainEntry chosenEntry = hitInterruptConfig.interruptResponses[roll];

        Debug.Log($"[BossV2] Hit interrupt triggered → {chosenEntry.attackType}");
        lastInterruptTime = Time.time;

        BossStateV2 interruptState = CreateStateFromType(chosenEntry.attackType, true, chosenEntry);
        if (interruptState != null)
            ChangeState(interruptState);
    }

    // ================================================================
    // PHASE SYSTEM
    // ================================================================

    private void CheckPhaseTransition()
    {
        if (phases == null || phases.Length == 0) return;

        BossDamageable damageable = GetComponentInChildren<BossDamageable>();
        if (damageable == null) return;

        float healthPct = damageable.currentHealth / damageable.maxHealth;

        for (int i = 0; i < phases.Length; i++)
        {
            if (i <= currentPhaseIndex) continue; // Already activated
            if (healthPct <= phases[i].healthThreshold)
            {
                ActivatePhase(i);
                break;
            }
        }
    }

    private void ActivatePhase(int phaseIndex)
    {
        currentPhaseIndex = phaseIndex;
        PhaseConfig phase = phases[phaseIndex];

        Debug.Log($"[BossV2] Phase {phaseIndex + 1} activated at health threshold {phase.healthThreshold}!");

        // AUDIO: phase transition sting (a roar, a stagger, whatever you want).
        PlaySound(BossSoundCue.PhaseTransition);

        // Swap attack chain if provided
        if (phase.phaseAttackChain != null && phase.phaseAttackChain.Length > 0)
        {
            activeChain = phase.phaseAttackChain;
            currentChainIndex = 0;
        }

        // Play transition animation if set
        if (!string.IsNullOrEmpty(phase.phaseTransitionAnimation))
        {
            PlayAnimation(phase.phaseTransitionAnimation);
        }

        ChangeState(new State_IdleV2(this, phase.phaseTransitionIdleTime));
    }

    // ================================================================
    // CHAIN FSM — The Core Loop
    // ================================================================

    /// <summary>
    /// Called by states when they finish. Walks the chain to find the next valid attack,
    /// then enters an idle gap before executing it.
    /// In testing mode, simply loops through the chain without distance checks.
    /// </summary>
    public void AdvanceChain()
    {
        if (!hasStartedCombat)
        {
            hasStartedCombat = true;
            Debug.Log("[BossV2] Initial idle complete → first chain entry.");
            ExecuteChainEntry(0);
            return;
        }

        if (currentTarget == null || !currentTarget.IsAlive())
        {
            ChangeState(new State_IdleV2(this, idleConfig.defaultIdleMin));
            return;
        }

        int nextIndex;

        // In testing mode, ALWAYS just cycle through the array without any checks
        if (testingMode)
        {
            nextIndex = (currentChainIndex + 1) % activeChain.Length;
            Debug.Log($"[BossV2] Testing Mode: Cycling chain {currentChainIndex} → {nextIndex}");
        }
        else
        {
            // Find the next valid chain entry (with distance checks)
            int startIndex = (currentChainIndex + 1) % activeChain.Length;
            nextIndex = FindNextValidChainEntry(startIndex);

            if (nextIndex < 0)
            {
                // No valid entry found — use fallback
                Debug.Log("[BossV2] No valid chain entry → fallback.");
                BossStateV2 fallback = CreateStateFromType(fallbackAttack, false);
                if (fallback != null)
                    ChangeState(fallback);
                else
                    ChangeState(new State_IdleV2(this, idleConfig.defaultIdleMin));
                return;
            }
        }

        // Determine idle gap before next attack
        AttackChainEntry prevEntry = activeChain[currentChainIndex < activeChain.Length ? currentChainIndex : 0];
        currentChainIndex = nextIndex;

        if (prevEntry.useIdleAfter)
        {
            float idleMin = prevEntry.idleTimeMin;
            float idleMax = prevEntry.idleTimeMax;
            float idleDuration = Random.Range(idleMin, idleMax);

            // Enter idle, then the idle will call ExecuteCurrentChainEntry when done
            ChangeState(new State_IdleV2(this, idleDuration, true, false, true));
        }
        else
        {
            // Skip idle and go straight to the next attack
            ExecuteCurrentChainEntry();
        }
    }

    /// <summary>
    /// Called by the idle state when the chain-advance idle completes.
    /// </summary>
    public void ExecuteCurrentChainEntry()
    {
        ExecuteChainEntry(currentChainIndex);
    }

    private void ExecuteChainEntry(int index)
    {
        if (activeChain == null || activeChain.Length == 0)
        {
            ChangeState(new State_IdleV2(this, idleConfig.defaultIdleMin));
            return;
        }

        currentChainIndex = index % activeChain.Length;
        AttackChainEntry entry = activeChain[currentChainIndex];

        BossStateV2 state = CreateStateFromType(entry.attackType, false, entry);
        if (state != null)
        {
            ChangeState(state);
        }
        else
        {
            Debug.LogWarning($"[BossV2] Failed to create state for {entry.attackType}. Idling.");
            ChangeState(new State_IdleV2(this, idleConfig.defaultIdleMin));
        }
    }

    private int FindNextValidChainEntry(int startIndex)
    {
        if (activeChain == null || activeChain.Length == 0) return -1;

        float dist = sensors != null ? sensors.DistanceToTarget : 0f;

        for (int i = 0; i < activeChain.Length; i++)
        {
            int idx = (startIndex + i) % activeChain.Length;
            AttackChainEntry entry = activeChain[idx];

            // 1. Evaluate conditions (only if skipIfConditionsFail is enabled)
            if (entry.skipIfConditionsFail)
            {
                // Distance check: minDistance = closest allowed, maxDistance = farthest allowed
                // A value of 0 means "no limit" for that end of the range
                bool tooClose = (entry.minDistance > 0 && dist < entry.minDistance);
                bool tooFar = (entry.maxDistance > 0 && dist > entry.maxDistance);

                if (tooClose || tooFar)
                {
                    Debug.Log($"[BossV2 Chain] SKIP [{idx}] {entry.attackType}: Distance {dist:F1}m " +
                              $"outside range [{entry.minDistance} → {(entry.maxDistance > 0 ? entry.maxDistance.ToString() : "∞")}]");
                    continue;
                }

                // Probability roll: 1.0 = always, 0.5 = 50% chance, 0.0 = never
                if (entry.probability < 1.0f)
                {
                    float roll = Random.value;
                    if (roll > entry.probability)
                    {
                        Debug.Log($"[BossV2 Chain] SKIP [{idx}] {entry.attackType}: Probability fail " +
                                  $"(rolled {roll:F2}, needed ≤ {entry.probability:F2})");
                        continue;
                    }
                }
            }

            // 2. Check cooldown
            BossStateV2 testState = CreateStateFromType(entry.attackType, false);
            if (testState != null && IsOnCooldown(testState.GetType()))
            {
                Debug.Log($"[BossV2 Chain] SKIP [{idx}] {entry.attackType}: On cooldown");
                continue;
            }

            Debug.Log($"[BossV2 Chain] SELECTED [{idx}] {entry.attackType} (dist={dist:F1}m)");
            return idx;
        }

        return -1; // Nothing valid
    }

    // ================================================================
    // STATE FACTORY
    // ================================================================

    public BossStateV2 CreateStateFromType(BossAttackType type, bool isInterrupt, AttackChainEntry entry = null)
    {
        switch (type)
        {
            case BossAttackType.WingAttack:
                return new State_WingAttackV2(this, isInterrupt);
            case BossAttackType.DashAttack:
                return new State_DashAttackV2(this);
            case BossAttackType.WavePushBack:
                return new State_WavePushBackV2(this);
            case BossAttackType.HeadSmash:
                return new State_HeadSmashAttackV2(this);
            case BossAttackType.RangedAttack:
                return new State_RangedAttackV2(this);
            case BossAttackType.PreciseProjectile:
                return new State_PreciseProjectileAttackV2(this);
            case BossAttackType.UpDownAttack:
                return new State_UpDownAttackV2(this);
            case BossAttackType.Chase:
                if (entry != null && (entry.chaseSpeedOverride > 0 || entry.chaseTimeOverride > 0 || entry.chaseStopDistanceOverride > 0))
                    return new State_ChaseV2(this, entry.chaseSpeedOverride, entry.chaseTimeOverride, entry.chaseStopDistanceOverride);
                return new State_ChaseV2(this);
            case BossAttackType.ArenaReposition:
                return new State_ArenaRepositionV2(this);
            default:
                Debug.LogWarning($"[BossV2] Unknown attack type: {type}");
                return null;
        }
    }

    // ================================================================
    // STATE MACHINE CORE
    // ================================================================

    public void ChangeState(BossStateV2 newState)
    {
        if (isStateTransitioning)
        {
            Debug.Log("[BossV2] Transition blocked — already transitioning.");
            return;
        }

        isStateTransitioning = true;

        currentState?.Exit();

        if (currentState != null && !IsOnCooldown(currentState.GetType()))
            SetCooldown(currentState.GetType(), 1.0f);

        string oldName = currentState?.GetStateName() ?? "None";
        currentState = newState;
        Debug.Log($"[BossV2] {gameObject.name}: {oldName} → {newState.GetStateName()}");
        currentState?.Enter();

        isStateTransitioning = false;
    }

    // ================================================================
    // ANIMATION
    // ================================================================

    public void PlayAnimation(string stateName, float crossFade = 0.2f, float speed = 1.0f)
    {
        if (animator == null)
        {
            Debug.LogError($"[BossV2] No Animator for '{stateName}'!");
            return;
        }

        Debug.Log($"[BossV2] Animator: Switching to '{stateName}' (Fade: {crossFade}s, Speed: {speed:F2}x)");
        animator.CrossFadeInFixedTime(stateName, crossFade);
        animator.speed = speed;
    }

    /// <summary>
    /// Set the Animator playback speed. Use to scale animations to match desired windup times.
    /// Speed = animClipLength / desiredTime. Call ResetAnimationSpeed() when done.
    /// </summary>
    public void SetAnimationSpeed(float speed)
    {
        if (animator != null) animator.speed = speed;
    }

    /// <summary>
    /// Reset the Animator speed back to normal (1.0).
    /// </summary>
    public void ResetAnimationSpeed()
    {
        if (animator != null) animator.speed = 1.0f;
    }

    // Animation event receivers.
    // These forward to the current state. If you want an animation event to ALSO
    // fire a sound regardless of state, route it directly to BossAudio in the
    // animation event track instead — that's the cleaner pattern.
    public void OnFlap() { currentState?.OnAnimationEvent("OnFlap"); }
    public void OnSmash() { currentState?.OnAnimationEvent("OnSmash"); }
    public void OnFire() { currentState?.OnAnimationEvent("OnFire"); }
    public void Hit() { currentState?.OnAnimationEvent("Hit"); }
    public void Footstep() { currentState?.OnAnimationEvent("Footstep"); }
    public void OnActionComplete() { currentState?.OnAnimationEvent("OnActionComplete"); }
    
    /// <summary>
    /// Call this from animation events on ALWAYS-hit attacks (e.g., UpDownAttack, area effects).
    /// Shakes camera regardless of whether player is hit.
    /// 
    /// For conditional camera shake (only on hit), add the logic directly in the attack state:
    /// Example in State_HeadSmashAttackV2.ExecuteCloseSmash():
    ///     if (playerDamageable != null)
    ///     {
    ///         playerDamageable.TakeDamage(damage, hitPos);
    ///         brain.ApplyCameraShake();  // <-- Add here
    ///     }
    /// </summary>
    public void OnCameraShakeAlways()
    {
        ApplyCameraShake(cameraShakeMagnitude, cameraShakeDuration);
    }

    // ================================================================
    // CAMERA SHAKE
    // ================================================================

    /// <summary>
    /// Apply a camera shake effect to the player's camera.
    /// Explicitly finds the player GameObject and shakes their camera.
    /// </summary>
    public void ApplyCameraShake(float magnitude = 0.3f, float duration = 0.2f)
    {
        // 1. Try the centralized manager
        if (CameraShakeManager.Instance != null)
        {
            CameraShakeManager.Instance.Shake(magnitude, duration);
            return;
        }

        // 2. Try CinemachineShake (The most robust method)
        if (CinemachineShake.Instance != null)
        {
            CinemachineShake.Instance.Shake(magnitude, duration);
            return;
        }

        Debug.LogWarning("[BossV2] No shaker found in scene! Ensure CinemachineShake is attached to the camera.");
    }

    // ================================================================
    // COOLDOWNS
    // ================================================================

    public void SetCooldown(System.Type stateType, float duration)
    {
        stateCooldowns[stateType] = Time.time + duration;
    }

    public bool IsOnCooldown(System.Type stateType)
    {
        if (stateCooldowns.TryGetValue(stateType, out float readyTime))
            return Time.time < readyTime;
        return false;
    }

    // ================================================================
    // ARENA HELPERS
    // ================================================================

    public bool IsPointInArena(Vector3 point)
    {
        if (arenaCollider == null) return true;
        return arenaCollider.bounds.Contains(point);
    }

    public Vector3 GetClosestArenaPoint(Vector3 point)
    {
        if (arenaCollider == null) return point;
        return arenaCollider.ClosestPoint(point);
    }

    public float GetGroundHeight(Vector3 position)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 100.0f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position.y;
        return position.y;
    }

    public bool IsPathToPlayerValid()
    {
        if (currentTarget == null) return true;
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null || !agent.enabled) return true;
        if (!agent.isOnNavMesh) return false;

        UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
        if (agent.CalculatePath(currentTarget.GetPosition(), path))
            return path.status != UnityEngine.AI.NavMeshPathStatus.PathInvalid;
        return false;
    }

    // ================================================================
    // HOP TRACKING
    // ================================================================

    public void IncrementHopCount() { consecutiveHopCount++; }
    public void ResetHopCount() { consecutiveHopCount = 0; }
    public bool ShouldUseGrapple() { return consecutiveHopCount >= maxHopsBeforeGrapple || isRetrievingPlayer; }

    // ================================================================
    // TARGET
    // ================================================================

    public void SetTarget(IPlayerState target) { currentTarget = target; }

    // ================================================================
    // IAIBrain
    // ================================================================

    public Vector3 GetMovementDirection() { return desiredMovementDirection; }
    public bool WantsToAttack() { return isAttackingFlag; }
    public Color GetDebugColor() { return currentState?.GetStateColor() ?? Color.white; }



    // ================================================================
    // EDITOR BAKE (mirrors V1)
    // ================================================================

#if UNITY_EDITOR
    [ContextMenu("Bake Cubes to ScriptableObject")]
    public void BakeWaypointsToSO()
    {
        if (arenaWaypoints == null)
        {
            Debug.LogError("Please assign a BossArenaWaypoints ScriptableObject first!");
            return;
        }
        if (waypointSetupRoot == null || waypointSetupRoot.childCount == 0)
        {
            Debug.LogError("Please assign waypointSetupRoot with child cubes!");
            return;
        }

        List<BossArenaWaypoints.WaypointData> bakedPoints = new List<BossArenaWaypoints.WaypointData>();
        foreach (Transform child in waypointSetupRoot)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(child.position, out UnityEngine.AI.NavMeshHit hit, 50.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                BossArenaWaypoints.WaypointData data = new BossArenaWaypoints.WaypointData();
                data.pointName = child.name;
                data.position = hit.position;

                if (child.name.ToLower().Contains("center"))
                    data.type = BossArenaWaypoints.WaypointType.Center;
                else if (child.name.ToLower().Contains("high"))
                    data.type = BossArenaWaypoints.WaypointType.High;
                else
                    data.type = BossArenaWaypoints.WaypointType.Bottom;

                string[] parts = child.name.Split('_');
                if (parts.Length >= 2)
                    data.groupName = parts[0] + "_" + parts[1];

                bakedPoints.Add(data);
            }
        }

        arenaWaypoints.waypoints = bakedPoints.ToArray();
        UnityEditor.EditorUtility.SetDirty(arenaWaypoints);
        Debug.Log($"[BossV2] Baked {bakedPoints.Count} waypoints.");
    }
#endif

    // ================================================================
    // SHARED VFX HELPERS
    // ================================================================

    /// <summary>
    /// Shared helper to apply fire visuals, egg-scaling, and recursive particle playback to a projectile or boulder.
    /// </summary>
    public void SetupFireVisuals(GameObject target, GameObject vfxPrefab, float yOffset, Vector3 vfxScale, Vector3 vfxRotation, Vector3 meshScale)
    {
        // 1. Mesh Scaling (Procedural Egg/Boulder Shape)
        target.transform.localScale = Vector3.Scale(target.transform.localScale, meshScale);

        // 2. HDRP Material Setup: Orange Emission
        Renderer rd = target.GetComponent<Renderer>();
        if (rd != null)
        {
            rd.material.EnableKeyword("_EMISSION");
            rd.material.SetColor("_EmissiveColor", new Color(1.0f, 0.45f, 0.05f) * 5.0f); 
            rd.material.color = new Color(0.8f, 0.3f, 0.05f);
        }

        // 3. VFX Instantiation
        if (vfxPrefab != null)
        {
            GameObject vfx = GameObject.Instantiate(vfxPrefab, target.transform, false);
            vfx.SetActive(true);

            // Layer Sync
            vfx.layer = target.layer;
            foreach (Transform child in vfx.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = target.layer;
            }
            
            // Cleanup Hook
            BossProjectile proj = target.GetComponent<BossProjectile>();
            if (proj == null) proj = target.AddComponent<BossProjectile>();
            proj.visualEffectSlot = vfx;

            // Transform Tuning
            vfx.transform.localScale = vfxScale;
            vfx.transform.localPosition = new Vector3(0, yOffset, 0);
            vfx.transform.localRotation = Quaternion.Euler(vfxRotation);

            // Particle Recursive Play
            ParticleSystem[] allPS = vfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allPS)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ps.Play(true);

                var shape = ps.shape; 
                shape.scale = meshScale;
            }
        }
    }
}