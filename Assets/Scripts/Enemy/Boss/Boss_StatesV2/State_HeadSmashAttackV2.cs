using UnityEngine;

public class State_HeadSmashAttackV2 : BossStateV2
{
    private enum SubPhase { Windup, Smashing, Stuck, Unstucking }
    private SubPhase currentSubPhase = SubPhase.Windup;

    private float stateTimer = 0f;
    private float phaseTimer = 0f;
    private bool isShockwaveMode = false;
    private Vector3 shockwaveDirection;

    public State_HeadSmashAttackV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        var cfg = brain.headSmashConfig;
        stateTimer = 0f;
        phaseTimer = 0f;
        currentSubPhase = SubPhase.Windup;
        brain.desiredMovementDirection = Vector3.zero;
        brain.headSmashUsageCount++;

        if (brain.currentTarget != null)
        {
            float dist = Vector3.Distance(brain.transform.position, brain.currentTarget.GetPosition());
            isShockwaveMode = dist > cfg.closeRangeThreshold;
            shockwaveDirection = brain.currentTarget.GetPosition() - brain.transform.position;
            shockwaveDirection.y = 0f;
            shockwaveDirection.Normalize();
        }
        else
        {
            isShockwaveMode = false;
            shockwaveDirection = brain.transform.forward;
        }

