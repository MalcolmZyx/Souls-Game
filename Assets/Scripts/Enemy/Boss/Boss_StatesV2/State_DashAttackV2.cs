using UnityEngine;

public class State_DashAttackV2 : BossStateV2
{
    private float stateTimer = 0f;
    private Vector3 dashDirection;
    private bool hasDealtDamage = false;
    private GameObject currentChargeVFX;
    private GameObject currentDashVFX;
    private bool dashVFXStarted = false;
    private float effectiveDashDuration;
    private Vector3 dashStartPos;

    public State_DashAttackV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        var cfg = brain.dashAttackConfig;
        stateTimer = 0f;
        hasDealtDamage = false;
        brain.SetCooldown(this.GetType(), cfg.cooldownDuration);

        // Calculate effective dash duration: use distance if set, otherwise use duration directly
        if (cfg.dashDistance > 0 && cfg.dashSpeed > 0)
            effectiveDashDuration = cfg.dashDistance / cfg.dashSpeed;
        else
            effectiveDashDuration = cfg.dashDuration;

        // Scale windup animation speed
        float animSpeed = cfg.windupAnimSpeed > 0 ? cfg.windupAnimSpeed : 1.0f;
        brain.PlayAnimation("boss_windUp", 0.2f, animSpeed);

        // Subtle particle effects before dashing (Charge/Windup phase)
        if (cfg.chargeVFXPrefab != null)
        {
            currentChargeVFX = Object.Instantiate(cfg.chargeVFXPrefab, brain.transform);
            currentChargeVFX.transform.localPosition = cfg.chargeVFXOffset;
            currentChargeVFX.transform.localScale = cfg.chargeVFXScale;
            
            // Recursive Play (ensure all nested particles start)
            foreach (var ps in currentChargeVFX.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Play(true);
            }
        }
    }

    public override void Tick()
    {
        var cfg = brain.dashAttackConfig;
        stateTimer += Time.deltaTime;

        if (stateTimer < cfg.windupTime)
        {
            brain.desiredMovementDirection = Vector3.zero;
        }
        else if (stateTimer < cfg.windupTime + effectiveDashDuration)
        {
            if (stateTimer - Time.deltaTime < cfg.windupTime)
            {
                // RIGHT BEFORE DASH: Get player's current location and lock the direction
                if (brain.currentTarget != null)
                {
                    Vector3 targetPos = brain.currentTarget.GetPosition();
                    targetPos.y = brain.transform.position.y;
                    dashDirection = (targetPos - brain.transform.position).normalized;
                    if (dashDirection != Vector3.zero)
                        brain.transform.rotation = Quaternion.LookRotation(dashDirection);
                }
                else
                {
                    dashDirection = brain.transform.forward;
                }

                brain.ResetAnimationSpeed();
                brain.PlayAnimation("boss_dash", 0.05f);
                
                // Transition from subtle charge VFX to the high-speed dash VFX (vfx_fire2)
                if (currentChargeVFX != null) Object.Destroy(currentChargeVFX);
                
                if (cfg.dashVFXPrefab != null && !dashVFXStarted)
                {
                    dashVFXStarted = true;
                    currentDashVFX = Object.Instantiate(cfg.dashVFXPrefab, brain.transform);
                    currentDashVFX.transform.localPosition = cfg.dashVFXOffset;
                    currentDashVFX.transform.localScale = cfg.dashVFXScale;

                    // Recursive Play (ensure all nested particles start)
                    foreach (var ps in currentDashVFX.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ps.Play(true);
                    }
                }
            }

            brain.desiredMovementDirection = dashDirection * cfg.dashSpeed;
            if (brain.bossWeapon != null) brain.bossWeapon.EnableHitbox();

            // Simple Wall Detection: Raycast forward from chest height to avoid hitting the ground
            // We check slightly ahead (2 units) to catch walls before clipping into them.
            if (stateTimer > cfg.windupTime + 0.1f) 
            {
                if (Physics.Raycast(brain.transform.position + Vector3.up * 1.5f, dashDirection, out RaycastHit wallHit, 2.5f))
                {
                    if (!wallHit.collider.isTrigger && !wallHit.collider.CompareTag("Player") && wallHit.transform.root != brain.transform.root)
                    {
                        Debug.Log($"[DashAttack] Wall detected ({wallHit.collider.name}), aborting dash early.");
                        stateTimer = cfg.windupTime + effectiveDashDuration; // Force jump to recovery phase
                    }
                }
            }

            if (!hasDealtDamage)
            {
                Collider[] hits = Physics.OverlapSphere(brain.transform.position, 3.5f);
                foreach (Collider hit in hits)
                {
                    if (hit.isTrigger && !hit.CompareTag("Player")) continue;

                    bool isPlayer = hit.CompareTag("Player");
                    if (!isPlayer)
                    {
                        Character.MovementController pmCheck = hit.GetComponentInParent<Character.MovementController>();
                        if (pmCheck == null) pmCheck = hit.transform.root.GetComponentInChildren<Character.MovementController>();
                        isPlayer = (pmCheck != null);
                    }

                    if (isPlayer)
                    {
                        IDamageable dmg = hit.GetComponent<IDamageable>();
                        if (dmg == null && hit.attachedRigidbody != null) dmg = hit.attachedRigidbody.GetComponent<IDamageable>();
                        if (dmg == null) dmg = hit.GetComponentInParent<IDamageable>();
                        if (dmg == null) dmg = hit.transform.root.GetComponentInChildren<IDamageable>();

                        if (dmg != null)
                        {
                            dmg.TakeDamage(cfg.damage, hit.transform.position);
                            hasDealtDamage = true;

                            Character.MovementController pm = hit.GetComponentInParent<Character.MovementController>();
                            if (pm == null) pm = hit.transform.root.GetComponentInChildren<Character.MovementController>();
                            if (pm == null) pm = Object.FindFirstObjectByType<Character.MovementController>();
                            if (pm != null)
                            {
                                Vector3 pushDir = dashDirection;
                                pushDir.y = 0f;
                                pm.ApplyKnockback(pushDir.normalized * cfg.knockbackForce, cfg.knockbackStunDuration);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            brain.ChangeState(new State_IdleV2(brain, cfg.recoveryIdleTime, false));
        }
    }

    public override void Exit()
    {
        brain.desiredMovementDirection = Vector3.zero;
        if (brain.bossWeapon != null) brain.bossWeapon.DisableHitbox();
        brain.SetCooldown(this.GetType(), brain.dashAttackConfig.cooldownDuration);

        brain.ResetAnimationSpeed();

        // Cleanup VFX on exit
        if (currentChargeVFX != null) Object.Destroy(currentChargeVFX);
        if (currentDashVFX != null) Object.Destroy(currentDashVFX);
    }

    public override Color GetStateColor()
    {
        return stateTimer < brain.dashAttackConfig.windupTime ? Color.magenta : new Color(0.5f, 0f, 0.5f);
    }

    public override string GetStateName()
    {
        return stateTimer < brain.dashAttackConfig.windupTime ? "PREPARING DASH..." : "DASHING!";
    }
}
