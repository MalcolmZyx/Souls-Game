using UnityEngine;

public class State_EdgeWatchV2 : BossStateV2
{
    private float stalkTimer = 0f;
    private bool isGrappling = false;
    private float grappleTimer = 0f;
    private LineRenderer grappleLine = null;
    private Vector3 grappleStartPos;
    private Vector3 grappleTargetPos;

    public State_EdgeWatchV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        stalkTimer = 0f;
        isGrappling = false;
        grappleTimer = 0f;
        grappleLine = null;
        brain.desiredMovementDirection = Vector3.zero;
    }

    public override void Tick()
    {
        var cfg = brain.edgeWatchConfig;

        if (brain.currentTarget == null || !brain.currentTarget.IsAlive())
        {
            brain.ChangeState(new State_IdleV2(brain, 1.0f));
            return;
        }

        if (brain.isPlayerInArena)
        {
            brain.ChangeState(new State_IdleV2(brain, 1.0f));
            return;
        }

        stalkTimer += Time.deltaTime;

        if (stalkTimer >= cfg.stalkDuration)
        {
            brain.ChangeState(new State_ReturnToSpawnV2(brain));
            return;
        }

        // Face player
        Vector3 lookDir = brain.currentTarget.GetPosition() - brain.transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
            brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 6f);

        // Grapple trigger
        if (!isGrappling && stalkTimer > cfg.grappleWindowStart)
        {
            Vector3 bossFlat = brain.transform.position; bossFlat.y = 0f;
            Vector3 playerFlat = brain.currentTarget.GetPosition(); playerFlat.y = 0f;
            float playerDistance = Vector3.Distance(playerFlat, bossFlat);

            if (playerDistance < cfg.grappleRange)
            {
                isGrappling = true;
                grappleTimer = 0f;
                grappleStartPos = brain.transform.position;
                grappleTargetPos = brain.currentTarget.GetPosition();

                UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                grappleLine = brain.gameObject.AddComponent<LineRenderer>();
                grappleLine.startWidth = 0.2f;
                grappleLine.endWidth = 0.2f;
                grappleLine.positionCount = 2;
            }
        }

        if (isGrappling)
        {
            grappleTimer += Time.deltaTime;

            if (grappleTimer <= 0.2f)
            {
                grappleLine.SetPosition(0, brain.transform.position);
                grappleLine.SetPosition(1, grappleTargetPos);
                brain.desiredMovementDirection = Vector3.zero;
            }
            else if (grappleTimer <= 0.4f)
            {
                float t = (grappleTimer - 0.2f) / 0.2f;
                brain.transform.position = Vector3.Lerp(grappleStartPos, grappleTargetPos, t);
                grappleLine.SetPosition(0, brain.transform.position);
                grappleLine.SetPosition(1, grappleTargetPos);
            }
            else if (grappleTimer <= 1.5f)
            {
                brain.desiredMovementDirection = Vector3.zero;
                grappleLine.SetPosition(0, brain.transform.position);
                grappleLine.SetPosition(1, grappleTargetPos);

                if (Vector3.Distance(brain.transform.position, grappleTargetPos) < 1f)
                {
                    Character.MovementController mc = ((MonoBehaviour)brain.currentTarget).GetComponent<Character.MovementController>();
                    if (mc == null) mc = ((MonoBehaviour)brain.currentTarget).GetComponentInParent<Character.MovementController>();
                    if (mc == null) mc = ((MonoBehaviour)brain.currentTarget).transform.root.GetComponentInChildren<Character.MovementController>();
                    if (mc != null)
                    {
                        Vector3 knockbackDir = (grappleTargetPos - brain.transform.position).normalized;
                        knockbackDir.y = 0.2f;
                        mc.ApplyKnockback(knockbackDir * cfg.grappleKnockbackForce, cfg.grappleKnockbackStun);

                        IDamageable dmg = mc.GetComponent<IDamageable>()
                            ?? mc.GetComponentInChildren<IDamageable>()
                            ?? mc.GetComponentInParent<IDamageable>()
                            ?? mc.transform.root.GetComponentInChildren<IDamageable>();
                        if (dmg != null) dmg.TakeDamage(cfg.grappleDamage, mc.transform.position);
                    }
                }
            }
            else
            {
                if (grappleLine != null) { Object.Destroy(grappleLine); grappleLine = null; }
                UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = true;
                isGrappling = false;
                stalkTimer = cfg.stalkDuration; // Force exit
            }
        }
        else
        {
            brain.desiredMovementDirection = Vector3.zero;
        }
    }

    public override void Exit()
    {
        if (grappleLine != null) { Object.Destroy(grappleLine); grappleLine = null; }
        UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = true;
        brain.desiredMovementDirection = Vector3.zero;
    }

    public override Color GetStateColor() { return Color.magenta; }
    public override string GetStateName() { return "EDGE WATCH (STALKING)"; }
}
