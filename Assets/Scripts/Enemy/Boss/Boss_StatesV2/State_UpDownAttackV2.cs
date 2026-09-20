using UnityEngine;

public class State_UpDownAttackV2 : BossStateV2
{
    private float stateTimer = 0f;
    private bool actionStarted = false;
    private bool groundImpactShaken = false;
    private float initialY = 0f;
    private Vector3 initialXZ;
    private Quaternion initialRotation;
    private bool flyDownTriggered = false;
    private UnityEngine.AI.NavMeshAgent navAgent;

    public State_UpDownAttackV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        var cfg = brain.upDownAttackConfig;
        stateTimer = 0f;
        actionStarted = false;
        groundImpactShaken = false;
        initialY = brain.GetGroundHeight(brain.transform.position);
        initialXZ = new Vector3(brain.transform.position.x, 0, brain.transform.position.z);
        flyDownTriggered = false;

        // Face the player before jumping (if enabled)
        if (cfg.facePlayerOnJump && brain.currentTarget != null)
        {
            Vector3 targetPos = brain.currentTarget.GetPosition();
            targetPos.y = brain.transform.position.y;
            Vector3 dir = (targetPos - brain.transform.position).normalized;
            if (dir != Vector3.zero)
                brain.transform.rotation = Quaternion.LookRotation(dir);
        }
        initialRotation = brain.transform.rotation;

        navAgent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        brain.desiredMovementDirection = Vector3.zero;

