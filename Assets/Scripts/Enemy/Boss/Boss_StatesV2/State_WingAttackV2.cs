using UnityEngine;

public class State_WingAttackV2 : BossStateV2
{
    private float stateTimer = 0f;
    private float windupDuration;
    private bool actionStarted = false;
    private bool isInterrupt;

    public State_WingAttackV2(BossBrainV2 brain, bool isInterrupt = false) : base(brain)
    {
        this.isInterrupt = isInterrupt;
    }

    public override void Enter()
    {
        var cfg = brain.wingAttackConfig;
        stateTimer = 0f;
        actionStarted = false;
        brain.isAttackingFlag = true;
        brain.desiredMovementDirection = Vector3.zero;

        windupDuration = isInterrupt ? cfg.fastWindupTime : cfg.windupTime;

        float animSpeed = cfg.windupAnimSpeed > 0 ? cfg.windupAnimSpeed : 1.0f;
        brain.PlayAnimation("boss_windUp", 0.2f, animSpeed);
    }

    public override void Tick()
    {
        var cfg = brain.wingAttackConfig;
        stateTimer += Time.deltaTime;
        brain.desiredMovementDirection = Vector3.zero;

        if (stateTimer < windupDuration)
        {
            // WINDUP: face player
            if (brain.currentTarget != null)
            {
                Vector3 targetPos = brain.currentTarget.GetPosition();
                targetPos.y = brain.transform.position.y;
                Vector3 dir = (targetPos - brain.transform.position).normalized;
                if (dir != Vector3.zero)
                    brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
            }
        }
        else if (stateTimer < windupDuration + cfg.activeTime)
        {
            // ACTION
            if (!actionStarted)
            {
                actionStarted = true;
                brain.ResetAnimationSpeed();
                if (brain.bossWeapon != null) brain.bossWeapon.EnableHitbox();
                brain.PlayAnimation("boss_wingAttack", 0.1f);
            }
        }
        // Wait for OnActionComplete
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "OnActionComplete")
            brain.AdvanceChain();
    }

    public override void Exit()
    {
        brain.ResetAnimationSpeed();
        brain.isAttackingFlag = false;
        if (brain.bossWeapon != null) brain.bossWeapon.DisableHitbox();
        brain.SetCooldown(this.GetType(), brain.wingAttackConfig.cooldownDuration);
    }

    public override Color GetStateColor()
    {
        return stateTimer < windupDuration ? Color.red : new Color(0.5f, 0f, 0f);
    }

    public override string GetStateName()
    {
        return stateTimer < windupDuration ? "WING SLASH WINDUP" : "WING SLASH ACTION!";
    }
}
