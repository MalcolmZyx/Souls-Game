using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
public class BossController : MonoBehaviour
{
    private UnityEngine.AI.NavMeshAgent agent;
    private IAIBrain activeBrain;
    private bool isDead = false;

    private float flashTimer = 0f;
    private float flashDuration = 0.1f;

    [Header("Visual Debug")]
    public SkinnedMeshRenderer bossRenderer;
    public Color idleColor = Color.yellow; // Default color when idle
    public Color damageFlashColor = Color.black;

    // Renderer cache + property block for color overrides
    private MaterialPropertyBlock mpb;
    private Renderer[] allRenderers;

    [Header("Debug Settings")]
    public bool showDebugLogs = true;
    [Tooltip("Tick in Play Mode to instantly force the boss black.")]
    [SerializeField] private bool debugForceBlack = false;
    [Tooltip("Tick in Play Mode to dump renderer/shader info to the Console.")]
    [SerializeField] private bool debugDumpRenderers = false;

    void Awake()
    {
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.updatePosition = true;
            agent.updateRotation = false; // We handle rotation manually
        }

        IAIBrain[] brains = GetComponents<IAIBrain>();
        foreach (var b in brains)
        {
            if (b is MonoBehaviour mb && mb.enabled)
            {
                activeBrain = b;
                break;
            }
        }
        if (activeBrain == null)
            Debug.LogError("BossController: No actively enabled IAIBrain found on this GameObject!");

        if (bossRenderer == null)
            bossRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        mpb = new MaterialPropertyBlock();

        BossDamageable damageable = GetComponentInChildren<BossDamageable>();
        if (damageable != null)
        {
            damageable.OnTakeDamage += TriggerDamageFlash;
            damageable.OnBossDeath += HandleBossDeath;
        }