        // Scale the fly-up animation to match the configured windup time
        float animSpeed = cfg.windupAnimSpeed > 0 ? cfg.windupAnimSpeed : 1.0f;
        brain.PlayAnimation("boss_flyUp", 0.1f, animSpeed);
        
    }

    public override void Tick()
    {
        var cfg = brain.upDownAttackConfig;
        stateTimer += Time.deltaTime;
        brain.desiredMovementDirection = Vector3.zero;

        if (stateTimer < cfg.windupTime)
        {
            // Jump up - ONLY Y axis, strictly locked XZ
            float t = stateTimer / cfg.windupTime;
            float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);
            float currentY = Mathf.Lerp(initialY, initialY + cfg.jumpHeight, easedT);
            brain.transform.position = new Vector3(initialXZ.x, currentY, initialXZ.z);
            brain.transform.rotation = initialRotation;
        }
        else if (stateTimer < cfg.windupTime + cfg.actionTime)
        {
            float actionTimer = stateTimer - cfg.windupTime;

            if (actionTimer <= cfg.dropTime)
            {
                if (!flyDownTriggered)
                {
                    flyDownTriggered = true;
                    // Use 0.0f for a "Hard Interrupt" to snap immediately into the fall
                    brain.PlayAnimation("boss_flyDown", 0.0f); 
                }

                // Fall down - ONLY Y axis, strictly locked XZ
                float dropT = actionTimer / cfg.dropTime;
                float easedDrop = dropT * dropT * dropT;
                float currentY = Mathf.Lerp(initialY + cfg.jumpHeight, initialY, easedDrop);
                brain.transform.position = new Vector3(initialXZ.x, currentY, initialXZ.z);
                brain.transform.rotation = initialRotation;
            }
            else
            {
                // Landed
                brain.transform.position = new Vector3(initialXZ.x, initialY, initialXZ.z);
                brain.transform.rotation = initialRotation;

                // Shake camera ONCE on impact
                if (!groundImpactShaken)
                {
                    groundImpactShaken = true;
                    brain.ApplyCameraShake(0.7f, 0.4f); // Slightly stronger shake
                }

                if (!actionStarted)
                {
                    actionStarted = true;
                    ExecuteLandedDamage();
                    SpawnRockWaves();
                }
            }
        }
        else
        {
            brain.AdvanceChain();
        }
    }


    private void SpawnRockWaves()
    {
        var cfg = brain.upDownAttackConfig;
        int rockCount = Random.Range(cfg.minRocks, cfg.maxRocks + 1);

        for (int i = 0; i < rockCount; i++)
        {
            float angle = i * Mathf.PI * 2 / rockCount;
            Vector3 spawnDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 spawnPos = brain.transform.position + spawnDir * cfg.rockSpawnRadius;
            spawnPos.y = initialY + 1.5f;

            GameObject rock;
            if (cfg.rockPrefab != null)
            {
                rock = GameObject.Instantiate(cfg.rockPrefab, spawnPos, Quaternion.identity);
                rock.transform.localScale = Vector3.one * cfg.rockScale;
            }
            else
            {
                rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.transform.position = spawnPos;
            }
            
            // Use the shared fire visuals setup for boulders too
            brain.SetupFireVisuals(
                rock, 
                cfg.fireVFXPrefab, 
                cfg.vfxYOffset, 
                cfg.vfxScale, 
                cfg.vfxRotationOffset, 
                Vector3.one * cfg.rockScale
            );

            Rigidbody rb = rock.GetComponent<Rigidbody>();
            if (rb == null) rb = rock.AddComponent<Rigidbody>();
            rb.mass = 20f;
            rb.angularDamping = 1f;

            // Handle Damage (Support both old rocks and new BossProjectiles)
            BossProjectile bp = rock.GetComponent<BossProjectile>();
            if (bp != null)
            {
                bp.damage = cfg.landDamage * 0.5f; // Rocks usually deal less than the impact
            }
            else
            {
                RollingRockDamage rrd = rock.GetComponent<RollingRockDamage>();
                if (rrd == null) rrd = rock.AddComponent<RollingRockDamage>();
            }

            rb.AddForce(spawnDir * cfg.rockForce, ForceMode.Impulse);
            
            GameObject.Destroy(rock, 12f);
        }
    }

    private void ExecuteLandedDamage()
    {
        var cfg = brain.upDownAttackConfig;
        Vector3 smashPoint = brain.transform.position;
        smashPoint.y += 1.0f; // Sphere center
        
        Collider[] hits = Physics.OverlapSphere(smashPoint, cfg.landRadius);
        Debug.Log($"[UpDown] Landing Impact! Radius: {cfg.landRadius}. Hits: {hits.Length}");

        brain.bossAudio.OnImpact();
        
        foreach (Collider hit in hits)
        {
            if (hit.transform.root == brain.transform.root) continue;
            
            // Check for Player tag or MovementController presence
            bool isPlayer = hit.CompareTag("Player");
            Character.MovementController mc = null;

            if (!isPlayer)
            {
                mc = hit.GetComponentInParent<Character.MovementController>();
                if (mc == null) mc = hit.transform.root.GetComponentInChildren<Character.MovementController>();
                isPlayer = (mc != null);
            }
            else
            {
                // If it IS tagged player, try to find the MC for knockback
                mc = hit.GetComponentInParent<Character.MovementController>() 
                     ?? hit.transform.root.GetComponentInChildren<Character.MovementController>();
            }
            
            if (isPlayer)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>()
                    ?? hit.GetComponentInParent<IDamageable>()
                    ?? hit.transform.root.GetComponentInChildren<IDamageable>();

                if (damageable != null)
                {
                    Debug.Log($"[UpDown] Damage applied to: {hit.name}");
                    damageable.TakeDamage(cfg.landDamage, hit.transform.position);
                    
                    if (mc != null)
                    {
                        Vector3 pushDir = col_flat(hit.transform.position) - col_flat(brain.transform.position);
                        if (pushDir == Vector3.zero) pushDir = brain.transform.forward;
                        mc.ApplyKnockback(pushDir.normalized * cfg.landKnockbackForce, cfg.landKnockbackStun);
                    }
                }
            }
        }
    }

    private Vector3 col_flat(Vector3 v) { return new Vector3(v.x, 0, v.z); }

    public override void Exit()
    {
        // Reset animation speed in case it was scaled
        brain.ResetAnimationSpeed();

        if (navAgent != null)
        {
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(brain.transform.position, out hit, 10.0f, UnityEngine.AI.NavMesh.AllAreas))
                brain.transform.position = hit.position;
            navAgent.enabled = true;
        }
        brain.desiredMovementDirection = Vector3.zero;
        brain.SetCooldown(this.GetType(), brain.upDownAttackConfig.cooldownDuration);
    }

    public override Color GetStateColor()
    {
        return stateTimer < brain.upDownAttackConfig.windupTime ? Color.green : new Color(0f, 0.5f, 0f);
    }

    public override string GetStateName()
    {
        return stateTimer < brain.upDownAttackConfig.windupTime ? "LEAPING INTO AIR" : "LANDING CRATER!";
    }
}
