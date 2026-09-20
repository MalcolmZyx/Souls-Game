using UnityEngine;

public class State_WavePushBackV2 : BossStateV2
{
    private float stateTimer = 0f;
    private int totalFlaps = 0;
    private int flapsExecuted = 0;
    private float actionDuration = 0f;
    private Character.MovementController cachedMC = null;

    public State_WavePushBackV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        var cfg = brain.wavePushBackConfig;
        stateTimer = 0f;
        flapsExecuted = 0;
        brain.desiredMovementDirection = Vector3.zero;

        totalFlaps = Random.Range(cfg.minFlaps, cfg.maxFlaps + 1);
        actionDuration = totalFlaps * cfg.flapInterval;

        float animSpeed = cfg.windupAnimSpeed > 0 ? cfg.windupAnimSpeed : 1.0f;
        brain.PlayAnimation("boss_windUp", 0.2f, animSpeed);

        cachedMC = FindMovementController();
    }

    private Character.MovementController FindMovementController()
    {
        if (brain.currentTarget == null) return null;
        MonoBehaviour targetMB = brain.currentTarget as MonoBehaviour;
        if (targetMB != null)
        {
            var mc = targetMB.GetComponent<Character.MovementController>();
            if (mc != null) return mc;
            mc = targetMB.GetComponentInParent<Character.MovementController>();
            if (mc != null) return mc;
            mc = targetMB.transform.root.GetComponentInChildren<Character.MovementController>();
            if (mc != null) return mc;
        }
        return Object.FindFirstObjectByType<Character.MovementController>();
    }

    public override void Tick()
    {
        var cfg = brain.wavePushBackConfig;
        stateTimer += Time.deltaTime;
        brain.desiredMovementDirection = Vector3.zero;

        if (stateTimer < cfg.windupTime)
        {
            // WINDUP
        }
        else if (stateTimer < cfg.windupTime + actionDuration)
        {
            float actionTimer = stateTimer - cfg.windupTime;
            if (flapsExecuted < totalFlaps && actionTimer >= flapsExecuted * cfg.flapInterval)
            {
                brain.ResetAnimationSpeed();
                brain.PlayAnimation("boss_flap", 0.1f);
                flapsExecuted++;
            }
        }
        else
        {
            brain.AdvanceChain();
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "OnFlap")
            ExecuteFlap(flapsExecuted - 1);
    }

    private void ExecuteFlap(int currentFlapIndex)
    {
        if (brain.currentTarget == null) return;
        var cfg = brain.wavePushBackConfig;

        if (cachedMC == null) cachedMC = FindMovementController();
        if (cachedMC == null) return;

        Vector3 pushDir = (brain.currentTarget.GetPosition() - brain.transform.position);
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude > 0.01f) pushDir.Normalize();
        if (pushDir.sqrMagnitude < 0.01f) pushDir = -brain.transform.forward;
        pushDir = pushDir.normalized;

        float pushForce, stunTime;
        if (currentFlapIndex == 0) { pushForce = cfg.firstFlapForce; stunTime = cfg.firstFlapStun; }
        else if (currentFlapIndex == 1) { pushForce = cfg.secondFlapForce; stunTime = cfg.secondFlapStun; }
        else { pushForce = cfg.followUpFlapForce; stunTime = cfg.followUpFlapStun; }

        cachedMC.ApplyKnockback(pushDir * pushForce, stunTime);
    }

    public override void Exit()
    {
        brain.ResetAnimationSpeed();
        brain.desiredMovementDirection = Vector3.zero;
        brain.SetCooldown(this.GetType(), brain.wavePushBackConfig.cooldownDuration);
    }

    public override Color GetStateColor()
    {
        return stateTimer < brain.wavePushBackConfig.windupTime ? Color.white : Color.cyan;
    }

    public override string GetStateName()
    {
        return stateTimer < brain.wavePushBackConfig.windupTime ? "WAVE WINDUP" : "WAVE PUSHBACK ACTION";
    }
}
