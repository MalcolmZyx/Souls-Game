using UnityEngine;

public class State_ChaseV2 : BossStateV2
{
    private Transform bossTransform;
    private float chaseTimer = 0f;
    private int totalPushbacks = 0;
    private bool inPushPause = false;
    private float pushPauseTimer = 0f;
    private float speedOverride = -1f;
    private float durationOverride = -1f;
    private float stopDistanceOverride = 0f;
    private float currentSpeed;

    public State_ChaseV2(BossBrainV2 brain, float speedOverride = -1f, float durationOverride = -1f, float stopDistanceOverride = 0f) : base(brain)
    {
        bossTransform = brain.transform;
        this.speedOverride = speedOverride;
        this.durationOverride = durationOverride;
        this.stopDistanceOverride = stopDistanceOverride;
    }

    public override void Enter()
    {
        var cfg = brain.chaseConfig;
        chaseTimer = 0f;
        totalPushbacks = 0;
        currentSpeed = speedOverride > 0 ? speedOverride : cfg.startSpeed;
        
        brain.PlayAnimation("walking");
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

    private IDamageable FindDamageableOnPlayer(Collider hit)
    {
        if (!hit.CompareTag("Player"))
        {
            var pmCheck = hit.GetComponentInParent<Character.MovementController>();
            if (pmCheck == null) pmCheck = hit.transform.root.GetComponentInChildren<Character.MovementController>();
            if (pmCheck == null) return null;
        }

        IDamageable dmg = hit.GetComponent<IDamageable>();
        if (dmg != null) return dmg;
        if (hit.attachedRigidbody != null) { dmg = hit.attachedRigidbody.GetComponent<IDamageable>(); if (dmg != null) return dmg; }
        dmg = hit.GetComponentInParent<IDamageable>();
        if (dmg != null) return dmg;
        return hit.transform.root.GetComponentInChildren<IDamageable>();
    }

    public override void Tick()
    {
        var cfg = brain.chaseConfig;

        if (brain.currentTarget == null || !brain.currentTarget.IsAlive())
        {
            brain.ChangeState(new State_IdleV2(brain, 1.0f));
            return;
        }

        chaseTimer += Time.deltaTime;
        float maxTime = durationOverride > 0 ? durationOverride : cfg.maxChaseTime;
        if (chaseTimer >= maxTime)
        {
            brain.AdvanceChain();
            return;
        }

        if (inPushPause)
        {
            pushPauseTimer -= Time.deltaTime;
            brain.desiredMovementDirection = Vector3.zero;
            if (pushPauseTimer <= 0f)
            {
                inPushPause = false;
                if (totalPushbacks >= cfg.maxPushbacks)
                {
                    brain.AdvanceChain();
                    return;
                }
            }
            return;
        }

        float distance = brain.sensors.DistanceToTarget;
        Vector3 direction = brain.sensors.DirectionToTarget;

        float effectiveStopDist = stopDistanceOverride > 0 ? stopDistanceOverride : cfg.attackRange;

        if (distance <= effectiveStopDist)
        {
            if (stopDistanceOverride > 0)
            {
                // Reached override distance, stop chasing and advance
                brain.AdvanceChain();
                return;
            }
            
            ExecutePushback(direction);
        }
        else
        {
            float oldSpeed = currentSpeed;
            float maxSpeed = speedOverride > 0 ? speedOverride : cfg.maxSpeed;
            currentSpeed = Mathf.Min(currentSpeed + cfg.acceleration * Time.deltaTime, maxSpeed);
            brain.desiredMovementDirection = direction * currentSpeed;

            if (oldSpeed < cfg.runAnimThreshold && currentSpeed >= cfg.runAnimThreshold)
                brain.PlayAnimation("running");
            else if (oldSpeed >= cfg.runAnimThreshold && currentSpeed < cfg.runAnimThreshold)
                brain.PlayAnimation("walking");
        }
    }

    private void ExecutePushback(Vector3 direction)
    {
        var cfg = brain.chaseConfig;
        Character.MovementController mc = FindMovementController();

        Collider[] hits = Physics.OverlapSphere(brain.transform.position, 4.0f);
        foreach (Collider hit in hits)
        {
            if (hit.isTrigger && !hit.CompareTag("Player")) continue;
            IDamageable dmg = FindDamageableOnPlayer(hit);
            if (dmg != null)
            {
                dmg.TakeDamage(cfg.pushbackDamage, hit.transform.position);
                if (mc == null)
                {
                    mc = hit.GetComponentInParent<Character.MovementController>();
                    if (mc == null) mc = hit.transform.root.GetComponentInChildren<Character.MovementController>();
                }
                break;
            }
        }

        if (mc != null)
        {
            Vector3 pushVector = direction;
            pushVector.y = 0f;
            mc.ApplyKnockback(pushVector.normalized * cfg.pushbackForce, cfg.pushbackStun);
        }

        totalPushbacks++;
        inPushPause = true;
        pushPauseTimer = cfg.pushPauseDuration;
        brain.desiredMovementDirection = Vector3.zero;
    }

    public override void Exit()
    {
        brain.desiredMovementDirection = Vector3.zero;
    }

    public override Color GetStateColor() { return Color.Lerp(Color.yellow, Color.red, 0.5f); }
    public override string GetStateName() { return $"CHASE [{totalPushbacks}/{brain.chaseConfig.maxPushbacks}]"; }
}