        // Do not force sleep here immediately; defer to Start so arena trigger state is evaluated first.
    }

    private void Start()
    {
        StartCoroutine(InitializeSleepState());
    }

    private System.Collections.IEnumerator InitializeSleepState()
    {
        // Wait one frame to let BossArenaTrigger.Start set isPlayerInArena in case player starts inside.
        yield return null;

        if (activeBrain is BossStateBrain stateBrain && !stateBrain.isPlayerInArena)
        {
            GoToSleep();
        }
        else if (activeBrain is BossBrainV2 v2Brain && !v2Brain.isPlayerInArena)
        {
            GoToSleep();
        }
        else
        {
            Debug.Log("BossController: Player already in arena at start, staying awake.");
        }
    }

    void Update()
    {
        // Inspector debug checkboxes — tick once in Play Mode to trigger
        if (debugForceBlack)
        {
            debugForceBlack = false;
            Debug.Log("[BossController] debugForceBlack — forcing black now.");
            HandleBossDeath();
        }
        if (debugDumpRenderers)
        {
            debugDumpRenderers = false;
            DiagnosticDump();
        }
    }

    void FixedUpdate()
    {
        if (activeBrain == null) return;

        Vector3 desiredMovement = activeBrain.GetMovementDirection();
        bool isAttacking = activeBrain.WantsToAttack();

        if (showDebugLogs && Time.frameCount % 60 == 0)
            Debug.Log($"[Boss Status] Moving: {desiredMovement} | Attacking: {isAttacking}");

        // --- BOSS MOVEMENT ---
        if (agent != null && agent.enabled)
        {
            // Failsafe: If the boss spawns in the air or gets slightly detached, the agent breaks.
            if (!agent.isOnNavMesh)
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 20.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            // NavMeshAgent.Move is safer than transform.position for physics interactions
            agent.Move(desiredMovement * Time.fixedDeltaTime);
        }
        else
        {
            Boss_Move(desiredMovement * (Time.fixedDeltaTime / Time.deltaTime)); // Adjust for FixedUpdate
        }
        
        Boss_Rotate(desiredMovement);

        // --- PLAYER GROUNDING (The "Fix") ---
        // Force the player down if they are near the boss to prevent "ramping" up the boss's collider.
        Collider[] nearbyPlayers = Physics.OverlapSphere(transform.position, 12f);
        foreach (var col in nearbyPlayers)
        {
            if (col.CompareTag("Player"))
            {
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    // 1. Kill any upward velocity caused by collision resolution
                    if (rb.linearVelocity.y > 0.01f)
                    {
                        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    }

                    // 2. Extra horizontal push if too close to the center (prevent clipping)
                    Vector3 flatSelf = new Vector3(transform.position.x, 0, transform.position.z);
                    Vector3 flatPlayer = new Vector3(col.transform.position.x, 0, col.transform.position.z);
                    float dist = Vector3.Distance(flatSelf, flatPlayer);
                    if (dist < 4.0f) // If inside the boss's core radius
                    {
                        Vector3 pushDir = (flatPlayer - flatSelf).normalized;
                        if (pushDir == Vector3.zero) pushDir = transform.forward;
                        rb.AddForce(pushDir * 50f, ForceMode.Acceleration);
                    }
                }
            }
        }
    }

    void LateUpdate()
    {
        if (isDead)
        {
            // Hammer black every frame — beats any Animator color keyframes
            ApplyColorToRenderers(Color.black);
            return;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            ApplyColorToRenderers(damageFlashColor);
            FlashAllBossMaterials(damageFlashColor);
            return;
        }

        if (activeBrain != null)
        {
            ApplyColorToRenderers(activeBrain.GetDebugColor());
        }
    }

    private void ApplyColorToRenderers(Color color)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();

        // Re-fetch every call so late-spawned child renderers are included
        allRenderers = GetComponentsInChildren<Renderer>(true);

        if (allRenderers == null || allRenderers.Length == 0)
        {
            Debug.LogWarning("[BossController] ApplyColorToRenderers: NO RENDERERS FOUND!");
            return;
        }

        foreach (Renderer rend in allRenderers)
        {
            if (rend == null) continue;

            // Layer 1: Write directly onto the instanced material copies
            foreach (Material mat in rend.materials)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_Color"))        mat.SetColor("_Color", color);
                if (mat.HasProperty("_BaseColor"))    mat.SetColor("_BaseColor", color);
                if (color == Color.black)
                {
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                }
            }

            // Layer 2: MaterialPropertyBlock on top for extra insurance
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", color);
            mpb.SetColor("_BaseColor", color);
            if (color == Color.black) mpb.SetColor("_EmissionColor", Color.black);
            rend.SetPropertyBlock(mpb);
        }

        if (showDebugLogs && color == Color.black)
            Debug.Log($"[BossController] BLACK applied to {allRenderers.Length} renderer(s).");
    }

    private void FlashAllBossMaterials(Color color)
    {
        if (bossRenderer == null) return;

        Material[] mats = bossRenderer.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            Material mat = mats[i];
            if (mat == null) continue;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (color == Color.black && mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);
        }
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    public void Boss_Move(Vector3 movementDirection)
    {
        transform.position += movementDirection * Time.deltaTime;
    }

    public void Boss_Rotate(Vector3 movementDirection)
    {
        if (movementDirection != Vector3.zero)
        {
            Vector3 flatDirection = movementDirection;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(flatDirection),
                    Time.deltaTime * 5f);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Wake / Sleep
    // -------------------------------------------------------------------------

    public void WakeUp(IPlayerState target)
    {
        // Re-enable GameObject first (critical for trigger callbacks after PowerDown)
        gameObject.SetActive(true);
        this.enabled = true;

        if (activeBrain is BossStateBrain stateBrain)
        {
            stateBrain.enabled = true;
            stateBrain.SetTarget(target);
            stateBrain.hasStartedCombat = false;
            stateBrain.ChangeState(new State_Idle(stateBrain, 10.0f));
        }
        else if (activeBrain is BossBrainV2 v2Brain)
        {
            v2Brain.enabled = true;
            v2Brain.SetTarget(target);
            v2Brain.hasStartedCombat = false;
            v2Brain.ChangeState(new State_IdleV2(v2Brain, v2Brain.idleConfig.initialIdleDuration));
        }

        Debug.Log("BossController: Waking up and locking onto target!");
    }

    public void PlayerLeftArena()
    {
        if (activeBrain is BossStateBrain stateBrain)
            stateBrain.isPlayerInArena = false;
        else if (activeBrain is BossBrainV2 v2Brain)
            v2Brain.isPlayerInArena = false;
    }

    public void PlayerEnteredArena()
    {
        if (activeBrain is BossStateBrain stateBrain)
            stateBrain.isPlayerInArena = true;
        else if (activeBrain is BossBrainV2 v2Brain)
            v2Brain.isPlayerInArena = true;
    }

    public void PowerDown()
    {
        if (activeBrain is BossStateBrain stateBrain)
            stateBrain.enabled = false;
        else if (activeBrain is BossBrainV2 v2Brain)
            v2Brain.enabled = false;

        this.enabled = false;
    }

    public void GoToSleep()
    {
        if (activeBrain is BossStateBrain stateBrain)
            stateBrain.ChangeState(new State_ReturnToSpawn(stateBrain));
        else if (activeBrain is BossBrainV2 v2Brain)
            v2Brain.ChangeState(new State_ReturnToSpawnV2(v2Brain));

        if (activeBrain is MonoBehaviour brainMB)
            brainMB.enabled = false;

        ApplyColorToRenderers(idleColor);
        this.enabled = false;
        Debug.Log("BossController: Going to sleep!");
    }

    // -------------------------------------------------------------------------
    // Death — uses the same confirmed-working ApplyColorToRenderers path
    // -------------------------------------------------------------------------

    /// <summary>Called by State_Death.Enter() via the state machine.</summary>
    public void TriggerDeathVisuals()
    {
        isDead = true;                          // LateUpdate will hammer black every frame
        ApplyColorToRenderers(Color.black);     // Also apply immediately this frame
        Debug.Log("[BossController] TriggerDeathVisuals called — boss is now black.");
    }

    /// Called directly by BossDamageable.OnBossDeath event.
    private void HandleBossDeath()
    {
        isDead = true;                          // LateUpdate will hammer black every frame
        ApplyColorToRenderers(Color.black);     // Also apply immediately this frame
        Debug.Log("[BossController] HandleBossDeath called — boss is now black.");
    }

    // -------------------------------------------------------------------------
    // Damage flash
    // -------------------------------------------------------------------------

    private void TriggerDamageFlash()
    {
        flashTimer = flashDuration;
        ApplyColorToRenderers(damageFlashColor);
    }

    // -------------------------------------------------------------------------
    // Diagnostic dump (triggered via Inspector checkbox)
    // -------------------------------------------------------------------------

    private void DiagnosticDump()
    {
        Renderer[] found = GetComponentsInChildren<Renderer>(true);
        Debug.Log($"[BossController DIAG] enabled={this.enabled}, isDead={isDead}, " +
                  $"found {found.Length} renderer(s) under '{gameObject.name}'.");
        foreach (Renderer r in found)
        {
            Debug.Log($"  RENDERER '{r.gameObject.name}' ({r.GetType().Name}) enabled={r.enabled}");
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) { Debug.Log("    MATERIAL: <null>"); continue; }
                Debug.Log($"    MATERIAL '{m.name}'  shader='{m.shader?.name}'");
#if UNITY_EDITOR
                if (m.shader == null) continue;
                int count = UnityEditor.ShaderUtil.GetPropertyCount(m.shader);
                for (int i = 0; i < count; i++)
                    Debug.Log($"      prop: {UnityEditor.ShaderUtil.GetPropertyName(m.shader, i)} " +
                              $"({UnityEditor.ShaderUtil.GetPropertyType(m.shader, i)})");
#endif
            }
        }
    }
}