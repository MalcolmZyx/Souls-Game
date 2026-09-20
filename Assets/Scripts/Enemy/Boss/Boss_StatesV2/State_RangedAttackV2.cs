using UnityEngine;

public class State_RangedAttackV2 : BossStateV2
{
    private float stateTimer = 0f;
    private float nextShotTimer = 0f;
    private int targetShots = 0;
    private int shotsFired = 0;
    private bool isAnimationPlaying = false;

    private GameObject projectile;
    
    public State_RangedAttackV2(BossBrainV2 brain) : base(brain)
    {
    }

    public override void Enter()
    {
        var cfg = brain.rangedAttackConfig;
        projectile = cfg.projectilePrefab;
        brain.SetCooldown(this.GetType(), cfg.cooldownDuration);
        brain.desiredMovementDirection = Vector3.zero;
        Rigidbody rb = brain.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        float animSpeed = cfg.windupAnimSpeed > 0 ? cfg.windupAnimSpeed : 1.0f;
        brain.PlayAnimation("boss_windUp", 0.2f, animSpeed);
        shotsFired = 0;
        stateTimer = 0f;
        isAnimationPlaying = false;

        targetShots = Random.Range(cfg.minShots, cfg.maxShots + 1);
        nextShotTimer = Time.time + Random.Range(0.2f, 0.5f);
        Debug.Log($"[State_RangedAttackV2] Straight Shots started. Target count: {targetShots}");

        if (brain.currentTarget != null)
        {
            Vector3 dir = brain.currentTarget.GetPosition() - brain.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f)
                brain.transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }

    public override void Tick()
    {
        var cfg = brain.rangedAttackConfig;
        brain.desiredMovementDirection = Vector3.zero;

        if (brain.currentTarget == null || !brain.currentTarget.IsAlive())
        {
            brain.ChangeState(new State_IdleV2(brain, 1.5f));
            return;
        }

        stateTimer += Time.deltaTime;

        // Face player
        Vector3 lookDir = brain.currentTarget.GetPosition() - brain.transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
            brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 10f);

        // Windup phase: wait before shooting
        if (stateTimer < cfg.windupTime)
        {
            return;
        }

        // Event-driven firing: trigger animation when ready
        if (!isAnimationPlaying && shotsFired < targetShots && Time.time >= nextShotTimer)
        {
            isAnimationPlaying = true;
            brain.ResetAnimationSpeed();
            brain.PlayAnimation("boss_projectile", 0.15f);
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "OnFire")
        {
            FireStraightShot();
            shotsFired++;
            Debug.Log($"[State_RangedAttackV2] Shot fired. {shotsFired}/{targetShots}");
        }
        else if (eventName == "OnActionComplete")
        {
            isAnimationPlaying = false;
            var cfg = brain.rangedAttackConfig;
            
            // Schedule the delay for the next shot
            nextShotTimer = Time.time + Random.Range(cfg.shotIntervalMin, cfg.shotIntervalMax);

            if (shotsFired >= targetShots)
            {
                Debug.Log("[State_RangedAttackV2] Sequence complete. Advancing.");
                if (brain.isInArenaSequence)
                    brain.ChangeState(new State_ReturnToCenterV2(brain));
                else
                    brain.AdvanceChain();
            }
        }
    }

    private void FireStraightShot()
    {
        var cfg = brain.rangedAttackConfig;
        GameObject orb = Object.Instantiate(projectile);
        orb.transform.localScale = Vector3.one * 1.0f;

        Vector3 spawnPos;
        if (brain.headTransform != null)
            spawnPos = brain.headTransform.position + brain.headTransform.forward * 0.5f;
        else
            spawnPos = brain.transform.position + brain.transform.forward * 1.5f + Vector3.up * 1.5f;

        orb.transform.position = spawnPos;

        Renderer rd = orb.GetComponent<Renderer>();
        if (rd != null) rd.material.color = new Color(0.4f, 0.3f, 0.2f);

        Collider col = orb.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Rigidbody rb = orb.GetComponent<Rigidbody>();
        if (rb == null) rb = orb.AddComponent<Rigidbody>();
        
        BossProjectile proj = orb.GetComponent<BossProjectile>();
        if (proj == null) proj = orb.AddComponent<BossProjectile>();
        
        proj.damage = cfg.projectileDamage;
        SetupFireVisuals(orb);
        rb.useGravity = false;

        Vector3 targetBodyPos = brain.currentTarget.GetPosition() + Vector3.up * 1.0f;
        Vector3 dirToPlayer = (targetBodyPos - spawnPos).normalized;
        rb.linearVelocity = dirToPlayer * cfg.projectileSpeed;

        GameObject.Destroy(orb, 15f);
    }

    private void SetupFireVisuals(GameObject projectile)
    {
        var cfg = brain.rangedAttackConfig;
        brain.SetupFireVisuals(
            projectile, 
            cfg.fireVFXPrefab, 
            cfg.vfxYOffset, 
            cfg.vfxScale, 
            cfg.vfxRotationOffset, 
            cfg.projectileBaseScale
        );
    }

    public override void Exit()
    {
        brain.ResetAnimationSpeed();
        brain.desiredMovementDirection = Vector3.zero;
    }

    public override Color GetStateColor() { return Color.yellow; }
    public override string GetStateName() { return "STRAIGHT SHOTS"; }
}