        float animSpeed = cfg.windupAnimSpeed > 0 ? cfg.windupAnimSpeed : 1.0f;
        brain.PlayAnimation("boss_windUp", 0.2f, animSpeed);
    }

    public override void Tick()
    {
        var cfg = brain.headSmashConfig;
        stateTimer += Time.deltaTime;
        phaseTimer += Time.deltaTime;
        brain.desiredMovementDirection = Vector3.zero;

        switch (currentSubPhase)
        {
            case SubPhase.Windup:
                // Rotate towards player during windup
                if (brain.currentTarget != null)
                {
                    Vector3 targetPos = brain.currentTarget.GetPosition();
                    targetPos.y = brain.transform.position.y;
                    Vector3 dir = (targetPos - brain.transform.position).normalized;
                    if (dir != Vector3.zero)
                        brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                }

                if (stateTimer >= cfg.windupTime)
                {
                    currentSubPhase = SubPhase.Smashing;
                    phaseTimer = 0f;
                    brain.ResetAnimationSpeed();
                    brain.PlayAnimation("boss_headSmash", 0.1f);
                }
                break;

            case SubPhase.Smashing:
                // Waiting for OnActionComplete event to transition to Stuck
                break;

            case SubPhase.Stuck:
                // Wait for the adjustable stuckTime
                if (phaseTimer >= cfg.stuckTime)
                {
                    currentSubPhase = SubPhase.Unstucking;
                    phaseTimer = 0f;
                    brain.PlayAnimation("boss_unstuck", 0.1f);
                }
                break;

            case SubPhase.Unstucking:
                // Waiting for OnActionComplete event to finish the state
                break;
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "OnSmash")
        {
            SpawnGroundSmashVFX();

            if (isShockwaveMode) 
            {
                LaunchGroundShockwave();
            }
            else 
            {
                ExecuteCloseSmash();
            }
        }
        else if (eventName == "OnCameraShakeAlways")
        {
            // Shake camera regardless of whether we hit the player (impact with ground)
            brain.ApplyCameraShake();
        }
        else if (eventName == "OnActionComplete")
        {
            if (currentSubPhase == SubPhase.Smashing)
            {
                // Transition to Stuck phase
                currentSubPhase = SubPhase.Stuck;
                phaseTimer = 0f;
                brain.PlayAnimation("boss_headStuck", 0.1f);
            }
            else if (currentSubPhase == SubPhase.Unstucking)
            {
                // Sequence complete
                if (brain.isInArenaSequence)
                    brain.ChangeState(new State_ReturnToCenterV2(brain));
                else
                    brain.AdvanceChain();
            }
        }
    }

    private void SpawnGroundSmashVFX()
    {
        var cfg = brain.headSmashConfig;
        if (cfg.groundSmashVFXPrefab == null) return;

        // Use the horizontal shockwave direction to ensure the 'lane' goes out towards the target
        // even if the boss is tilted or looking down during the animation impact.
        Vector3 targetDir = shockwaveDirection;
        if (targetDir == Vector3.zero) targetDir = brain.transform.forward;
        targetDir.y = 0;
        targetDir.Normalize();

        // Position: forward from the boss to align with where the head impacts (using configurable offset)
        Vector3 spawnPos = brain.transform.position + targetDir * cfg.groundSmashForwardOffset;
        Quaternion spawnRot = Quaternion.LookRotation(targetDir, Vector3.up);

        // Terrain Alignment: Raycast downward to ensure it's on the ground and aligned with the slope
        if (Physics.Raycast(spawnPos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 15f, brain.groundLayer))
        {
            spawnPos = hit.point + Vector3.up * cfg.groundSmashHeightOffset;
            
            // Align the VFX with the ground normal so the "lane" stays flush with the hill
            spawnRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * spawnRot;
        }

        // Apply any manual rotation offset from the Inspector (e.g. if the prefab is built sideways)
        spawnRot *= Quaternion.Euler(cfg.groundSmashRotationOffset);

        // Instantiate as top-level object (reverted from parenting as requested)
        Object.Instantiate(cfg.groundSmashVFXPrefab, spawnPos, spawnRot);
    }

    private void LaunchGroundShockwave()
    {
        var cfg = brain.headSmashConfig;
        Vector3 spawnPos = brain.transform.position + shockwaveDirection * 1.5f;
        spawnPos.y = brain.transform.position.y;

        GameObject shockwave = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shockwave.transform.localScale = new Vector3(4f, 0.6f, 2.5f);
        shockwave.transform.position = spawnPos;
        shockwave.transform.rotation = Quaternion.LookRotation(shockwaveDirection);

        Renderer rd = shockwave.GetComponent<Renderer>();
        if (rd != null) rd.material.color = new Color(1f, 0.45f, 0f);

        Collider col = shockwave.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        ShockwaveSlab slab = shockwave.AddComponent<ShockwaveSlab>();
        slab.Initialize(shockwaveDirection, cfg.shockwaveSpeed, cfg.shockwaveTravelDistance, cfg.shockwaveDamage);
    }

    private void ExecuteCloseSmash()
    {
        var cfg = brain.headSmashConfig;
        // Smash point: forward from boss, and slightly UP (1.5m) to catch the player's body center
        Vector3 smashPoint = brain.transform.position + brain.transform.forward * cfg.groundSmashForwardOffset;
        smashPoint.y += 1.5f; 

        Collider[] hits = Physics.OverlapSphere(smashPoint, cfg.smashRadius);
        Debug.Log($"[HeadSmash] Executing impact at {smashPoint}. Radius: {cfg.smashRadius}. Hits found: {hits.Length}");

        IDamageable playerDamageable = null;
        Vector3 hitPos = Vector3.zero;

        foreach (Collider hit in hits)
        {
            // Ignore the boss itself
            if (hit.transform.root == brain.transform.root) continue;
            
            // Check for Player tag or MovementController presence
            bool isPlayer = hit.CompareTag("Player");
            if (!isPlayer)
            {
                var mcCheck = hit.GetComponentInParent<Character.MovementController>();
                if (mcCheck == null) mcCheck = hit.transform.root.GetComponentInChildren<Character.MovementController>();
                isPlayer = (mcCheck != null);
            }

            if (isPlayer)
            {
                // Find IDamageable anywhere in the player's hierarchy
                playerDamageable = hit.GetComponent<IDamageable>()
                    ?? hit.GetComponentInParent<IDamageable>()
                    ?? hit.transform.root.GetComponentInChildren<IDamageable>();
                
                if (playerDamageable != null) 
                { 
                    hitPos = hit.transform.position; 
                    Debug.Log($"[HeadSmash] Successfully targeted player: {hit.name}");
                    break; 
                }
            }
        }

        if (playerDamageable != null)
        {
            playerDamageable.TakeDamage(cfg.smashDamage, hitPos);
            
            // Shake camera on successful hit
            brain.ApplyCameraShake();
            
            Character.MovementController mc = FindMovementController();
            if (brain.headSmashUsageCount > cfg.throwAfterUsageCount && mc != null)
            {
                Vector3 backDir = -brain.transform.forward; backDir.y = 0f;
                mc.ApplyKnockback(backDir.normalized * cfg.throwKnockbackForce, cfg.throwKnockbackStun);
                brain.headSmashUsageCount = 0;
            }
            else if (mc != null)
            {
                Vector3 fwd = brain.transform.forward; fwd.y = 0f;
                mc.ApplyKnockback(fwd.normalized * cfg.smashKnockbackForce, cfg.smashKnockbackStun);
            }
        }
    }

    private Character.MovementController FindMovementController()
    {
        if (brain.currentTarget == null) return null;
        MonoBehaviour targetMB = brain.currentTarget as MonoBehaviour;
        if (targetMB != null)
        {
            var mc = targetMB.GetComponent<Character.MovementController>(); if (mc != null) return mc;
            mc = targetMB.GetComponentInParent<Character.MovementController>(); if (mc != null) return mc;
            mc = targetMB.transform.root.GetComponentInChildren<Character.MovementController>(); if (mc != null) return mc;
        }
        return Object.FindFirstObjectByType<Character.MovementController>();
    }

    public override void Exit()
    {
        brain.ResetAnimationSpeed();
        brain.desiredMovementDirection = Vector3.zero;
        brain.SetCooldown(this.GetType(), brain.headSmashConfig.cooldownDuration);
    }

    public override Color GetStateColor()
    {
        var cfg = brain.headSmashConfig;
        if (stateTimer < cfg.windupTime) return new Color(1f, 0.4f, 0.7f);
        return isShockwaveMode ? new Color(1f, 0.4f, 0f) : new Color(0.5f, 0.2f, 0.35f);
    }

    public override string GetStateName()
    {
        var cfg = brain.headSmashConfig;
        if (stateTimer < cfg.windupTime) return isShockwaveMode ? "CHARGING SHOCKWAVE" : "PREPARING SMASH";
        return isShockwaveMode ? "GROUND SHOCKWAVE!" : "HEAD SMASH!";
    }
}
