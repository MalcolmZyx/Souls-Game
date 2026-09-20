using UnityEngine;

public class State_DeathV2 : BossStateV2
{
    private bool animationFinished = false;
    private float despawnTimer = 0f;
    public State_DeathV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        brain.desiredMovementDirection = Vector3.zero;
        if (brain.bossWeapon != null) brain.bossWeapon.DisableHitbox();

        BossController body = brain.GetComponent<BossController>();
        if (body != null) body.TriggerDeathVisuals();

        brain.PlayAnimation("boss_death", 0.1f);
    }

    public override void Tick()
    {
        if (animationFinished)
        {
            despawnTimer += Time.deltaTime;
            if (despawnTimer >= brain.deathConfig.timeBeforeDespawn)
                Object.Destroy(brain.gameObject);
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "OnActionComplete")
        {
            animationFinished = true;
            Debug.Log("[State_DeathV2] Death animation complete. Despawn countdown.");
        }
    }

    public override Color GetStateColor() { return Color.black; }
    public override string GetStateName() { return "DEAD"; }
}
