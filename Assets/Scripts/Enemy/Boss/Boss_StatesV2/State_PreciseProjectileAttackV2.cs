using UnityEngine;

public class State_PreciseProjectileAttackV2 : BossStateV2
{
    private float stateTimer = 0f;
    private bool hasFired = false;

    public State_PreciseProjectileAttackV2(BossBrainV2 brain) : base(brain) { }

    GameObject projectile = null;
    
    public override void Enter()
    {
        var cfg = brain.preciseProjectileConfig;
        stateTimer = 0f;
        hasFired = false;
        brain.desiredMovementDirection = Vector3.zero;
        brain.SetCooldown(this.GetType(), cfg.cooldownDuration);
        float animSpeed = cfg.windupAnimSpeed > 0 ? cfg.windupAnimSpeed : 1.0f;
        brain.PlayAnimation("boss_windUp", 0.2f, animSpeed);
        projectile = cfg.projectilePrefab;
    }

    public override void Tick()
    {
        var cfg = brain.preciseProjectileConfig;
        stateTimer += Time.deltaTime;
        brain.desiredMovementDirection = Vector3.zero;

        if (stateTimer < cfg.windupTime)
        {
            if (brain.currentTarget != null)
            {
                Vector3 targetPos = brain.currentTarget.GetPosition();
                targetPos.y = brain.transform.position.y;
                Vector3 dir = (targetPos - brain.transform.position).normalized;
                if (dir != Vector3.zero)
                    brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 20f);
            }
        }
        else if (stateTimer < cfg.windupTime + cfg.actionTime)
        {
            if (!hasFired)
            {
                hasFired = true;
                brain.ResetAnimationSpeed();
                brain.PlayAnimation("boss_preciseProjectile", 0.1f);
            }
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "OnFire")
        {
            FirePreciseShot();
            PunishHuggers();
        }
        else if (eventName == "OnActionComplete")
        {
            brain.AdvanceChain();
        }
    }

    private void FirePreciseShot()
    {
        var cfg = brain.preciseProjectileConfig;

        GameObject orb = Object.Instantiate(projectile);
        orb.transform.localScale = new Vector3(1.5f, 1.5f, 4.0f);

        Renderer rd = orb.GetComponent<Renderer>();
        if (rd != null) rd.material.color = Color.blue;

        Vector3 spawnPos = brain.headTransform != null
            ? brain.headTransform.position + brain.headTransform.forward * 1.5f
            : brain.transform.position + brain.transform.forward * 2f + Vector3.up * 1.5f;

        orb.transform.position = spawnPos;

        Vector3 targetPos = brain.currentTarget.GetPosition() + Vector3.up * 0.5f;
        Vector3 dir = (targetPos - spawnPos).normalized;
        orb.transform.rotation = Quaternion.LookRotation(dir);

        Collider col = orb.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Rigidbody rb = orb.AddComponent<Rigidbody>();
        rb.useGravity = false;

        BossProjectile proj = orb.AddComponent<BossProjectile>();
        proj.damage = cfg.projectileDamage;

        rb.AddForce(dir * cfg.projectileSpeed, ForceMode.Impulse);
        Object.Destroy(orb, 30f);
    }

    private void PunishHuggers()
    {
        var cfg = brain.preciseProjectileConfig;

        Collider[] hits = Physics.OverlapSphere(brain.transform.position, cfg.punishRadius);
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
                Character.MovementController pm = hit.GetComponentInParent<Character.MovementController>();
                if (pm == null) pm = hit.transform.root.GetComponentInChildren<Character.MovementController>();
                if (pm == null) pm = Object.FindFirstObjectByType<Character.MovementController>();

                if (pm != null)
                {
                    Vector3 pushDir = hit.transform.position - brain.transform.position;
                    pushDir.y = 0f;
                    pm.ApplyKnockback(pushDir.normalized * cfg.punishKnockbackForce, cfg.punishKnockbackStun);
                }

                IDamageable dmg = hit.GetComponent<IDamageable>();
                if (dmg == null && hit.attachedRigidbody != null) dmg = hit.attachedRigidbody.GetComponent<IDamageable>();
                if (dmg == null) dmg = hit.GetComponentInParent<IDamageable>();
                if (dmg == null) dmg = hit.transform.root.GetComponentInChildren<IDamageable>();
                if (dmg != null) dmg.TakeDamage(cfg.punishDamage, hit.transform.position);

                break;
            }
        }
    }

    public override void Exit()
    {
        brain.ResetAnimationSpeed();
        brain.desiredMovementDirection = Vector3.zero;
    }

    public override Color GetStateColor()
    {
        return stateTimer < brain.preciseProjectileConfig.windupTime ? Color.blue : new Color(0f, 0f, 0.5f);
    }

    public override string GetStateName()
    {
        return stateTimer < brain.preciseProjectileConfig.windupTime ? "LOCKING PRECISION TARGET" : "PRECISE FIRE!";
    }
}
